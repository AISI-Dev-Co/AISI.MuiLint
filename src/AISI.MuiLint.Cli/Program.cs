using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace AISI.MuiLint.Cli
{
    /// <summary>
    /// Command-line entry point for AISI.MuiLint. Walks files or directories and prints
    /// HTML merge findings.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Scans each argument as an HTML file or a directory of <c>.html</c> files.
        /// </summary>
        /// <param name="args">Paths to scan.</param>
        /// <returns>0 if clean, 1 if any finding, 2 if usage error.</returns>
        public static int Main(string[] args)
        {
            if (args is null || args.Length == 0)
            {
                Console.Error.WriteLine("Usage: muilint <file-or-directory>...");
                return 2;
            }

            int findings = 0;
            bool missing = false;
            foreach (string path in Expand(args))
            {
                if (path.Length == 0)
                {
                    missing = true;
                    continue;
                }

                string text = File.ReadAllText(path);
                IReadOnlyList<Diagnostic> results = HtmlMergeScanner.Analyze(path, text);
                for (int i = 0; i < results.Count; i++)
                {
                    Diagnostic d = results[i];
                    Console.WriteLine(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}({1},{2}): {3}: {4}",
                        d.Path,
                        d.Line,
                        d.Column,
                        d.Id,
                        d.Message));
                    findings++;
                }
            }

            if (missing && findings == 0)
            {
                return 2;
            }

            return findings == 0 ? 0 : 1;
        }

        private static IEnumerable<string> Expand(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (Directory.Exists(arg))
                {
                    foreach (string file in Directory.EnumerateFiles(arg, "*.html", SearchOption.AllDirectories))
                    {
                        yield return file;
                    }
                }
                else if (File.Exists(arg))
                {
                    yield return arg;
                }
                else
                {
                    Console.Error.WriteLine("Not found: " + arg);
                    yield return string.Empty;
                }
            }
        }
    }
}
