namespace gov.nasa.arc.ccsds.core
{
    /// <summary>
    ///     Renamed version of PacketFile
    /// </summary>
    public class ITOSPacketFile : PacketFile
    {
        public ITOSPacketFile()
        {
        }

        public ITOSPacketFile(string filename) : base(filename)
        {
        }

        public override string NameForSequence()
        {
            return "ITOS Packet File Sequence";
        }
    }
}