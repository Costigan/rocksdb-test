using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using gov.nasa.arc.ccsds.core;

namespace gov.nasa.arc.ccsds.network
{
    public abstract class FramesFromTCP : IPacketSource
    {
        public int FrameFooterLength = 0;
        public int FrameHeaderLength = 4;
        public int FrameLength = FrameAccessor.VCDULength; // ladee default

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

        protected FramesFromTCP(string host, int port, bool reconnect)
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
                var buffer = new byte[FrameLength];
                using (var reader = new BinaryReader(stream))
                {
                    while (true)
                    {
                        if (ReadFrame(reader, buffer))
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
        protected bool ReadFrame(BinaryReader reader, byte[] fbuf)
        {
            try
            {
                // Read the header
                var numBytesToRead = FrameHeaderLength;
                var numBytesRead = 0;

                // Optionally skip FrameWrapperLen + the 4-byte sync mark
                while (numBytesToRead > 0)
                {
                    var count = reader.Read(fbuf, numBytesRead, numBytesToRead);
                    if (count == 0) return false; // Exit if we hit an EOF
                    numBytesToRead -= count;
                    numBytesRead += count;
                }

                Position += FrameHeaderLength;

                // Read the frame
                numBytesToRead = FrameLength;
                numBytesRead = 0;

                // Optionally skip FrameWrapperLen + the 4-byte sync mark
                while (numBytesToRead > 0)
                {
                    var count = reader.Read(fbuf, numBytesRead, numBytesToRead);
                    if (count == 0) return false; // Exit if we hit an EOF
                    numBytesToRead -= count;
                    numBytesRead += count;
                }

                Position += FrameLength;

                for (var i = 0; i < FrameFooterLength; i++)
                    reader.ReadByte();

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