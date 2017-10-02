using System;
using System.Net;
using System.Net.Sockets;

namespace gov.nasa.arc.ccsds.network
{
    public class PacketsFromTcpListener : PacketsFromTCP
    {
        public PacketsFromTcpListener(int port, bool reconnect) : base(null, port, reconnect)
        {
        }

        public override NetworkStream GetStream()
        {
            try
            {
                IPAddress localAddr = IPAddress.Parse("127.0.0.1");
                Server = new TcpListener(localAddr, Port) {ExclusiveAddressUse = true};
                Server.Start();
                TcpClient client = Server.AcceptTcpClient();
                return client.GetStream();
            }
            catch (Exception e)
            {
                if (OnError != null) OnError(e);
            }
            return null;
        }
    }
}