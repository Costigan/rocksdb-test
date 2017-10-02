using System;
using System.IO;
using System.Text;

namespace gov.nasa.arc.ccsds.core
{
    /// <summary>
    ///     Static methods for accessing packet fields
    /// </summary>
    public static class PacketAccessor
    {
        public const int PacketFixedHeaderLength = 6;
        public const int PacketSecondaryHeaderLength = 6;
        public const int PacketHeaderLength = PacketFixedHeaderLength + PacketSecondaryHeaderLength;

        public const int MaximumPacketSize = 65536;  //TODO: this isn't the right number.  It should be 65535+7.

        public static int APID(byte[] packet)
        {
            return ((0x7 & packet[0]) << 8) + packet[1];
        }

        public static int APID(byte[] packet, int offset)
        {
            return ((0x7 & packet[offset]) << 8) + packet[offset + 1];
        }

        public static int StreamId(byte[] packet)
        {
            return packet[0] << 8 + packet[1];
        }

        public static int StreamId(byte[] packet, int offset)
        {
            return packet[offset] << 8 + packet[offset + 1];
        }

        public static int SequenceCount(byte[] packet)
        {
            return 0x3FFF & ((packet[2] << 8) | packet[3]);
        }

        public static int SequenceCount(byte[] packet, int offset)
        {
            return ((0x3F & packet[offset + 2]) << 8) + packet[offset + 3];
        }

        public static int Length(byte[] packet)
        {
            return ((packet[4]) << 8) + packet[5];
        }

        public static int Length(byte[] packet, int offset)
        {
            return ((packet[offset + 4]) << 8) + packet[offset + 5];
        }

        public static int Seconds(byte[] packet)
        {
            return ((0x7F & packet[6]) << 24) + (packet[7] << 16) + (packet[8] << 8) + packet[9];
        }

        public static int Seconds(byte[] packet, int offset)
        {
            return ((0x7F & packet[offset + 6]) << 24) + (packet[offset + 7] << 16) + (packet[offset + 8] << 8) +
                   packet[offset + 9];
        }

        public static int SubSeconds(byte[] packet)
        {
            return (packet[10] << 8) + packet[11];
        }

        public static int SubSeconds(byte[] packet, int offset)
        {
            return (packet[offset + 10] << 8) + packet[offset + 11];
        }

        public static long Time42(byte[] packet)
        {
            if (8 == (packet[0] & 8))
            {
                return (((long)packet[6]) << 40) | ((long)packet[7] << 32) | ((long)packet[8] << 24) |
                      ((long)packet[9] << 16) | ((long)packet[10] << 8) | packet[11];
            }

            return 0L;
        }

        public static long Time42Force(byte[] packet)
        {
            return (((long) packet[6]) << 40) | ((long) packet[7] << 32) | ((long) packet[8] << 24) |
                   ((long) packet[9] << 16) | ((long) packet[10] << 8) | packet[11];
        }

        public static long SecondaryHeader(byte[] packet)
        {
            if (8 == (packet[0] & 8))
            {
                return (((long)packet[6]) << 40) | ((long)packet[7] << 32) | ((long)packet[8] << 24) |
                      ((long)packet[9] << 16) | ((long)packet[10] << 8) | packet[11];
            }

            return 0L;
        }

        public static long SecondaryHeaderForce(byte[] packet)
        {
            return (((long)packet[6]) << 40) | ((long)packet[7] << 32) | ((long)packet[8] << 24) |
                   ((long)packet[9] << 16) | ((long)packet[10] << 8) | packet[11];
        }

        public static bool HasSecondaryHeader(byte[] packet)
        {
            return (((byte)8) & packet[0]) != ((byte)0);
        }

        public static Byte GetByte(byte[] packet, int offset)
        {
            return packet[offset];
        }

        public static UInt16 GetU12(byte[] packet, int offset)
        {
            return (UInt16) ((packet[offset] << 8) | (packet[offset + 1]));
        }

        public static UInt16 GetU21(byte[] packet, int offset)
        {
            return (UInt16)((packet[offset+1] << 8) | (packet[offset]));
        }

