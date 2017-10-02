using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using gov.nasa.arc.ccsds.prechannelized;
using Newtonsoft.Json.Linq;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using static gov.nasa.arc.ccsds.decomm.PointInfo;

namespace gov.nasa.arc.ccsds.decomm
{
    /// <summary>
    ///     An instance of TelemetryDescription describes how telemetry points are laid out in packets.
    ///     This uses the singleton pattern ... only one instance is supported per process.  The TelemetryDescriptionFile
    ///     determines which xml file is loaded, and this class property can be set before the program creates the single
    ///     instance.
    /// </summary>
    public class TelemetryDescription
    {
        protected static TelemetryDescription Database = null;

        protected Dictionary<int, PacketInfo> APIDToPacket = null;
        private string _filename;
        public string Filename
        {
            get { return _filename; }
        }

        private List<PacketInfo> _packets = new List<PacketInfo>();
        private List<PointInfo> _points = new List<PointInfo>();

        private readonly JObject _pointInfoOverride = new JObject();

        private Dictionary<string, PacketInfo> _packetLookup;
        private readonly Dictionary<string, PointInfo> _pointLookup = new Dictionary<string, PointInfo>(StringComparer.OrdinalIgnoreCase);

        public ResourceProspectorPayloadDictionary PayloadDictionary;

        #region Static Methods

        public static string TelemetryDescriptionFile { get; set; } = @"warp.dictionary.json.gz";

        public static void SetDefaultIfNull(string value)
        {
            if (TelemetryDescription.TelemetryDescriptionFile == null)
                TelemetryDescription.TelemetryDescriptionFile = value;
        }

        public static TelemetryDescription GetDatabase()
        {
            return GetDatabase(TelemetryDescriptionFile);
        }

        public static TelemetryDescription GetDatabase(string filename)
        {
            if (Database == null)
                return Database = new TelemetryDescription(filename);
            if (!Database.Filename.Equals(filename))
                throw new Exception(string.Format(@"Attempt to load two databases: {0} vs {1}", Database.Filename, filename));
            return Database;
        }

        #endregion Static Methods

        #region Initializers

        public TelemetryDescription()
        {
            Load(TelemetryDescriptionFile);
        }

        public TelemetryDescription(string filename)
        {
            Load(filename);
        }

        #endregion Initializers

        #region Loading

        public void Load(string filename)
        {
            var foundName = SearchForDictionaryFile(filename);
            if (foundName== null)
                throw new Exception(@"Couldn't find telemetry description file.  Throwing exception.");
            _filename = foundName;
            Console.WriteLine(@"Loading telemetry description file: {0}", _filename);
            var holder = ReadHolder(foundName);
            if (holder == null)
                throw new Exception(string.Format(@"Unrecognized file type: {0}", foundName));
            _packets = holder.Packets;
            PointInfo.UnitsArray = holder.Units.ToArray();
            PropagateConvertors(holder);
            LinkPointsToPackets(Packets);
            _points = GetAllPoints(Packets);
            BuildApidDictionary();
            FixSignedBitExtractions();
            Console.WriteLine(@" done.");
        }

        public SerializedTelemetryDictionary ReadHolder(string filename)
        {
            if (filename.IndexOf("xml", StringComparison.OrdinalIgnoreCase) > 0)
            {
                var deserializer = GetSerializer();
                SerializedTelemetryDictionary holder;
                using (var stream = filename.EndsWith(".gz", StringComparison.InvariantCultureIgnoreCase)
                    ? new GZipStream(File.OpenRead(filename), CompressionMode.Decompress)
                    : (Stream)new FileStream(filename, FileMode.Open, FileAccess.Read))
                {
                    holder = (SerializedTelemetryDictionary)deserializer.ReadObject(stream);
                }
                return holder;
            }
            if (filename.IndexOf("json", StringComparison.OrdinalIgnoreCase) > 0)
            {
                var deserializer = new JsonSerializer();
                SerializedTelemetryDictionary holder;
                using (var stream = filename.EndsWith(".gz", StringComparison.InvariantCultureIgnoreCase)
                    ? new GZipStream(File.OpenRead(filename), CompressionMode.Decompress)
                    : (Stream)new FileStream(filename, FileMode.Open, FileAccess.Read))
                using (var tr = new StreamReader(stream))
                using (var jr = new JsonTextReader(tr))
                     holder = deserializer.Deserialize<SerializedTelemetryDictionary>(jr);
                return holder;
            }
            return null;
        }

