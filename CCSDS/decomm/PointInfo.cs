using System;
using System.Text;
using System.Xml.Serialization;
using Newtonsoft.Json;
using System.Runtime.Serialization;

namespace gov.nasa.arc.ccsds.decomm
{
    [JsonObject(MemberSerialization.OptOut)]
    public class PointInfo
    {
        //NOTE: GetWarpPointType() depends on this order not changing
        public enum PointType
        {
            F1234,
            F12345678,
            I1,
            I12,
            I1234,
            S1,
            TIME40,
            TIME42,
            TIME44,
            U1,
            U12,
            U1234,
            U12345678,
            U21,
            U4321,
            // These are versions needed for bit extraction
            U1b,
            U12b,
            U1234b,
            U4321b,
            I12b,
            I1234b,
            Pseudo,
            FullPacketConversion
        };

        [XmlIgnore][IgnoreDataMember]
        public static string[] UnitsArray { get; set; }

        private readonly uint[] _bit32 =
        {
            0x0001, 0x0002, 0x0004, 0x0008, 0x0010, 0x0020, 0x0040, 0x0080,
            0x0100, 0x0200, 0x0400, 0x0800, 0x1000, 0x2000, 0x4000, 0x8000,
            0x10000, 0x20000, 0x40000, 0x80000, 0x100000, 0x200000, 0x400000,
            0x800000, 0x1000000, 0x2000000, 0x4000000, 0x8000000, 0x10000000,
            0x20000000, 0x40000000, 0x80000000
        };

        private readonly uint[] _mask32 =
        {
            0x0, 0x1, 0x3, 0x7, 0xF, 0x1F, 0x3F, 0x7F, 0xFF, 0x1FF, 0x3FF, 0x7FF,
            0xFFF, 0x1FFF, 0x3FFF, 0x7FFF, 0xFFFF, 0x1FFFF, 0x3FFFF, 0x7FFFF,
            0xFFFFF, 0x1FFFFF, 0x3FFFFF, 0x7FFFFF, 0xFFFFFF, 0x1FFFFFF, 0x3FFFFFF,
            0x7FFFFFF, 0xFFFFFFF, 0x1FFFFFFF, 0x3FFFFFFF, 0x7FFFFFFF, 0xFFFFFFFF
        };

