using System;
using System.Collections.Generic;
using System.IO;

namespace CCSDS.utilities
{
    public class ITOSMetafile
    {
        public List<MetafileLine> Lines = new List<MetafileLine>();

        public ITOSMetafile(string path)
        {
            foreach (var line in File.ReadLines(path))
            {
                if (line == null) continue;
                var tokens = line.Split('#');
                if (tokens.Length != 2)
                    throw new Exception(string.Format(@"Malformed ITOS metafile line: {0}", line));
                var tokens2 = tokens[1].Split('|');
                Lines.Add(new MetafileLine { Value = tokens[0].Trim(), Id = tokens2[0].Trim(), Args = tokens2.Length < 2 ? null : tokens2[1].Trim() });
            }
        }

        public string GetValue(string id)
        {
            if (id == null) return null;
            foreach (var line in Lines)
            {
                if (id.Equals(line.Id))
                    return line.Value;
            }

            return null;
        }

        public bool IsFrameFile => GetValue("source type").Equals("frames") && GetValue("frame type").Equals("aos");
        public bool IsPacketFile => GetValue("source type").Equals("packets") && GetValue("frame type").Equals("packet");

        public int FrameLength
        {
            get
            {
                if (!IsFrameFile) return 0;
                int l;
                if (!int.TryParse(GetValue("frame length"), out l)) return 0;
                return l;
            }
        }

        public int SpacecraftId
        {
            get
            {
                if (!IsFrameFile) return 0;
                int l;
                if (!int.TryParse(GetValue("spacecraft id"), out l)) return 0;
                return l;
            }
        }

        public struct MetafileLine
        {
            public string Value;
            public string Id;
            public string Args;
        }
    }
}
