using System.Collections.Generic;

namespace gov.nasa.arc.ccsds.core
{
    public interface IPacketSource
    {
        IEnumerable<byte[]> Iterator();
        IEnumerable<byte[]> Iterator(long startTime, long stopTime);
        IEnumerable<byte[]> Iterator(int[] apids, long startTime, long stopTime, long skip = 0L);
    }
}