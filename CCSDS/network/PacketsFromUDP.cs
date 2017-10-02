using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using gov.nasa.arc.ccsds.core;

namespace gov.nasa.arc.ccsds.network
{
    /// <summary>
    /// Simple implementation of a UDP listener that provides the packets received as an IEnumerable.
    /// </summary>
    /// TODO: Provide a resource of byte arrays for the packets
    public class PacketsFromUDP : IPacketSource, IDisposable
    {
        private readonly BlockingCollection<byte[]> _queue =
            new BlockingCollection<byte[]>(new ConcurrentQueue<byte[]>());

        public string Host = "239.0.0.42";
        public int Port = 42000;
        public int QueueTimeout = 2000;
        public int ReceiveTimeout = 0;    // Changed the default to an infinite timeout

        protected UdpClient Listener;
        protected IPEndPoint EndPoint;

        private int _maxQueueLength;

        public PacketsFromUDP()
        {
        }

        public PacketsFromUDP(int port)
        {
            Port = port;
        }

        public PacketsFromUDP(int port, int timeout)
        {
            Port = port;
            ReceiveTimeout = timeout;
        }

        public bool Finished { get; set; }

        public int Count
        {
            get { return _queue.Count; }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual bool RegisterEndpoint()
        {
            try
            {
                Listener = new UdpClient(Port);
                EndPoint = new IPEndPoint(IPAddress.Any, Port);
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return false;
            }
        }

        /// <summary>
        ///     Return a stream of packets read from a udp port.  This reads from the port in a separate thread
        ///     so that it can service it quickly enough that packets aren't dropped.  The packets are written
        ///     to a fifo, and the calling thread pulls packets from that fifo until it's been closed.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<byte[]> Iterator()
        {
            if (!RegisterEndpoint())
                return Enumerable.Empty<byte[]>();
            Task.Run(() =>
            {
                Console.WriteLine("Listening for UDP packets on port " + Port);
                try
                {
                    Listener.Client.ReceiveTimeout = ReceiveTimeout;
                    _maxQueueLength = 0;
                    while (!Finished)
                    {
                        //Console.WriteLine(@"Waiting to receive");
                        var bytes = Listener.Receive(ref EndPoint);
                        if (!IsValidPacket(bytes))
                        {
                            Console.WriteLine(@"Warning: received an invalid packet from UDP.  Ignoring.");
                            continue;
                        }
                        //Console.WriteLine(@"PacketsFromUDP: packet (apid={0} pktlen={1} bytes={2} q.count={3})", PacketAccessor.APID(bytes), PacketAccessor.Length(bytes), bytes.Length, _queue.Count);
                        _queue.Add(bytes);
                        _maxQueueLength = Math.Max(_maxQueueLength, _queue.Count);
                    }
                }
                catch (SocketException)
                {
                    Console.WriteLine(@"Received a socket exception.  Closing the queue.");
                    //_queue.CompleteAdding();
                }
                catch (Exception e2)
                {
                    Console.WriteLine(e2.ToString());
                }
                finally
                {
                    _queue.CompleteAdding();
                    Listener?.Close();
                }
            });

            // Now, retrieve the packets from the queue and send them to the output stream
            // This is a separate method because, when running the queue is written as
            // an explicit loop with a yield return, the compiler can't handle combining that with
            // the normal return above.
            return RunQueue(_queue);
        }

        /// <summary>
        ///     Filter the stream of packets returned by Iterator() to those within a time range
        /// </summary>
        /// <param name="startTime"></param>
        /// <param name="stopTime"></param>
        /// <returns></returns>
        public IEnumerable<byte[]> Iterator(long startTime, long stopTime)
        {
            foreach (var p in Iterator())
            {
                var t = PacketAccessor.Time42(p);
                if (t < startTime) continue;
                if (stopTime < t) yield break;
                yield return p;
            }
        }

        /// <summary>
        ///     Filter the packet stream returned by Iterator() to those within a time range and having apids within a given set.
        ///     Optionally skip the first n packets.  (This feature is used by table views when paging.)
        /// </summary>
        /// <param name="apids"></param>
        /// <param name="startTime"></param>
        /// <param name="stopTime"></param>
        /// <param name="skip"></param>
        /// <returns></returns>
        public IEnumerable<byte[]> Iterator(int[] apids, long startTime, long stopTime, long skip = 0L)
        {
            var count = 0;
            foreach (var p in Iterator())
            {
                if (skip >= ++count) continue;
                var t = PacketAccessor.Time42(p);
                if (t < startTime) continue;
                if (stopTime < t) yield break;
                var apid = PacketAccessor.APID(p);
                if (!apids.Contains(apid)) continue;
                yield return p;
            }
        }

        private bool IsValidPacket(byte[] p)
        {
            // Just checks length for now
            return p != null && (p.Length >= 6 && p.Length == PacketAccessor.Length(p) + 7);
        }

        public IEnumerable<byte[]> RunQueue(BlockingCollection<byte[]> q)
        {
            while (true)
            {
                if (Finished || q.IsCompleted)
                    yield break;
                byte[] p;
                q.TryTake(out p, QueueTimeout);
                if (p != null)
                    yield return p;
            }
        }

        private void Dispose(bool ignore)
        {
            Console.WriteLine(@"disposing of the iterator");
            Finished = true;
        }
    }
}