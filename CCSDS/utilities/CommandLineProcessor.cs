using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;

namespace gov.nasa.arc.ccsds.utilities
{
    /// <summary>
    /// A class that contains helper functions for command line parsing.
    /// This shouldn't be in CCSDS, but it's small and CCSDS is the one library that's used everywhere on the server side of WARP
    /// </summary>
    public class CommandLineProcessor
    {
        protected virtual void ValidateNumberOfArguments(List<string> args, string flag, int min, int max)
        {
            if (args.Count > max)
                ErrorExit(@"Too many arguments were supplied for " + flag + ".  Exiting.");
            if (args.Count < min)
                ErrorExit(@"Not enough arguments were supplied for " + flag + ".  Exiting.");
        }

        protected virtual void ErrorExit(string msg)
        {
            Console.Error.WriteLine(@"ERROR: {0}", msg);
            WriteHelpMessage();
            Environment.Exit(1);
        }

        protected virtual void WriteHelpMessage()
        {
            Console.WriteLine(@"<default help message>");
        }

        protected virtual List<string> ExpandFilenames(IEnumerable<string> args, string root)
        {
            var result = new List<String>();
            foreach (var arg in args)
            {
                if (arg.Contains("*") || arg.Contains('?'))
                {
                    var path = (arg.Length > 0 && arg[0] == Path.DirectorySeparatorChar)
                        ? arg
                        : Combine(root, arg);
                    var dir = Path.GetDirectoryName(path);
                    var filepat = Path.GetFileName(path);
                    result.AddRange(Directory.GetFiles(dir ?? ".", filepat));
                }
                else
                {
                    result.Add(arg);
                }
            }
            // Remove .h files and sort by numemric extension (if present)
            result = result.Where(f => !ExtensionEquals(f, ".h")).ToList();
            var groups = result.GroupBy(Path.GetFileNameWithoutExtension);
            var result2 = groups.SelectMany(g => g.OrderBy(NumericExtension)).ToList();
            return result2;
        }

        public static string Combine(params string[] parts) => (new Uri(Path.Combine(parts)).LocalPath);

        protected virtual bool ExtensionEquals(string f, string t)
        {
            var e = Path.GetExtension(f);
            return e?.ToLowerInvariant().Equals(t) == true;
        }

        protected virtual int NumericExtension(string path)
        {
            var e = Path.GetExtension(path);
            if (e == null) return -1;
            int v;
            return int.TryParse(e, out v) ? v : -1;
        }

        protected virtual List<string> PopNonCommandArgs(Stack<string> stack)
        {
            var lst = new List<string>();
            while (stack.Count > 0 && stack.Peek()[0] != '-')
                lst.Add(stack.Pop());
            return lst;
        }

        public static int ReadAppSettingInt(string name, int defaultValue)
        {
            int v;
            return Int32.TryParse(ConfigurationManager.AppSettings[name], out v) ? v : defaultValue;
        }

        public static string ReadAppSetting(string name, string defaultValue)
        {
            return ConfigurationManager.AppSettings[name] ?? defaultValue;
        }
    }
}
