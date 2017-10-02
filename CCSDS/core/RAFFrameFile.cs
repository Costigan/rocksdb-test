namespace gov.nasa.arc.ccsds.core
{
    internal class RAFFrameFile : FrameFile
    {
        // The length should be 1113 with a footer of 2
        public RAFFrameFile(string filename) : base(filename, 1115, 0, 164, 0)
        {
        }
    }
}