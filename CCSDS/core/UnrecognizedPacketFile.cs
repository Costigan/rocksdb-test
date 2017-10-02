using System.Collections.Generic;
using System.Linq;

namespace gov.nasa.arc.ccsds.core
{
    public class UnrecognizedPacketFile : PacketFile
    {
        public override IEnumerable<byte[]> Iterator()
        {
            return Enumerable.Empty<byte[]>();
        }

        public override IEnumerable<byte[]> Iterator(long startTime, long stopTime)
        {
            return Enumerable.Empty<byte[]>();
        }

        public override IEnumerable<byte[]> Iterator(int[] apids, long startTime, long stopTime, long skip = 0L)
        {
            return Enumerable.Empty<byte[]>();
        }
    }
}