        //TODO: This is overly complicated.  All I really want to do is to add a directory if it's missing from the path,
        // then check to see that the path exists.
        private string SearchForDictionaryFile(string path)
        {
            if (File.Exists(path)) return path;
            if (path == null) return null;

            var filename = Path.GetFileName(path);

            //var workingDir = Directory.GetCurrentDirectory();
            var exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase);
            if (exeDir.StartsWith(@"file:\"))
                exeDir = exeDir.Substring(6);

            var paths = new HashSet<string>();
            paths.Add(filename);
            paths.Add(AddGz(filename));
            paths.Add(RemoveGz(filename));
            paths.Add(TelemetryDescription.TelemetryDescriptionFile);
            paths.Add(AddGz(TelemetryDescription.TelemetryDescriptionFile));
            paths.Add(RemoveGz(TelemetryDescription.TelemetryDescriptionFile));

            var soFar = paths.ToList();
            foreach (var p in soFar)
                paths.Add(Path.Combine(exeDir, p));

            foreach (var p in paths)
            {
                if (File.Exists(p))
                {
                    TelemetryDescription.TelemetryDescriptionFile = p;
                    return p;
                }
            }

            Console.Error.WriteLine(@"Can't find telemetry description file.  Tried these paths:");
            foreach (var p in paths)
                Console.Error.WriteLine(@"  {0}", p);
            return null;
        }

        private string AddGz(string path)
        {
            return ".gz".Equals(Path.GetExtension(path)) ? path : path + ".gz";
        }

        private string RemoveGz(string path)
        {
            return ".gz".Equals(Path.GetExtension(path))
                ? Path.GetFileNameWithoutExtension(path)
                : path;
        }

        protected DataContractSerializer GetSerializer()
        {
            var extraTypes = new[] { typeof(PointInfo), typeof(PointInfoLim), typeof(List<PointInfo>), typeof(DiscreteConversionList), typeof(DiscreteConversionMap), typeof(DiscreteConversionRangeList),
                typeof(DiscreteConversionRange), typeof(PolynomialConversion), typeof(ConversionPlaceholder) };
            return new DataContractSerializer(typeof(SerializedTelemetryDictionary), extraTypes);
        }

        // For each converter, find the one real converter, then replace all conversion placeholders with that
        protected void PropagateConvertors(SerializedTelemetryDictionary holder)
        {
            var dict = new Dictionary<string, IConversion>();
            foreach (var c in holder.ListConversions)
                dict.Add(c.GetName(), c);
            foreach (var c in holder.MapConversions)
                dict.Add(c.GetName(), c);
            foreach (var c in holder.RangeConversions)
                dict.Add(c.GetName(), c);
            foreach (var c in holder.PolyConversions)
                dict.Add(c.GetName(), c);
            dict.Add(Conversion.SubsecondsToFraction.GetName(), Conversion.SubsecondsToFraction);

            IConversion conv = null;
            foreach (var pkt in holder.Packets)
            {
                foreach (var pt in pkt.Points)
                {
                    var f = pt.ConversionFunction;
                    if (f == null) continue;
                    var ph = f as ConversionPlaceholder;
                    if (ph == null)
                    {
                        Console.WriteLine("Unrecognized placeholder: {0}", f.GetName());
                        pt.ConversionFunction = null;
                        continue;
                    }
                    if (!dict.TryGetValue(ph.GetName(), out conv))
                    {
                        Console.WriteLine("Unrecognized placeholder: {0}", f.GetName());
                        pt.ConversionFunction = null;
                        continue;
                    }
                    pt.ConversionFunction = conv;
                }
            }

            // Add timestamp conversions and jam identity conversions
            var total = 0;
            var countIdentity = 0;
            foreach (var pkt in holder.Packets)
            {
                foreach (var pt in pkt.Points)
                {
                    total++;
                    switch (pt.FieldType)
                    {
                        case PointInfo.PointType.TIME40:
                            pt.ConversionFunction = Conversion.Time40Timestamp;
                            break;
                        case PointInfo.PointType.TIME42:
                            pt.ConversionFunction = Conversion.Time42Timestamp;
                            break;
                        case PointInfo.PointType.TIME44:
                            pt.ConversionFunction = Conversion.Time44Timestamp;
                            break;
                        default:
                            if (pt.ConversionFunction == null)
                            {
                                pt.ConversionFunction = Conversion.Identity;
                                countIdentity++;
                            }
                            break;
                    }
                }
            }
            //Console.WriteLine(@"Inserted {0} identity functions out of {1} total functions ({2:F0}%)", countIdentity, total, (100f*countIdentity)/total);
        }

        protected void LinkPointsToPackets(List<PacketInfo> packets)
        {
            foreach (var packet in packets)
            {
                var apid = packet.APID;
                foreach (var point in packet.Points)
                {
                    point.Packet = packet;
                    point.APID = apid;
                }
            }
        }

        private void BuildApidDictionary()
        {
            APIDToPacket = new Dictionary<int, PacketInfo>(Packets.Count);
            foreach (var p in Packets.Where(p => !APIDToPacket.ContainsKey(p.APID)))
                APIDToPacket.Add(p.APID, p);
        }

        private void FixSignedBitExtractions()
        {
            foreach (var point in from packet in Packets
                                  from point in packet.Points
                                  where
                                      point.FieldType == PointInfo.PointType.I1234
                                      || point.FieldType == PointInfo.PointType.I12
                                  select point)
            {
                if (point.FieldType == PointInfo.PointType.I1234 && (point.bit_start != 0 || point.bit_stop != 31))
                    point.FieldType = PointInfo.PointType.I1234b;
                else if (point.FieldType == PointInfo.PointType.I12 && (point.bit_start != 0 || point.bit_stop != 15))
                    point.FieldType = PointInfo.PointType.I12b;
            }
            //Console.WriteLine(@"Fixed signed bit extractions.");
        }

        public static string GetWarpPointType(PointInfo pi)
        {
            var fieldint = (int)pi.FieldType;

            if (fieldint == 5) return "string";
            if (fieldint < 2) return "float";
            if (6 <= fieldint && fieldint <= 8) return "utc";
            if (fieldint >= 20) return "number";
            return "integer";
        }

        #endregion Loading

        #region Accessors

        public List<PacketInfo> Packets
        {
            get { return _packets; }
        }

        public List<PointInfo> Points
        {
            get { return _points; }
        }

        private Dictionary<string, PacketInfo> PacketLookup
        {
            get
            {
                if (_packetLookup != null) return _packetLookup;
                _packetLookup = new Dictionary<string, PacketInfo>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in Packets)
                    _packetLookup.Add(p.Id, p);
                return _packetLookup;
            }
        }

