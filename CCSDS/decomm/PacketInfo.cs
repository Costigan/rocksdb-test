using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

// ReSharper disable InconsistentNaming

namespace gov.nasa.arc.ccsds.decomm
{
    [JsonObject(MemberSerialization.OptOut)]
    public class PacketInfo
    {
        public int APID { get; set; }
        public string Documentation { get; set; } = string.Empty;
        public string Id { get; set; }
        public bool IsTable { get; set; }
        public string Name { get; set; }

        [XmlArrayItem(ElementName = "PointInfo", IsNullable = true, Type = typeof (PointInfo))]
        public List<PointInfo> Points = new List<PointInfo>();

        public PointInfo GetPoint(string name)
        {
            var max = Points.Count;
            for (var i = 0; i < max; i++)
            {
                if (string.Compare(Points[i].Name, name, StringComparison.OrdinalIgnoreCase) == 0)
                    return Points[i];
            }

            return null;
        }

        public PointInfo GetPointById(string id)
        {
            var max = Points.Count;
            for (var i = 0; i < max; i++)
            {
                if (string.Compare(Points[i].Id, id, StringComparison.OrdinalIgnoreCase) == 0)
                    return Points[i];
            }

            return null;
        }

        public int GetPacketLength()
        {
            return Points.Max(p => p.byte_offset + p.byte_size);
        }
    }
}

// ReSharper restore InconsistentNaming