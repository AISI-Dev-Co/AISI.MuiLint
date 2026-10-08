using System;
using System.Collections.Generic;

namespace AISI.MuiLint
{
    /// <summary>Metadata for one diagnostic: what it is called, how loud it is, where its docs live.</summary>
    public sealed class Rule
    {
        internal Rule(string id, string title, Severity defaultSeverity, string description)
        {
            Id = id;
            Title = title;
            DefaultSeverity = defaultSeverity;
            Description = description;
        }

        /// <summary>Gets the diagnostic id, for example <c>AISI0001</c>.</summary>
        public string Id { get; }

        /// <summary>Gets the one-line title.</summary>
        public string Title { get; }

        /// <summary>Gets the severity used when nothing overrides it.</summary>
        public Severity DefaultSeverity { get; }

        /// <summary>Gets the longer explanation.</summary>
        public string Description { get; }

        /// <summary>Gets the link to this rule's page under <c>docs/rules</c>.</summary>
        public string HelpUri
        {
            get { return Rules.RepositoryUrl + "/blob/main/docs/rules/" + Id + ".md"; }
        }
    }

    /// <summary>Every rule the scanner can report, in id order.</summary>
    public static class Rules
    {
        /// <summary>Home of the project.</summary>
        public const string RepositoryUrl = "https://github.com/AISI-Dev-Co/AISI.MuiLint";

        /// <summary>Gets all rules.</summary>
        public static IReadOnlyList<Rule> All { get; } = new[]
        {
            new Rule(
                DiagnosticIds.SelfClosing,
                "Self-closing Modern UI tag",
                Severity.Error,
                "Acumatica's docs: you can't use shortened versions of custom HTML tags such as <qp-grid .../>. Use explicit end tags."),
            new Rule(
                DiagnosticIds.StockScreensPath,
                "Stock src/screens path",
                Severity.Error,
                "src/screens holds Acumatica's own screen sources. Customizations belong in src/development/screens, where a customization project picks them up and publishing leaves them alone."),
            new Rule(
                DiagnosticIds.ExtensionBasename,
                "Extension named as the parent screen",
                Severity.Warning,
                "Acumatica names extension files <ScreenID>_<postfix> (SO301000_Custom.html). An extension called just SO301000 is named like the screen it extends."),
            new Rule(
                DiagnosticIds.EmptyFieldset,
                "Empty qp-fieldset",
                Severity.Suggestion,
                "A qp-fieldset with nothing in it that nothing in the file adds to. Hidden fieldsets, wg-containers and merge-operation fieldsets are exempt."),
            new Rule(
                DiagnosticIds.MalformedSelector,
                "Malformed merge selector",
                Severity.Error,
                "A merge selector (after, before, append, prepend, modify, remove, replace) is not a valid CSS selector. The Modern UI build fails on a selector that doesn't match exactly one element."),
            new Rule(
                DiagnosticIds.DuplicateNameOrId,
                "Duplicate field name or id",
                Severity.Warning,
                "The same id or the same field twice in one container. A selector that matches more than one element fails the build."),
            new Rule(
                DiagnosticIds.SelectorNotInStock,
                "Selector target not in stock HTML",
                Severity.Warning,
                "A merge selector names a [name] or #id that the stock screen, its other extensions and the lines above don't have. The Modern UI build fails on a selector that matches nothing."),
            new Rule(
                DiagnosticIds.BindingNotInTypeScript,
                "Binding not declared in the screen's TypeScript",
                Severity.Warning,
                "view.bind, a field's name or a button's state.bind refers to something the screen's TypeScript (with its imports and every extension) doesn't declare. Acumatica's docs: you declare every field you want to show or use in the UI."),
            new Rule(
                DiagnosticIds.QpControlWithoutId,
                "qp-* control without an id",
                Severity.Suggestion,
                "Customizations target controls by #id. Controls Acumatica doesn't give ids (qp-field, qp-label, qp-include, qp-address-lookup and the like) are exempt, as are ids given through config.bind."),
            new Rule(
                DiagnosticIds.MalformedConfig,
                "Malformed config.bind",
                Severity.Error,
                "config.bind has an unclosed brace, bracket, parenthesis or quote, so it isn't a valid expression."),
            new Rule(
                DiagnosticIds.HalfAnExtension,
                "Half an extension",
                Severity.Warning,
                "Acumatica's docs: an extension is an interface that extends the class and a class with the same name. An empty interface without its class, or a class with Modern UI members and no interface, adds nothing."),
            new Rule(
                DiagnosticIds.DecoratorViewNotDeclared,
                "Decorator names an unknown view",
                Severity.Warning,
                "primaryView in @graphInfo, or view in @handleEvent, names a view the screen's TypeScript does not declare, in any case."),
            new Rule(
                DiagnosticIds.ViewFromNonView,
                "View created from a class that isn't a PXView",
                Severity.Warning,
                "Acumatica's docs give createSingle and createCollection a view class that extends PXView. This one is given something else, usually an extension class."),
            new Rule(
                DiagnosticIds.ExtensionOutsideExtensions,
                "Extension outside an extensions folder",
                Severity.Warning,
                "A file named <ScreenID>_<postfix> under development/screens or customizationScreens that isn't in the screen's extensions folder, where Acumatica keeps extensions."),
            new Rule(
                DiagnosticIds.MergeTagNotAtTopLevel,
                "Customizing tag not at the top level",
                Severity.Error,
                "Acumatica's docs: all tags that customize the original HTML must be on the highest level of the layout, in the top-level template tag. Inside a qp-include that brings in part of a form is the one exception.")
        };

        /// <summary>Looks up a rule by id.</summary>
        /// <returns>The rule, or null for an unknown id.</returns>
        public static Rule? Find(string id)
        {
            for (int i = 0; i < All.Count; i++)
            {
                if (string.Equals(All[i].Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return All[i];
                }
            }

            return null;
        }
    }
}
