using System;
using gov.nasa.arc.ccsds.decomm;

namespace gov.nasa.arc.ccsds.utilities
{
    public class SeqPrintPointSpec
    {
        public string Format;
        [NonSerialized] public Func<dynamic, dynamic> Function = null;
        public string Header = null;
        public string Line = null;
        public PointInfo Point;
        public bool Raw = false;
    }
}