using System;
using System.Net;
using System.Net.Sockets;

namespace gov.nasa.arc.ccsds.network
{
    public class PacketsFromMulticast : PacketsFromUDP
    {
        protected override bool RegisterEndpoint()
        {
            try
            {
                Listener?.Close();
                Listener = new UdpClient(Port);
                var grpAddr = IPAddress.Parse(Host);
                Listener.JoinMulticastGroup(grpAddr);
                EndPoint = new IPEndPoint(grpAddr, Port);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return false;
            }
            return true;
        }
    }
}
