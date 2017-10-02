using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using gov.nasa.arc.ccsds.core;
using gov.nasa.arc.ccsds.decomm;

namespace gov.nasa.arc.ccsds.utilities
{
    public class SeqPrintSpec
    {
        public static char[] TrimChars = {' ', '\t'};
        public bool AddApid;
        public bool AddLoganTimestamp;
        public bool AddStkTimestamp;
        public bool AddTimestamp;
        public bool AddTimestampEt;
        public bool AddTimestampLong;
        public bool AddTimestampSeconds;
        public bool AddTimestampSubseconds;
        public bool AddTimestampSubsecondsFloat;
        public string Filename;
        public List<SeqPrintPointSpec> Points;

        /// <summary>
        /// </summary>
        /// <param name="specFilename"></param>
        /// <returns>A SeqPrintSpec or an error string</returns>
        public static object Load(string specFilename)
        {
            var outputFilename = Path.ChangeExtension(specFilename, ".csv");
            return Load(specFilename, outputFilename);
        }

        /// <summary>
        ///     Create a specification for printing a set of telemetry points
        /// </summary>
        /// <param name="specFilename"></param>
        /// <param name="outputFilename"></param>
        /// <returns>A SeqPrintSpec or an error string</returns>
        public static object Load(string specFilename, string outputFilename = null)
        {
            var points = new List<SeqPrintPointSpec>();
            try
            {
                string stringResult = null;
                var linenum = 0;
                string line;
                object spec;
                var isFirst = true;
                using (var sr = new StreamReader(specFilename))
                    while ((line = sr.ReadLine()) != null)
                    {
                        if ((spec = ParseSeqprintPointSpec(line, ++linenum, isFirst)) is string)
                        {
                            Console.WriteLine("Error parsing seqprint spec: {0}", stringResult = spec as string);
                        }
                        else if (spec != null)
                        {
                            points.Add(spec as SeqPrintPointSpec);
                            isFirst = false;
                        }
                    }

                if (stringResult != null)
                    return stringResult;
                return new SeqPrintSpec {Filename = outputFilename, Points = points};
            }
            catch (Exception e)
            {
                return string.Format("Error while reading {0}: {1}", specFilename, e.Message);
            }
        }

        public static SeqPrintSpec LoadThrowError(string specFilename, string outputFilename = null)
        {
            var spec = LoadFromString(specFilename, outputFilename);
            if (spec is string)
                throw new Exception(spec as string);
            return spec as SeqPrintSpec;
        }

        /// <summary>
        /// </summary>
        /// <param name="specString"></param>
        /// <param name="outputFilename"></param>
        /// <returns>A SeqPrintSpec or an error string</returns>
        public static object LoadFromString(string specString, string outputFilename = null)
        {
            var points = new List<SeqPrintPointSpec>();
            try
            {
                var linenum = 0;
                string line;
                object spec;
                var isFirst = true;
                using (var sr = new StringReader(specString))
                {
                    while ((line = sr.ReadLine()) != null)
                        if ((spec = ParseSeqprintPointSpec(line, ++linenum, isFirst)) is string)
                        {
                            return spec;
                        }
                        else if (spec != null)
                        {
                            points.Add(spec as SeqPrintPointSpec);
                            isFirst = false;
                        }
                }

                return new SeqPrintSpec {Filename = outputFilename, Points = points};
            }
            catch (Exception e)
            {
                return string.Format("Error while reading SeqPrintSpec from string: {0}", e.Message);
            }
        }

        public static SeqPrintSpec Load(List<PointInfo> points, bool isRaw = false, string outputFilename = null)
        {
            var pointSpecs =
                points.Select(p => new SeqPrintPointSpec {Point = p, Format = null, Header = p.Id, Raw = isRaw})
                    .ToList();
            for (var i = 0; i < pointSpecs.Count; i++)
                pointSpecs[i].Format = i == 0 ? "{0}" : ", {0}";
            return new SeqPrintSpec {Filename = outputFilename, Points = pointSpecs};
        }

        /// <summary>
        /// </summary>
        /// <param name="line"></param>
        /// <param name="linenum"></param>
        /// <param name="isFirst"></param>
        /// <returns>SeqPrintPointSpec or error string</returns>
        public static object ParseSeqprintPointSpec(string line, int linenum, bool isFirst)
        {
            line = line.Trim(TrimChars);
            if (line.Length < 1) return null;
            var sep = line[0];
            if (sep == ';') return null;

