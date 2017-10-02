using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using gov.nasa.arc.ccsds.core;

namespace gov.nasa.arc.ccsds.network
{
    public abstract class PacketsFromTCP : IPacketSource
    {
        public string Host;
        public Action OnConnect;
        public Action OnDisconnect;
        public Action<Exception> OnError;

        public Action OnFoundZeros = null;
        public Action OnPrematureEOF = null;
        public int Port;
        public long Position;
        public bool Reconnect;

        public int MaxReconnects = 100;
        private int _reconnects = 0;

        protected TcpListener Server;

        protected PacketsFromTCP(string host, int port, bool reconnect)
        {
            Host = host;
            Port = port;
            Reconnect = reconnect;
        }

        public IEnumerable<byte[]> Iterator()
        {
            Position = 0L;
            while (true)
            {
                NetworkStream stream = null;
                try
                {
                    stream = GetStream();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    if (!Reconnect || (++_reconnects) > MaxReconnects)
                        yield break;
                    continue;
                }
                if (stream == null) yield break;
                var buffer = new byte[PacketAccessor.MaximumPacketSize]; // Should I allocate the max size here?
                using (var reader = new BinaryReader(stream))
                {
                    while (true)
                    {
                        if (ReadPacket(reader, buffer))
                            yield return buffer;
                        else if (!Reconnect || (++_reconnects) > MaxReconnects)
                            yield break;
                        else
                            break;
                    }
                } // I'm not sure closing the reader will work if there was a network error
            }
        }

        public virtual IEnumerable<byte[]> Iterator(Int64 startTime, Int64 stopTime)
        {
            return from p in Iterator()
                let timestamp = PacketAccessor.Time42(p)
                where timestamp >= startTime && timestamp <= stopTime
                select p;
        }

        public virtual IEnumerable<byte[]> Iterator(int[] apids, Int64 startTime, Int64 stopTime, long skip = 0L)
        {
            var counter = 0L;
            foreach (var p in Iterator())
            {
                var timestamp = PacketAccessor.Time42(p);
                if (timestamp < startTime || timestamp > stopTime) continue;
                var apid = PacketAccessor.APID(p);
                var len = apids.Length;
                for (var i = 0; i < len; i++)
                {
                    if (apid == apids[i])
                    {
                        if (counter++ >= skip)
                            yield return p;
                        break;
                    }
                }
            }
        }

        public abstract NetworkStream GetStream();

        /// <summary>
        /// Read a single packet.  This exists so that a try/catch block can be set up (which interacts badly with yields)
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="buffer"></param>
        /// <returns></returns>
        protected bool ReadPacket(BinaryReader reader, byte[] buffer)
        {
            try
            {
                // Read the packet header
                var ptr = 0;
                var toread = 6;
                int read;
                while (toread > 0)
                {
                    read = reader.Read(buffer, ptr, toread);
                    if (read == 0)
                    {
                        if (OnPrematureEOF != null) OnPrematureEOF();
                        return false;
                    }
                    toread -= read;
                    ptr += read;
                }

                // Check whether we've run off the end.  Valid packets can't start with three 0's
                //TODO: There's a potential bug here.  This only recognizes the end when we get to a header.
                //The end of the previous packet might be missing.
                if ((buffer[0] | buffer[1] | buffer[2]) == 0)
                {
                    if (OnFoundZeros != null) OnFoundZeros();
                    return false;
                }

                var length = PacketAccessor.Length(buffer);
                toread = 1 + length;

                // Read the rest of the packet
                while (toread > 0)
                {
                    read = reader.Read(buffer, ptr, toread);
                    if (read == 0)
                    {
                        if (OnPrematureEOF != null) OnPrematureEOF();
                        return false;
                    }
                    toread -= read;
                    ptr += read;
                }

                Position += ptr;
                return true;
            }
            catch (Exception e)
            {
                if (OnError != null) OnError(e);
                return false;
            }
        }
    }
}