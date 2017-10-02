using gov.nasa.arc.ccsds.core;
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace gov.nasa.arc.ccsds.decomm
{
    public interface IConversion
    {
        string GetName();
        dynamic Convert(dynamic raw);
        EngineeringType ReturnedType();
    }

    public interface EnumConversion
    {
        Newtonsoft.Json.Linq.JArray EnumerationForClient();
    }

    public enum EngineeringType { None = 0, Number, Enum, String, Image, Spectrum, Raw };

    public class Conversion
    {
        public static IConversion Identity = new IdentityConversion();
        public static IConversion Time42Timestamp = new Time42Conversion();
        public static IConversion Time42ToSeconds = new LambdaConversion { Func = (dynamic raw) => TimeUtilities.Time42ToSeconds((Int64)raw), Name = "Time42ToSeconds", TheReturnedType = EngineeringType.Number };
        public static IConversion Time42ToSubseconds = new LambdaConversion { Func = (dynamic raw) => TimeUtilities.Time42ToSubseconds((Int64)raw), Name = "Time42ToSubseconds", TheReturnedType = EngineeringType.Number };
        public static IConversion SubsecondsToFraction = new LambdaConversion { Func = (dynamic raw) => (float)(raw / 65536f), Name = "SubsecondsToFraction", TheReturnedType = EngineeringType.Number };
        public static IConversion Time40Timestamp = new Time40Conversion();
        public static IConversion Time44Timestamp = new Time44Conversion();
        public static IConversion Image = new Imageconversion();
    }

    public class ConversionPlaceholder : IConversion
    {
        // ITOS' name for this conversion for doc purposes
        public string Name;
        public dynamic Convert(dynamic raw)
        {
            throw new NotImplementedException();
        }
        public string GetName() { return Name; }
        public EngineeringType ReturnedType() { return EngineeringType.None; }
    }

    public class IdentityConversion : IConversion
    {
        public string Name = "Identity";
        public string GetName() { return Name; }
        public dynamic Convert(dynamic raw) { return raw; }
        public EngineeringType ReturnedType() { return EngineeringType.Raw; }
    }

    public class LambdaConversion : IConversion
    {
        public string Name;
        public string GetName() { return Name; }
        public EngineeringType TheReturnedType = EngineeringType.Number; // default
        public EngineeringType ReturnedType() { return TheReturnedType; }
        public dynamic Convert(dynamic raw)
        {
            return Func(raw);
        }
        public Func<dynamic, dynamic> Func;
    }

    public class PolynomialConversion : IConversion
    {
        public string Name;

        public string GetName() { return Name; }

        public EngineeringType ReturnedType() { return EngineeringType.Number; }

        public int Order;
        public double[] Coefficients;

        public dynamic Convert(dynamic dynamicRaw)
        {
            var raw = (double)dynamicRaw;
            var c = Coefficients;
            switch (Order)
            {
                case 0:
                    return c[0];
                case 1:
                    return c[0] + raw * c[1];
                case 2:
                    return c[0] + raw * c[1] + raw * raw * c[2];
                case 3:
                    {
                        var v = c[0];
                        var r = raw;
                        v += c[1] * r;
                        r *= raw;
                        v += c[2] * r;
                        r *= raw;
                        v += c[3] * r;
                        return r;
                    }
                case 4:
                    {
                        var v = c[0];
                        var r = raw;
                        v += c[1] * r;
                        r *= raw;
                        v += c[2] * r;
                        r *= raw;
                        v += c[3] * r;
                        r *= raw;
                        v += c[4] * r;
                        return r;
                    }
                case 5:
                    {
                        var v = c[0];
                        var r = raw;
                        v += c[1] * r;
                        r *= raw;
                        v += c[2] * r;
                        r *= raw;
                        v += c[3] * r;
                        r *= raw;
                        v += c[4] * r;
                        r *= raw;
                        v += c[5] * r;
                        return r;
                    }
                case 6:
                    {
                        var v = c[0];
                        var r = raw;
                        v += c[1] * r;
                        r *= raw;
                        v += c[2] * r;
                        r *= raw;
                        v += c[3] * r;
                        r *= raw;
                        v += c[4] * r;
                        r *= raw;
                        v += c[5] * r;
                        r *= raw;
                        v += c[6] * r;
                        return r;
                    }
                case 7:
                    {
                        var v = c[0];
                        var r = raw;
                        v += c[1] * r;
                        r *= raw;
                        v += c[2] * r;
                        r *= raw;
                        v += c[3] * r;
                        r *= raw;
                        v += c[4] * r;
                        r *= raw;
                        v += c[5] * r;
                        r *= raw;
                        v += c[6] * r;
                        r *= raw;
                        v += c[7] * r;
                        return r;
                    }
                default:
                    return 0d;
            }
        }
    }

    public class Time42Conversion : IConversion
    {
        public string Name = "Time42 Conversion";

        public string GetName() { return Name; }

        public dynamic Convert(dynamic raw)
        {
            if (raw is ulong)
                return TimeUtilities.Time42ToITOS(raw);
            throw new Exception(@"Illegal time type");
        }

        public EngineeringType ReturnedType() { return EngineeringType.String; }
    }

    public class Time44Conversion : IConversion
    {
        public string Name = "Time44 Conversion";
        public string GetName() { return Name; }
        public dynamic Convert(dynamic raw)
        {
            if (raw is ulong)
                return TimeUtilities.Time44ToITOS(raw);
            throw new Exception(@"Illegal time type");
        }
        public EngineeringType ReturnedType() { return EngineeringType.String; }
    }

    public class Time40Conversion : IConversion
    {
        public string Name = "Time40 Conversion";
        public string GetName() { return Name; }
        public dynamic Convert(dynamic raw)
        {
            if (raw is UInt32)
                return TimeUtilities.Time40ToITOS(raw);
            throw new Exception(@"Illegal time type");
        }
        public EngineeringType ReturnedType() { return EngineeringType.String; }
    }

    /// <summary>
    /// Object representing a mapping from raw ints to strings.  Indices are implicit
    /// </summary>
    public class DiscreteConversionList : IConversion, EnumConversion
    {
        public static string illegal = "illegal_conversion";

        // ITOS' name for this conversion for doc purposes
        public string Name;
        public string GetName() { return Name; }

        // Map index to string.  Indices start at 0.
        public string[] Values;

        // Indices start at this value;  Usually 0.
        public int LowIndex;

        public dynamic Convert(dynamic raw)
        {
            var idx = (int)raw - LowIndex;
            if (idx >= 0 && idx < Values.Length)
                return Values[idx];
            return illegal;
        }

        public EngineeringType ReturnedType() { return EngineeringType.Enum; }

        public JArray EnumerationForClient()
        {
            var a = new JArray();
            for (var i = 0; i < Values.Length; i++)
                a.Add(new JObject { { "value", i + LowIndex }, { "string", Values[i] } });
            return a;
        }
    }

    /// <summary>
    /// Object representing a mapping from raw ints to strings.  Indices are explicit.
    /// </summary>
    public class DiscreteConversionMap : IConversion, EnumConversion
    {
        // ITOS' name for this conversion for doc purposes
        public string Name;
        public string GetName() { return Name; }

        private string[] _values;
        private int[] _indices;

        // Map index to string.  Indices start at 0.
        public string[] Values { get { return _values; } set { _values = value; Init(); } }

        // Map index to string.  Indices start at 0.
        public int[] Indices { get { return _indices; } set { _indices = value; Init(); } }

        private Dictionary<int, string> _map;

        // Handle 0 specially.  This is because short packets that have been extended are extended with 0's, and this map might not contain that value.
        // If not, return 0.
        private dynamic zeroValue = 0;

        public void Init()
        {
            if (_values == null || _indices == null) return;
            _map = new Dictionary<int, string>();
            var min = Math.Min(_values.Length, _indices.Length);
            string currentValue;
            for (var i = 0; i < min; i++)
            {
                if (!_map.TryGetValue(_indices[i], out currentValue))
                    _map.Add(_indices[i], _values[i]);
                else
                    Console.WriteLine(@"Duplicate value in {0}: index={1} values={2} and {3}", Name, _indices[i], currentValue, Values[i]);
            }
            if (_map.ContainsKey(0))
                zeroValue = _map[0];
        }

        public dynamic Convert(dynamic raw)
        {
            if (raw == 0)
                return zeroValue;
            return _map[(int)raw];
        }

        public EngineeringType ReturnedType() { return EngineeringType.Enum; }

        public JArray EnumerationForClient()
        {
            var a = new JArray();
            for (var i = 0; i < Values.Length; i++)
                a.Add(new JObject { { "value", Indices[i] }, { "string", Values[i] } });
            return a;
        }
    }

    public class DiscreteConversionRangeList : IConversion
    {
        // ITOS' name for this conversion for doc purposes
        public string Name;
        public string GetName() { return Name; }

        // Map index to string.  Indices start at 0.
        public DiscreteConversionRange[] Ranges;

        public dynamic Convert(dynamic dynamicRaw)
        {
            var raw = (int)dynamicRaw;
            for (var i = 0; i < Ranges.Length; i++)
            {
                var range = Ranges[i];
                if (range.Low <= raw && raw <= range.High)
                    return range.Value;
            }
            return "No Conversion";
        }

        public EngineeringType ReturnedType() { return EngineeringType.Enum; }
    }

    public struct DiscreteConversionRange
    {
        public int Low;
        public int High;
        public string Value;
    }

    public class Imageconversion : IConversion
    {
        public string Name = "Image Conversion";

        public string GetName() { return Name; }
        public string Camera;

        //TODO: ImageConversion isn't implemented yet.
        public dynamic Convert(dynamic raw)        {            return raw;         }

        public EngineeringType ReturnedType() { return EngineeringType.Image; }
    }
}
