using System;
using System.Collections.Generic;

namespace gov.nasa.arc.ccsds.core
{
    public interface IPacketFileSequence
    {
        IEnumerable<string> GetPaths();
        IEnumerable<object> GetPacketFiles();
        int[] GetApids();
        int GetPacketCount();
        Dictionary<int, int> GetApidCounts();
        long GetBeginTime42();
        long GetEndTime42();
        long Position();
        string Filename();
        string DisplayName();
        object GetPacketFile();
        void UnloadStats();
        IEnumerable<byte[]> Iterator();
        IEnumerable<byte[]> Iterator(Int64 startTime, Int64 stopTime);
        IEnumerable<byte[]> Iterator(int[] apids, Int64 startTime, Int64 stopTime, long skip = 0L);
    }
}