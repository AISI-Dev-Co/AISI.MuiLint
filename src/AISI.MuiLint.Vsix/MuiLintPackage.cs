using System;
using System.Collections.Generic;
using System.IO;
using AISI.MuiLint;

namespace AISI.MuiLint.Vsix
{
    /// <summary>
    /// Visual Studio entry point for AISI.MuiLint analysis.
    /// </summary>
    /// <remarks>
    /// The MEF tagger and Error List source call <see cref="Analyze"/> on HTML and TypeScript
    /// buffers. The CLI goes through the same <see cref="MuiLinter"/>.
    /// Editor MEF types live under Editor/ and compile only on Windows (Visual Studio SDK).
    /// On Linux this stub still compiles as net472; packing a VSIX requires Windows.
    /// </remarks>
    public static class MuiLintPackage
    {
        /// <summary>
        /// Lints an HTML or TypeScript buffer. The editor tagger calls this for the current buffer.
        /// </summary>
        /// <param name="path">Path of the document.</param>
        /// <param name="text">Its text.</param>
        /// <returns>Findings in source order.</returns>
        public static IReadOnlyList<Diagnostic> Analyze(string path, string text)
        {
            // A buffer with no real file behind it has no neighbours to look at.
            return MuiLinter.Analyze(path, text, Path.IsPathRooted(path) ? TryReadFile : null);
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
