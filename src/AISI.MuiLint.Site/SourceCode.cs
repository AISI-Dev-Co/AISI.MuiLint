using System;
using System.Collections.Generic;
using System.IO;

namespace AISI.MuiLint.Site
{
    /// <summary>The C# a site compiles outside Bin: App_RuntimeCode, and the extension libraries under App_Data/Projects.</summary>
    internal static class SourceCode
    {
        private static readonly string[] Skipped = { "bin", "obj", "node_modules" };

        /// <summary>Every .cs file in those folders. Callers catch the IO errors.</summary>
        public static IEnumerable<string> Files(string site)
        {
            foreach (string root in new[] { Path.Combine(site, "App_RuntimeCode"), Path.Combine(site, "App_Data", "Projects") })
            {
                if (!Directory.Exists(root))
                {
                    continue;
                }

                var folders = new Stack<string>();
                folders.Push(root);
                while (folders.Count > 0)
                {
                    string folder = folders.Pop();
                    foreach (string sub in Directory.GetDirectories(folder))
                    {
                        string name = Path.GetFileName(sub);
                        if (!name.StartsWith(".", StringComparison.Ordinal) && Array.FindIndex(Skipped, s => s.Equals(name, StringComparison.OrdinalIgnoreCase)) < 0)
                        {
                            folders.Push(sub);
                        }
                    }

                    foreach (string file in Directory.GetFiles(folder, "*.cs"))
                    {
                        yield return file;
                    }
                }
            }
        }

        /// <summary>The site folder: the one holding Bin.</summary>
        public static string SiteOf(string binFolder)
        {
            string full = Path.GetFullPath(binFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return Path.GetDirectoryName(full) ?? full;
        }
    }
}
