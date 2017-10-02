using System;
using gov.nasa.arc.ccsds.decomm;

namespace gov.nasa.arc.ccsds.prechannelized
{
    public class MSID : PointInfo
    {
        public string Label;
        public UInt16 Value;
        public string Nomenclature;
        public string System;

        public string Subsystem { get { return MSIDDecoder.Subsystem(Label); } }
        public string Class { get { return MSIDDecoder.Class(Label); } }
        public string Kind { get { return MSIDDecoder.Kind(Label); } }
        public string SerialNo { get { return MSIDDecoder.SerialNo(Label); } }
        public override string Units { get { return MSIDDecoder.Units(Label); } }
    }
}