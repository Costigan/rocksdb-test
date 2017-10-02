using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using gov.nasa.arc.ccsds.core;
using CCSDS.utilities;

namespace gov.nasa.arc.ccsds.network
{
    public class PacketsToUDP
    {
        private readonly string _host;
        private readonly int _port;
        private readonly int _bitsPerSecond;
        private readonly IEnumerable<byte[]> _source;

        public PacketsToUDP(string host, int port, int bitsPerSecond, IPacketSource source)
        {
            _port = port;
            _source = source.Iterator();
            _bitsPerSecond = bitsPerSecond;
            _host = host;
        }

        public PacketsToUDP(string host, int port, int bitsPerSecond, IEnumerable<byte[]> source)
        {
            _port = port;
            _source = source;
            _bitsPerSecond = bitsPerSecond;
            _host = host;
        }

        public void Run()
        {
            var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            var target = IPAddress.Parse(_host);
            var ep = new IPEndPoint(target, _port);
            foreach (var packet in PacketFilter.BitsPerSecond(_bitsPerSecond, _source))
            {
                //Console.WriteLine(@"sending apid {0}", PacketAccessor.APID(packet));
                s.SendTo(packet, PacketAccessor.Length(packet) + 7, SocketFlags.None, ep);
                //Thread.Sleep(1);
            }
        }
    }
}