        public static Int16 GetI12(byte[] packet, int offset)
        {
            return (Int16)((packet[offset] << 8) | (packet[offset + 1]));
        }

        public static UInt32 GetU1234(byte[] packet, int offset)
        {
            return (UInt32)((packet[offset] << 24) | (packet[offset+1] << 16) | (packet[offset+2] << 8) | (packet[offset + 3]));
        }

        public static UInt32 GetU4321(byte[] packet, int offset)
        {
            return (UInt32)((packet[offset + 3] << 24) | (packet[offset + 2] << 16) | (packet[offset + 1] << 8) | (packet[offset]));
        }

        public static Int32 GetI1234(byte[] packet, int offset)
        {
            return (Int32)((packet[offset] << 24) | (packet[offset+1] << 16) | (packet[offset+2] << 8) | (packet[offset + 3]));
        }

        public static UInt64 GetU12345678(byte[] packet, int offset)
        {
            return (UInt64)(((ulong)packet[offset] << 56) | ((ulong)packet[offset + 1] << 48) | ((ulong)packet[offset + 2] << 40) | ((ulong)packet[offset + 3] << 32) | ((ulong)packet[offset + 4] << 24) | ((ulong)packet[offset + 5] << 16) | ((ulong)packet[offset + 6] << 8) | ((ulong)packet[offset + 7]));
        }

        public static Int64 GetI12345678(byte[] packet, int offset)
        {
            return (Int64)(((long)packet[offset] << 56) | ((long)packet[offset + 1] << 48) | ((long)packet[offset + 2] << 40) | ((long)packet[offset + 3] << 32) | ((long)packet[offset + 4] << 24) | ((long)packet[offset + 5] << 16) | ((long)packet[offset + 6] << 8) | ((long)packet[offset + 7]));
        }

        public static unsafe float GetF1234(byte[] packet, int offset)
        {
            var v = ((UInt32)packet[3 + offset] & 0xFF)
                    | (((UInt32)packet[2 + offset] & 0xFF) << 8)
                    | (((UInt32)packet[1 + offset] & 0xFF) << 16)
                    | (((UInt32)packet[0 + offset] & 0xFF) << 24);

            var fp = (float*) &v;
            var f = *fp;
            return f;

            //float asFloat = BitConverter.Int64BitsToDouble(asInt);
            //return asFloat; 

            //bool v = BitConverter.IsLittleEndian;
            //return BitConverter.ToSingle(packet, offset);

            /*
            byte[] t = new byte[4];
            t[3] = packet[offset];
            t[2] = packet[offset + 1];
            t[1] = packet[offset + 2];
            t[0] = packet[offset + 3];
            float r = BitConverter.ToSingle(t, 0);
            return r;
             * */
        }

        public static unsafe double GetF12345678(byte[] packet, int offset)
        {
            var v = ((UInt64)packet[7 + offset])
                    | (((UInt64)packet[6 + offset]) << 8)
                    | (((UInt64)packet[5 + offset]) << 16)
                    | (((UInt64)packet[4 + offset]) << 24)
                    | (((UInt64)packet[3 + offset]) << 32)
                    | (((UInt64)packet[2 + offset]) << 40)
                    | (((UInt64)packet[1 + offset]) << 48)
                    | (((UInt64)packet[0 + offset]) << 56);

            var fp = (double*) &v;
            var f = *fp;
            return f;
        }

        public static unsafe double GetF87654321(byte[] packet, int offset)
        {
            var v = ((UInt64)packet[0 + offset])
                    | (((UInt64)packet[1 + offset]) << 8)
                    | (((UInt64)packet[2 + offset]) << 16)
                    | (((UInt64)packet[3 + offset]) << 24)
                    | (((UInt64)packet[4 + offset]) << 32)
                    | (((UInt64)packet[5 + offset]) << 40)
                    | (((UInt64)packet[6 + offset]) << 48)
                    | (((UInt64)packet[7 + offset]) << 56);

            var fp = (double*)&v;
            var f = *fp;
            return f;
        }