            string format;
            string id;
            string header;

            var tokens = ReadTokensFromString(line);
            if (tokens == null) return null;
            switch (tokens.Count)
            {
                case 0:
                    return null;
                case 1:
                    format = isFirst ? "{0}" : ", {0}";
                    id = tokens[0];
                    header = id;
                    break;
                case 2:
                    format = tokens[0];
                    id = tokens[1];
                    header = id;
                    break;
                case 3:
                    format = tokens[0];
                    id = tokens[1];
                    header = tokens[2];
                    break;
                default:
                    return null;
            }

            bool raw;
            Func<dynamic, dynamic> function = null;

            switch (id.Length > 2 ? id.Substring(0, 2).ToLower() : null)
            {
                case "p@":
                    raw = false;
                    id = id.Substring(2);
                    break;
                case "t@":
                    raw = true;
                    function = t => TimeUtilities.Time42ToString((long) t);
                    id = id.Substring(2);
                    break;
                case "l@":
                    raw = true;
                    function = t => TimeUtilities.Time42ToLogan((long) t);
                    id = id.Substring(2);
                    break;
                case "e@":
                    raw = true;
                    function = t => TimeUtilities.Time42ToET((long) t);
                    id = id.Substring(2);
                    break;
                default:
                    raw = true;
                    break;
            }

            var pi = TelemetryDescription.GetDatabase().GetPoint(id);
            if (pi == null)
            {
                return string.Format("Line {0}: Lookup failed for {1}", linenum, id);
            }
            return new SeqPrintPointSpec
            {
                Format = format,
                Function = function,
                Header = header,
                Point = pi,
                Raw = raw,
                Line = line
            };
        }

        private static List<string> ReadTokensFromString(string line)
        {
            if (line == null) return null;
            if (line.Length == 0) return null;

            var result = new List<string>(3);
            var ptr = 0;
            var max = line.Length;

            while (ScanForTokenChar(line, max, ptr, out ptr))
            {
                result.Add(line[ptr] == '"'
                    ? ReadQuotedStringFromString(line, max, ptr, out ptr)
                    : ReadTokenFromString(line, max, ptr, out ptr));
            }
            return result;
        }

        private static bool ScanForTokenChar(string line, int max, int ptr, out int nextPtr)
        {
            while (ptr < max && IsWhitespace(line[ptr]))
                ptr++;
            nextPtr = ptr;
            return (ptr < max);
        }

        private static bool IsWhitespace(char c)
        {
            return c == ' ' || c == '\t';
        }

        private static string ReadTokenFromString(string line, int max, int ptr, out int nextPtr)
        {
            var start = ptr;
            while (ptr < max && !IsWhitespace(line[ptr]))
                ptr++;
            nextPtr = ptr;
            return line.Substring(start, ptr - start);
        }

        private static string ReadQuotedStringFromString(string line, int max, int ptr, out int nextPtr)
        {
            var sb = new StringBuilder();
            while (++ptr < max)
            {
                var c = line[ptr];
                if (c == '"')
                {
                    nextPtr = ptr + 1;
                    return sb.ToString();
                }
                if (c == '\\')
                {
                    if (++ptr < max)
                    {
                        sb.Append(line[ptr]);
                    }
                    else
                    {
                        nextPtr = ptr;
                        return sb.ToString();
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            nextPtr = ptr;
            return sb.ToString();
        }

        public string WriteToString()
        {
            var sb = new StringBuilder();
            foreach (var ps in Points)
            {
                if (ps.Line != null)
                    sb.AppendLine(ps.Line);
                else
                    sb.AppendFormat("\"{0}\"{1}{2} {3}\n", ps.Format, ps.Raw ? "" : "p@", ps.Point.Id, ps.Header);
            }

            return sb.ToString();
        }

        public bool IsEmpty()
        {
            return Points.Count == 0;
        }

        public List<PointInfo> GetPoints()
        {
            return Points.Select(ps => ps.Point).ToList();
        }

        public void SetAllRaw(bool flag)
        {
            foreach (var p in Points)
                p.Raw = flag;
        }

        public void SetAllRaw()
        {
            SetAllRaw(true);
        }

        public void SetAllEngineering()
        {
            SetAllRaw(false);
        }
    }
}