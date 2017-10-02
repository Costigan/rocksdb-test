using System;
using System.IO;
using System.Text.RegularExpressions;

namespace gov.nasa.arc.ccsds.utilities
{
    public class FileTreeWalker
    {
        public delegate void WalkFunction(FileInfo fi);

        public static readonly RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.Compiled |
                                                      RegexOptions.Singleline;

        public static readonly Regex DatRecognizer = new Regex(@"^.*\.dat(\.\d*)?$", Options);
        public static readonly Regex HkRecognizer = new Regex(@"^.*\.hk(\.\d\*)?$", Options);
        public static readonly Regex LogRecognizer = new Regex(@"^.*\.log(\.\d\*)?$", Options);
        public static readonly Regex DBFileRecognizer = new Regex(@"^.*db_\d\d_\d\d\d.*\.dat$", Options);

        public static void Walk(string dir, WalkFunction f)
        {
            Walk(new DirectoryInfo(dir), f);
        }

        public static void Walk(DirectoryInfo di, WalkFunction f)
        {
            FileInfo[] files = null;
            try
            {
                files = di.GetFiles("*.*");
            }
                // This is thrown if even one of the files requires permissions greater 
                // than the application provides. 
            catch (UnauthorizedAccessException e)
            {
                Console.WriteLine(e.Message);
            }
            catch (DirectoryNotFoundException e)
            {
                Console.WriteLine(e.Message);
                return;
            }
            if (files != null)
            {
                foreach (var fi in files)
                {
                    // In this example, we only access the existing FileInfo object. If we 
                    // want to open, delete or modify the file, then 
                    // a try-catch block is required here to handle the case 
                    // where the file has been deleted since the call to TraverseTree().
                    f(fi);
                }
            }

            // Now find all the subdirectories under this directory.
            foreach (var dirInfo in di.GetDirectories())
            {
                if (!dirInfo.Name.ToLowerInvariant().Equals(".svn"))
                    Walk(dirInfo, f);
            }
        }

        public static void Walk(FileInfo fi, WalkFunction f)
        {
            f(fi);
        }

        public static bool IsSuccessfulCFDPFile(string p)
        {
            var path = p + ".meta";
            if (!File.Exists(path))
                return false;
            using (var sr = new StreamReader(path))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.Equals("File Transfer Status::       Successful"))
                        return true;
                }

                return false;
            }
        }
    }
}