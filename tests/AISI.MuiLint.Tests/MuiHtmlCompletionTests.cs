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

        [Fact]
        public void GetSelectorValues_PrefersBaseHtml_MarksSameFile_ReadsPxFieldState()
        {
            const string baseHtml = "<field name=\"InventoryID\"></field><field name=\"Descr\"></field>";
            const string currentHtml = "<field name=\"UsrLocal\" after=\"[name='InventoryID']\"></field>";
            const string siblingTs = "export class SO301000 { InventoryID: PXFieldState; SiteID: PXFieldState; }";

            IReadOnlyList<MuiCompletionItem> items = MuiHtmlCompletion.GetSelectorValues(
                currentHtml,
                baseHtml,
                siblingTs);

            Assert.Equal("[name='InventoryID']", items[0].DisplayText);
            Assert.Contains("base HTML", items[0].Description, StringComparison.Ordinal);
            Assert.Contains(items, i => i.DisplayText == "[name='Descr']" && i.Description.IndexOf("base HTML", StringComparison.Ordinal) >= 0);
            Assert.Contains(items, i => i.DisplayText == "[name='SiteID']" && i.Description.IndexOf("PXFieldState", StringComparison.Ordinal) >= 0);

            MuiCompletionItem sameFile = Assert.Single(items, i => i.DisplayText == "[name='UsrLocal']");
            Assert.Contains("same file", sameFile.Description, StringComparison.Ordinal);
            Assert.Contains("AISI0002", sameFile.Description, StringComparison.Ordinal);

            // Base wins over same-file duplicate; InventoryID must not be re-listed as same-file.
            Assert.Equal(1, items.Count(i => i.DisplayText == "[name='InventoryID']"));
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
