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

        /// <summary>Unbalanced brackets or quotes in a merge selector.</summary>
        public const string MalformedSelector = "AISI0006";

        /// <summary>Extension HTML with no TypeScript file of the same name beside it.</summary>
        public const string ExtensionWithoutTypeScript = "AISI0007";

        /// <summary>The same field name (per view) or id appears twice in one file.</summary>
        public const string DuplicateNameOrId = "AISI0008";

        /// <summary>A merge selector points at a name or id the stock screen does not have.</summary>
        public const string SelectorNotInStock = "AISI0009";

        /// <summary>A field added by an extension is not Usr-prefixed.</summary>
        public const string FieldWithoutUsrPrefix = "AISI0010";

        /// <summary>A view, field, action or panel the screen's TypeScript does not declare.</summary>
        public const string BindingNotInTypeScript = "AISI0011";

        /// <summary>A <c>qp-*</c> control without an id.</summary>
        public const string QpControlWithoutId = "AISI0012";

        /// <summary>Unbalanced brackets or quotes in <c>config.bind</c>.</summary>
        public const string MalformedConfig = "AISI0013";

        /// <summary>An extension interface without its class, or the other way round.</summary>
        public const string HalfAnExtension = "AISI0014";

        /// <summary>A decorator names a view the screen doesn't have.</summary>
        public const string DecoratorViewNotDeclared = "AISI0015";

        /// <summary>createSingle/createCollection given a class that isn't a PXView.</summary>
        public const string ViewFromNonView = "AISI0016";
    }
}
