using System;

namespace AISI.MuiLint
{
    /// <summary>
    /// One HTML merge finding. Independent of Roslyn so the CLI and the VSIX stub can consume it
    /// without a compilation.
    /// </summary>
    public sealed class Diagnostic
    {
        /// <summary>
        /// Initializes a finding.
        /// </summary>
        /// <param name="id">Diagnostic identifier, one of <see cref="DiagnosticIds"/>.</param>
        /// <param name="message">Human-readable message.</param>
        /// <param name="path">File path the scanner was given.</param>
        /// <param name="start">0-based UTF-16 offset of the span.</param>
        /// <param name="length">UTF-16 length of the span.</param>
        /// <param name="line">1-based start line.</param>
        /// <param name="column">1-based start column.</param>
        /// <param name="endLine">1-based end line.</param>
        /// <param name="endColumn">1-based end column (exclusive).</param>
        /// <param name="severity">Effective severity after .editorconfig overrides.</param>
        public Diagnostic(
            string id,
            string message,
            string path,
            int start,
            int length,
            int line,
            int column,
            int endLine,
            int endColumn,
            Severity severity)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Path = path ?? throw new ArgumentNullException(nameof(path));
            Start = start;
            Length = length;
            Line = line;
            Column = column;
            EndLine = endLine;
            EndColumn = endColumn;
            Severity = severity;
        }

        /// <summary>Gets the diagnostic identifier.</summary>
        public string Id { get; }

        /// <summary>Gets the message.</summary>
        public string Message { get; }

        /// <summary>Gets the file path.</summary>
        public string Path { get; }

        /// <summary>Gets the 0-based UTF-16 start offset.</summary>
        public int Start { get; }

        /// <summary>Gets the UTF-16 span length.</summary>
        public int Length { get; }

        /// <summary>Gets the 1-based start line.</summary>
        public int Line { get; }

        /// <summary>Gets the 1-based start column.</summary>
        public int Column { get; }

        /// <summary>Gets the 1-based end line.</summary>
        public int EndLine { get; }

        /// <summary>Gets the 1-based exclusive end column.</summary>
        public int EndColumn { get; }

        /// <summary>Gets the severity.</summary>
        public Severity Severity { get; }

        internal Diagnostic WithSeverity(Severity severity)
        {
            return new Diagnostic(Id, Message, Path, Start, Length, Line, Column, EndLine, EndColumn, severity);
        }
    }
}
