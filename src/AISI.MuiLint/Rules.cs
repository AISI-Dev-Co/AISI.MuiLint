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
                "Acumatica's docs: all tags that customize the original HTML must be on the highest level of the layout, in the top-level template tag. Inside a qp-include that brings in part of a form is the one exception."),
            new Rule(
                DiagnosticIds.IncludeParameters,
                "qp-include parameters",
                Severity.Warning,
                "The included file declares its parameters in qp-include-parameters. A required one (name.required) must be given; an attribute it doesn't declare goes nowhere."),
            new Rule(
                DiagnosticIds.UnknownTemplate,
                "Unknown qp-template name",
                Severity.Warning,
                "qp-template renders the predefined template its name picks. A name the site's client-controls package doesn't define renders nothing."),
            new Rule(
                DiagnosticIds.RecordTemplateOutsideDataFeed,
                "record-* template outside a data feed",
                Severity.Warning,
                "The record-* templates lay out the records of a qp-data-feed. Outside one, they have nothing to lay out."),
            new Rule(
                DiagnosticIds.UnknownConfigKey,
                "Unknown config.bind key",
                Severity.Warning,
                "config.bind sets properties of the control's config interface, as the site's client-controls package declares it. A key it doesn't declare is ignored, usually a typo."),
            new Rule(
                DiagnosticIds.UnknownControlType,
                "Unknown control-type",
                Severity.Warning,
                "control-type picks the qp-* control a field is shown with. A value the site's client-controls package doesn't define falls back to the default control."),
            new Rule(
                DiagnosticIds.UnbalancedTag,
                "Unclosed or stray Modern UI tag",
                Severity.Error,
                "A qp-*, field, template or using element without its end tag swallows everything after it; an end tag with no start tag closes the wrong element."),
            new Rule(
                DiagnosticIds.SelectorMatchesSeveral,
                "Selector matches more than one element",
                Severity.Warning,
                "Acumatica's docs: if more than one item satisfies the CSS selector, the build process fails. Qualify the selector with the container's #id."),
            new Rule(
                DiagnosticIds.GraphInfoWithoutGraphType,
                "@graphInfo without graphType",
                Severity.Warning,
                "Acumatica's docs: the screen class has the graphInfo decorator, in which you specify the graph. Without graphType the screen has no graph to talk to."),
            new Rule(
                DiagnosticIds.GridWithoutPreset,
                "@gridConfig without a preset",
                Severity.Suggestion,
                "Acumatica's docs: for each table, you must specify a preset in the preset property of the gridConfig decorator. Acumatica's own older screens often don't, so this is a hint."),
            new Rule(
                DiagnosticIds.GraphNotInSite,
                "graphType not in the site",
                Severity.Warning,
                "@graphInfo names a graph that none of the assemblies in the site's Bin folder defines. Usually a typo, or an assembly that hasn't been built into Bin."),
            new Rule(
                DiagnosticIds.MemberNotInGraph,
                "View or action not on the graph",
                Severity.Warning,
                "The screen's views and actions bind to the graph's views and actions by name. One the graph (with its extensions, as compiled in the site's Bin) doesn't have binds to nothing."),
            new Rule(
                DiagnosticIds.FieldNotInView,
                "Field not on the view's DAC",
                Severity.Warning,
                "A PXView class's fields bind to the fields of the DAC behind the view. One the DAC (with its cache extensions, as compiled in the site's Bin) doesn't have binds to nothing."),
            new Rule(
                DiagnosticIds.LinkCommandUnknownAction,
                "@linkCommand to an unknown action",
                Severity.Warning,
                "@linkCommand makes a field a link that runs a graph action. One the graph (as compiled in the site's Bin) doesn't have does nothing."),
            new Rule(
                DiagnosticIds.FeatureNotInSite,
                "@featureInstalled with an unknown feature",
                Severity.Warning,
                "@featureInstalled names a FeaturesSet field (PX.Objects.CS.FeaturesSet+Name). One the site's FeaturesSet doesn't have is never switched on.")
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
