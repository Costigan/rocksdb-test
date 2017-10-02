using CCSDS.utilities;
using gov.nasa.arc.ccsds.core;
using System.Collections.Generic;
using System.IO;

namespace RocksDB_test1
{
    /// <summary>
    /// Static methods for streaming packets from various sources
    /// </summary>
    public static class PacketStreamUtilities
    {
        public static IEnumerable<byte[]> PacketsFromFileTree(string root) => PacketsFromFileTree(new string[] { root });

        /// <summary>
        /// Walk the file tree starting at a set of roots.
        /// Collect files in bunches by matching their filenames (without extensions)
        /// Determine what kind of files they are (packet, frame) and then iterate.
        /// </summary>
        /// <param name="roots"></param>
        /// <returns></returns>
        public static IEnumerable<byte[]> PacketsFromFileTree(IEnumerable<string> roots)
        {
            var fullnames = new List<string>();
            var filenames = new List<string>();
            foreach (var root in roots)
            {
                foreach (var f in FileWalker.WalkITOSFiles(root))
                {
                    var filename = Path.GetFileNameWithoutExtension(f);
                    if (filenames.Count < 1 || filenames[filenames.Count - 1].Equals(filename))
                    {
                        filenames.Add(filename);
                        fullnames.Add(f);
                    }
                    else
                    {  // Release files that were buffered
                        foreach (var p in PacketFilter.MaybeUniqueByteArray(FilesToStream(fullnames).Iterator()))
                            yield return p;
                        filenames.Clear();
                        fullnames.Clear();
                        filenames.Add(filename);
                        fullnames.Add(f);
                    }
                }
            }
            // Flush any files that were buffered
            foreach (var p in PacketFilter.MaybeUniqueByteArray(FilesToStream(fullnames).Iterator()))
                yield return p;
        }

        public static IPacketFile FilesToStream(List<string> filenames)
        {
            if (filenames.Count < 1) return null;
            switch (PacketFile.GetFileTypeId(filenames[0]))
            {
                case PacketFile.PacketFileType.CFE_PACKET_FILE:
                    return new PacketFileSequence<CFEPacketFile>(filenames);
                case PacketFile.PacketFileType.ITOS_PACKET_FILE:
                    return new PacketFileSequence<ITOSPacketFile>(filenames, true);
                case PacketFile.PacketFileType.ITOS_FRAME_FILE:
                case PacketFile.PacketFileType.RAF_FRAME_FILE:
                    return new FrameFileSequence(filenames);
                case PacketFile.PacketFileType.ANNO12_PACKET_FILE:
                    return new PacketFileSequence<Anno12PacketFile>(filenames);
                case PacketFile.PacketFileType.UNRECOGNIZED_FILE:
                default:
                    //Console.WriteLine(@"Unrecognized kind of packet file: {0}", filenames[0]);
                    return null;
            }
        }
    }
}
