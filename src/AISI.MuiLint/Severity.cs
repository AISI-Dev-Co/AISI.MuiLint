namespace AISI.MuiLint
{
    /// <summary>How loudly a finding is reported; only <see cref="Error"/> fails the CLI.</summary>
    public enum Severity
    {
        /// <summary>Something Acumatica's docs rule out, or that breaks the build.</summary>
        Error = 0,

        /// <summary>Probably wrong, but there are legitimate exceptions.</summary>
        Warning = 1,

        /// <summary>A nudge that shows in the editor and never fails a build.</summary>
        Suggestion = 2,
    }
}
