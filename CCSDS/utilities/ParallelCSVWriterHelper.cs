using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using gov.nasa.arc.ccsds.core;
using gov.nasa.arc.ccsds.decomm;

namespace gov.nasa.arc.ccsds.utilities
{
    /// <summary>
    /// An instance of this class manages the writing of one csv file for ParallelCSVWriter instances.
    /// </summary>
    public class ParallelCSVWriterHelper
    {
        private readonly ParallelCSVWriter _parent; //todo why do we need this?

        public string Name;
        public int APID;
        public bool AddApid = true;
        public bool AddLoganTimestamp = false;
        public bool AddStkTimestamp = false;
        public bool AddTimestamp = true;
        public bool AddTimestampEt = false;
        public bool AddTimestampLong = false;
        public bool AddTimestampSeconds = true;
        public bool AddTimestampSubseconds = true;
        public bool AddTimestampSubsecondsFloat = false;
        public StringBuilder Buffer;
        public int BufferMax = 65536;
        public bool FirstLineHasBeenWritten = false;

        public string Path;
        public List<PointInfo> Points = new List<PointInfo>();
        public bool WriteEngineeringValues = false;
        public TextWriter Writer;

        public ParallelCSVWriterHelper(ParallelCSVWriter parent, PacketInfo packet)
        {
            _parent = parent;
            APID = packet.APID;
            Name = packet.Name.ToLowerInvariant();
            Points = packet.Points;
        }

        public int FillPointer
        {
            get { return Buffer.Length; }
        }

        public bool IsOpen()
        {
            return Writer != null;
        }

        public void Open()
        {
            //Console.Error.WriteLine(@"Opening {0}", Path);
            Writer = new StreamWriter(new FileStream(Path, FileMode.Append, FileAccess.Write));
        }

        /// <summary>
        /// Called in either the open or closed state
        /// </summary>
        public void Flush()
        {
            if (FillPointer <= 0) return;
            if (Writer == null)
                _parent.OpenPair(this);
            Debug.Assert(Writer != null);
            Writer.Write(Buffer.ToString());
            Buffer.Clear();
        }

        public void Close()
        {
            //Console.Error.WriteLine(@"Closing {0}", Path);
            if (Writer == null)
                return;
            // Flush any packets
            Flush();
            Writer.Close();
            Writer = null;
        }

        public void AppendPacket(byte[] packet, int len)
        {
            if (!FirstLineHasBeenWritten)
                WriteFirstLine();
            if (FillPointer + len >= BufferMax)
                Flush();
            var points = Points;
            var apid = PacketAccessor.APID(packet);
            var timestamp = PacketAccessor.Time42(packet);

            if (AddTimestamp)
            {
                Buffer.Append(TimeUtilities.Time42ToString(timestamp));
                Buffer.Append(@", ");
            }
            if (AddLoganTimestamp)
            {
                Buffer.Append(TimeUtilities.Time42ToLogan(timestamp));
                Buffer.Append(@", ");
            }
            if (AddStkTimestamp)
            {
                Buffer.Append(TimeUtilities.Time42ToSTK(timestamp));
                Buffer.Append(@", ");
            }
            if (AddTimestampEt)
            {
                Buffer.Append(TimeUtilities.Time42ToET(timestamp));
                Buffer.Append(@", ");
            }
            if (AddTimestampSeconds)
            {
                Buffer.Append(TimeUtilities.Time42ToSeconds(timestamp));
                Buffer.Append(@", ");
            }
            if (AddTimestampSubseconds)
            {
                Buffer.Append(TimeUtilities.Time42ToSubseconds(timestamp));
                Buffer.Append(@", ");
            }
            if (AddTimestampSubsecondsFloat)
            {
                Buffer.Append(TimeUtilities.Time42ToSubsecondsFloat(timestamp));
                Buffer.Append(@", ");
            }
            if (AddTimestampLong)
            {
                Buffer.Append(timestamp);
                Buffer.Append(@", ");
            }
            if (AddApid)
            {
                Buffer.Append(apid);
                Buffer.Append(@", ");
            }

            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];
                var v = WriteEngineeringValues ? point.GetValue(packet) : point.GetRawValue(packet);
                if (i > 0)
                    Buffer.Append(", ");
                Buffer.Append(v);
            }
            Buffer.AppendLine();
        }

        public void WriteFirstLine()
        {
            if (AddTimestamp) Buffer.Append(@"Timestamp, ");
            if (AddLoganTimestamp) Buffer.Append(@"Timestamp, ");
            if (AddStkTimestamp) Buffer.Append(@"Timestamp_stk, ");
            if (AddTimestampEt) Buffer.Append(@"Epoch_Seconds, ");
            if (AddTimestampSeconds) Buffer.Append(@"Seconds, ");
            if (AddTimestampSubseconds) Buffer.Append(@"Subseconds, ");
            if (AddTimestampSubsecondsFloat) Buffer.Append(@"Subseconds, ");
            if (AddTimestampLong) Buffer.Append(@"Time42, ");
            if (AddApid) Buffer.Append(@"Apid, ");

            var firstId = Points[0].Id;
            var take = firstId.IndexOf('.') + 1;

            for (var i = 0; i < Points.Count; i++)
                Buffer.Append(string.Format(i > 0 ? ", {0}" : "{0}", Points[i].Id.Substring(take)));
            Buffer.AppendLine();
            FirstLineHasBeenWritten = true;
        }
    }
}