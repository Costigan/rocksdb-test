// ReSharper disable InconsistentNaming

namespace gov.nasa.arc.ccsds.core
{
    /// <summary>
    ///     Static methods for accessing frame fields
    /// </summary>
    public static class FrameAccessor
    {
        public const int VCDULength = 1115; // doesn't count 4 byte sync  and 160 bytes RSS
        //public const int VCDULength = 1115; // doesn't count 4 byte sync  and 160 bytes RSS
        //public const int VCDULength = 2048; // doesn't count 4 byte sync  and 160 bytes RSS

        public const int PrimaryHeaderFixedLength = 6;
        public const int FrameErrorControlFieldLength = 0;
        public const int VCDUHeaderLength = PrimaryHeaderFixedLength + FrameErrorControlFieldLength;

        public const int MPDUStart = VCDUHeaderLength + 2;

        public const int VCDUTrailerLength = 6;
        public const int VCDUDataLength = VCDULength - VCDUHeaderLength - VCDUTrailerLength; // 1105
        public const int MPDUPacketZoneLength = VCDUDataLength - 2;
        public const int FirstHeaderPointerOverflow = 0x7FF;

        public const int MPDUEnd = MPDUStart + MPDUPacketZoneLength;

        public const int LADEE_SpacecraftId = 53;

        public static int FrameCount(byte[] buf)
        {
            return (buf[2] << 16) + (buf[3] << 8) + buf[4];
        }

        public static int VirtualChannel(byte[] buf)
        {
            return 0x3F & buf[1];
        }

        public static int FirstHeaderPointer(byte[] buf)
        {
            return ((0x7 & buf[VCDUHeaderLength]) << 8) + buf[VCDUHeaderLength + 1];
        }

        public static int SpacecraftId(byte[] buf)
        {
            return ((0x3F & buf[0]) << 2) | ((0xC0 & buf[1]) >> 6);
        }
    }
}

// ReSharper restore InconsistentNaming