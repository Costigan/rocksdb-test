using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using gov.nasa.arc.ccsds.core;
using gov.nasa.arc.ccsds.decomm;

namespace gov.nasa.arc.ccsds.utilities
{
    public class SeqprintEngine
    {
        private readonly TelemetryDescription _tdb;

        public SeqprintEngine(TelemetryDescription tdb)
        {
            _tdb = tdb;
        }

        public SeqPrintPointSpec MakeSeqPoint(string format, string pointId)
        {
            return new SeqPrintPointSpec {Point = _tdb.GetPoint(pointId), Format = format};
        }

        public SeqPrintPointSpec MakeSeqPoint(string format, string pointId, bool raw)
        {
            return new SeqPrintPointSpec {Point = _tdb.GetPoint(pointId), Format = format, Raw = raw};
        }

        public SeqPrintPointSpec MakeSeqPoint(string format, string pointId, bool raw, Func<dynamic, dynamic> function)
        {
            return new SeqPrintPointSpec
            {
                Point = _tdb.GetPoint(pointId),
                Format = format,
                Raw = raw,
                Function = function
            };
        }

        public void GenerateSeqprintsFromSpecs(IPacketFile source, List<SeqPrintSpec> specs)
        {
            StreamWriter[] writers = null;
            try
            {
                writers = specs.Select(s => new StreamWriter(s.Filename, true)).ToArray();

                // Write header lines
                for (var j = 0; j < writers.Count(); j++)
                {
                    for (var header = 0; header < specs[j].Points.Count; header++)
                    {
                        if (header > 0) writers[j].Write(',');
                        writers[j].Write(specs[j].Points[header].Point.Name);
                    }
                }

                foreach (var w in writers) w.WriteLine();

                // Set up arrays for persistance
                var persistance = specs.Select(spec => new dynamic[spec.Points.Count]).ToArray();

                // Set up arrays for apid filtering
                var apids = specs.Select(spec =>
                {
                    var set = new HashSet<int>();
                    foreach (var pspec in spec.Points)
                        set.Add(pspec.Point.APID);
                    return set.ToArray();
                }).ToArray();

                var set1 = new HashSet<int>();
                foreach (var x in apids.SelectMany(ary => ary))
                    set1.Add(x);
                var allApids = set1.ToArray();

                // Write the seqprint
                foreach (var packet in source.Iterator())
                {
                    var apid = PacketAccessor.APID(packet);
                    if (!allApids.Contains(apid)) continue;

                    //var dt = TimeUtilities.Time42ToDateTime(PacketAccessor.Time42(packet));
                    //var timestamp = dt.ToString("YY-mm-dd HH:mm:ss.mmmm");

                    for (var i = 0; i < specs.Count; i++)
                    {
                        if (!apids[i].Contains(apid)) continue;
                        var spec = specs[i];
                        for (var j = 0; j < spec.Points.Count; j++)
                        {
                            var point = spec.Points[j];
                            dynamic v;
                            if (point.Point.APID != apid)
                            {
                                v = persistance[i][j] ?? "null";
                            }
                            else
                            {
                                v = point.Raw ? point.Point.GetRawValue(packet) : point.Point.GetValue(packet);
                                if (point.Function != null)
                                    v = point.Function(v);
                                persistance[i][j] = v;
                            }
                            writers[i].Write(point.Format, v);
                        }
                        writers[i].WriteLine();
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                if (writers != null)
                {
                    foreach (var w in writers)
                        w.Close();
                }
            }
        }
    }
}