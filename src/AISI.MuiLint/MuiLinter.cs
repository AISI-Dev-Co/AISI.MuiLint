using System;
using System.Collections.Generic;

namespace AISI.MuiLint
{
    /// <summary>Picks the scanner for a file by its extension.</summary>
    public static class MuiLinter
    {
        /// <summary>True for .html and .ts files (not .d.ts).</summary>
        public static bool CanScan(string path)
        {
            if (path is null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            return path.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
                || (path.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".d.ts", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Runs <see cref="TypeScriptScanner"/> on .ts files and <see cref="HtmlMergeScanner"/> on anything else.</summary>
        public static IReadOnlyList<Diagnostic> Analyze(
            string path,
            string text,
            Func<string, string?>? readFile,
            Func<string, IEnumerable<string>>? listFolder = null)
        {
            if (path is null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            return path.EndsWith(".ts", StringComparison.OrdinalIgnoreCase)
                ? TypeScriptScanner.Analyze(path, text, readFile, listFolder)
                : HtmlMergeScanner.Analyze(path, text, readFile, listFolder);
        }
    }
}
