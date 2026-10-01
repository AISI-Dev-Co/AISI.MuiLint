using System;
using System.Collections.Generic;
using System.IO;
using AISI.MuiLint;

namespace AISI.MuiLint.Vsix
{
    /// <summary>
    /// Visual Studio entry point for AISI.MuiLint HTML analysis.
    /// </summary>
    /// <remarks>
    /// The VS 2022 HTML editor MEF tagger and Error List source call
    /// <see cref="AnalyzeHtml"/> on the buffer. This is the single call into
    /// <see cref="HtmlMergeScanner"/>. The CLI and <see cref="Analyzer"/> use the same scanner.
    /// Editor MEF types live under Editor/ and compile only on Windows (Visual Studio SDK).
    /// On Linux this stub still compiles as net472; packing a VSIX requires Windows.
    /// </remarks>
    public static class MuiLintPackage
    {
        /// <summary>
        /// Runs the HTML merge analyzer. The HTML-editor tagger calls this for the current buffer.
        /// </summary>
        /// <param name="path">Path of the HTML document.</param>
        /// <param name="text">Raw HTML.</param>
        /// <returns>Findings in source order.</returns>
        public static IReadOnlyList<Diagnostic> AnalyzeHtml(string path, string text)
        {
            // A buffer with no real file behind it has no neighbours to look at.
            return HtmlMergeScanner.Analyze(path, text, Path.IsPathRooted(path) ? TryReadFile : null);
        }

        /// <summary>Reads a file, or returns null if it is missing or locked.</summary>
        public static string? TryReadFile(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }
    }
}