        public List<PointInfo> GetAllPoints(List<PacketInfo> packets)
        {
            var result = new List<PointInfo>();
            foreach (var packet in packets)
                result.AddRange(packet.Points);
            result.Sort(ComparePointInfo);
            return result;
        }

        public PacketInfo GetPacket(string name)
        {
            return
                Packets.Find(packet => 0 == string.Compare(name, packet.Name, StringComparison.OrdinalIgnoreCase));
        }

        public PacketInfo GetPacketById(string id)
        {
            // return
            //     Packets.FirstOrDefault(
            //         packet => 0 == string.Compare(id, packet.Id, StringComparison.OrdinalIgnoreCase));
            PacketInfo pi;
            return PacketLookup.TryGetValue(id, out pi) ? pi : null;
        }

        public PacketInfo GetPacket(int apid)
        {
            PacketInfo p;
            APIDToPacket.TryGetValue(apid, out p);
            return p;
        }

        public List<PacketInfo> GetPackets(int apid)
        {
            return Packets.Where(p => p.APID == apid).ToList();
        }

        public bool IsValidAPID(int apid)
        {
            PacketInfo p;
            APIDToPacket.TryGetValue(apid, out p);
            if (p != null) return true;
            return apid == 2001;
        }

        public PointInfo GetPoint(string packetId, string pointId)
        {
            var packet = GetPacket(packetId);
            return packet == null ? null : packet.GetPoint(pointId);
        }

        public PointInfo GetPoint(string pointId)
        {
            PointInfo pi;
            if (_pointLookup.TryGetValue(pointId, out pi))
                return pi;
            var index = pointId.IndexOf('.');
            if (index < 0)
                return null;  // Bad index format
            var packetId = pointId.Substring(0, index);

            // We haven't indexed this packet yet.
            var packetInfo = GetPacketById(packetId);
            if (packetInfo == null)
                return null;
            foreach (var pointInfo in packetInfo.Points)
            {
                if (!_pointLookup.ContainsKey(pointInfo.Id))
                    _pointLookup.Add(pointInfo.Id, pointInfo);
            }

            // Now, lookup again
            return (_pointLookup.TryGetValue(pointId, out pi)) ? pi : null;
        }

        public JToken GetPointInfoOverride(string id)
        {
            return _pointInfoOverride[id];
        }

        #endregion Accessors
    }
}