using System;

namespace AISI.MuiLint
{
    /// <summary>One finding, independent of Roslyn so the CLI and the VSIX can consume it without a compilation.</summary>
    public sealed class Diagnostic
    {
        /// <summary>Initializes a finding.</summary>
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
