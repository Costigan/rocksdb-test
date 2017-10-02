using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace RocksDB_test1
{
    /// <summary>
    /// Provides static methods for walking a file tree
    /// </summary>
    public class FileWalker
    {
        private static readonly string[] IgnoreTheseDirectories = { ".git", "ignored_data" };
        public static char[] DirectorySplitChars = new char[] { '/', '\\' };

        private static readonly RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline;
        private static readonly Regex ITOSArchiveFilePattern = new Regex(@"^.*\.\d+$", Options);
        private static readonly Regex DatRecognizer = new Regex(@"^.*\.dat(\.\d*)?$", Options);
        private static readonly Regex HkRecognizer = new Regex(@"^.*\.hk(\.\d\*)?$", Options);
        private static readonly Regex LogRecognizer = new Regex(@"^.*\.log(\.\d\*)?$", Options);
        private static readonly Regex DBFileRecognizer = new Regex(@"^.*db_\d\d_\d\d\d.*\.dat$", Options);

        #region Public Methods

        public static IEnumerable<string> Walk(string pattern)
        {
            var root = Path.GetPathRoot(pattern);
            var remainder = pattern.Substring(root == null ? 0 : root.Length);
            var remainderArray = remainder.Split(DirectorySplitChars);
            // handle case of dir with final / by removing the null path at the end
            if (remainderArray.Length > 0 && remainderArray[remainderArray.Length - 1].Length == 0)
                remainderArray = remainderArray.Take(remainderArray.Length - 1).ToArray();
            return Walk(new DirectoryInfo((root == null || root.Length == 0) ? Directory.GetCurrentDirectory() : root), remainderArray, 0);
        }

        public static IEnumerable<string> WalkITOSFiles(string pattern)
        {
            var buffer = new List<string>();
            string bufferedFilename = null;
            foreach (var f in Walk(pattern))
            {
                if (!KeepThisFile(f))
                    continue;
                if (bufferedFilename == null)  // not buffering
                {
                    if (NumericExtension(f) > -2) // Start buffering
                    {
                        bufferedFilename = Path.GetFileNameWithoutExtension(f);
                        buffer.Add(f);
                    }
                    else
                    {
                        yield return f;
                    }
                }
                else
                {
                    if (bufferedFilename.Equals(Path.GetFileNameWithoutExtension(f)))
                    {
                        buffer.Add(f);
                    }
                    else // finished buffering
                    {
                        foreach (var f1 in buffer.OrderBy(NumericExtension))
                            yield return f1;
                        bufferedFilename = null;
                        buffer.Clear();
                        // Can't push back, so repeat some code
                        if (NumericExtension(f) > -2) // Start buffering again
                        {
                            bufferedFilename = Path.GetFileNameWithoutExtension(f);
                            buffer.Add(f);
                        }
                        else
                        {
                            yield return f;
                        }
                    }
                }
            }
            // Empty the buffer
            foreach (var f1 in buffer.OrderBy(NumericExtension))
                yield return f1;
        }

        #endregion Public Methods

        #region Protected Methods

        protected static IEnumerable<string> Walk(DirectoryInfo parent, string[] dirs, int index)
        {
            //Console.WriteLine(@"parent={0} index={1} dirs={2}", parent.FullName, index, dirs.Aggregate((a, b) => string.Concat(a, ", ", b)));
            if (!IgnoreTheseDirectories.Contains(parent.Name))
            {
                if (index < dirs.Length - 1)
                {
                    foreach (var childdir in parent.EnumerateDirectories(dirs[index]))
                    {
                        foreach (var f in Walk(childdir, dirs, index + 1))
                            yield return f;
                    }
                }
                else if (index == dirs.Length - 1)
                {
                    foreach (var childfile in parent.EnumerateFiles(dirs[index]))
                        yield return childfile.FullName;
                    foreach (var childdir in parent.EnumerateDirectories(dirs[index]))
                    {
                        foreach (var f in Walk(childdir, dirs, index + 1))
                            yield return f;
                    }
                }
                else if (index > dirs.Length - 1)
                {
                    foreach (var childfile in parent.EnumerateFiles())
                        yield return childfile.FullName;
                    foreach (var childdir in parent.EnumerateDirectories())
                    {
                        foreach (var f in Walk(childdir, dirs, index + 1))
                            yield return f;
                    }
                }
            }
        }

        protected static bool KeepThisFile(string filename) =>
            ".H".Equals(Path.GetExtension(filename), StringComparison.InvariantCultureIgnoreCase)
            || ITOSArchiveFilePattern.IsMatch(filename)
            || DatRecognizer.IsMatch(filename)
            || HkRecognizer.IsMatch(filename)
            || LogRecognizer.IsMatch(filename)
            || DBFileRecognizer.IsMatch(filename);

        protected static bool ExtensionEquals(string f, string t)
        {
            var e = Path.GetExtension(f);
            return e?.ToLowerInvariant().Equals(t) == true;
        }

        public static int NumericExtension(string path)
        {
            var e = Path.GetExtension(path);
            if (e == null || e.Length < 1) return -2;
            if (".H".Equals(e, StringComparison.InvariantCultureIgnoreCase))
                return -1;
            e = e.Substring(1);
            int v;
            return int.TryParse(e, out v) ? v : -2;
        }

        public static bool IsGlob(string p) => p.Contains('*') || p.Contains('?');

        #endregion Protected Methods


    }
}
