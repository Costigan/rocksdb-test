using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CsvHelper;
using gov.nasa.arc.ccsds.core;
using gov.nasa.arc.ccsds.decomm;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace gov.nasa.arc.ccsds.prechannelized
{
    public class ResourceProspectorPayloadDictionary
    {
        public string MSIDFilename;
        public string MSIDPacketFilename;

// ReSharper disable InconsistentNaming
        public List<MSID> MSIDs;
// ReSharper restore InconsistentNaming
        public List<PacketInfo> Packets = new List<PacketInfo>();

        public Dictionary<string, MSID> MSIDToPointInfo;
        public MSID[] MSIDValueToPointInfo;

        public List<PacketInfo> GetMSIDPackets()
        {
            LoadMSIDCsv();
            WriteMSIDPacketFile();
            LoadMSIDPackets();
            HandBuildPayloadPackets();
            return Packets;
        }

        public void LoadMSIDCsv()
        {
            MSIDToPointInfo = new Dictionary<string, MSID>();
            using (var csv = new CsvReader(File.OpenText(MSIDFilename)))
            {
                while (csv.Read())
                {
                    if (csv.CurrentRecord.Length < 11)
                        throw new Exception(string.Format(@"Wrong number of columns in {0}", MSIDFilename));
                    var label = csv.GetField<string>(5);
                    var valueString = csv.GetField<string>(6);
                    var value = Convert.ToUInt16(valueString.Substring(2), 16);
                    var nomenclature = csv.GetField<string>(9);
                    var system = csv.GetField<string>(10);
                    var typeString = csv.GetField<string>(11);
                    if ("Command".Equals(typeString))
                        continue;

                    MSID pi;
                    if (MSIDToPointInfo.TryGetValue(label, out pi))
                        throw new Exception(@"Duplicate MSID");
                    var msid = new MSID
                    {
                        Label = label,
                        Value = value,
                        Nomenclature = nomenclature,
                        System = system,
                        Name = label + " - " + nomenclature,
                        APID = 513 // not all MSIDs will really come on that apid, but all DataPointFrame apids dispatch to the same handler
                    };
                    MSIDToPointInfo.Add(label, msid);
                }
            }

            MSIDValueToPointInfo = new MSID[65536];
            foreach (var msid in MSIDToPointInfo.Values)
                MSIDValueToPointInfo[msid.Value] = msid;
        }

        /// <summary>
        /// This generates a default msid packet file based on subsystems and end item classes.
        /// This must be called after LoadMSIDCsv
        /// 
        /// Environment
        /// Heater                  H
        /// Cooler (TEC)            O
        /// Temperature Sensor      T
        /// Pressure Sensor         P
        /// Humidity Sensor         R
        /// 
        /// Movement
        /// Valve                   V
        /// Motor                   M
        /// Brake                   K
        /// Solenoid                S
        /// Encoder                 E
        /// Rotary Solenoid         Y
        /// 
        /// Power
        /// Voltage Sensor          L
        /// Current Sensor          A
        /// Power Switch            W
        /// Switch Indicator        I
        /// Load Cell               C
        /// Potentiometer           N
        /// 
        /// Other
        /// Pseudo End Item         X
        /// Generic End Item        G
        /// </summary>
        public void WriteMSIDPacketFile()
        {
            if (MSIDToPointInfo == null)
                throw new Exception(@"Call LoadMSIDCsv before calling WriteMSIDPacketFile");
            var top = new JObject();
            AddGenericPayloadPackets(ref top, 'N', "PAYLOAD_NSS");
            AddGenericPayloadPackets(ref top, 'I', "PAYLOAD_NIRVSS");
            AddGenericPayloadPackets(ref top, 'F', "PAYLOAD_Fluid System");
            AddGenericPayloadPackets(ref top, 'O', "PAYLOAD_OVEN");
            AddGenericPayloadPackets(ref top, 'D', "PAYLOAD_Drill");
            AddGenericPayloadPackets(ref top, 'C', "PAYLOAD_Drill Ops Camera");
            AddGenericPayloadPackets(ref top, 'A', "PAYLOAD_Avionics");
            AddGenericPayloadPackets(ref top, 'S', "PAYLOAD_Software");
            AddGenericPayloadPackets(ref top, 'R', "PAYLOAD_NIRST");
            AddGenericPayloadPackets(ref top, 'W', "PAYLOAD_Water Droplet Camera");
            AddGenericPayloadPackets(ref top, 'M', "PAYLOAD_MS");
            AddGenericPayloadPackets(ref top, 'V', "PAYLOAD_Oven Ops Camera");
            AddGenericPayloadPackets(ref top, 'G', "PAYLOAD_GC");
        }

        private void AddGenericPayloadPackets(ref JObject top, char subsystem, string prefix)
        {
            AddPayloadPacket(ref top, prefix + "_Environment", subsystem, "HOTPR");
            AddPayloadPacket(ref top, prefix + "_Movement", subsystem, "VMKSEY");
            AddPayloadPacket(ref top, prefix + "_Power", subsystem, "LAWICN");
            AddPayloadPacket(ref top, prefix + "_Other", subsystem, "XG");

            using (var sw = new StreamWriter(MSIDPacketFilename))
            using (var jw = new JsonTextWriter(sw))
            {
                var serializer = new JsonSerializer();
                serializer.Serialize(jw, top);
            }
        }

        private void AddPayloadPacket(ref JObject top, string packetName, char subsystem, string classes)
        {
            var points =
                MSIDToPointInfo.Values.Where(m => m.Label[0] == subsystem && classes.Contains(m.Label[1]))
                    .OrderBy(m => m.Label)
                    .ToList();
            if (points.Count < 1) return;

            //Console.Write(@"{0} => ", packetName);
            //foreach (var p in points) Console.Write(@"{0}, ", p.Label);
            //Console.WriteLine();

            var array = new JArray();
            foreach (var point in points)
                array.Add(point.Label);
            top.Add(packetName, array);
        }

        public void LoadMSIDPackets()
        {
            JObject top;
            using (var file = File.OpenText(MSIDPacketFilename))
            using (var jsonReader = new JsonTextReader(file))
            {
                var serializer = new JsonSerializer();
                top = serializer.Deserialize(jsonReader) as JObject;
            }
            if (top == null)
                throw new Exception(string.Format(@"Failed to read {0}", MSIDPacketFilename));

            Packets = new List<PacketInfo>();
            //MSIDToPointInfo = new Dictionary<string, MSID>();
            foreach (var prop in top.Properties())
            {
                var pktName = prop.Name;
                var dotted = pktName + ".";
                var points = new List<PointInfo>();
                var packet = new PacketInfo {Id = pktName, Name = pktName, Points = points};
                Packets.Add(packet);
                foreach (var pn in prop.Value)
                {
                    try
                    {
                        var pointName = (string) pn;
                        MSID msid;
                        if (!MSIDToPointInfo.TryGetValue(pointName, out msid))
                        {
                            msid = new MSID {Id = dotted + pointName, Name = pointName, APID=513};  // default apid
                            MSIDToPointInfo.Add(pointName, msid);
                        }
                        else
                        {
                            msid.Id = dotted + pointName;
                        }
                        points.Add(msid);
                    }
                    catch (Exception e1)
                    {
                        Console.WriteLine(e1);
                    }
                }
            }
        }

        public void HandBuildPayloadPackets()
        {
            var pkts = new List<PacketInfo>();
            var pkt = new PacketInfo  // NearIrFrame
            {
                APID = 524,
                Documentation = "NIRVSS spectrum and metadata",
                Name = "PAYLOAD_NIRVSS_SPECTRUM"
            };
            AddCcsdsPacketHeaderFields(pkt);
            pkt.Points.Add(new PointInfo { Name = "IR_Frame_Type", UnitsIndex = 0, bit_start = 0, bit_stop = 7, byte_offset = 12, byte_size = 1, FieldType = PointInfo.PointType.U1 });
            //pkt.Points.Add(new PointInfo { Name = "Frame_Acq_Time_sec", UnitsIndex = 0, bit_start = 0, bit_stop = 31, byte_offset = 13, byte_size = 4, FieldType = PointInfo.PointType.U1234 });
            //pkt.Points.Add(new PointInfo { Name = "Frame_Acq_Time_nsec", UnitsIndex = 0, bit_start = 0, bit_stop = 31, byte_offset = 17, byte_size = 4, FieldType = PointInfo.PointType.U1234 });
            pkt.Points.Add(new PointInfo { Name = "Acquisition_Time", UnitsIndex = 0, bit_start = 0, bit_stop = 63, byte_offset = 13, byte_size = 8, FieldType = PointInfo.PointType.U12345678, ConversionFunction = new Time44Conversion() });
            pkt.Points.Add(new PointInfo { Name = "Instrument_Id", UnitsIndex = 0, bit_start = 0, bit_stop = 31, byte_offset = 21, byte_size = 4, FieldType = PointInfo.PointType.U1234 });
            pkt.Points.Add(new PointInfo { Name = "OpCode", UnitsIndex = 0, bit_start = 0, bit_stop = 7, byte_offset = 25, byte_size = 1, FieldType = PointInfo.PointType.U1 });
            pkt.Points.Add(new PointInfo { Name = "Valid_Frame", UnitsIndex = 0, bit_start = 0, bit_stop = 7, byte_offset = 26, byte_size = 1, FieldType = PointInfo.PointType.U1 });
            pkt.Points.Add(new PointInfo { Name = "Number_Of_Coads", UnitsIndex = 0, bit_start = 0, bit_stop = 15, byte_offset = 427, byte_size = 1, FieldType = PointInfo.PointType.U12 });
            pkt.Points.Add(new PointInfo { Name = "Shifted_Flag", UnitsIndex = 0, bit_start = 0, bit_stop = 15, byte_offset = 429, byte_size = 2, FieldType = PointInfo.PointType.U1234 });
            pkts.Add(pkt);

            pkt = new PacketInfo  // CameraFrame
            {
                APID = 524,
                Documentation = "Camera Frame",
                Name = "PAYLOAD_CAMERA_FRAME"
            };
            AddCcsdsPacketHeaderFields(pkt);
            pkt.Points.Add(new PointInfo { Name = "Frame_Acquisition_Time", UnitsIndex = 0, bit_start = 0, bit_stop = 63, byte_offset = 12, byte_size = 8, FieldType = PointInfo.PointType.U12345678, ConversionFunction = new Time44Conversion() });
            pkt.Points.Add(new PointInfo { Name = "Picture_Acquisition_Time", UnitsIndex = 0, bit_start = 0, bit_stop = 63, byte_offset = 20, byte_size = 8, FieldType = PointInfo.PointType.U12345678, ConversionFunction = new Time44Conversion() });
            pkt.Points.Add(new PointInfo { Name = "Camera_Id", UnitsIndex = 0, bit_start = 0, bit_stop = 63, byte_offset = 28, byte_size = 4, FieldType = PointInfo.PointType.U1234 });
            pkt.Points.Add(new PointInfo { Name = "Valid_Frame", UnitsIndex = 0, bit_start = 0, bit_stop = 7, byte_offset = 32, byte_size = 1, FieldType = PointInfo.PointType.U1 });
            pkt.Points.Add(new PointInfo { Name = "Image_Size", UnitsIndex = 0, bit_start = 0, bit_stop = 31, byte_offset = 33, byte_size = 4, FieldType = PointInfo.PointType.U1234 });
            pkt.Points.Add(new PointInfo { Name = "Frame_Type", UnitsIndex = 0, bit_start = 0, bit_stop = 31, byte_offset = 37, byte_size = 4, FieldType = PointInfo.PointType.U1234 });
            pkt.Points.Add(new PointInfo { Name = "Frame_Height", UnitsIndex = 0, bit_start = 0, bit_stop = 31, byte_offset = 41, byte_size = 4, FieldType = PointInfo.PointType.U1234 });
            pkt.Points.Add(new PointInfo { Name = "Frame_Width", UnitsIndex = 0, bit_start = 0, bit_stop = 31, byte_offset = 45, byte_size = 4, FieldType = PointInfo.PointType.U1234 });
            pkts.Add(pkt);

            pkt = new PacketInfo  // GcFrame
            {
                APID = 516,
                Documentation = "GC Frame",
                Name = "GC_FRAME"
            };
            AddCcsdsPacketHeaderFields(pkt);
            pkt.Points.Add(new PointInfo { Name = "Frame_Acquisition_Time", UnitsIndex = 0, bit_start = 0, bit_stop = 63, byte_offset = 12, byte_size = 8, FieldType = PointInfo.PointType.U12345678, ConversionFunction = new Time44Conversion() });
            pkt.Points.Add(new PointInfo { Name = "Run_Number", UnitsIndex = 0, bit_start = 0, bit_stop = 31, byte_offset = 20, byte_size = 4, FieldType = PointInfo.PointType.U1234 });
            pkt.Points.Add(new PointInfo { Name = "Detector_Report_Counter", UnitsIndex = 0, bit_start = 0, bit_stop = 31, byte_offset = 24, byte_size = 4, FieldType = PointInfo.PointType.U1234 });
            pkt.Points.Add(new PointInfo { Name = "Frequency", UnitsIndex = 0, bit_start = 0, bit_stop = 31, byte_offset = 28, byte_size = 4, FieldType = PointInfo.PointType.F1234 });
            pkt.Points.Add(new PointInfo { Name = "TCD_Data_Size", UnitsIndex = 0, bit_start = 0, bit_stop = 31, byte_offset = 32, byte_size = 4, FieldType = PointInfo.PointType.U1234 });
            pkts.Add(pkt);

            pkt = new PacketInfo  // GcFrame
            {
                APID = 531,
                Documentation = "System Acknowledgment",
                Name = "SystemAck"
            };
            AddCcsdsPacketHeaderFields(pkt);

            // Set the ids
            foreach (var pk in pkts)
                pk.Id = pk.Name;
            foreach (var pk in pkts)
            {
                foreach (var pt in pk.Points)
                    pt.Id = pk.Id + "." + pt.Name;
            }

            foreach (var pi in pkts)
            {
                if (pi.APID == 0)
                    throw new Exception(@"Zero apid in payload packet.  There's been an error building the payload dictionary");
                foreach (var pt in pi.Points)
                    pt.APID = pi.APID;
            }

            Packets.AddRange(pkts);
        }

        private void AddCcsdsPacketHeaderFields(PacketInfo pkt)
        {
            pkt.Points.Add(new PointInfo { Name = "applicationId", UnitsIndex = 0, bit_start = 5, bit_stop = 15, byte_offset = 0, byte_size = 2, FieldType = PointInfo.PointType.U12b });
            pkt.Points.Add(new PointInfo { Name = "sequenceCount", UnitsIndex = 0, bit_start = 2, bit_stop = 15, byte_offset = 2, byte_size = 2, FieldType = PointInfo.PointType.U12b });
            pkt.Points.Add(new PointInfo { Name = "length", UnitsIndex = 0, bit_start = 0, bit_stop = 15, byte_offset = 4, byte_size = 2, FieldType = PointInfo.PointType.U12 });
            pkt.Points.Add(new PointInfo { Name = "Timestamp", UnitsIndex = 0, bit_start = 0, bit_stop = 47, byte_offset = 6, byte_size = 6, FieldType = PointInfo.PointType.TIME42 });
        }
    }

    public class DataPointFrameParser
    {
        public byte[] Packet;
        public int Offset;

        public Byte GetByte()
        {
            var v = Packet[Offset];
            Offset++;
            return v;
        }

        public UInt16 GetUInt16()
        {
            var v = PacketAccessor.GetU12(Packet, Offset);
            Offset += 2;
            return v;
        }

        public UInt32 GetUInt32()
        {
            var v = PacketAccessor.GetU1234(Packet, Offset);
            Offset += 4;
            return v;
        }

        public UInt64 GetUInt64()
        {
            var v = PacketAccessor.GetU12345678(Packet, Offset);
            Offset += 8;
            return v;
        }

        public Int16 GetInt16()
        {
            var v = PacketAccessor.GetI12(Packet, Offset);
            Offset += 2;
            return v;
        }

        public Int32 GetInt32()
        {
            var v = PacketAccessor.GetI1234(Packet, Offset);
            Offset += 4;
            return v;
        }

        public Int64 GetInt64()
        {
            var v = PacketAccessor.GetI12345678(Packet, Offset);
            Offset += 8;
            return v;
        }

        public Single GetSingle()
        {
            var v = PacketAccessor.GetF1234(Packet, Offset);
            Offset += 4;
            return v;
        }

        public Double GetDouble()
        {
            var v = PacketAccessor.GetF12345678(Packet, Offset);
            Offset += 8;
            return v;
        }
    }
}