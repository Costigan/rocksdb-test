using System.Collections.Generic;

namespace gov.nasa.arc.ccsds.core
{
    /// <summary>
    ///     Something that can generate a sequence of packets
    /// </summary>
    /// TODO: There are operations here that go beyond generating the stream that should be in another interface.
    public interface IPacketFile : IPacketSource
    {
        string DisplayName();
        void SetDisplayName(string name);
        string Filename();
        long Position();
        Dictionary<int, int> GetApidCounts();
        int[] GetApids();
        int GetPacketCount();
        int GetPacketCount(int[] apids, long start, long stop);
        long GetBeginTime42();
        long GetEndTime42();
        long GetBeginTime42Jammed();
    }
}