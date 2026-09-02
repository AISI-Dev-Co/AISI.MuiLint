using System.Collections.Generic;
using AISI.MuiLint;

namespace AISI.MuiLint.Vsix
{
    /// <summary>
    /// Visual Studio package stub for AISI.MuiLint.
    /// </summary>
    /// <remarks>
    /// A later iteration hooks the VS 2022 HTML editor and calls
    /// <see cref="AnalyzeHtml"/> on the buffer. The scanner is editor-agnostic and is already
    /// used by the CLI and by <see cref="Analyzer"/>. This project ships a valid
    /// VS 2022 vsixmanifest (Publisher AISI Dev Co, installation target [17.0,18.0)) so it
    /// can be packed on Windows; the Visual Studio SDK is not required to compile the stub.
    /// </remarks>
    public static class MuiLintPackage
    {
        /// <summary>
        /// Runs the HTML merge analyzer. Future HTML-editor integration calls this.
        /// </summary>
        /// <param name="path">Path of the HTML document.</param>
        /// <param name="text">Raw HTML.</param>
        /// <returns>Findings in source order.</returns>
        public static IReadOnlyList<Diagnostic> AnalyzeHtml(string path, string text)
        {
            return HtmlMergeScanner.Analyze(path, text);
        }
    }
}
