namespace AISI.MuiLint
{
    /// <summary>
    /// Stable diagnostic identifiers for the HTML merge linter.
    /// </summary>
    public static class DiagnosticIds
    {
        /// <summary>Self-closing <c>&lt;field&gt;</c> or <c>&lt;qp-*&gt;</c> tag.</summary>
        public const string SelfClosing = "AISI0001";

        // AISI0002 is retired: anchoring on a field the same file adds works. Don't reuse the id.

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

        // AISI0007 is retired: Acumatica ships extension HTML with no .ts of its own. Don't reuse the id.

        /// <summary>The same field name (per view) or id appears twice in one file.</summary>
        public const string DuplicateNameOrId = "AISI0008";

        /// <summary>A merge selector points at a name or id the stock screen does not have.</summary>
        public const string SelectorNotInStock = "AISI0009";

        // AISI0010 is retired: Usr is a database column convention, not a binding rule. Don't reuse the id.

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

        /// <summary>An extension .html or .ts outside an extensions folder.</summary>
        public const string ExtensionOutsideExtensions = "AISI0017";

        /// <summary>A tag with a merge attribute that isn't a direct child of the top-level template.</summary>
        public const string MergeTagNotAtTopLevel = "AISI0018";
    }
}
