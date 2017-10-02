using System;
using System.Collections.Generic;
using System.Linq;

namespace gov.nasa.arc.ccsds.core
{
    public class CFEPacketFile : PacketFile
    {
        public CFEPacketFile()
        {
        }

        public CFEPacketFile(string filename) : base(filename)
        {
        }

        public override int GetHeaderLength()
        {
            return 140;
        }

        public override PacketFileType FileType()
        {
            return PacketFileType.CFE_PACKET_FILE;
        }

        public new static IEnumerable<byte[]> Iterator(String filename)
        {
            return (new CFEPacketFile(filename)).Iterator();
        }

        public new static IEnumerable<byte[]> Iterator(String filename, int[] apids, Int64 startTime,
            Int64 stopTime, long skip = 0L)
        {
            return (new CFEPacketFile(filename)).Iterator(apids, startTime, stopTime, skip);
        }

        public new static IEnumerable<byte[]> Iterator(String[] filenames)
        {
            return filenames.SelectMany(Iterator);
        }

        public new static IEnumerable<byte[]> Iterator(List<String> filenames)
        {
            return filenames.SelectMany(Iterator);
        }

        public override string NameForSequence()
        {
            return "CFE Packet File Sequence";
        }
    }
}