using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace gov.nasa.arc.ccsds.core
{
    public class FrameFileSequence : IPacketFile
    {
        public enum State
        {
            Idle, // Initial state.  No frame counter defined.
            Running,
            // Receiving Packets.  PacketPtr > 0 indicates a partial packet is in the packet buffer and more bytes are expected
            Iterate // Not really a state but a label for the IterateOverPackets code
        };

        public List<string> Filenames;
        public List<FrameFile> Files;
        public int FrameLength = FrameAccessor.VCDULength; // ladee default
        public int LogLevel = 0;
        private Dictionary<int, int> _apidCountsCache;
        private int[] _apids;
        // ReSharper disable InconsistentNaming
        protected string _displayName;
        protected FrameFile _frameFile;
        // ReSharper restore InconsistentNaming

        public FrameFileSequence(IEnumerable<string> filenames)
        {
            Filenames = new List<string>(filenames.Where(f =>
            {
                var ext = Path.GetExtension(f);
                return ext == null || !".h".Equals(ext.ToLowerInvariant());
            }));
            Filenames.Sort(PacketFile.Compare);
            Files = Filenames.Select(filename => new FrameFile(filename)).ToList();
        }

        /*
        IEnumerable<byte[]> CCSDSPacketSource.Iterator()
        {
            foreach (var pf in FrameFiles)
            {
                _frameFile = pf;
                foreach (var packet in _frameFile.Iterator())
                    yield return packet;
                _frameFile = null;
            }
        }
        */

        public IEnumerable<byte[]> Iterator(int virtualChannel)
        {
            var state = State.Idle;
            var packet = new byte[65516 + 12]; // add some margin 65536 bytes of data plus 6 of header + 6 of timestamp

            var frameCounter = 0;
            var packetPtr = 0; // copy pointer into the packet array
            var framePtr = 0; // copy pointer into the frame array

            foreach (var frame in FrameIterator())
            {
                var vc = FrameAccessor.VirtualChannel(frame);
                //Console.WriteLine("VirtualChannel={0}", virtualChannel);
                if (vc != virtualChannel) continue;
                var frameCount = FrameAccessor.FrameCount(frame);
                //Console.WriteLine("FrameCount={0} State={1}", frameCounter, state);
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
                                //Console.WriteLine("yield apid={0}", PacketAccessor.APID(packet));
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
                                //Console.WriteLine("yield apid={0}", PacketAccessor.APID(packet));
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
                                    //Console.WriteLine("yield apid={0}", PacketAccessor.APID(packet));
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

        public IEnumerable<byte[]> Iterator()
        {
            var state = new State[64];
            for (var i = 0; i < 63; i++)
                state[i] = State.Idle;
            var packet = new byte[64][];
            const int bufferSize = 65516 + 12;  // add some margin 65536 bytes of data plus 6 of header + 6 of timestamp

            var frameCounter = new int[64];  // initial values of 0
            var packetPtr = new int[64]; // copy pointer into the packet array
            var framePtr = new int[64]; // copy pointer into the frame array

            foreach (var frame in FrameIterator())
            {
                var idx = -1;
                if (0 == (frame[++idx] | frame[++idx] | frame[++idx] | frame[++idx] | frame[++idx] | frame[++idx] | frame[++idx] | frame[++idx]))
                    continue;
                var vc = FrameAccessor.VirtualChannel(frame);
                if (packet[vc] == null)
                    packet[vc] = new byte[bufferSize];
                //Console.WriteLine("VirtualChannel={0}", virtualChannel);
                //if (vc != 1) continue;
                var frameCount = FrameAccessor.FrameCount(frame);
                //Console.WriteLine("FrameCount={0} State={1}", frameCounter[vc], state[vc]);
                var firstPacketPtr = FrameAccessor.FirstHeaderPointer(frame);
                switch (state[vc])
                {
                    case State.Idle:
                        if (firstPacketPtr == FrameAccessor.FirstHeaderPointerOverflow)
                            break; // No packet starts here
                        framePtr[vc] = firstPacketPtr + FrameAccessor.MPDUStart;
                        state[vc] = State.Running;
                        frameCounter[vc] = frameCount;
                        packetPtr[vc] = 0;
                        goto case State.Iterate;
                    case State.Running:
                        frameCounter[vc]++;
                        if (frameCounter[vc] != frameCount && !(frameCount == 0 && frameCounter[vc] == 16777216))
                        {
                            var delta = frameCount - frameCounter[vc];
                            if (delta < 0) delta += 16777216;
                            if (LogLevel>0)
                                Console.WriteLine(@"Frame sequence error: expected {0}, saw {1}, {2} frames skipped", frameCounter[vc], frameCount, delta);
                            state[vc] = State.Idle;
                            continue;
                        }
                        framePtr[vc] = FrameAccessor.MPDUStart;
                        if (firstPacketPtr == 0)
                        {
                            if (packetPtr[vc] > 0)
                            {
                                CheckPacketLength(packet[vc], packetPtr[vc]); // Get rid of this after debugging
                                yield return packet[vc];
                                packetPtr[vc] = 0;
                            }
                            goto case State.Iterate;
                        }
                        if (firstPacketPtr == FrameAccessor.FirstHeaderPointerOverflow)
                        {
                            AppendToPacket(frame, FrameAccessor.MPDUPacketZoneLength, packet[vc], ref framePtr[vc],
                                ref packetPtr[vc]);
                            break;
                        }
                        if (firstPacketPtr == 2047)
                        {
                            // Idle Frame
                            break;
                        }
                        if (firstPacketPtr > 0)
                        {
                            AppendToPacket(frame, firstPacketPtr, packet[vc], ref framePtr[vc], ref packetPtr[vc]);
                            {
                                CheckPacketLength(packet[vc], packetPtr[vc]); // Get rid of this after debugging
                                yield return packet[vc];
                                packetPtr[vc] = 0;
                            }
                            goto case State.Iterate;
                        }
                        break;
                    case State.Iterate:
                        {
                            var flag = true;
                            var p = packet[vc];
                            while (flag)
                            {
                                var gap = FrameAccessor.MPDUEnd - framePtr[vc];
                                if (gap < PacketAccessor.PacketFixedHeaderLength)
                                {
                                    // This packet must extend past
                                    for (var i = 0; i < gap; i++)
                                        p[packetPtr[vc]++] = frame[framePtr[vc]++];
                                    flag = false;
                                    continue;
                                }
                                for (var i = 0; i < PacketAccessor.PacketFixedHeaderLength; i++)
                                    p[packetPtr[vc]++] = frame[framePtr[vc]++];
                                var len = PacketAccessor.Length(p) + 1; // real length is always the field + 1
                                gap = FrameAccessor.MPDUEnd - framePtr[vc];
                                if (gap <= 0)
                                {
                                    flag = false;
                                    continue;
                                }
                                if (gap >= len)
                                {
                                    // it'll fit
                                    AppendToPacket(frame, len, p, ref framePtr[vc], ref packetPtr[vc]);
                                    {
                                        CheckPacketLength(p, packetPtr[vc]); // Get rid of this after debugging
                                        yield return p;
                                        packetPtr[vc] = 0;
                                    }
                                    flag = (gap > len); // Maybe it fits just barely
                                    continue;
                                }
                                // It won't fit
                                AppendToPacket(frame, gap, p, ref framePtr[vc], ref packetPtr[vc]);
                                flag = false;
                            }
                        }
                        break;
                }
            }
            // Should there be a yield break here?
        }


        public IEnumerable<byte[]> Iterator(Int64 startTime, Int64 stopTime)
        {
            //foreach (byte[] p in Iterator())
            //{
            //    var timestamp = PacketAccessor.Time42(p);
            //    if (startTime <= timestamp && timestamp <= stopTime) yield return p;
            //}

            return from p in Iterator()
                let timestamp = PacketAccessor.Time42(p)
                where startTime <= timestamp && timestamp <= stopTime
                select p;
            /*
            Console.WriteLine("startTime={0} stopTime={1}", startTime, stopTime);
            Console.WriteLine("startTime={0} stopTime={1}", TimeUtilities.Time42ToString(startTime),  TimeUtilities.Time42ToString(stopTime));
            foreach (byte[] p in Iterator())
            {
                var timestamp = PacketAccessor.Time42(p);
                if (startTime <= timestamp && timestamp <= stopTime)
                    yield return p;
            }
             */
        }

        public IEnumerable<byte[]> Iterator(int[] apids, Int64 startTime, Int64 stopTime, long skip = 0L)
        {
            var counter = 0L;
            return from p in Iterator()
                let timestamp = PacketAccessor.Time42(p)
                let apid = PacketAccessor.APID(p)
                where startTime <= timestamp && timestamp <= stopTime && apids.Contains(apid) && counter++ > skip
                select p;
            //return Iterator();
        }

        public virtual string DisplayName()
        {
            return _displayName ?? "Frame file sequence";
        }

        public void SetDisplayName(string name)
        {
            _displayName = name;
        }

        public string Filename()
        {
            if (_frameFile != null) return _frameFile.Filename();
            return Files.Count < 1 ? "Filename not known" : Files[0].Filename();
        }

        public Int64 Position()
        {
            throw new NotImplementedException();
        }

        public int[] GetApids()
        {
            if (_apids != null) return _apids;
            var apids = new List<int>();
            foreach (
                var apid in from pf in Files from apid in pf.GetApids() where !apids.Contains(apid) select apid)
            {
                apids.Add(apid);
            }

            _apids = apids.ToArray();
            return _apids;
        }

        public int GetPacketCount()
        {
            return Files.Sum(pf => pf.GetPacketCount());
        }

        public int GetPacketCount(int[] apids, long start, long stop)
        {
            return Files.Sum(pf => pf.GetPacketCount(apids, start, stop));
        }

        public Dictionary<int, int> GetApidCounts()
        {
            if (_apidCountsCache != null) return _apidCountsCache;
            _apidCountsCache = new Dictionary<int, int>();
            foreach (var pf in Files)
                PacketFile.MergeApidCounts(_apidCountsCache, pf.GetApidCounts());
            return _apidCountsCache;
        }

        public long GetBeginTime42()
        {
            return Files.Count > 0 ? Files[0].BeginTime42 : -1;
        }

        public long GetEndTime42()
        {
            return Files.Count > 0 ? Files[Files.Count - 1].EndTime42 : -1;
        }

        public long GetBeginTime42Jammed()
        {
            return Files.Count > 0 ? Files.Min(f => f.BeginTime42Jammed) : -1;
        }

        public virtual IEnumerable<byte[]> FrameIterator()
        {
            foreach (var pf in Files)
            {
                _frameFile = pf;
                foreach (var frame in _frameFile.FrameIterator())
                    yield return frame;
                _frameFile = null;
            }
        }

        protected void CheckByte(int expected, int actual)
        {
            if (expected != actual)
                throw new Exception("Frame sync failure");
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
                Console.WriteLine(@"Packet length check exception: apid={0} timestamp={1}", PacketAccessor.APID(packet),
                   PacketAccessor.Time42(packet));
            }
        }
    }
}