        public int APID { get; set; }
        public string Documentation { get; set; }
        public PointType FieldType { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        [NonSerialized] public PacketInfo Packet;
        public UInt16 UnitsIndex { get; set; }
        public UInt16 bit_start { get; set; }
        public UInt16 bit_stop { get; set; }
        public UInt16 byte_offset { get; set; }
        public UInt16 byte_size { get; set; }

        [JsonConverter(typeof(ConcreteConverter<ConversionPlaceholder>))]
        public IConversion ConversionFunction { get; set; }

        [JsonIgnore]
        public virtual string Units
        {
            get { return UnitsIndex >= UnitsArray.Length ? @"Unknown" : UnitsArray[UnitsIndex]; }
        }

        public dynamic GetValue(byte[] p)
        {
            var raw = GetRawValue(p);
            if (raw == null) return null;
            var cnv = ConversionFunction.Convert(raw);
            return cnv;
        }

        public static int ComparePointInfo(PointInfo a, PointInfo b)
        {
            return string.CompareOrdinal(a.Name, b.Name);
        }

        public unsafe dynamic GetRawValue(byte[] p)
        {
            switch (FieldType)
            {
                case PointType.F1234:
                {
                    var v = (p[byte_offset] << 24) | (p[1 + byte_offset] << 16) | (p[2 + byte_offset] << 8) |
                            p[3 + byte_offset];
                    var fp = (float*) &v;
                    var f = *fp;
                    return f;
                }
                case PointType.F12345678:
                {
                    var v = ((ulong) p[0 + byte_offset] << 56) | ((ulong) p[1 + byte_offset] << 48) |
                            ((ulong) p[2 + byte_offset] << 40) | ((ulong) p[3 + byte_offset] << 32) |
                            ((ulong) p[4 + byte_offset] << 24) | ((ulong) p[5 + byte_offset] << 16) |
                            ((ulong) p[6 + byte_offset] << 8) | (ulong) p[7 + byte_offset];
                    var dp = (double*) &v;
                    var d = *dp;
                    return d;
                }
                case PointType.I1:
                    return (sbyte) p[byte_offset];
                case PointType.I12:
                    return (Int16) ((p[byte_offset] << 8) | (p[byte_offset + 1]));
                case PointType.I12b:
                {
                    long temp = ((p[byte_offset] << 8) | (p[byte_offset + 1]));
                    temp = temp >> (15 - bit_stop);
                    var len = bit_stop - bit_start + 1;
                    var result = (_mask32[len] & temp);
                    var isNeg = (_bit32[len - 1] & result) != 0;
                    Int16 r = 0;
                    if (isNeg)
                    {
                        result = result - _bit32[len];
                        r = (Int16) result;
                    }
                    return r;
                }
                case PointType.I1234:
                    return
                        (Int32)
                            ((p[byte_offset] << 24) | (p[byte_offset + 1] << 16) | (p[byte_offset + 2] << 8) |
                             (p[byte_offset + 3]));
                case PointType.I1234b:
                {
                    long temp = ((p[byte_offset] << 24) | (p[byte_offset + 1] << 16) | (p[byte_offset + 2] << 8) |
                                 (p[byte_offset + 3]));
                    temp = temp >> (31 - bit_stop);
                    var len = bit_stop - bit_start + 1;
                    var result = (_mask32[len] & temp);
                    var isNeg = (_bit32[len - 1] & result) != 0;
                    var r = 0;
                    if (isNeg)
                    {
                        result = result - _bit32[len];
                        r = (Int32) result;
                    }
                    return r;
                }
                case PointType.S1:
                {
                    int count = byte_size;
                    for (var i = 0; i < byte_size; i++)
                        {
                            if (p[i + byte_offset] == 0)
                            {
                                count = i;
                                break;
                            }
                        }

                        var s = Encoding.ASCII.GetString(p, byte_offset, count);
                    return s;
                }
                case PointType.TIME40:
                    return
                        (UInt32)
                            ((p[byte_offset] << 24) | (p[byte_offset + 1] << 16) | (p[byte_offset + 2] << 8) |
                             (p[byte_offset + 3]));
                case PointType.TIME42:
                    return ((ulong) p[0 + byte_offset] << 40) | ((ulong) p[1 + byte_offset] << 32) |
                           ((ulong) p[2 + byte_offset] << 24) | ((ulong) p[3 + byte_offset] << 16) |
                           ((ulong) p[4 + byte_offset] << 8) | (ulong) p[5 + byte_offset];
                case PointType.TIME44:
                    return ((ulong) p[0 + byte_offset] << 56) | ((ulong) p[1 + byte_offset] << 48) |
                           ((ulong) p[2 + byte_offset] << 40) | ((ulong) p[3 + byte_offset] << 32) |
                           ((ulong) p[4 + byte_offset] << 24) | ((ulong) p[5 + byte_offset] << 16) |
                           ((ulong) p[6 + byte_offset] << 8) | (ulong) p[7 + byte_offset];
                case PointType.U1:
                    return p[byte_offset];
                case PointType.U1b:
                {
                    int temp = p[byte_offset];
                    temp = temp >> (7 - bit_stop);
                    var len = bit_stop - bit_start + 1;
                    var result = (byte) (_mask32[len] & temp);
                    return result;
                }
                case PointType.U12:
                    return (UInt16) ((p[byte_offset] << 8) | (p[byte_offset + 1]));
                case PointType.U12b:
                {
                    var temp = ((p[byte_offset] << 8) | (p[byte_offset + 1]));
                    temp = temp >> (15 - bit_stop);
                    var len = bit_stop - bit_start + 1;
                    var result = (UInt16) (_mask32[len] & temp);
                    return result;
                }
                case PointType.U1234:
                    return
                        (UInt32)
                            ((p[byte_offset] << 24) | (p[byte_offset + 1] << 16) | (p[byte_offset + 2] << 8) |
                             (p[byte_offset + 3]));
                case PointType.U12345678:
                    return
                        (UInt64)
                            (((ulong)p[byte_offset] << 56) | ((ulong)p[byte_offset + 1] << 48) | ((ulong)p[byte_offset + 2] << 40) | ((ulong)p[byte_offset + 3] << 32) |
                            ((ulong)p[byte_offset + 4] << 24) | ((ulong)p[byte_offset + 5] << 16) | ((ulong)p[byte_offset + 6] << 8) |
                             ((ulong)p[byte_offset + 7]));

                    // This isn't right because the value is big endian
                    //return (dynamic) BitConverter.ToUInt64(p, byte_offset);
                case PointType.U1234b:
                {
                    var temp = ((p[byte_offset] << 24) | (p[byte_offset + 1] << 16) | (p[byte_offset + 2] << 8) |
                                (p[byte_offset + 3]));
                    temp = temp >> (31 - bit_stop);
                    var len = bit_stop - bit_start + 1;
                    var result = (UInt32) (_mask32[len] & temp);
                    return result;
                }
                case PointType.U21:
                    return (UInt16) ((p[byte_offset + 1] << 8) | (p[byte_offset]));
                case PointType.U4321:
                    return
                        (UInt32)
                            ((p[byte_offset + 3] << 24) | (p[byte_offset + 2] << 16) | (p[byte_offset + 1] << 8) |
                             (p[byte_offset]));
                case PointType.U4321b:
                {
                    var temp = ((p[byte_offset + 3] << 24) | (p[byte_offset + 2] << 16) | (p[byte_offset + 1] << 8) |
                                (p[byte_offset]));
                    temp = temp >> (31 - bit_stop);
                    var len = bit_stop - bit_start + 1;
                    var result = (UInt32) (_mask32[len] & temp);
                    return result;
                }
                case PointType.Pseudo:
                    return ConversionFunction.Convert(0);
                case PointType.FullPacketConversion:
                    return ConversionFunction.Convert(p);
            }
            return null;
        }

        public enum ConversionType
        {
            None,
            Time42Timestamp,
            Time42ToSeconds,
            Time42ToSubseconds,
            SubsecondsToFraction,
            Time40Timestamp,
            Time44Timestamp,

            Dispatch,

            // No incomming telemetry point should havem one of these conversion types.
            ImageUrl,
            Spectrum,

        };
    }

    public class ConcreteConverter<T> : JsonConverter
    {
        public override bool CanConvert(Type objectType) => true;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return serializer.Deserialize<T>(reader);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }
}

