using System;
using System.Collections.Generic;

namespace gov.nasa.arc.ccsds.core
{
    // Not referenced
    public class FramesToPackets
    {
        public int LogLevel = 0;
        public static IEnumerable<byte[]> Iterator(IEnumerable<byte[]> frames)
        {
            var state = new FrameFileSequence.State[64];
            for (var i = 0; i < 63; i++)
                state[i] = FrameFileSequence.State.Idle;
            var packet = new byte[64][];
            const int bufferSize = 65516 + 12;  // add some margin 65536 bytes of data plus 6 of header + 6 of timestamp

            var frameCounter = new int[64];  // initial values of 0
            var packetPtr = new int[64]; // copy pointer into the packet array
            var framePtr = new int[64]; // copy pointer into the frame array

            foreach (var frame in frames)
            {
                var vc = FrameAccessor.VirtualChannel(frame);
                if (packet[vc] == null)
                    packet[vc] = new byte[bufferSize];
                //Console.WriteLine("VirtualChannel={0}", vc);
                //if (vc != 1) continue;
                var frameCount = FrameAccessor.FrameCount(frame);
                //Console.WriteLine("FrameCount={0} State={1}", frameCounter[vc], state[vc]);
                var firstPacketPtr = FrameAccessor.FirstHeaderPointer(frame);
                switch (state[vc])
                {
                    case FrameFileSequence.State.Idle:
                        if (firstPacketPtr == FrameAccessor.FirstHeaderPointerOverflow)
                            break; // No packet starts here
                        framePtr[vc] = firstPacketPtr + FrameAccessor.MPDUStart;
                        state[vc] = FrameFileSequence.State.Running;
                        frameCounter[vc] = frameCount;
                        packetPtr[vc] = 0;
                        goto case FrameFileSequence.State.Iterate;
                    case FrameFileSequence.State.Running:
                        frameCounter[vc]++;
                        if (frameCounter[vc] != frameCount && !(frameCount == 0 && frameCounter[vc] == 16777216))
                        {
                            var delta = frameCount - frameCounter[vc];
                            if (delta < 0) delta += 16777216;
                            //if (LogLevel > 0)
                            //    Console.WriteLine(@"Frame sequence error: expected {0}, saw {1}, {2} frames skipped", frameCounter[vc], frameCount, delta);
                            state[vc] = FrameFileSequence.State.Idle;
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
                            goto case FrameFileSequence.State.Iterate;
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
                            goto case FrameFileSequence.State.Iterate;
                        }
                        break;
                    case FrameFileSequence.State.Iterate:
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

        private static void AppendToPacket(byte[] frame, int len, byte[] packet, ref int framePtr, ref int packetPtr)
        {
            Array.Copy(frame, framePtr, packet, packetPtr, len);
            framePtr += len;
            packetPtr += len;
        }

        public static void CheckPacketLength(byte[] packet, int packetPtr)
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
