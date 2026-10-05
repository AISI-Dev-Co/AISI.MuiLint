using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AISI.MuiLint.Tests
{
    public sealed class NavigationTests
    {
        private const string StockHtml =
            "<template>\n" +
            "  <qp-fieldset id=\"fsColumnA-Order\" view.bind=\"Document\">\n" +
            "    <field name=\"OrderDate\"></field>\n" +
            "  </qp-fieldset>\n" +
            "</template>\n";

        [Theory]
        [InlineData("[name='Order|Date']", 3, 5)]
        [InlineData("#fsColumn|A-Order [name='OrderDate']", 2, 3)]
        [InlineData("#fsColumnA-Order [name='|OrderDate']", 3, 5)]
        public void Selectors_GoToTheStockHtml(string selector, int line, int column)
        {
            SourceLocation location = Go("<template><field name=\"UsrPriority\" after=\"" + selector + "\"></field></template>").GetValueOrDefault();
            Assert.Equal("/site/src/screens/SO/SO301000/SO301000.html", location.Path);
            Assert.Equal((line, column), (location.Line, location.Column));
        }

        [Fact]
        public void Bindings_GoToTheTypeScript()
        {
            SourceLocation view = Go("<template><qp-grid id=\"g\" view.bind=\"Trans|actions\"></qp-grid></template>").GetValueOrDefault();
            Assert.Equal((BindingTests.StockTs, 9, 5), (view.Path, view.Line, view.Column));

            SourceLocation action = Go("<template><qp-button id=\"b\" state.bind=\"|AddInvBySite\"></qp-button></template>").GetValueOrDefault();
            Assert.Equal((BindingTests.StockTs, 6, 5), (action.Path, action.Line, action.Column));
        }

        [Fact]
        public void Fields_GoToTheClassThatDeclaresThem()
        {
            // UsrPriority lives in the extension, merged into SOOrderHeader.
            SourceLocation added = Go("<template><qp-fieldset id=\"f\" view.bind=\"Document\"><field name=\"Usr|Priority\"></field></qp-fieldset></template>").GetValueOrDefault();
            Assert.Equal((BindingTests.ExtensionTs, 4, 5), (added.Path, added.Line, added.Column));

            // No view around it: whichever view has the field.
            SourceLocation merged = Go("<template><field name=\"Order|Date\" after=\"[name='X']\"></field></template>").GetValueOrDefault();
            Assert.Equal((BindingTests.StockTs, 15, 5), (merged.Path, merged.Line, merged.Column));
        }

        [Fact]
        public void DottedName_GoesToTheViewOrTheField()
        {
            Assert.Equal(8, Go("<template><field name=\"Docu|ment.OrderDate\"></field></template>").GetValueOrDefault().Line);
            Assert.Equal(15, Go("<template><field name=\"Document.Order|Date\"></field></template>").GetValueOrDefault().Line);
        }

        [Theory]
        [InlineData("<template><qp-grid id=\"g|rid\"></qp-grid></template>")]
        [InlineData("<template><field name=\"Nope|\"></field></template>")]
        [InlineData("<template>|<field name=\"OrderDate\"></field></template>")]
        [InlineData("<template><field name=\"UsrA\" after=\"[name='Missing|']\"></field></template>")]
        public void NothingToGoTo_ReturnsNull(string html)
        {
            Assert.Null(Go(html));
        }

        [Fact]
        public void Completion_OffersViewsActionsAndTheEnclosingViewsFields()
        {
            Assert.Equal(new[] { "Document", "Transactions" }, Complete("<template><qp-grid id=\"g\" view.bind=\"|"));
            Assert.Equal(new[] { "AddInvBySite" }, Complete("<template><qp-button id=\"b\" state.bind=\"|"));
            Assert.Equal(new[] { "Document", "Transactions" }, Complete("<template><qp-panel id=\"|"));
            Assert.Equal(new[] { "InventoryID", "OrderQty" }, Complete("<template><qp-grid id=\"g\" view.bind=\"Transactions\">\n  <field name=\"|"));

            // Outside any view: every field on every view.
            Assert.Contains("UsrPriority", Complete("<template>\n  <field name=\"|"));
            Assert.Contains("OrderQty", Complete("<template>\n  <field name=\"|"));
        }

        [Theory]
        [InlineData("<qp-grid view.bind=\"|", MuiCompletionTarget.ViewValue)]
        [InlineData("<qp-panel id=\"|", MuiCompletionTarget.ViewValue)]
        [InlineData("<qp-button state.bind='|", MuiCompletionTarget.ActionValue)]
        [InlineData("<field name=\"|", MuiCompletionTarget.FieldValue)]
        [InlineData("<qp-template name=\"|", MuiCompletionTarget.None)]
        [InlineData("<qp-grid id=\"|", MuiCompletionTarget.None)]
        public void Classify_KnowsBindingValues(string html, MuiCompletionTarget expected)
        {
            int caret = html.IndexOf('|');
            Assert.Equal(expected, MuiHtmlCompletion.Classify(html.Remove(caret, 1), caret));
        }

        private static SourceLocation? Go(string htmlWithCaret)
        {
            int caret = htmlWithCaret.IndexOf('|');
            Dictionary<string, string> files = BindingTests.Files();
            files["/site/src/screens/SO/SO301000/SO301000.html"] = StockHtml;
            return MuiNavigation.FindDefinition(BindingTests.Extension, htmlWithCaret.Remove(caret, 1), caret, p => files.TryGetValue(p, out string? text) ? text : null);
        }

        private static string[] Complete(string htmlWithCaret)
        {
            int caret = htmlWithCaret.IndexOf('|');
            string html = htmlWithCaret.Remove(caret, 1);
            Dictionary<string, string> files = BindingTests.Files();
            MuiCompletionTarget target = MuiHtmlCompletion.Classify(html, caret);
            return MuiHtmlCompletion.GetBindingValues(BindingTests.Extension, html, caret, target, p => files.TryGetValue(p, out string? text) ? text : null)
                .Select(i => i.DisplayText)
                .ToArray();
        }
    }
}
