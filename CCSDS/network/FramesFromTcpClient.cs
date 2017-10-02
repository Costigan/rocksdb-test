using System;
using System.Net.Sockets;

namespace gov.nasa.arc.ccsds.network
{
    public class FramesFromTcpClient : FramesFromTCP
    {
        public FramesFromTcpClient(string host, int port, bool reconnect)
            : base(host, port, reconnect)
        {
        }

        public override NetworkStream GetStream()
        {
            try
            {
                var client = new TcpClient(Host, Port);
                return client.GetStream();
            }
            catch (Exception e)
            {
                if (OnError != null)
                {
                    OnError(e);
                }
                else
                {
                    Console.Error.WriteLine(e);
                    Console.Error.WriteLine(e.StackTrace);
                }
            }
            return null;
        }
    }
}