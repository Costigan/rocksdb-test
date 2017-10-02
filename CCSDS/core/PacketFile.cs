using CCSDS.utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// ReSharper disable InconsistentNaming

namespace gov.nasa.arc.ccsds.core
{
    /// <summary>
    ///     Base class of the classes that implement CCSDSPacketSource.  This class represents an ITOS Packet Archive,
    ///     which is a sequence of packets with no packet wrappers and no header.
    /// </summary>
    public class PacketFile : IPacketFile
    {
        public enum PacketFileType
        {
            CFE_PACKET_FILE,
            ITOS_PACKET_FILE,
            RAF_FRAME_FILE,
            ITOS_FRAME_FILE,
            ANNO12_PACKET_FILE,
            ACTIVE_PACKET_FILE,
            UNRECOGNIZED_FILE
        }

        public static readonly long DayInSubseconds = 5662310400L; // (* (expt 2 16) 60 60 24)
        protected Dictionary<int, int> _ApidCount = null;

        protected string _BeginString = "";
        protected long _BeginTime42;
        [NonSerialized]
        private bool _Changed;
        protected string _EndString = "";
        protected long _EndTime42;
        protected long _BeginTime42Jammed;
        protected string _JammedString;
        protected long _LastTouch = 0L;
        protected long _Length;
        protected string _Name;
        protected int _PacketCount;
        protected string _Path = null;
        protected long _Position = 0L;
        [NonSerialized]
        private int _cacheApidMatchesAfterStart;

        [NonSerialized]
        private int[] _cacheApids;
        [NonSerialized]
        private Int64 _cacheStartTime = Int64.MaxValue;
        protected Boolean _isFullyReadable;
        protected Boolean _isValid;

        public PacketFile()
        {
        }

        public PacketFile(string path)
        {
            Path = path;
        }

        public bool Changed
        {
            get { return _Changed; }
        }

        public string Name
        {
            get { return _Name; }
            set { _Name = value; }
        }

        public string Path
        {
            get { return _Path; }
            set
            {
                _Path = value;
                _Name = System.IO.Path.GetFileName(_Path);
                _LastTouch = File.GetLastWriteTimeUtc(_Path).Ticks;
            }
        }

        public long Length
        {
            get { return _Length; }
            set { _Length = value; }
        }

        public int PacketCount
        {
            get { return _PacketCount; }
            set { _PacketCount = value; }
        }

        public Boolean IsValid
        {
            get { return _isValid; }
            set { _isValid = value; }
        }

        public Boolean IsFullyReadable
        {
            get { return _isFullyReadable; }
            //set { _isFullyReadable = value; }
        }

        public string Begin
        {
            get
            {
                LoadStatsMaybe();
                return _BeginString;
            }
        }

        public string End
        {
            get
            {
                LoadStatsMaybe();
                return _EndString;
            }
        }

        public string Jammed
        {
            get
            {
                LoadStatsMaybe();
                return _JammedString;
            }
        }

        public long BeginTime42
        {
            get
            {
                LoadStatsMaybe();
                return _BeginTime42;
            }
        }

        public long BeginTime42Jammed
        {
            get
            {
                LoadStatsMaybe();
                return _BeginTime42Jammed;
            }
        }

        public long EndTime42
        {
            get
            {
                LoadStatsMaybe();
                return _EndTime42;
            }
        }

        public long Time42Jammed
        {
            get
            {
                LoadStatsMaybe();
                return _BeginTime42Jammed;
            }
        }

        public string DisplayName()
        {
            return _Path;
        }

        /// <summary>
        ///     Packet file does not change its display name
        /// </summary>
        /// <param name="name"></param>
        public void SetDisplayName(string name)
        {
        }

        public string Filename()
        {
            return _Path;
        }

        public virtual long Position()
        {
            return _Position;
        }

        public long GetBeginTime42()
        {
            return BeginTime42;
        }

        public long GetBeginTime42Jammed()
        {
            return BeginTime42Jammed;
        }

        public long GetEndTime42()
        {
            return EndTime42;
        }

        public virtual IEnumerable<byte[]> Iterator(Int64 startTime, Int64 stopTime)
        {
            return from p in Iterator()
                let timestamp = PacketAccessor.Time42(p)
                where timestamp >= startTime && timestamp <= stopTime
                select p;
        }

