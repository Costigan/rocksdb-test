using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace gov.nasa.arc.ccsds.core
{
    /// <summary>
    ///     Static methods for manipulating timestamps
    /// </summary>
    public static class TimeUtilities
    {
        public enum TimeFormat
        {
            ITOS,
            FDS,
            DateTimeString,
            ET,
            STK
        };

        //public static DateTime Epoch = new DateTime(2000, 1, 1, 11, 58, 55, 816);
        public static DateTime Epoch = new DateTime(2000, 1, 1, 12, 0, 0, 0);
        public static DateTime TTEpoch = new DateTime(2000, 1, 1, 12, 0, 0, 0);
        public static DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public static DateTime AtomicEpoch = new DateTime(1958, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static TimeFormat StringFormat = TimeFormat.ITOS;

        public static Regex DOYDateRecognizer = null;
        public static DateTime InjectorEpoch = new DateTime(2000, 1, 1, 11, 58, 56, 0);

        public static UInt32 Time42ToSeconds(long time42)
        {
            return (UInt32) (time42 >> 16);
        }

        public static UInt16 Time42ToSubseconds(long time42)
        {
            return (UInt16) (time42 & 0xFFFFL);
        }

        public static float Time42ToSubsecondsFloat(long time42)
        {
            return (time42 & 0xFFFFL)/65536f;
        }

        public static DateTime SecondaryHeaderToDateTime(long value)
        {
#if DaySegmentedTime
            var day = value >> 32;
            var msec = value & 0xFFFFFFFFL;
            const long tickResolution = 10000000L; // Ticks per second
            var dayTicks = day * 86400L * tickResolution;
            var msecTicks = msec * 10000L;
            var ticks = dayTicks + msecTicks;
            var date = AtomicEpoch.AddTicks(ticks);
            return date;
#else
            var seconds = value >> 16;
            var subseconds = value & 0xFFFFL;
            const long tickResolution = 10000000L; // Ticks per second
            var coarseTicks = seconds * tickResolution;
            var ffine = (subseconds) / 65536.0D;
            var fineTicks = (long)(ffine * tickResolution);
            var ticks = coarseTicks + fineTicks;
            var date = Epoch.AddTicks(ticks);
            return date;
#endif
        }

        // Went a long time without this, but the switch to ConversionFunction for times causes the values to be boxed in and out of the conversion function
        public static DateTime SecondaryHeaderToDateTime(ulong value)
        {
#if DaySegmentedTime
            var day = value >> 32;
            var msec = value & 0xFFFFFFFFL;
            const long tickResolution = 10000000L; // Ticks per second
            var dayTicks = day * 86400L * tickResolution;
            var msecTicks = msec * 10000L;
            var ticks = dayTicks + msecTicks;
            var date = AtomicEpoch.AddTicks((long)ticks);
            return date;
#else
            var seconds = value >> 16;
            var subseconds = value & 0xFFFFL;
            const long tickResolution = 10000000L; // Ticks per second
            var coarseTicks = seconds * tickResolution;
            var ffine = (subseconds) / 65536.0D;
            var fineTicks = (ulong)(ffine * tickResolution);
            var ticks = coarseTicks + fineTicks;
            var date = Epoch.AddTicks((long)ticks);
            return date;
#endif
        }

        public static Int64 DateTimeToSecondaryHeader(DateTime time)
        {
#if DaySegmentedTime
            const long TicksPerDay = 10000000L * 86400L;
            var ticks = (time - AtomicEpoch).Ticks;
            var days = ticks / TicksPerDay;
            var msec = (ticks - days * TicksPerDay) / 10000L;
            var v = (days << 32) | msec;
            return v;
#else
            var span = time - Epoch;
            var seconds = span.Days * 86400L + span.Hours * 3600L + span.Minutes * 60L + span.Seconds;
            long fine = (span.Milliseconds * 65536) / 1000;
            var v = (seconds << 16) | fine;
            return v;
#endif
        }

        public static DateTime Time42ToDateTime(long time42)
        {
            //if (time42 > 31020971732762L || time42 < 0L)
            //    time42 = 31020971732762; // clipping due to bugs
            //else if (time42 < 0L)
            //    time42 = 0L;
            var seconds = time42 >> 16;
            var subseconds = time42 & 0xFFFFL;
            const long tickResolution = 10000000L; // Ticks per second
            var coarseTicks = seconds*tickResolution;
            var ffine = (subseconds)/65536.0D;
            var fineTicks = (long) (ffine*tickResolution);
            var ticks = coarseTicks + fineTicks;
            var date = Epoch.AddTicks(ticks);
            return date;
        }

        // Went a long time without this, but the switch to ConversionFunction for times causes the values to be boxed in and out of the conversion function
        public static DateTime Time42ToDateTime(ulong time42)
        {
            //if (time42 > 31020971732762L || time42 < 0L)
            //    time42 = 31020971732762; // clipping due to bugs
            //else if (time42 < 0L)
            //    time42 = 0L;
            var seconds = time42 >> 16;
            var subseconds = time42 & 0xFFFFL;
            const long tickResolution = 10000000L; // Ticks per second
            var coarseTicks = seconds * tickResolution;
            var ffine = (subseconds) / 65536.0D;
            var fineTicks = (ulong)(ffine * tickResolution);
            var ticks = coarseTicks + fineTicks;
            var date = Epoch.AddTicks((long)ticks);
            return date;
        }

        public static DateTime TIMECDS24ToDateTime(long time42)
        {
            var day = time42 >> 32;
            var msec = time42 & 0xFFFFFFFFL;
            const long tickResolution = 10000000L; // Ticks per second
            var dayTicks = day * 86400L * tickResolution;
            var msecTicks = msec * 10000L;
            var ticks = dayTicks + msecTicks;
            var date = AtomicEpoch.AddTicks(ticks);
            return date;
        }

        public static long TIMECDS24ToTIME42(long t) => DateTimeToTime42(TIMECDS24ToDateTime(t));

        // Went a long time without this, but the switch to ConversionFunction for
        // times causes the values to be boxed in and out of the conversion function
        public static DateTime TIMECDS24ToDateTime(ulong time42)
        {
            var day = time42 >> 32;
            var msec = time42 & 0xFFFFFFFFL;
            const long tickResolution = 10000000L; // Ticks per second
            var dayTicks = day * 86400L * tickResolution;
            var msecTicks = msec * 10000L;
            var ticks = dayTicks + msecTicks;
            var date = AtomicEpoch.AddTicks((long)ticks);
            return date;
        }

        public static DateTime Time40ToDateTime(UInt32 time40)
        {
            return Epoch.AddTicks(10000000L*time40);
        }

        public static string SecondaryHeaderToString(long secondaryHeader)
        {
            var dt = SecondaryHeaderToDateTime(secondaryHeader);
            switch (StringFormat)
            {
                case TimeFormat.ITOS:
                    return DateTimeToITOS(dt);
                case TimeFormat.FDS:
                    return dt.ToString("yy-MM-dd HH:mm:ss.ffffff");
                case TimeFormat.DateTimeString:
                    return dt.ToString("yyyy-MM-ddTHH:mm:ss.ffffff");
                case TimeFormat.ET:
                    return dt.ToString(CultureInfo.InvariantCulture);
                case TimeFormat.STK:
                    return dt.ToString("dd MMM yyyy HH:mm:ss.fff");
                default:
                    return DateTimeToITOS(dt);
            }
        }

        public static DateTime Time42ToTTDateTime(long time42)
        {
            var seconds = time42 >> 16;
            var subseconds = time42 & 0xFFFFL;
            const long tickResolution = 10000000L; // Ticks per second
            var coarseTicks = seconds*tickResolution;
            var ffine = (subseconds)/65536.0D;
            var fineTicks = (long) (ffine*tickResolution);
            var ticks = coarseTicks + fineTicks;
            var date = TTEpoch.AddTicks(ticks);
            return date;
        }

        public static string Time42ToString(long time42)
        {
            switch (StringFormat)
            {
                case TimeFormat.ITOS:
                    return Time42ToITOS(time42);
                case TimeFormat.FDS:
                    return Time42ToLogan(time42);
                case TimeFormat.DateTimeString:
                    return Time42ToDateTimeString(time42);
                case TimeFormat.ET:
                    return Time42ToEtString(time42);
                case TimeFormat.STK:
                    return Time42ToSTK(time42);
                default:
                    return Time42ToITOS(time42);
            }
        }

        public static dynamic Time40ToString(UInt32 time40)
        {
            switch (StringFormat)
            {
                case TimeFormat.ITOS:
                    return Time40ToITOS(time40);
                case TimeFormat.FDS:
                    return Time40ToLogan(time40);
                case TimeFormat.DateTimeString:
                    return Time40ToDateTimeString(time40);
                default:
                    return Time40ToITOS(time40);
            }
        }

        public static string DateTimeToITOS(DateTime dt)
        {
            var result = dt.ToString("yy-") + dt.DayOfYear.ToString("000");
            result = result + dt.ToString("-HH:mm:ss.") + dt.Millisecond.ToString("000");
            return result;
        }

        public static string Time42ToITOS(long time42)
        {
            var dt = Time42ToDateTime(time42);
            var result = dt.ToString("yy-") + dt.DayOfYear.ToString("000");
            result = result + dt.ToString("-HH:mm:ss.") + dt.Millisecond.ToString("000");
            return result;
        }

        public static string Time42ToITOS(ulong time42)
        {
            var dt = Time42ToDateTime(time42);
            var result = dt.ToString("yy-") + dt.DayOfYear.ToString("000");
            result = result + dt.ToString("-HH:mm:ss.") + dt.Millisecond.ToString("000");
            return result;
        }

        public static string Time40ToITOS(UInt32 time40)
        {
            var dt = Time40ToDateTime(time40);
            var result = dt.ToString("yy-") + dt.DayOfYear.ToString("000");
            result = result + dt.ToString("-HH:mm:ss.") + dt.Millisecond.ToString("000");
            return result;
        }

        public static string Time42ToSTK(long time42)
        {
            var dt = Time42ToDateTime(time42);
            return dt.ToString("dd MMM yyyy HH:mm:ss.fff");
        }

        public static DateTime Time44ToDate(ulong time44)
        {
            var seconds = time44 >> 32;
            var nanosec = 0XFFFFFFFF & time44;
            var date = UnixEpoch.AddSeconds(seconds).AddMilliseconds(nanosec / 1000000D);
            return date;
        }

        public static string Time44ToITOS(ulong time44)
        {
            var seconds = time44 >> 32;
            var nanosec = 0XFFFFFFFF & time44;
            var dt = UnixEpoch.AddSeconds(seconds).AddMilliseconds(nanosec / 1000000D);
            var result = dt.ToString("yy-") + dt.DayOfYear.ToString("000");
            result = result + dt.ToString("-HH:mm:ss.") + dt.Millisecond.ToString("000");
            return result;
        }

        public static string Time42ToFilename(long time42)
        {
            var dt = Time42ToTTDateTime(time42);
            var result = dt.ToString("yy-") + dt.DayOfYear.ToString("000");
            result = result + dt.ToString("-HH-mm-ss-") + dt.Millisecond.ToString("000");
            return result;
        }

        public static string DateTimeToFilename(DateTime dt)
        {
            var result = dt.ToString("yy-") + dt.DayOfYear.ToString("000");
            result = result + dt.ToString("-HH-mm-ss-") + dt.Millisecond.ToString("000");
            return result;
        }

        public static DateTime? ZuluStringToDateTime(string zulu)
        {
            DateTime d2;
            if (DateTime.TryParse(zulu, null, DateTimeStyles.RoundtripKind, out d2))
                return d2;
            return null;
        }

        public static long ZuluStringToTime42(string zulu)
        {
            var dt = ZuluStringToDateTime(zulu);
            return dt.HasValue ? DateTimeToTime42(dt.Value) : -1L;
        }

        public static string Time42ToZuluString(long time42)
        {
            return DateTimeToZuluString(Time42ToDateTime(time42));
        }

        public static long ZuluDayOfYearStringToTime42(string s)
        {
            var dt = ZuluDayOfYearStringToDateTime(s);
            return dt.HasValue ? DateTimeToTime42(dt.Value) : -1L;  // Not sure about what this should be
        }

        public static string Time44ToFilename(ulong time44)
        {
            var seconds = time44 >> 32;
            var nanosec = 0XFFFFFFFF & time44;
            var dt = UnixEpoch.AddSeconds(seconds).AddMilliseconds(nanosec / 1000000D);
            var result = dt.ToString("yy-") + dt.DayOfYear.ToString("000");
            result = result + dt.ToString("-HH-mm-ss-") + dt.Millisecond.ToString("000");
            return result;
        }

        public static string Time40ToSTK(UInt32 time40)
        {
            var dt = Time42ToDateTime(time40);
            dt = dt.AddSeconds(-3);
            return dt.ToString("dd MMM yyyy HH:mm:ss.fff");
        }

        public static string Time42ToEtString(long time42)
        {
            return Time42ToET(time42).ToString(CultureInfo.InvariantCulture);
        }

        public static long STKToTime42(string s)
        {
            DateTime result;
            return DateTime.TryParse(s, out result) ? DateTimeToTime42(result) : 0;
        }

        public static string DateTimeToString(DateTime dt)
        {
            var result = dt.ToString("yy-") + dt.DayOfYear.ToString("000");
            result = result + dt.ToString("-HH:mm:ss.") + dt.Millisecond.ToString("000");
            return result;
        }

        public static string FileTimestamp(DateTime dt)
        {
            return dt.ToString("yy") + dt.DayOfYear.ToString("000") + dt.ToString("HHmmss");
        }

        public static string Time42ToLogan(long time42)
        {
            return Time42ToDateTime(time42).ToString("yy-MM-dd HH:mm:ss.ffffff");
        }

        public static string Time40ToLogan(UInt32 time40)
        {
            return Time40ToDateTime(time40).ToString("yy-MM-dd HH:mm:ss.ffffff");
        }

        public static string Time42ToDateTimeString(long time42)
        {
            return Time42ToDateTime(time42).ToString("yyyy-MM-ddTHH:mm:ss.ffffff");
        }

        public static string Time40ToDateTimeString(UInt32 time40)
        {
            return Time40ToDateTime(time40).ToString("yyyy-MM-ddTHH:mm:ss.ffffff");
        }

        public static string ScTimeToGseString(uint coarse, uint fine)
        {
            // To convert from epoch UTC (which should be 1/1/1970
            // 00:00:00 UTC) to a human readable time, you'll need to
            // find the number of ticks between the DateTime class'
            // base time (1/1/0001 00:00:00) to epoch time. You
            // multiply your epoch time by the tick resolution (100
            // nanoseconds / tick) and add your base ticks (epoch time
            // - base time). Then you pass the ticks into the DateTime
            // constructor and get a nice human-readable result. Here
            // is an example:

            //long baseTicks = 621355968000000000;
            const long tickResolution = 10000000;
            var coarseTicks = (coarse*tickResolution);
            var ffine = fine/65536.0;
            var fineTicks = (long) (ffine*tickResolution);
            var ticks = coarseTicks + fineTicks;
            var date = Epoch.AddTicks(ticks);
            //DateTime date = new DateTime(ticks, DateTimeKind.Utc);
            var msec = (int) (ffine*1000d);
            return date.ToString("yy-MM-dd HH:mm:ss.") + msec.ToString("000");
            //return date.ToLongDateString() + " " + date.ToLongTimeString();
        }

        public static Int64 StringToTime42(string datestring)
        {
            var d = DateTime.Parse(datestring);
            return DateTimeToTime42(d);
        }

        //        public static string TimeTypeToString(TimeType time)
        //        {
        //            // To convert from epoch UTC (which should be 1/1/1970
        //            // 00:00:00 UTC) to a human readable time, you'll need to
        //            // find the number of ticks between the DateTime class'
        //            // base time (1/1/0001 00:00:00) to epoch time. You
        //            // multiply your epoch time by the tick resolution (100
        //            // nanoseconds / tick) and add your base ticks (epoch time
        //            // - base time). Then you pass the ticks into the DateTime
        //            // constructor and get a nice human-readable result. Here
        //            // is an example:
        //
        //            long fine = (long)(0xFFFF & time);
        //            long coarse = (long)(time >> 16);
        //            long tickResolution = 10000000;
        //            long coarseTicks = (coarse * tickResolution);
        //            double ffine = fine / 65536.0;
        //            long fineTicks = (long)(ffine * tickResolution);
        //            long ticks = coarseTicks + fineTicks;
        //            DateTime date = Epoch.AddTicks(ticks);
        //            return date.ToString("yy-MM-dd HH:mm:ss.") + date.Millisecond.ToString("000");
        //        }

        //        public static DateTime Time42ToDateTime(TimeType time)
        //        {
        //            long fine = (long)(0xFFFF & time);
        //            long coarse = (long)(time >> 16);
        //            long tickResolution = 10000000;
        //            long coarseTicks = (coarse * tickResolution);
        //            double ffine = fine / 65536.0;
        //            long fineTicks = (long)(ffine * tickResolution);
        //            long ticks = coarseTicks + fineTicks;
        //            DateTime date = Epoch.AddTicks(ticks);
        //            return date;
        //        }

        public static Int64 DateTimeToTime42(DateTime time)
        {
            var span = time - Epoch;
            var seconds = span.Days*86400L + span.Hours*3600L + span.Minutes*60L + span.Seconds;
            long fine = (span.Milliseconds*65536)/1000;
            var v = (seconds << 16) | fine;
            return v;
        }

        public static Int64 ToTime42(TimeSpan span)
        {
            var seconds = span.Days * 86400L + span.Hours * 3600L + span.Minutes * 60L + span.Seconds;
            long fine = (span.Milliseconds * 65536) / 1000;
            var v = (seconds << 16) | fine;
            return v;
        }

        public static DateTime ETToDateTime(double et)
        {
            var ticks = (long) (et*10000000L);
            var result = Epoch.AddTicks(ticks);
            return result;
        }

        public static double DateTimeToET(DateTime time)
        {
            return (time - Epoch).TotalSeconds + 3d;
            // The 3D accounts for leap seconds since 2000.  This is valid only for dates after Jul 1 2012.
        }

        public static long ETToTime42(double et)
        {
            return (long) ((et - 3d)*65536D);
        }

        public static double Time42ToET(long time42)
        {
            return time42/65536D + 3d;
        }

        public static Int64 Time42FromSecondsSubseconds(uint seconds, ushort subseconds)
        {
            return ((seconds << 16) | subseconds);
        }

        public static long Time42ToTicks(long time42)
        {
            return (long) (time42*(10000000D/65536D));
        }

        public static long Time42ToInjectorSeconds(long time42)
        {
            var dt = Time42ToDateTime(time42);
            var span = dt - InjectorEpoch;
            var totalSeconds = span.Days*3600L*24L + span.Hours*3600L + span.Minutes*60L + span.Seconds;
            return totalSeconds;
        }

        public static long Time42ToInjectorSubseconds(long time42)
        {
            var dt = Time42ToDateTime(time42);
            var msec = dt.Millisecond;
            return msec*1000L;
        }

        public static string DateTimeToZuluString(DateTime dt)
        {
            return dt.ToString("yyyy-") + dt.DayOfYear.ToString("000") + dt.ToString("THH:mm:ss.fffZ");
        }

        public static DateTime? ZuluDayOfYearStringToDateTime(string s)
        {
            // Format: yyyy-DDDTHH:mm:ss.fffZ
            //         012345678901234567890123456789
            if (s.Length != 22 | s[4] != '-' | s[8] != 'T' | s[11] != ':' | s[14] != ':' | s[17] != '.' | s[21] != 'Z')
                return null;
            int year, day, hour, minute, second, millisecond;
            if (!int.TryParse(s.Substring(0, 4), out year)) return null;
            if (!int.TryParse(s.Substring(5, 3), out day)) return null;
            if (!int.TryParse(s.Substring(9, 2), out hour)) return null;
            if (!int.TryParse(s.Substring(12, 2), out minute)) return null;
            if (!int.TryParse(s.Substring(15, 2), out second)) return null;
            if (!int.TryParse(s.Substring(18, 3), out millisecond)) return null;
            var dt = new DateTime(year, 1, 1, hour, minute, second).AddDays(day - 1).AddMilliseconds(millisecond);
            return dt;
        }

        public static DateTime? ParseDOYTime(string text)
        {
            try
            {
                if (DOYDateRecognizer == null)
                {
                    DOYDateRecognizer = new Regex(@"^(\d\d)-(\d\d\d)-(\d\d):(\d\d):(\d\d).(\d\d\d)[ ]*$",
                       RegexOptions.IgnoreCase | RegexOptions.Compiled |
                       RegexOptions.Singleline);
                }

                var match = DOYDateRecognizer.Match(text);
                if (!match.Success) return null;
                var year = int.Parse(match.Groups[1].Value) + 2000;
                if (year < 2000 || year > 2099) return null;
                var doy = int.Parse(match.Groups[2].Value);
                if (doy > 366 || doy == 366 && !DateTime.IsLeapYear(year)) return null;
                var hour = int.Parse(match.Groups[3].Value);
                if (hour > 23) return null;
                var minute = int.Parse(match.Groups[4].Value);
                if (minute > 59) return null;
                var second = int.Parse(match.Groups[5].Value);
                if (second > 59) return null;
                var msec = int.Parse(match.Groups[6].Value);
                var result = new DateTime(year, 1, 1, hour, minute, second, msec, DateTimeKind.Utc);
                result = result.AddDays(doy - 1);
                if (result < Epoch) return null;
                return result;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static TimeSpan Time42ToTimeSpan(long time42)
        {
            return Time42ToDateTime(time42) - Epoch;
        }

        public static string TimeSpanToString(TimeSpan span)
        {
            return span.ToString();
        }

        public static string Time42ToSpanString(long time42)
        {
            return TimeSpanToString(Time42ToTimeSpan(time42));
        }
    }
}