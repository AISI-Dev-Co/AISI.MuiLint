namespace AISI.MuiLint
{
    /// <summary>How loudly a finding is reported. Only <see cref="Error"/> fails the CLI.</summary>
    public enum Severity
    {
        /// <summary>A merge that will break or silently drop markup.</summary>
        Error = 0,

        /// <summary>Probably wrong, but there are legitimate exceptions.</summary>
        Warning = 1,

        /// <summary>A nudge. Shown in the editor, never fails a build.</summary>
        Suggestion = 2,
    }
}