        public static void DumpPacket(byte[] packet)
        {
            DumpPacket(packet, Console.Out);
        }

        public static void DumpPacket(byte[] p, TextWriter sb)
        {
            sb.WriteLine("Packet: apid={0} seq={1} len={2}", APID(p), SequenceCount(p), Length(p));
            sb.WriteLine("        timestamp={0} = {1}", Time42(p), TimeUtilities.Time42ToString(Time42(p)));
            var currentPacketSize = Length(p) + 7;
            var col = 0; // Not really the display column, but the virtual column
            for (var i = 0; i < currentPacketSize; i++)
            {
                if (Rem(i, 16) == 0 && i > 0)
                {
                    // Display the ascii version
                    sb.Write("  |  ");
                    for (var j = i - 16; j < i; j++)
                        sb.Write(SafeChar(p[j]));

                    sb.Write("\r\n");
                    col = 0;
                }
                else if (Rem(i, 4) == 0 && i > 0)
                {
                    sb.Write("  ");
                }
                sb.Write("{0:x2}", p[i]);
                col++;
            }

            // Handle the tail end
            var skip = 40 - (2*col + ((col/4)*2));
            for (var i = 0; i < skip; i++) sb.Write(' ');
            sb.Write("|  ");
            for (var i = currentPacketSize - col; i < currentPacketSize; i++)
                sb.Write(SafeChar(p[i]));
            sb.WriteLine();
            sb.WriteLine("----------------------------");
        }

        public static char SafeChar(int b)
        {
            if (b < 32) b = 46;
            return (char) b;
        }

        public static int Rem(int a, int b)
        {
            int rem;
            Math.DivRem(a, b, out rem);
            return rem;
        }

        // From Mike's spreadsheet (this was for LADEE only)
        internal static bool IsValidApid(int a)
        {
            //return true;
            if (0 <= a && a <= 127) return true;
            if (128 <= a && a <= 255) return true;
            if (257 <= a && a <= 386) return true;
            if (432 <= a && a <= 432) return true;
            if (512 <= a && a <= 527) return true;
            if (514 <= a && a <= 518) return true;
            if (528 <= a && a <= 575) return true;
            if (576 <= a && a <= 591) return true;
            if (592 <= a && a <= 607) return true;
            if (768 <= a && a <= 832) return true;
            if (1024 <= a && a <= 1056) return true;
            if (2031 <= a && a <= 452) return true;
            if (2046 <= a && a <= 2046) return true;
            if (1937 <= a && a <= 1937) return true;
            if (a == 2000 || a == 2001 || a == 2045 || a == 2046) return true;
            return false;
        }

        public static void WriteHexToStringBuilder(byte[] packet, StringBuilder sb)
        {
            var packetSize = Length(packet) + 7;
            var col = 0;
            for (var i = 0; i < packetSize; i++)
            {
                if (Rem(i, 16) == 0 && i > 0)
                {
                    // Display the ascii version
                    sb.Append("  |  ");
                    for (var j = i - 16; j < i; j++)
                        sb.Append(SafeChar(packet[j]));
                    sb.Append("\r\n");
                    col = 0;
                }
                else if (Rem(i, 4) == 0 && i > 0)
                {
                    sb.Append("  ");
                }

                sb.AppendFormat("{0:x2}", packet[i]);
                col++;
            }
            // Handle the tail end
            var skip = 40 - (2*col + ((col/4)*2));
            for (var i = 0; i < skip; i++) sb.Append(' ');
            sb.Append("  |  ");
            for (var i = packetSize - col; i < packetSize; i++)
                sb.Append(SafeChar(packet[i]));
        }

        public static string ToHex(byte[] packet)
        {
            var sb = new StringBuilder();
            WriteHexToStringBuilder(packet, sb);
            return sb.ToString();
        }

        public static bool IsShortPacket(byte[] packet, int zeroCount)
        {
            var len = Length(packet) + 7;
            var low = Math.Max(0, len - zeroCount);
            for (var i = len - 1; i >= low; i--)
                if (packet[i] != 0) return false;
            return true;
        }
    }
}