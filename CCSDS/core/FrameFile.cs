using System;
using System.Collections.Generic;
using System.IO;

namespace gov.nasa.arc.ccsds.core
{
    /// <summary>
    ///     A file containing CCSDS Frames.  Defaults to the LADEE frame length;
    ///     This doesn't currently handle packets across file boundaries correctly.
    ///     The code does, but I'm not calling it correctly.)  Also, this doesn't
    ///     handle multiple virtual channels correctly.  I'm ignoring everything but
    ///     VC1 right now.
    /// </summary>
    public class FrameFile : PacketFile, IDisposable
    {
        public enum State
        {
            Idle, // Initial state.  No frame counter defined.
            Running,
            // Receiving Packets.  PacketPtr > 0 indicates a partial packet is in the packet buffer and more bytes are expected
            Iterate // Not really a state but a label for the IterateOverPackets code
        };

        private readonly int _skipInitialBytes;
        public int FrameFooterLength = 0;
        public int FrameHeaderLength = 4;
        public int FrameLength = FrameAccessor.VCDULength; // ladee default
        public int VirtualChannel = 1;
        public int LogLevel = 0;

        // Used by the non-iterator implementation
        private BinaryReader _reader;
        private long _readerlen;
        private long _readerpos;

        public FrameFile()
        {
        }

        public FrameFile(string filename,
            int frameLength = FrameAccessor.VCDULength,
            int skipInitialBytes = 0,
            int headerLength = 4,
            int footerLength = 0,
            int virtualChannel = 1
            )
            : base(filename)
        {
            FrameLength = frameLength;
            FrameHeaderLength = headerLength;
            FrameFooterLength = footerLength;
            _skipInitialBytes = skipInitialBytes;
            VirtualChannel = virtualChannel;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool flag)
        {
            if (_reader == null) return;
            _reader.Dispose();
            _reader = null;
        }

        public override long Position()
        {
            throw new Exception("Not implemented");
        }

        public new static IEnumerable<byte[]> Iterator(string filename)
        {
            return (new FrameFile(filename)).Iterator();
        }

        public new static IEnumerable<byte[]> Iterator(String filename, int[] apids, Int64 startTime,
            Int64 stopTime, long skip = 0L)
        {
            return (new FrameFile(filename)).Iterator(apids, startTime, stopTime, skip);
        }