        public virtual IEnumerable<byte[]> Iterator(int[] apids, Int64 startTime, Int64 stopTime, long skip = 0L)
        {
            var counter = 0L;
            foreach (var p in Iterator())
            {
                var timestamp = PacketAccessor.Time42(p);
                if (timestamp < startTime || timestamp > stopTime) continue;
                var apid = PacketAccessor.APID(p);
                var len = apids.Length;
                for (var i = 0; i < len; i++)
                {
                    if (apid == apids[i])
                    {
                        if (counter++ >= skip)
                            yield return p;
                        break;
                    }
                }
            }
        }

        public virtual IEnumerable<byte[]> Iterator()
        {
            var buffer = new byte[PacketAccessor.MaximumPacketSize];
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
                    _Position = readerpos;

                    // Read the packet header
                    var ptr = 0;
                    var toread = 6;
                    int read;
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
                    if ((buffer[0] | buffer[1] | buffer[2]) == 0)
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

                    //totalLength = length + 7;
                    //byte[] packet = new byte[totalLength];
                    //Array.Copy(buffer, packet, totalLength);
                    //yield return packet;
                    yield return buffer;

                    readerpos += ptr;
                }
            }
        }

        public static IEnumerable<byte[]> Iterator(Stream stream)
        {
            var buffer = new byte[PacketAccessor.MaximumPacketSize];
            using (var reader = new BinaryReader(stream))
            {
                while (true)
                {
                    // Read the packet header
                    var ptr = 0;
                    var toread = 6;
                    int read;
                    while (toread > 0)
                    {
                        read = reader.Read(buffer, ptr, toread);
                        if (read == 0)
                        {
                            yield break;
                        }
                        toread -= read;
                        ptr += read;
                    }

                    // Check whether we've run off the end.
                    if ((buffer[0] | buffer[1] | buffer[2]) == 0)
                        yield break;

                    var length = PacketAccessor.Length(buffer);
                    toread = 1 + length;

                    // Read the rest of the packet
                    while (toread > 0)
                    {
                        read = reader.Read(buffer, ptr, toread);
                        if (read == 0)
                        {
                            yield break;
                        }
                        toread -= read;
                        ptr += read;
                    }

                    yield return buffer;
                }
            }
        }

        public int[] GetApids()
        {
            LoadStatsMaybe();
            return _ApidCount.Keys.ToArray();
        }

        public int GetPacketCount()
        {
            LoadStatsMaybe();

/*            Console.WriteLine(Path);
            var a = _PacketCount;
            var b = Iterator().Count();
            var c = _ApidCount.Sum(pair => pair.Value);

            Console.WriteLine("_PacketCount={0}", a);
            Console.WriteLine("Actual={0}", b);
            Console.WriteLine("_ApidCount={0}", c);
            if (a != b || b != c)
                Console.WriteLine("friday");
            //todo*/

            return _PacketCount;
        }

        public int GetPacketCount(int[] apids, long start, long stop)
        {
            return Iterator(apids, start, stop).Count();
        }

        public Dictionary<int, int> GetApidCounts()
        {
            LoadStatsMaybe();
            return _ApidCount;
        }

        public static IEnumerable<byte[]> EmptyPacketEnumeration()
        {
            yield break;
        }

        public virtual int GetHeaderLength()
        {
            return 0;
        }

        public static Type FileType(string path)
        {
            switch (GetFileTypeId(path))
            {
                case PacketFileType.CFE_PACKET_FILE:
                    return typeof (CFEPacketFile);
                case PacketFileType.ANNO12_PACKET_FILE:
                    return typeof (Anno12PacketFile);
                    //case PacketFileType.ACTIVE_PACKET_FILE:
                    //    return typeof (ActivePacketFile);
                case PacketFileType.ITOS_PACKET_FILE:
                    return typeof (ITOSPacketFile);
                case PacketFileType.ITOS_FRAME_FILE:
                    return typeof (FrameFile);
                default:
                    return typeof (UnrecognizedPacketFile);
            }
        }

        public static PacketFileType GetFileTypeId(string path)
        {
            try
            {
                var ext = System.IO.Path.GetExtension(path);
                ext = ext != null ? ext.ToLower() : null;
                if (".meta".Equals(ext, StringComparison.InvariantCultureIgnoreCase)
                    || ".dir".Equals(ext, StringComparison.InvariantCultureIgnoreCase)
                    || ".gz".Equals(ext, StringComparison.InvariantCultureIgnoreCase)
                    || ".itf".Equals(ext, StringComparison.InvariantCultureIgnoreCase))
                {
                    return PacketFileType.UNRECOGNIZED_FILE;
                }

                if (".H".Equals(ext, StringComparison.InvariantCultureIgnoreCase))
                    return GetFileTypeFromItosMetafile(path);

                var fname = System.IO.Path.GetFileNameWithoutExtension(path);
                var buf = new byte[8];
                using (var s = File.OpenRead(path))
                {
                    for (var i = 0; i < 8; i++)
                        buf[i] = (byte)s.ReadByte();
                }

                // CFE_PACKET_FILE
                if (buf[0] == 'c' && buf[1] == 'F' && buf[2] == 'E')
                    return PacketFileType.CFE_PACKET_FILE;

                if (buf[0] == 'R' && buf[1] == 'A' && buf[2] == 'F' && buf[3] == '1')
                    return PacketFileType.RAF_FRAME_FILE;

                // ITOS_FRAME_FILE
                if (buf[0] == 26 && buf[1] == 207 && (buf[4] & 248) == 72)
                    return PacketFileType.ITOS_FRAME_FILE;

                if (buf[0] == 0 && buf[1] == 0 && buf[2] == 252 && buf[3] == 29)
                    return PacketFileType.ITOS_FRAME_FILE;

                //for (var i = 0; i < 8; i++)
                //    Console.WriteLine(@"buf[{0}]={1}", i, buf[i]);

                try
                {
                    var i = 0;

                    // ITOS_PACKET_FILE
                    if ((buf[0] & 0xF8) == 0x08 || (buf[0] == 7 && 2001 == PacketAccessor.APID(buf)))
                    {
                        foreach (var p in (new ITOSPacketFile(path)).Iterator())
                        {
                            //if (!PacketAccessor.IsValidApid(PacketAccessor.APID(p)))
                            //    goto tryAnno12PacketFile;
                            if (i++ > 30)
                                break;
                        }
                        return PacketFileType.ITOS_PACKET_FILE;
                    }

                    //tryAnno12PacketFile:
                    i = 0;
                    foreach (var p in (new Anno12PacketFile(path)).Iterator())
                    {
                        //if (!PacketAccessor.IsValidApid(PacketAccessor.APID(p)))
                        //    goto unrecognizedFile;
                        if (i++ > 30)
                            break;
                    }
                    return PacketFileType.ANNO12_PACKET_FILE;
                }
                catch (Exception e)
                {
                    Console.WriteLine(@"Error reading telemetry database: {0}", e);
                }
            }
            catch (IOException e)
            {
                Console.WriteLine(@"Error reading telemetry database: {0}", e);
            }
            //unrecognizedFile:
            return PacketFileType.UNRECOGNIZED_FILE;
        }

        private static PacketFileType GetFileTypeFromItosMetafile(string path)
        {
            var meta = new ITOSMetafile(path);
            if (meta.IsFrameFile)
                return PacketFileType.ITOS_FRAME_FILE;
            if (meta.IsPacketFile)
                return PacketFileType.ITOS_PACKET_FILE;
            return PacketFileType.UNRECOGNIZED_FILE;
        }

        public Boolean Touches(DateTime day)
        {
            var t = TimeUtilities.DateTimeToTime42(day);
            return !(_EndTime42 < t || (t + DayInSubseconds) < _BeginTime42);
        }

        public virtual PacketFileType FileType()
        {
            return PacketFileType.ITOS_PACKET_FILE;
        }

        public int ApidCount(int apid)
        {
            LoadStatsMaybe();
            int result;
            return _ApidCount.TryGetValue(apid, out result) ? result : 0;
        }

        public int ApidCount(int[] apids)
        {
            return apids.Sum(apid => ApidCount(apid));
        }

        public void ClearPacketCount()
        {
            _ApidCount = null;
        }

        public bool IsUpToDate()
        {
            return _LastTouch >= File.GetLastWriteTimeUtc(_Path).Ticks;
        }

        public void LoadStatsMaybe()
        {
            if (_ApidCount == null)
                LoadStats();
        }

        // This calculates that start, stop and a pseudo-start that's after 2014 (heuristically trying to find
        // times when the clock had been jammed). 
        public void LoadStats()
        {
            var jamfilter = TimeUtilities.DateTimeToTime42(new DateTime(2014, 1, 1));
            //var endFilter = TimeUtilities.DateTimeToTime42(new DateTime(2018, 1, 1));
            const long endFilter = 37226859724800L;
            try
            {
                var dict = new Dictionary<int, int>();
                var begin = Int64.MaxValue;
                var jam = Int64.MaxValue;
                var end = Int64.MinValue;
                var count = 0;
                foreach (var packet in Iterator())
                {
                    count++;
                    var apid = PacketAccessor.APID(packet);

                    var timestamp = PacketAccessor.Time42(packet);

                    if (timestamp == 0L) continue;  // ignore CLCW packets, no timestamp
                    if (timestamp < begin)
                        begin = timestamp;
                    if (timestamp < endFilter)
                    {
                        if (timestamp > end) end = timestamp;
                        if (timestamp < jam && timestamp > jamfilter) jam = timestamp;
                    }
                    int prev;
                    if (dict.TryGetValue(apid, out prev))
                        dict[apid] = prev + 1;
                    else
                        dict.Add(apid, 1);
                }
                if (count > 0)
                {
                    _BeginTime42 = begin;
                    _EndTime42 = end;
                    _BeginTime42Jammed = jam == long.MaxValue ? jamfilter : jam;
                }
                else
                {
                    _BeginTime42 = 0L;
                    _EndTime42 = 0L;
                    _BeginTime42Jammed = 0L;
                }
                _PacketCount = count;
                _isFullyReadable = true;
                _ApidCount = dict;
                _Changed = true;
                _BeginString = TimeUtilities.Time42ToString(_BeginTime42);
                _EndString = TimeUtilities.Time42ToString(_EndTime42);
                _JammedString = TimeUtilities.Time42ToString(_BeginTime42Jammed);

                //Console.WriteLine("b={0}\nj={1}\ne={2} f={3}", _BeginString, _JammedString, _EndString, Name);
            }
            catch (Exception e1)
            {
                Console.WriteLine(e1);
                Console.WriteLine(e1.StackTrace);
                _isFullyReadable = false;
            }

/*            Console.WriteLine(Path);
            var a = _PacketCount;
            var b = Iterator().Count();
            var c = _ApidCount.Sum(pair => pair.Value);

            Console.WriteLine("_PacketCount={0}", a);
            Console.WriteLine("Actual={0}", b);
            Console.WriteLine("_ApidCount={0}",c);
            if (a != b || b != c)
                Console.WriteLine("friday");
            //todo*/
        }

        public void UnloadStats()
        {
            _ApidCount = null;
            _BeginString = null;
            _EndString = null;
            _PacketCount = 0;
            _isFullyReadable = false;
        }

        public void InsertStats(Dictionary<int, int> ApidCount, Int64 begin, Int64 end, int count)
        {
            _ApidCount = ApidCount;
            _BeginTime42 = begin;
            _BeginString = TimeUtilities.Time42ToString(_BeginTime42);
            _EndTime42 = end;
            _EndString = TimeUtilities.Time42ToString(_EndTime42);
            _PacketCount = count;
        }

        public static PacketFile Factory(string path)
        {
            switch (GetFileTypeId(path))
            {
                case PacketFileType.CFE_PACKET_FILE:
                    return new CFEPacketFile(path);
                case PacketFileType.ITOS_PACKET_FILE:
                    return new ITOSPacketFile(path);
                case PacketFileType.ITOS_FRAME_FILE:
                    return new FrameFile(path);
                    //case PacketFileType.ACTIVE_PACKET_FILE:
                    //    return new ActivePacketFile(path);
                case PacketFileType.ANNO12_PACKET_FILE:
                    return new Anno12PacketFile(path);
                default:
                    return null;
                    //throw new Exception("Unrecognized packet file");
            }
        }

        public static IPacketFile Factory(List<string> filenames)
        {
            switch (GetFileTypeId(filenames[0]))
            {
                case PacketFileType.CFE_PACKET_FILE:
                    return new PacketFileSequence<CFEPacketFile>(filenames);
                case PacketFileType.ITOS_PACKET_FILE:
                    return new PacketFileSequence<ITOSPacketFile>(filenames);
                case PacketFileType.ITOS_FRAME_FILE:
                case PacketFileType.RAF_FRAME_FILE:
                    return new SwapFixingFrameFileSequence(filenames);
                case PacketFileType.ANNO12_PACKET_FILE:
                    return new PacketFileSequence<Anno12PacketFile>(filenames);
                default:
                    return new PacketFileSequence<UnrecognizedPacketFile>(filenames);
            }
        }

        public static void MergeApidCounts(Dictionary<int, int> sum, Dictionary<int, int> addend)
        {
            foreach (var pair in addend)
            {
                if (sum.ContainsKey(pair.Key))
                    sum[pair.Key] += pair.Value;
                else
                    sum[pair.Key] = pair.Value;
            }
        }

        public static IEnumerable<byte[]> Iterator(String filename)
        {
            var f = Factory(filename);
            return f == null ? NullIterator() : f.Iterator();
        }

        public static IEnumerable<byte[]> NullIterator()
        {
            yield break;
        }

        public static IEnumerable<byte[]> Iterator(String filename, Int64 startTime, Int64 stopTime)
        {
            var f = Factory(filename);
            return f == null ? NullIterator() : f.Iterator(startTime, stopTime);
        }

        public static IEnumerable<byte[]> Iterator(String filename, int[] apids, Int64 startTime, Int64 stopTime,
            long skip = 0L)
        {
            var f = Factory(filename);
            return f == null ? NullIterator() : f.Iterator(apids, startTime, stopTime, skip);
        }

        public static IEnumerable<byte[]> Iterator(List<String> filenames)
        {
            return filenames.SelectMany(Iterator);
        }

        public static IEnumerable<byte[]> Iterator(String[] filenames)
        {
            return filenames.SelectMany(Iterator);
        }

        public static IEnumerable<byte[]> Iterator(List<String> filenames, Int64 startTime, Int64 stopTime)
        {
            return filenames.SelectMany(filename => Iterator(filename, startTime, stopTime));
        }

        public static IEnumerable<byte[]> Iterator(List<String> filenames, int[] apids, Int64 startTime, Int64 stopTime)
        {
            return filenames.SelectMany(filename => Iterator(filename, apids, startTime, stopTime));
        }

        /// <summary>
        ///     Used by the iterator with the skip operational parameter.  This will cause two initial
        ///     passes through the file and can be made more efficient.
        /// </summary>
        /// <param name="apids"></param>
        /// <param name="startTime"></param>
        /// <returns></returns>
        public int ApidMatchesAfterStart(int[] apids, Int64 startTime)
        {
            if (startTime == _cacheStartTime && _cacheApids == apids)
                return _cacheApidMatchesAfterStart;
            // Cache miss
            _cacheApids = apids;
            _cacheStartTime = startTime;
            if (EndTime42 < startTime)
            {
                _cacheApidMatchesAfterStart = 0;
                return _cacheApidMatchesAfterStart;
            }
            if (startTime <= BeginTime42)
            {
                _cacheApidMatchesAfterStart = ApidCount(apids);
                return _cacheApidMatchesAfterStart;
            }
            var count = 0;
            var max = apids.Count();
            foreach (var p in Iterator(startTime, Int64.MaxValue))
            {
                var apid = PacketAccessor.APID(p);
                for (var i = 0; i < max; i++)
                {
                    if (apid == apids[i])
                    {
                        count++;
                        break;
                    }
                }
            }
            _cacheApidMatchesAfterStart = count;
            return _cacheApidMatchesAfterStart;
        }

        public static int Compare(string a, string b)
        {
            var f1 = String.CompareOrdinal(System.IO.Path.GetFileNameWithoutExtension(a),
                System.IO.Path.GetFileNameWithoutExtension(b));
            if (f1 != 0) return f1;
            var aExt = System.IO.Path.GetExtension(a);
            var bExt = System.IO.Path.GetExtension(b);
            if (aExt == null || bExt == null) return 1;
            aExt = aExt.Substring(1);
            bExt = bExt.Substring(1);
            int aNum, bNum;
            if (!int.TryParse(aExt, out aNum) || !int.TryParse(bExt, out bNum))
                return String.CompareOrdinal(aExt, bExt);
            return aNum.CompareTo(bNum);
        }

        public static int Compare(PacketFile a, PacketFile b)
        {
            return Compare(a.Path, b.Path);
        }

        public virtual string NameForSequence()
        {
            return "Packet File Sequence";
        }
    }
}

// ReSharper restore InconsistentNaming