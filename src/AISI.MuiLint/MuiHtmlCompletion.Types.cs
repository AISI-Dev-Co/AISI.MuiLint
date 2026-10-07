using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AISI.MuiLint
{
    /// <summary>What the caret is asking for. Computed from raw text, no editor types.</summary>
    public enum MuiCompletionTarget
    {
        /// <summary>Caret is outside a tag, or in a place this slice does not complete.</summary>
        None = 0,

        /// <summary>Caret is in the tag name right after <c>&lt;</c>.</summary>
        TagName = 1,

        /// <summary>Caret is where an attribute name goes.</summary>
        AttributeName = 2,

        /// <summary>Caret is inside a merge selector: <c>after</c>, <c>before</c>, <c>append</c> and the rest.</summary>
        SelectorValue = 3,

        /// <summary>Caret is inside <c>view.bind=</c>, or a qp-panel's <c>id=</c>.</summary>
        ViewValue = 4,

        /// <summary>Caret is inside the <c>name=</c> of a <c>&lt;field&gt;</c>.</summary>
        FieldValue = 5,

        /// <summary>Caret is inside <c>state.bind=</c>.</summary>
        ActionValue = 6,

        /// <summary>Caret is inside any other attribute value, such as <c>slot=</c> or a template's <c>name=</c>.</summary>
        AttributeValue = 7,
    }

    /// <summary>Kind of a completion candidate.</summary>
    public enum MuiCompletionKind
    {
        /// <summary>Modern UI element name.</summary>
        Tag = 0,

        /// <summary>Attribute name (merge operator or common attribute).</summary>
        Attribute = 1,

        /// <summary>A <c>[name='X']</c> or <c>#id</c> selector value.</summary>
        SelectorValue = 2,

        /// <summary>A multi-character expansion such as the Usr field block.</summary>
        Snippet = 3,

        /// <summary>A view, field or action name from the screen's TypeScript.</summary>
        BindingValue = 4,

        /// <summary>A value the stock screen uses for the same attribute.</summary>
        AttributeValue = 5,
    }

    /// <summary>One completion candidate. Editor-free so it is testable on Linux.</summary>
    public sealed class MuiCompletionItem
    {
        /// <summary>Creates a candidate.</summary>
        public MuiCompletionItem(string displayText, string insertText, MuiCompletionKind kind, string description)
        {
            if (string.IsNullOrEmpty(displayText))
            {
                throw new ArgumentException("Display text is required.", nameof(displayText));
            }

            DisplayText = displayText;
            InsertText = string.IsNullOrEmpty(insertText) ? displayText : insertText;
            Kind = kind;
            Description = description ?? string.Empty;
        }

        /// <summary>Text shown in the completion list.</summary>
        public string DisplayText { get; }

        /// <summary>Text committed into the buffer.</summary>
        public string InsertText { get; }

        /// <summary>Candidate kind.</summary>
        public MuiCompletionKind Kind { get; }

        /// <summary>Tooltip text. Says where a selector value came from.</summary>
        public string Description { get; }
    }
}