        public IEnumerable<byte[]> FrameIterator()
        {
            var fbuf = new byte[FrameLength];

            using (var reader = new BinaryReader(File.Open(_Path, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                var readerlen = (int) reader.BaseStream.Length;
                var readerpos = 0;

                // Optionally skip some initial bytes
                for (var i = 0; i < _skipInitialBytes; i++)
                    reader.ReadByte();

                while (readerpos < readerlen)
                {
                    var numBytesToRead = FrameHeaderLength;
                    var numBytesRead = 0;

                    // Optionally skip FrameWrapperLen + the 4-byte sync mark
                    while (numBytesToRead > 0)
                    {
                        var count = reader.Read(fbuf, numBytesRead, numBytesToRead);
                        if (count == 0) yield break; // Exit if we hit an EOF
                        numBytesToRead -= count;
                        numBytesRead += count;
                    }

                    readerpos += FrameHeaderLength;

                    //CheckByte(0x00, reader.ReadByte());
                    //CheckByte(0x00, reader.ReadByte());
                    //CheckByte(0xFC, reader.ReadByte());
                    //CheckByte(0x1D, reader.ReadByte());
                    //CheckByte(0x4D, reader.ReadByte());
                    //CheckByte(0x41, reader.ReadByte());

                    //readerpos += 4;

                    numBytesToRead = FrameLength;
                    numBytesRead = 0;

                    while (numBytesToRead > 0)
                    {
                        var bytesRead = reader.Read(fbuf, numBytesRead, numBytesToRead); // Bytes read this time

                        // Break when the end of the file is reached.
                        if (bytesRead == 0)
                            break;

                        numBytesRead += bytesRead;
                        numBytesToRead -= bytesRead;
                    }
                    readerpos += numBytesRead;

                    // Pass the frame along to the next in the chain
                    yield return fbuf;

                    numBytesToRead = FrameFooterLength;
                    numBytesRead = 0;

                    while (numBytesToRead > 0)
                    {
                        var bytesRead = reader.Read(fbuf, numBytesRead, numBytesToRead); // Bytes read this time

                        // Break when the end of the file is reached.
                        if (bytesRead == 0)
                            break;

                        numBytesRead += bytesRead;
                        numBytesToRead -= bytesRead;
                    }
                    readerpos += numBytesRead;
                }
            }
        }

        public override IEnumerable<byte[]> Iterator()
        {
            var state = State.Idle;
            var packet = new byte[65516 + 12]; // add some margin 65536 bytes of data plus 6 of header + 6 of timestamp

            var frameCounter = 0;
            var packetPtr = 0; // copy pointer into the packet array
            var framePtr = 0; // copy pointer into the frame array

            foreach (var frame in FrameIterator())
            {
                var virtualChannel = FrameAccessor.VirtualChannel(frame);
                //Console.WriteLine("VirtualChannel={0}", virtualChannel);
                if (virtualChannel != VirtualChannel) continue;
                var frameCount = FrameAccessor.FrameCount(frame);
                //Console.WriteLine("FrameCount={0}", frameCounter);
                var firstPacketPtr = FrameAccessor.FirstHeaderPointer(frame);
                switch (state)
                {
                    case State.Idle:
                        if (firstPacketPtr == FrameAccessor.FirstHeaderPointerOverflow)
                            break; // No packet starts here
                        framePtr = firstPacketPtr + FrameAccessor.MPDUStart;
                        state = State.Running;
                        frameCounter = frameCount;
                        packetPtr = 0;
                        goto case State.Iterate;
                    case State.Running:
                        frameCounter++;
                        if (frameCounter != frameCount && !(frameCount == 0 && frameCounter == 16777216))
                        {
                            var delta = frameCount - frameCounter;
                            if (delta < 0) delta += 16777216;
                            if (LogLevel>0)
                                Console.Error.WriteLine(@"Frame sequence error: expected {0}, saw {1}, {2} frames skipped", frameCounter, frameCount, delta);
                            state = State.Idle;
                            continue;
                        }
                        framePtr = FrameAccessor.MPDUStart;
                        if (firstPacketPtr == 0)
                        {
                            if (packetPtr > 0)
                            {
                                CheckPacketLength(packet, packetPtr); // Get rid of this after debugging
                                yield return packet;
                                packetPtr = 0;
                            }
                            goto case State.Iterate;
                        }
                        if (firstPacketPtr == FrameAccessor.FirstHeaderPointerOverflow)
                        {
                            AppendToPacket(frame, FrameAccessor.MPDUPacketZoneLength, packet, ref framePtr,
                                ref packetPtr);
                            break;
                        }
                        if (firstPacketPtr == 2047)
                        {
                            // Idle Frame
                            break;
                        }
                        if (firstPacketPtr > 0)
                        {
                            AppendToPacket(frame, firstPacketPtr, packet, ref framePtr, ref packetPtr);
                            {
                                CheckPacketLength(packet, packetPtr); // Get rid of this after debugging
                                yield return packet;
                                packetPtr = 0;
                            }
                            goto case State.Iterate;
                        }
                        break;
                    case State.Iterate:
                    {
                        var flag = true;
                        while (flag)
                        {
                            var gap = FrameAccessor.MPDUEnd - framePtr;
                            if (gap < PacketAccessor.PacketFixedHeaderLength)
                            {
                                // This packet must extend past
                                for (var i = 0; i < gap; i++)
                                    packet[packetPtr++] = frame[framePtr++];
                                flag = false;
                                continue;
                            }
                            for (var i = 0; i < PacketAccessor.PacketFixedHeaderLength; i++)
                                packet[packetPtr++] = frame[framePtr++];
                            var len = PacketAccessor.Length(packet) + 1; // real length is always the field + 1
                            gap = FrameAccessor.MPDUEnd - framePtr;
                            if (gap <= 0)
                            {
                                flag = false;
                                continue;
                            }
                            if (gap >= len)
                            {
                                // it'll fit
                                AppendToPacket(frame, len, packet, ref framePtr, ref packetPtr);
                                {
                                    CheckPacketLength(packet, packetPtr); // Get rid of this after debugging
                                    yield return packet;
                                    packetPtr = 0;
                                }
                                flag = (gap > len); // Maybe it fits just barely
                                continue;
                            }
                            // It won't fit
                            AppendToPacket(frame, gap, packet, ref framePtr, ref packetPtr);
                            flag = false;
                        }
                    }
                        break;
                }
            }
            // Should there be a yield break here?
        }

        protected void CheckByte(int expected, int actual)
        {
            if (expected != actual)
                Console.Error.WriteLine(@"Frame sync failure: expected={0} actual={1}", expected, actual);
            //if (expected != actual)
            //    throw new Exception(string.Format("Frame sync failure: expected={0} actual={1}", expected, actual));
        }

        private void AppendToPacket(byte[] frame, int len, byte[] packet, ref int framePtr, ref int packetPtr)
        {
            Array.Copy(frame, framePtr, packet, packetPtr, len);
            framePtr += len;
            packetPtr += len;
        }

        public void CheckPacketLength(byte[] packet, int packetPtr)
        {
            var len = PacketAccessor.PacketFixedHeaderLength + 1 + PacketAccessor.Length(packet);
            //if (len != packetPtr)
            //    throw new Exception("Packet Length Check Exception");
            if (len != packetPtr)
            {
                Console.Error.WriteLine(@"Packet length check exception: apid={0} timestamp={1}", PacketAccessor.APID(packet),
                   PacketAccessor.Time42(packet));
            }
        }

        public override string NameForSequence()
        {
            return "Frame Packet File Sequence";
        }

        public void Open()
        {
            _reader = new BinaryReader(File.Open(_Path, FileMode.Open, FileAccess.Read, FileShare.Read));
            _readerlen = (int) _reader.BaseStream.Length;
            _readerpos = 0;

            // Optionally skip some initial bytes
            for (var i = 0; i < _skipInitialBytes; i++)
                _reader.ReadByte();
        }

        /// <summary>
        ///     Return true if there is more data
        /// </summary>
        /// <param name="fbuf"></param>
        /// <returns></returns>
        public bool Read(byte[] fbuf)
        {
            if (_readerpos >= _readerlen)
                return false; // We're done

            var numBytesToRead = FrameHeaderLength;
            var numBytesRead = 0;

            // Optionally skip FrameWrapperLen + the 4-byte sync mark
            while (numBytesToRead > 0)
            {
                var count = _reader.Read(fbuf, numBytesRead, numBytesToRead);
                if (count == 0) return false; // Exit if we hit an EOF
                numBytesToRead -= count;
                numBytesRead += count;
            }

            _readerpos += FrameHeaderLength;

            numBytesToRead = FrameLength;
            numBytesRead = 0;

            while (numBytesToRead > 0)
            {
                var bytesRead = _reader.Read(fbuf, numBytesRead, numBytesToRead); // Bytes read this time

                // Break when the end of the file is reached.
                if (bytesRead == 0)
                    break;

                numBytesRead += bytesRead;
                numBytesToRead -= bytesRead;
            }
            _readerpos += numBytesRead;

            numBytesToRead = FrameFooterLength;
            numBytesRead = 0;

            // Optionally skip FrameWrapperLen + the 4-byte sync mark
            while (numBytesToRead > 0)
            {
                var count = _reader.Read(fbuf, numBytesRead, numBytesToRead);
                if (count == 0) return false; // Exit if we hit an EOF
                numBytesToRead -= count;
                numBytesRead += count;
            }
            _readerpos += FrameFooterLength;

            return true;
        }

        public void Close()
        {
            if (_reader == null) return;
            _reader.Close();
            _reader = null;
        }
    }
}