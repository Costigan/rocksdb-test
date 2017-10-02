using gov.nasa.arc.ccsds.core;
using RocksDB_test1;
using RocksDbSharp;
using STSdb4.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace rocksdb_test
{
    public partial class Form1 : Form
    {
        bool isFinished = false;

#if MONO
        public static string DataDirectory = @"/home/mshirley/data/2015-08-25/";
#else
        public static string DataDirectory = @"C:\RP\data\2015-08-25";
#endif
        public Form1()
        {
            InitializeComponent();
        }

        public void Test1()
        {
            var ticks = Enumerable.Range(0, 100).Select(i => new Struct1 { }).ToList();
            using (var ms = new MemoryStream())
            {
                //create raw serialization logic
                Persist<Struct1> persist = new Persist<Struct1>();
                //write tick by tick
                BinaryWriter writer = new BinaryWriter(ms);
                for (int i = 0; i < ticks.Count; i++)
                {
                    persist.Write(writer, ticks[i]);
                }

                //read
                var reader = new BinaryReader(ms);
                var tmp = new List<Struct1>();
                ms.Seek(0, SeekOrigin.Begin);
                for (int i = 0; i < ticks.Count; i++)
                {
                    var tick = persist.Read(reader);
                    tmp.Add(tick);
                }
            }
        }

        static void v1()
        {
            var options = new DbOptions()
                .SetCreateIfMissing(true);
            using (var db = RocksDb.Open(options, "db1"))
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                var count = 0;
                foreach (var p in PacketStreamUtilities.PacketsFromFileTree(DataDirectory))
                {
                    var len = PacketAccessor.Length(p);
                    if (len < 5)
                        continue;
                    var timestamp = PacketAccessor.Time42(p);
                    var apid = PacketAccessor.APID(p);
                    var key = new PacketKey { APID = (short)apid, Timestamp = timestamp };
                    var keybuf = KeyToByteArray(key);
                    db.Put(keybuf, p);
                    count++;
                }
                stopwatch.Stop();
                Console.WriteLine($"{count} packets; {count / (stopwatch.ElapsedMilliseconds / 1000d)} packets/sec");
            }
        }

        static byte[] KeyToByteArray(PacketKey key)
        {
            var r = new byte[10];
            var apid = key.APID;
            r[0] = (byte)(apid >> 8 & 0xFF);
            r[1] = (byte)(apid & 0xFF);
            var timestamp = key.Timestamp;
            r[2] = (byte)(timestamp >> 56 & 0xFF);
            r[3] = (byte)(timestamp >> 48 & 0xFF);
            r[4] = (byte)(timestamp >> 40 & 0xFF);
            r[5] = (byte)(timestamp >> 32 & 0xFF);
            r[6] = (byte)(timestamp >> 24 & 0xFF);
            r[7] = (byte)(timestamp >> 16 & 0xFF);
            r[8] = (byte)(timestamp >> 8 & 0xFF);
            r[9] = (byte)(timestamp >> 0 & 0xFF);
            return r;
        }

        static bool ByteArrayLessThanEqualTo(byte[] a, byte[] b) => a[0] <= b[0] & a[1] <= b[1] & a[2] <= b[2] & a[3] <= b[3] & a[4] <= b[4] & a[5] <= b[5] & a[6] <= b[6] & a[7] <= b[7] & a[8] <= b[8] & a[9] <= b[9];

        void v2()
        {
            var options = new DbOptions()
                .SetCreateIfMissing(true);
            var addTimes = new List<long>();
            using (var db = RocksDb.Open(options, "db1"))
            {
                var elapsedStopwatch = new Stopwatch();
                var addStopwatch = new Stopwatch();
                elapsedStopwatch.Start();
                var count = 0;
                foreach (var p in PacketStreamUtilities.PacketsFromFileTree(DataDirectory))
                {
                    var len = PacketAccessor.Length(p);
                    if (len < 5)
                        continue;
                    var timestamp = PacketAccessor.Time42(p);
                    var apid = PacketAccessor.APID(p);
                    var key = new PacketKey { APID = (short)apid, Timestamp = timestamp };
                    var keybuf = KeyToByteArray(key);
                    addStopwatch.Reset();
                    addStopwatch.Start();
                    db.Put(keybuf, p);
                    addStopwatch.Stop();
                    addTimes.Add(addStopwatch.ElapsedTicks);
                    count++;
                }
                elapsedStopwatch.Stop();
                Console.WriteLine($"{count} packets; {count / (elapsedStopwatch.ElapsedMilliseconds / 1000d)} packets/sec");
            }
            addTimes.Sort();
            foreach (var t in addTimes)
            {
                Console.WriteLine((new TimeSpan(t)).TotalSeconds);
            }
        }

        void v3()
        {
            var fetchTimes = new List<long>();
            var apidCounts = new short[2048];

            using (var db = RocksDb.Open(new DbOptions().SetCreateIfMissing(true), "db1"))
            {
                var readerCount = 2;
                Task[] tasks = new Task[readerCount + 1];
                tasks[0] = Task.Run(() => AddPackets(db, apidCounts));
                for (var i = 1; i <= readerCount; i++)
                    tasks[i] = Task.Run(() => ReadPackets(db, apidCounts));
                Task.WaitAll(tasks);
            }
        }

        void AddPackets(RocksDb db, short[] apidCounts)
        {
            var addTimes = new List<long>();
            var elapsedStopwatch = new Stopwatch();
            var addStopwatch = new Stopwatch();
            elapsedStopwatch.Start();
            var count = 0;
            foreach (var p in PacketStreamUtilities.PacketsFromFileTree(DataDirectory))
            {
                var len = PacketAccessor.Length(p);
                if (len < 5)
                    continue;
                var timestamp = PacketAccessor.Time42(p);
                var apid = PacketAccessor.APID(p);
                var key = new PacketKey { APID = (short)apid, Timestamp = timestamp };
                var keybuf = KeyToByteArray(key);
                addStopwatch.Reset();
                addStopwatch.Start();
                db.Put(keybuf, p);
                addStopwatch.Stop();
                addTimes.Add(addStopwatch.ElapsedTicks);
                apidCounts[apid]++;
                count++;
            }
            elapsedStopwatch.Stop();
            Console.WriteLine($"{count} packets; {count / (elapsedStopwatch.ElapsedMilliseconds / 1000d)} packets/sec");

            isFinished = true;

            addTimes.Sort();
            var startAt = Math.Max(0, addTimes.Count - 1000);
            for (var i = startAt; i < addTimes.Count; i++)
            {
                Console.WriteLine((new TimeSpan(addTimes[i])).TotalSeconds);
            }
        }

        void ReadPackets(RocksDb db, short[] apidCounts)
        {
            var readStopwatch = new Stopwatch();
            TimeSpan slowestRead = new TimeSpan();
            var slowestCount = 0L;
            while (!isFinished)
                for (var apid = 0; apid < apidCounts.Length; apid++)
                {
                    if (isFinished) break;
                    if (apidCounts[apid] > 0)
                    {
                        readStopwatch.Reset();
                        readStopwatch.Start();
                        var firstKey = KeyToByteArray(new PacketKey { APID = (short)apid, Timestamp = 0L });
                        var lastKey = KeyToByteArray(new PacketKey { APID = (short)apid, Timestamp = long.MaxValue });

                        var count = 0L;
                        var iterator = db.NewIterator();
                        for (iterator.Seek(firstKey); iterator.Valid(); iterator.Next())
                        {
                            if (!ByteArrayLessThanEqualTo(iterator.Key(), lastKey)) break;
                            count++;
                        }
                        readStopwatch.Stop();
                        if (readStopwatch.ElapsedTicks > slowestRead.Ticks)
                        {
                            slowestRead = readStopwatch.Elapsed;
                            slowestCount = count;
                        }
                    }
                    System.Threading.Thread.Sleep(10);
                }
            Console.WriteLine($"Read count={slowestCount}  slowest read={slowestRead}");
        }


        private void button1_Click(object sender, EventArgs e)
        {
            //Test1();
            v1();
            Console.WriteLine(@"Finished");
        }
    }

    public struct Struct1
    {
        public long A { get; set; }
        public DateTime B { get; set; }
    }

    public class PacketKey
    {
        public static PacketKey Factory(Int16 apid, long timestamp) => new PacketKey { APID = apid, Timestamp = timestamp };
        public static PacketKey Factory(int apid, long timestamp) => new PacketKey { APID = (Int16)apid, Timestamp = timestamp };

        public Int16 APID;
        public long Timestamp;
    }
}
