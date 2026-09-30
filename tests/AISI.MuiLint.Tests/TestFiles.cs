using System;
using System.IO;

namespace AISI.MuiLint.Tests
{
    internal static class TestFiles
    {
        public static string RepoRoot { get; } = FindRepoRoot();

        public static string? ReadDisk(string path)
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        public static string Apply(string text, TextEdit edit)
        {
            return text.Substring(0, edit.Start) + edit.NewText + text.Substring(edit.Start + edit.Length);
        }

        private static string FindRepoRoot()
        {
            string? dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "AISI.MuiLint.sln")))
            {
                dir = Path.GetDirectoryName(dir);
            }

            return dir ?? throw new InvalidOperationException("Cannot find the repository root.");
        }
    }
}
