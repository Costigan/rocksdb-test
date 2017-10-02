using System;

namespace gov.nasa.arc.ccsds.utilities
{
    /// <summary>
    ///     Static methods for creating a packet
    /// </summary>
    public static class PacketBuilder
    {
        /// <summary>
        ///     This writes the CCSDS version, type indicator, secondary header flag (1 for LADEE) and apid
        /// </summary>
        /// <param name="p"></param>
        /// <param name="apid"></param>
        public static void WriteAPID(byte[] p, int apid)
        {
            p[0] = (byte) (((apid >> 8) & 0x7) | 0x8);
            p[1] = (byte) (apid & 0xFF);
        }

        public static void WriteSequenceCount(byte[] p, int seqcnt)
        {
            p[2] = (byte) ((seqcnt >> 8) | 192);
            p[3] = (byte) (seqcnt & 0xFF);
        }

        /// <summary>
        /// </summary>
        /// <param name="p"></param>
        /// <param name="length">The actual length of the data, not counting the secondary header</param>
        public static void WriteDataLength(byte[] p, int length)
        {
            var len = (UInt16) (length + 5); // + 6 (secondary header) - 1 (CCSDS convention)
            p[4] = (byte) (len >> 8);
            p[5] = (byte) (len & 0xFF);
        }

        public static void WriteTimestamp(byte[] p, Int64 timestamp)
        {
            p[6] = (byte) (0xff & (timestamp >> 40));
            p[7] = (byte) (0xff & (timestamp >> 32));
            p[8] = (byte) (0xff & (timestamp >> 24));
            p[9] = (byte) (0xff & (timestamp >> 16));
            p[10] = (byte) (0xff & (timestamp >> 8));
            p[11] = (byte) (0xff & (timestamp >> 0));
        }

        public static void WriteByte(byte[] p, int index, byte b)
        {
            p[index] = b;
        }

        public static void WriteInt(byte[] p, int index, int val)
        {
            var bytes = BitConverter.GetBytes(val);
            var len1 = bytes.Length - 1;
            for (var i = 0; i <= len1; i++)
                p[index++] = bytes[len1 - i];
        }

        public static void WriteUInt32(byte[] p, int index, UInt32 val)
        {
            var bytes = BitConverter.GetBytes(val);
            var len1 = bytes.Length - 1;
            for (var i = 0; i <= len1; i++)
                p[index++] = bytes[len1 - i];
        }

        //        case PointType.U21:
        //            return (UInt16) ((p[byte_offset + 1] << 8) | (p[byte_offset]));
        public static void WriteU21(byte[] p, int index, UInt16 val)
        {
            p[index] = (byte) val;
            p[index + 1] = (byte) (val >> 8);
        }

        //                case PointType.U12:
        //            return (UInt16) ((p[byte_offset] << 8) | (p[byte_offset + 1]));
        public static void WriteU12(byte[] p, int index, UInt16 val)
        {
            p[index + 1] = (byte) val;
            p[index] = (byte) (val >> 8);
        }

        public static void WriteFloat(byte[] p, int index, float val)
        {
            var bytes = BitConverter.GetBytes(val);
            var len1 = bytes.Length - 1;
            for (var i = 0; i <= len1; i++)
                p[index++] = bytes[len1 - i];
        }
    }
}