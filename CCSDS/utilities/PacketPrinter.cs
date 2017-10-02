using gov.nasa.arc.ccsds.core;
using gov.nasa.arc.ccsds.decomm;
using System;
using System.Collections.Generic;

namespace CCSDS.utilities
{
    public enum PacketPrinterMode { Off, SummaryLine, PacketContents, PacketBinary, PacketCount }

    /// <summary>
    /// A class that receives a stream of packets and writes messages to a text stream
    /// </summary>
    public class PacketPrinter
    {
        public PacketPrinterMode Mode;
        public TelemetryDescription TelemetryDictionary;
        public float PacketCountSummaryInterval = 10f;
        public System.IO.TextWriter Writer;

        /// <summary>
        /// Used when the mode is PacketCount and the summary interval is > 0
        /// </summary>
        private System.Timers.Timer _timer = null;
        private bool _firstLineWritten = false;
        private readonly int[] _packetCounts = new int[2048];
        private readonly List<string[]> _decomRows = new List<string[]>();

        public void Handle(byte[] p)
        {
            if (Writer == null)
                return;
            var apid = PacketAccessor.APID(p);
            _packetCounts[apid]++;
            switch (Mode)
            {
                case PacketPrinterMode.SummaryLine:
                    WriteSummaryLine(p);
                    break;
                case PacketPrinterMode.PacketContents:
                    WritePacketContents(p);
                    break;
                case PacketPrinterMode.PacketCount:
                    WritePacketCount(p);
                    break;
                case PacketPrinterMode.PacketBinary:
                    WritePacketBinary(p);
                    break;
            }
        }

        private void WriteSummaryLine(byte[] p)
        {
            if (!_firstLineWritten)
            {
                Writer.WriteLine(@"Timestamp     APID SeqCount PktLen Packet_Name");
                _firstLineWritten = true;
            }
            var apid = PacketAccessor.APID(p);
            var seqnum = PacketAccessor.SequenceCount(p);
            var pktlen = PacketAccessor.Length(p);
            var secondaryHeader = PacketAccessor.SecondaryHeader(p);
            var timestamp = TimeUtilities.SecondaryHeaderToString(secondaryHeader);
            var packetName = TelemetryDictionary?.GetPacket(apid)?.Name;
            if (packetName == null) packetName = String.Empty;
            Writer.WriteLine(@"{0} {1:####} {2:#####} {3:#####} {4} #x{5:X16}", timestamp, apid, seqnum, pktlen, packetName, secondaryHeader);
        }

        private void WritePacketContents(byte[] p)
        {
            if (!_firstLineWritten)
            {
                Writer.WriteLine(@"Timestamp     APID SeqCount PktLen Packet_Name");
                _firstLineWritten = true;
            }
            var apid = PacketAccessor.APID(p);
            var seqnum = PacketAccessor.SequenceCount(p);
            var pktlen = PacketAccessor.Length(p);
            var timestamp = TimeUtilities.SecondaryHeaderToString(PacketAccessor.SecondaryHeader(p));
            var packetName = TelemetryDictionary?.GetPacket(apid)?.Name;
            if (packetName == null) packetName = String.Empty;
            Writer.WriteLine(@"{0} {1:####} {2:#####} {3:#####} {4}", timestamp, apid, seqnum, pktlen, packetName);

            var pkt = TelemetryDictionary?.GetPacket(apid);
            if (pkt == null) return;
            _decomRows.Clear();
            int nameWidth = 0, engWidth = 0, rawWidth = 0;
            foreach (var point in pkt.Points)
            {
                var name = point.Name;
                var raw = point.GetRawValue(p).ToString();
                var eng = point.GetValue(p).ToString();
                nameWidth = Math.Max(nameWidth, name.Length);
                engWidth = Math.Max(engWidth, eng.Length);
                rawWidth = Math.Max(rawWidth, raw.Length);
                _decomRows.Add(new string[] { name, eng, raw });
            }
            foreach (var row in _decomRows)
                Console.WriteLine("  " + row[0].PadRight(nameWidth) + " = " + row[1].PadLeft(engWidth) + " (eng), " + row[2].PadLeft(rawWidth) + " (raw)");
        }

        private void WritePacketCount(byte[] p)
        {
            if (PacketCountSummaryInterval > 0)
            {
                if (_timer == null)
                {
                    _timer = new System.Timers.Timer(PacketCountSummaryInterval * 1000d);
                    _timer.Elapsed += PacketCountTimerElapsed;
                    _timer.Start();
                    PacketCountTimerElapsed(null, null);
                }
            }
            else
            {
                PacketCountTimerElapsed(null, null);
            }
        }

        private void PacketCountTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Writer.WriteLine(@"Packet Counts---------------------------");
            Writer.WriteLine(@"Count    APID Name");
            for (var apid = 0; apid < 2048; apid++)
            {
                var count = _packetCounts[apid];
                if (count < 1) continue;
                Writer.Write(@"{0:D8} {1:D4}", count, apid);
                if (TelemetryDictionary != null)
                {
                    var pkt = TelemetryDictionary.GetPacket(apid);
                    if (pkt != null)
                    {
                        Writer.Write(' ');
                        Writer.Write(pkt.Name);
                    }
                }
                Writer.WriteLine();
            }
        }

        private void WritePacketBinary(byte[] p)
        {
            throw new NotImplementedException();
        }
    }
}
