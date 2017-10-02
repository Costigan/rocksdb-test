namespace gov.nasa.arc.ccsds.core
{
    /// <summary>
    ///     Recieves calls representing a packet stream.  The byte array holding the packets is reused.
    /// </summary>
    public interface IPacketReceiver
    {
        void Start();
        void Packet(byte[] packet);
        void End();
    }
}