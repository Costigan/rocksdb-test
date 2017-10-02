using System.Collections.Generic;

namespace gov.nasa.arc.ccsds.decomm
{
    /// <summary>
    /// This class is the top level of the 
    /// </summary>
    public class SerializedTelemetryDictionary
    {
        public List<PacketInfo> Packets;

        //[XmlArrayItem(ElementName = "Units", IsNullable = true, Type = typeof(string))]
        public List<string> Units;

        /// <summary>
        /// These are used to determine which engineering conversions really are enumerations.
        /// Anything with a name within this range (inclusive) is a enum.
        /// </summary>
        public string FirstEnum = string.Empty;
        public string LastEnum = string.Empty;

        public List<DiscreteConversionList> ListConversions;
        public List<DiscreteConversionMap> MapConversions;
        public List<DiscreteConversionRangeList> RangeConversions;
        public List<PolynomialConversion> PolyConversions;
    }
}
