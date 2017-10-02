using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using gov.nasa.arc.ccsds.core;
using gov.nasa.arc.ccsds.decomm;
using System.Diagnostics;

namespace gov.nasa.arc.ccsds.utilities
{
    /// <summary>
    /// An instance of this class receives packets through its AppendPacket method and writes them
    /// to a set of csv files, one file per apid.  It limits the number of open file handles to
    /// OpenFileMax (default=30), and buffers the output so that it isn't constantly thrashing
    /// back and forth between files.  This is capable of efficiently writing all apids in
    /// a stream to csv files in one pass.
    /// </summary>
    public class ParallelCSVWriter : IDisposable
    {
        public const int BufferMaxSize = 65536;  //65536
        public const int OpenFileMax = 30;
        public string FilePrefix = @"";
        public bool[] IgnorePackets = new bool[2048];
        public int OpenFileCount = 0;
        public List<ParallelCSVWriterHelper> OpenPairs = new List<ParallelCSVWriterHelper>();
        public string Root = @".";
        public List<ParallelCSVWriterHelper> AllPairs = new List<ParallelCSVWriterHelper>();
        public List<ParallelCSVWriterHelper>[] StreamPairs = new List<ParallelCSVWriterHelper>[2048];
        public TelemetryDescription Dictionary;
        public bool WriteEngineeringValues = true;

        public void OpenPair(ParallelCSVWriterHelper p)
        {
            CheckOpenPairs();         //TODO
            int index;
            if (0 <= (index = OpenPairs.IndexOf(p)))
            {
                OpenPairs.RemoveAt(index);
                OpenPairs.Add(p);
            }
            else if (OpenPairs.Count < OpenFileMax)
            {
                p.Open();
                OpenPairs.Add(p);
            }
            else
            {
                OpenPairs[0].Close();
                OpenPairs.RemoveAt(0);
                OpenPairs.Add(p);
                p.Open();
            }
            CheckOpenPairs();      //TODO
        }

        public void Flush()
        {
            foreach (var p in AllPairs)
                p.Flush();
        }

        public void Close()
        {
            Flush();
            CheckOpenPairs();
            foreach (var p in OpenPairs)
                p.Close();
            OpenPairs.Clear();
        }

        private void CheckOpenPairs()
        {
            if (OpenPairs.Any(p => !(p.IsOpen())))
                throw new Exception("Invalid open pair check (1)");
            if (AllPairs.Any(p => p.IsOpen() && !OpenPairs.Contains(p)))
                throw new Exception("Invalid open pair check (2)");
        }

        public void AppendPacket(byte[] buf)
        {
            var apid = PacketAccessor.APID(buf);
            var len = PacketAccessor.Length(buf) + 7;
            Debug.Assert(apid < StreamPairs.Length);
            if (IgnorePackets[apid]) return;
            var pairs = StreamPairs[apid];
            if (pairs == null)
            {
                var packets = Dictionary.GetPackets(apid);
                if (packets.Count == 0)
                {
                    IgnorePackets[apid] = true;
                    //Console.Error.WriteLine(@"Ignoring apid {0}", apid);
                    return;
                }
                pairs = new List<ParallelCSVWriterHelper>(packets.Count);
                pairs.AddRange(packets.Select(packet => new ParallelCSVWriterHelper(this, packet)
                {
                    Path = DataPath(packet, FilePrefix),
                    Buffer = new StringBuilder(BufferMaxSize),
                    BufferMax = BufferMaxSize,
                    WriteEngineeringValues = WriteEngineeringValues
                }));
//                foreach (var pkt in packets)
//                    Console.Error.WriteLine(@"Adding handler for {0:d4} {1} ", apid, pkt.Name);
                StreamPairs[apid] = pairs;
                AllPairs.AddRange(pairs);
            }
            foreach (var pair in pairs)
                pair.AppendPacket(buf, len);
        }

        protected string DataPath(PacketInfo packet, string prefix = "")
        {
            //return Path.Combine(Root, string.Format(@"{0}{1:0000}.csv", prefix, apid));
            return Combine(Root, string.Format(@"{0}{1}.csv", prefix, packet.Name.ToLowerInvariant()));
        }

        //public static string Combine(params string[] parts) => (new Uri(Path.Combine(parts)).LocalPath);
        public static string Combine(params string[] parts)
        {
            if (_monostatus == MonoStatus.NotChecked)
                _monostatus = Type.GetType("Mono.Runtime") == null ? MonoStatus.Windows : MonoStatus.Mono;
            return _monostatus == MonoStatus.Mono ? new Uri(Path.Combine(parts)).LocalPath : string.Join("/", parts);
        }

        private enum MonoStatus { NotChecked, Windows, Mono }
        private static MonoStatus _monostatus = MonoStatus.NotChecked;

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // dispose managed state (managed objects).
                    Close();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~ParallelCSVWriter() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion


    }
}