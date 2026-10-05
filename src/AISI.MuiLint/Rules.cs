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
                "Acumatica Modern UI merge does not treat self-closing <field> or <qp-*> tags as a full start/end pair. Use explicit end tags."),
            new Rule(
                DiagnosticIds.AfterBeforeSameFile,
                "after/before name selector defined in this file",
                Severity.Error,
                "HTML merge only sees stock HTML. An after/before [name='X'] selector cannot target a name introduced in the same extension file."),
            new Rule(
                DiagnosticIds.StockScreensPath,
                "Stock src/screens path",
                Severity.Error,
                "Custom and customized Modern UI source belongs in development/screens or customizationScreens, not the stock src/screens tree."),
            new Rule(
                DiagnosticIds.ExtensionBasename,
                "Extension named as the parent screen",
                Severity.Error,
                "An extensions/*.html file must not use the parent screen folder as its basename (SO301000/extensions/SO301000.html). Add a postfix."),
            new Rule(
                DiagnosticIds.EmptyFieldset,
                "Empty qp-fieldset",
                Severity.Error,
                "A qp-fieldset whose body is only whitespace or comments will merge as an empty fieldset. Merge-operation fieldsets (modify/remove/replace) are allowed to be empty."),
            new Rule(
                DiagnosticIds.MalformedSelector,
                "Malformed merge selector",
                Severity.Error,
                "A merge selector (after, before, append, prepend, modify, remove, replace) has an unclosed bracket, parenthesis or quote, so it cannot match anything."),
            new Rule(
                DiagnosticIds.ExtensionWithoutTypeScript,
                "Extension HTML without a TypeScript file",
                Severity.Error,
                "Modern UI loads an extension's HTML through the TypeScript file of the same name. Without SO301000_Custom.ts, SO301000_Custom.html is never merged."),
            new Rule(
                DiagnosticIds.DuplicateNameOrId,
                "Duplicate field name or id",
                Severity.Warning,
                "The same field appears twice in one view, or the same id is used twice in one file. Selectors will only ever find the first one."),
            new Rule(
                DiagnosticIds.SelectorNotInStock,
                "Selector target not in stock HTML",
                Severity.Warning,
                "A merge selector refers to a [name] or #id that the stock screen HTML does not contain, so the merge has nothing to attach to. Usually a typo."),
            new Rule(
                DiagnosticIds.FieldWithoutUsrPrefix,
                "Added field is not Usr-prefixed",
                Severity.Suggestion,
                "Custom DAC fields carry the Usr prefix. A field an extension adds without it is either a stock field being moved (fine) or a name that will not bind."),
            new Rule(
                DiagnosticIds.BindingNotInTypeScript,
                "Binding not declared in the screen's TypeScript",
                Severity.Warning,
                "view.bind, a field's name, state.bind or a qp-panel id refers to something the screen's .ts (with everything it imports and its extensions) does not declare. Modern UI only binds what the TypeScript declares."),
            new Rule(
                DiagnosticIds.QpControlWithoutId,
                "qp-* control without an id",
                Severity.Suggestion,
                "Modern UI controls should carry an id so customizations can target them with #id and tests can find them. qp-field, qp-label and qp-include are exempt, as are elements that modify or remove an existing one."),
            new Rule(
                DiagnosticIds.MalformedConfig,
                "Malformed config.bind",
                Severity.Error,
                "config.bind has an unclosed brace, bracket, parenthesis or quote, so the binding expression cannot be parsed."),
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
