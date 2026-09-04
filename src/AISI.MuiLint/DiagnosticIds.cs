namespace AISI.MuiLint
{
    /// <summary>
    /// Stable diagnostic identifiers for the HTML merge linter.
    /// </summary>
    public static class DiagnosticIds
    {
        /// <summary>Self-closing <c>&lt;field&gt;</c> or <c>&lt;qp-*&gt;</c> tag.</summary>
        public const string SelfClosing = "AISI0001";

        /// <summary>
        /// <c>after</c>/<c>before</c> <c>[name='X']</c> where <c>X</c> is a <c>name=</c> in the same file.
        /// </summary>
        public const string AfterBeforeSameFile = "AISI0002";

        /// <summary>Path is stock <c>src/screens</c> rather than <c>development/screens</c>.</summary>
        public const string StockScreensPath = "AISI0003";

        /// <summary>
        /// File under <c>/extensions/</c> whose basename equals the parent screen folder.
        /// </summary>
        public const string ExtensionBasename = "AISI0004";

        /// <summary>Empty <c>qp-fieldset</c> (whitespace or comments only).</summary>
        public const string EmptyFieldset = "AISI0005";
    }
}
