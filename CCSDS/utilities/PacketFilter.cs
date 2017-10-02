using gov.nasa.arc.ccsds.core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace CCSDS.utilities
{
    public static class PacketFilter
    {
        public static IEnumerable<byte[]> BitsPerSecond(int bitsPerSecond, IEnumerable<byte[]> source)
        {
            var stopwatch = new Stopwatch();
            var bytesPerMsec = bitsPerSecond / 8000D;
            var totalBytes = 0L;
            stopwatch.Start();
            foreach (var p in source)
            {
                var lengthInBytes = PacketAccessor.Length(p) + 7;
                totalBytes += lengthInBytes;
                var targetElapsedMsec = (long)(totalBytes / bytesPerMsec);
                var elapsedMsec = stopwatch.ElapsedMilliseconds;
                var delta = (int)(targetElapsedMsec - elapsedMsec);
                if (delta > 0)
                    Thread.Sleep(delta);
                yield return p;
            }
        }

        public static IEnumerable<byte[]> BitsPerSecond(int bitsPerSecond, IPacketSource source)
        {
            return BitsPerSecond(bitsPerSecond, source.Iterator());
        }

        /// <summary>
        /// Guarantee unique byte arrays.  Ignore packets too short to have a length field.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static IEnumerable<byte[]> UniqueByteArray(IEnumerable<byte[]> source)
        {
            foreach (var p in source)
            {
                var len = p.Length;
                if (len < 6)  // too short for a length field
                    continue;
                var tocopy = Math.Min(len, PacketAccessor.Length(p) + 7);
                var buf = new byte[tocopy];
                Array.Copy(p, buf, tocopy);
                yield return buf;
            }
        }

        public static IEnumerable<byte[]> UniqueByteArray(IPacketSource source)
        {
            return UniqueByteArray(source.Iterator());
        }

        /// <summary>
        /// Allocate a new byte array if the packet appears to be a maximum-sized static buffer
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static IEnumerable<byte[]> MaybeUniqueByteArray(IEnumerable<byte[]> source)
        {
            foreach (var p in source)
            {
                var len = p.Length;
                if (len < 6)  // too short for a length field
                    continue;
                var tocopy = Math.Min(len, PacketAccessor.Length(p) + 7);
                if (len > tocopy)
                {
                    var buf = new byte[tocopy];
                    Array.Copy(p, buf, tocopy);
                    yield return buf;
                }
                else
                {
                    yield return p;
                }
            }
        }

        public static IEnumerable<byte[]> MaybeUniqueByteArray(IPacketSource source)
        {
            return MaybeUniqueByteArray(source.Iterator());
        }
    }
}
