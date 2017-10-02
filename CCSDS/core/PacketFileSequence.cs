using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace gov.nasa.arc.ccsds.core
{
    public class PacketFileSequence<T> : IPacketFileSequence, IPacketFile where T : PacketFile, new()
    {
        public List<T> Files = new List<T>();
        private Dictionary<int, int> _apidCountsCache;
        private int[] _apids;
        private string _displayName;
        private T _packetFile;

        public PacketFileSequence(List<string> filenames, bool force = false)
        {
            filenames = filenames.Where(f => { var ext = Path.GetExtension(f); return ext == null ? true : !".H".Equals(ext.ToUpperInvariant()); }).ToList();
            filenames.Sort(PacketFile.Compare);
            Files = new List<T>();
            foreach (var filename in filenames)
            {
                if (force)
                {
                    Files.Add(new T {Path = filename});
                    continue;
                }
                var filetype = PacketFile.FileType(filename);
                if (typeof(T).IsAssignableFrom(filetype))
                {
                    Files.Add(new T { Path = filename });
                }
                //else if (filetype == typeof (ITOSPacketFile) && typeof (T) == typeof (ActivePacketFile))
                //    Files.Add((new ActivePacketFile {Path = filename}) as T);
                else
                {
                    Console.WriteLine(@"{0} is not a {1}.  Ignoring.", filename, typeof(T));
                }
            }
        }

        public void SetDisplayName(string name)
        {
            _displayName = name;
        }

        public int GetPacketCount(int[] apids, long start, long stop)
        {
            return Files.Sum(pf => pf.GetPacketCount(apids, start, stop));
        }

        public IEnumerable<byte[]> Iterator()
        {
            foreach (var pf in Files)
            {
                _packetFile = pf;
                //Console.WriteLine(@"Reading packet file {0} of type {1}", pf.Filename(), pf.FileType());
                foreach (var packet in _packetFile.Iterator())
                    yield return packet;
            }
        }

        public IEnumerable<byte[]> Iterator(Int64 startTime, Int64 stopTime)
        {
            foreach (var pf in Files)
            {
                // Don't cause the file to be pre-read to get the begin and end times.
                //if (pf.EndTime42 < startTime || pf.BeginTime42 > stopTime) continue;
                _packetFile = pf;
                foreach (var packet in _packetFile.Iterator(startTime, stopTime))
                    yield return packet;
            }
        }

        public IEnumerable<byte[]> Iterator(int[] apids, Int64 startTime, Int64 stopTime, long skip = 0L)
        {
            foreach (var pf in Files)
            {
                if (pf.EndTime42 < startTime)
                    continue; // redundant w/ PacketFile.ApidMatchesAfterStart(), but efficient
                int matches;
                if (skip > 0 && (matches = pf.ApidMatchesAfterStart(apids, startTime)) <= skip)
                {
                    // can skip
                    skip -= matches;
                }
                else
                {
                    // can't skip
                    _packetFile = pf;
                    foreach (var packet in _packetFile.Iterator(apids, startTime, stopTime, skip))
                        yield return packet;
                    skip = 0;
                }
            }
        }

        public string DisplayName()
        {
            if (_displayName != null) return _displayName;
            return Files.Count < 1 ? Filename() : Files[0].NameForSequence();
        }

        public string Filename()
        {
            if (_packetFile != null) return _packetFile.Filename();
            return Files.Count < 1 ? "Filename not known" : Files[0].Filename();
        }

        public Int64 Position()
        {
            return _packetFile.Position();
        }

        public int[] GetApids()
        {
            if (_apids != null) return _apids;
            var apids = new List<int>();
            foreach (var apid in from pf in Files from apid in pf.GetApids() where !apids.Contains(apid) select apid)
                apids.Add(apid);
            _apids = apids.ToArray();
            return _apids;
        }

        public int GetPacketCount()
        {
            return Files.Sum(pf => pf.GetPacketCount());
        }

        public Dictionary<int, int> GetApidCounts()
        {
            if (_apidCountsCache != null) return _apidCountsCache;
            _apidCountsCache = new Dictionary<int, int>();
            foreach (var pf in Files)
                PacketFile.MergeApidCounts(_apidCountsCache, pf.GetApidCounts());
            return _apidCountsCache;
        }

        public long GetBeginTime42()
        {
            return Files.Count > 0 ? Files[0].BeginTime42 : -1;
        }

        public long GetBeginTime42Jammed()
        {
            return Files.Count > 0 ? Files.Min(f => f.BeginTime42Jammed): -1;
        }

        public long GetEndTime42()
        {
            return Files.Count > 0 ? Files[Files.Count - 1].EndTime42 : -1;
        }

        public IEnumerable<string> GetPaths()
        {
            return Files.Select(f => f.Path);
        }

        public IEnumerable<object> GetPacketFiles()
        {
            return Files;
        }

        public object GetPacketFile()
        {
            return _packetFile;
        }

        public void UnloadStats()
        {
            _apidCountsCache = null;
            _apids = null;
        }
    }
}