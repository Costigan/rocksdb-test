namespace gov.nasa.arc.ccsds.decomm
{
    /// <summary>
    ///     A version with limits
    /// </summary>
    public class PointInfoLim : PointInfo
    {
        public enum Limits
        {
            RedLow,
            YellowLow,
            YellowHigh,
            RedHigh
        }

        public float? RedHigh;

        public float? RedLow;
        public float? YellowHigh;
        public float? YellowLow;
    }
}