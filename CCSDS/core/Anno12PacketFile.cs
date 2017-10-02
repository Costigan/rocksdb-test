using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace gov.nasa.arc.ccsds.core
{
    /// <summary>
    ///     File reader for packet files containing Anno12 and Anno12AOS headers.
    ///     This reader only uses to headers to ignore zero-filled packets.
    /// </summary>
    public class Anno12PacketFile : PacketFile
    {
        // Constants for reading status from the Anno12 header
        public const byte ErrorMask = PacketHeaderError | FrameCrcError | IncompletePacket; // 0x94;  //  #b10010100
        public const byte PacketHeaderError = 0x80;
        public const byte FrameCrcError = 0x10;
        public const byte FrameCrcEnabled = 0x08;
        public const byte IncompletePacket = 0x04;

        public Anno12PacketFile()
        {
        }

        public Anno12PacketFile(string filename)
            : base(filename)
        {
        }

        public override int GetHeaderLength()
        {
            return 0;
        }

        public override PacketFileType FileType()
        {
            return PacketFileType.ANNO12_PACKET_FILE;
        }

        public new static IEnumerable<byte[]> Iterator(String filename)
        {
            return (new Anno12PacketFile(filename)).Iterator();
        }

        public new static IEnumerable<byte[]> Iterator(String filename, int[] apids, Int64 startTime,
            Int64 stopTime, long skip = 0L)
        {
            return (new Anno12PacketFile(filename)).Iterator(apids, startTime, stopTime, skip);
        }

        public new static IEnumerable<byte[]> Iterator(String[] filenames)
        {
            return filenames.SelectMany(Iterator);
        }

        public new static IEnumerable<byte[]> Iterator(List<String> filenames)
        {
            return filenames.SelectMany(Iterator);
        }

        public override IEnumerable<byte[]> Iterator()
        {
            var buffer = new byte[PacketAccessor.MaximumPacketSize]; // Should I allocate the max size here?
            var header = new byte[12];
            using (var reader = new BinaryReader(File.Open(_Path, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                var readerlen = (int) reader.BaseStream.Length;
                long readerpos = 0;

                // Skip the header, if any
                var headerLength = GetHeaderLength();
                for (var i = 0; i < headerLength; i++)
                    reader.ReadByte();
                readerpos += headerLength;

                while (readerpos < readerlen)
                {
                    int read;

                    // Skip the 12 byte annotation
                    var ptr = 0;
                    var toread = 12;
                    while (toread > 0)
                    {
                        read = reader.Read(header, ptr, toread);
                        if (read == 0)
                        {
                            if (ptr != 0)
                                Console.WriteLine(@"Premature EOF: {0}", Path);
                            yield break;
                        }
                        toread -= read;
                        ptr += read;
                    }

                    readerpos += 12;
                    _Position = readerpos;

                    // Read the packet header
                    ptr = 0;
                    toread = 6;
                    while (toread > 0)
                    {
                        read = reader.Read(buffer, ptr, toread);
                        if (read == 0)
                        {
                            Console.WriteLine(@"Premature EOF: {0}", Path);
                            yield break;
                        }
                        toread -= read;
                        ptr += read;
                    }

                    // Check whether we've run off the end.
                    if (buffer[0] == 0 && buffer[1] == 0 && buffer[2] == 0)
                        yield break;

                    var length = PacketAccessor.Length(buffer);
                    toread = 1 + length;

                    // Read the rest of the packet

                    while (toread > 0)
                    {
                        read = reader.Read(buffer, ptr, toread);
                        if (read == 0)
                        {
                            Console.WriteLine(@"Premature EOF: {0}", Path);
                            yield break;
                        }
                        toread -= read;
                        ptr += read;
                    }

                    if ((header[3] & ErrorMask) == 0)
                        yield return buffer;

                    readerpos += ptr;
                }
            }
        }

        public override string NameForSequence()
        {
            return "Anno12 Packet File Sequence";
        }
    }
}