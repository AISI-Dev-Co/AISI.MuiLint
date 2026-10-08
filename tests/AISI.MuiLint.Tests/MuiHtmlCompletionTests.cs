using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AISI.MuiLint.Tests
{
    public sealed class MuiHtmlCompletionTests
    {
        [Fact]
        public void Classify_TagName_AfterOpenAngle()
        {
            const string text = "<fi";
            Assert.Equal(MuiCompletionTarget.TagName, MuiHtmlCompletion.Classify(text, text.Length));
        }

        [Fact]
        public void Classify_AttributeName_InsideTag()
        {
            const string text = "<field ";
            Assert.Equal(MuiCompletionTarget.AttributeName, MuiHtmlCompletion.Classify(text, text.Length));
        }

        [Fact]
        public void Classify_SelectorValue_InsideAfterOpenQuote()
        {
            const string text = "<field after=\"";
            Assert.Equal(MuiCompletionTarget.SelectorValue, MuiHtmlCompletion.Classify(text, text.Length));
        }

        [Fact]
        public void Classify_SelectorValue_InsideBeforeOpenQuote()
        {
            const string text = "<field before='";
            Assert.Equal(MuiCompletionTarget.SelectorValue, MuiHtmlCompletion.Classify(text, text.Length));
        }

        [Fact]
        public void GetTags_IncludesFieldAndQpStar()
        {
            IReadOnlyList<MuiCompletionItem> tags = MuiHtmlCompletion.GetTags();
            Assert.Contains(tags, t => t.DisplayText == "field" && t.Kind == MuiCompletionKind.Tag);
            Assert.Contains(tags, t => t.DisplayText.StartsWith("qp-", StringComparison.Ordinal));
            Assert.Contains(tags, t => t.DisplayText == "qp-fieldset");
            Assert.Contains(tags, t => t.DisplayText == "qp-grid");
        }

        [Fact]
        public void GetAttributes_IncludesAfterBeforeName()
        {
            IReadOnlyList<MuiCompletionItem> attrs = MuiHtmlCompletion.GetAttributes("field");
            Assert.Contains(attrs, a => a.DisplayText == "after" && a.Kind == MuiCompletionKind.Attribute);
            Assert.Contains(attrs, a => a.DisplayText == "before");
            Assert.Contains(attrs, a => a.DisplayText == "name");
        }

        private const string StockHtml =
            "<template>\n" +
            "  <qp-template id=\"form-Order\" name=\"17-17-14\" wg-container=\"Document_form\">\n" +
            "    <qp-fieldset id=\"fsColumnA-Order\" slot=\"A\" view.bind=\"Document\">\n" +
            "      <field name=\"OrderType\"></field>\n" +
            "      <field name=\"OrderNbr\"></field>\n" +
            "    </qp-fieldset>\n" +
            "    <qp-fieldset id=\"fsColumnB-Order\" slot=\"B\" view.bind=\"Document\">\n" +
            "      <field name=\"CustomerID\"></field>\n" +
            "    </qp-fieldset>\n" +
            "  </qp-template>\n" +
            "</template>\n";

        [Theory]
        [InlineData("after")]
        [InlineData("before")]
        [InlineData("append")]
        [InlineData("prepend")]
        [InlineData("modify")]
        [InlineData("remove")]
        [InlineData("replace")]
        public void Classify_EveryMergeOperatorIsASelector(string attribute)
        {
            string text = "<qp-fieldset " + attribute + "=\"#fs";
            Assert.Equal(MuiCompletionTarget.SelectorValue, MuiHtmlCompletion.Classify(text, text.Length));
        }

        [Fact]
        public void Selector_OffersStockNamesAndIdsButNotLayouts()
        {
            string[] values = Values("<template><field name=\"UsrA\" after=\"|");
            Assert.Contains("[name='OrderType']", values);
            Assert.Contains("[name='CustomerID']", values);
            Assert.Contains("#fsColumnA-Order", values);
            Assert.DoesNotContain("[name='17-17-14']", values);
        }

        [Fact]
        public void Selector_OffersWhatThisFileAddsAbove()
        {
            // Acumatica's own T-series example chains UsrRepairItemType after UsrRepairItem.
            string[] values = Values("<template><field after=\"#fsColumnA-Order [name='OrderType']\" name=\"UsrRepairItem\"></field>"
                + "<qp-fieldset id=\"fsAISI\" after=\"#fsColumnA-Order\"></qp-fieldset>"
                + "<field name=\"UsrRepairItemType\" after=\"#fsColumnA-Order |");
            Assert.Equal("[name='UsrRepairItem']", values[0]);
            Assert.Contains("#fsAISI", values);
            Assert.DoesNotContain("[name='UsrRepairItemType']", values);
        }

        [Fact]
        public void Selector_ScopesNamesToTheContainerItAlreadyNames()
        {
            const string html = "<template><field name=\"UsrA\" after=\"#fsColumnB-Order [name='Cu|";
            Assert.Equal(new[] { "[name='CustomerID']" }, Values(html).Where(v => v.StartsWith("[", StringComparison.Ordinal)));

            int caret = html.IndexOf('|');
            string text = html.Remove(caret, 1);
            Assert.Equal(text.IndexOf("[name='Cu", StringComparison.Ordinal), MuiHtmlCompletion.ApplicableStart(text, caret, MuiCompletionTarget.SelectorValue));
        }

        [Fact]
        public void AttributeValuesAndNames_AreLearntFromTheStockScreen()
        {
            Assert.Equal(new[] { "A", "B" }, Values("<template><qp-fieldset id=\"x\" slot=\"|"));
            Assert.Equal(new[] { "17-17-14" }, Values("<template><qp-template id=\"x\" name=\"|"));

            IReadOnlyList<MuiCompletionItem> extra = MuiHtmlCompletion.GetStockAttributes("qp-template", BindingTests.Extension, Reader());
            Assert.Contains(extra, a => a.DisplayText == "wg-container");
            Assert.DoesNotContain(extra, a => a.DisplayText == "id");
        }

        [Theory]
        [InlineData("<qp-grid view.bind=\"Doc|", "Doc")]
        [InlineData("<field after=\"#fs [name='Ord|", "[name='Ord")]
        [InlineData("<qp-gr|", "qp-gr")]
        [InlineData("<field na|", "na")]
        public void ApplicableStart_CoversWhatIsBeingTyped(string html, string typed)
        {
            int caret = html.IndexOf('|');
            string text = html.Remove(caret, 1);
            MuiCompletionTarget target = MuiHtmlCompletion.Classify(text, caret);
            Assert.Equal(typed, text.Substring(MuiHtmlCompletion.ApplicableStart(text, caret, target), caret - MuiHtmlCompletion.ApplicableStart(text, caret, target)));
        }

        private static string[] Values(string htmlWithCaret)
        {
            int caret = htmlWithCaret.IndexOf('|');
            string text = htmlWithCaret.Remove(caret, 1);
            MuiCompletionTarget target = MuiHtmlCompletion.Classify(text, caret);
            return MuiHtmlCompletion.GetValues(BindingTests.Extension, text, caret, target, Reader()).Select(i => i.DisplayText).ToArray();
        }

        private static Func<string, string?> Reader()
        {
            Dictionary<string, string> files = BindingTests.Files();
            files["/site/src/screens/SO/SO301000/SO301000.html"] = StockHtml;
            return p => files.TryGetValue(p, out string? text) ? text : null;
        }

        [Fact]
        public void UsrFieldSnippet_InsertText_ContainsFieldAfterName()
        {
            MuiCompletionItem snippet = MuiHtmlCompletion.UsrFieldSnippet();
            Assert.Equal("field-usr", snippet.DisplayText);
            Assert.Equal(MuiCompletionKind.Snippet, snippet.Kind);
            Assert.Equal(MuiHtmlCompletion.UsrFieldSnippetText, snippet.InsertText);
            Assert.Contains("<field", snippet.InsertText, StringComparison.Ordinal);
            Assert.Contains("after=", snippet.InsertText, StringComparison.Ordinal);
            Assert.Contains("name=", snippet.InsertText, StringComparison.Ordinal);
            Assert.Contains("Usr", snippet.InsertText, StringComparison.Ordinal);
        }
    }
}
