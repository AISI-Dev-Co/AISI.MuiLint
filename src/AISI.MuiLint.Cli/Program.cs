using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace AISI.MuiLint.Cli
{
    /// <summary>
    /// Command-line entry point for AISI.MuiLint. Walks files or directories and prints
    /// findings for Modern UI HTML and TypeScript.
    /// </summary>
    public static class Program
    {
        private const string Usage =
@"Usage: muilint [options] <file-or-directory>...

Scans Acumatica Modern UI HTML and TypeScript. Directories are searched for *.html
and *.ts, skipping node_modules and dot-folders.

Options:
  -f, --format <text|json|sarif|github>   Output format (default: text).
                                          github prints workflow commands, so findings
                                          show up as annotations in GitHub Actions.
  -h, --help                              Show this help.
      --version                           Show the version.

Exit codes: 0 no errors, 1 at least one error, 2 bad usage or an unreadable input.
Warnings and suggestions are reported but never fail the run; raise one to an error
with dotnet_diagnostic.AISI0008.severity = error in .editorconfig.";

        private static readonly string[] Formats = { "text", "json", "sarif", "github" };

        /// <summary>
        /// Scans each argument as a file, or a directory of <c>.html</c> and <c>.ts</c> files.
        /// </summary>
        /// <param name="args">Options and paths to scan.</param>
        /// <returns>0 if no errors, 1 if any error, 2 on usage or input problems.</returns>
        public static int Main(string[] args)
        {
            string format = "text";
            var inputs = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg == "-h" || arg == "--help")
                {
                    Console.WriteLine(Usage);
                    return 0;
                }

                if (arg == "--version")
                {
                    Console.WriteLine(Version);
                    return 0;
                }

                if (arg == "-f" || arg == "--format")
                {
                    if (i + 1 >= args.Length)
                    {
                        return Fail(arg + " needs a value.");
                    }

                    format = args[++i];
                }
                else if (arg.StartsWith("--format=", StringComparison.Ordinal))
                {
                    format = arg.Substring("--format=".Length);
                }
                else if (arg.Length > 1 && arg[0] == '-')
                {
                    return Fail("Unknown option " + arg + ".");
                }
                else
                {
                    inputs.Add(arg);
                }
            }

            if (inputs.Count == 0)
            {
                return Fail(null);
            }

            if (!Formats.Contains(format))
            {
                return Fail("Unknown format '" + format + "'. Pick one of: " + string.Join(", ", Formats) + ".");
            }

            bool badInput = false;
            var cache = new Dictionary<string, string?>(StringComparer.Ordinal);
            var scanned = new List<ScannedFile>();
            foreach (string path in Expand(inputs, ref badInput))
            {
                string text;
                try
                {
                    text = File.ReadAllText(path);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    Console.Error.WriteLine("muilint: cannot read " + path + ": " + ex.Message);
                    badInput = true;
                    continue;
                }

                // Full path so .editorconfig lookup can walk above the current directory.
                string fullPath = Path.GetFullPath(path);
                scanned.Add(new ScannedFile(path, MuiLinter.Analyze(fullPath, text, p => ReadCached(p, cache), ListFolder)));
            }

            switch (format)
            {
                case "json":
                    Output.WriteJson(Console.Out, scanned);
                    break;
                case "sarif":
                    Output.WriteSarif(Console.Out, scanned, Version);
                    break;
                case "github":
                    Output.WriteGitHub(Console.Out, scanned);
                    break;
                default:
                    Output.WriteText(Console.Out, Console.Error, scanned);
                    break;
            }

            if (badInput)
            {
                return 2;
            }

            return scanned.Any(f => f.Diagnostics.Any(d => d.Severity == Severity.Error)) ? 1 : 0;
        }

        private static string Version
        {
            get
            {
                string? version = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                if (string.IsNullOrEmpty(version))
                {
                    return "0.0.0";
                }

                int plus = version.IndexOf('+', StringComparison.Ordinal);
                return plus < 0 ? version : version.Substring(0, plus);
            }
        }

        private static int Fail(string? message)
        {
            if (message != null)
            {
                Console.Error.WriteLine("muilint: " + message);
            }

            Console.Error.WriteLine(Usage);
            return 2;
        }

        private static List<string> Expand(List<string> inputs, ref bool badInput)
        {
            var files = new List<string>();
            foreach (string input in inputs)
            {
                if (Directory.Exists(input))
                {
                    AddFiles(input, files);
                }
                else if (File.Exists(input))
                {
                    files.Add(input);
                }
                else
                {
                    Console.Error.WriteLine("muilint: not found: " + input);
                    badInput = true;
                }
            }

            return files;
        }

        private static void AddFiles(string directory, List<string> files)
        {
            string[] found = Directory.GetFiles(directory).Where(MuiLinter.CanScan).ToArray();
            Array.Sort(found, StringComparer.Ordinal);
            files.AddRange(found);

            string[] subdirectories = Directory.GetDirectories(directory);
            Array.Sort(subdirectories, StringComparer.Ordinal);
            foreach (string subdirectory in subdirectories)
            {
                string name = Path.GetFileName(subdirectory);
                if (name != "node_modules" && !name.StartsWith('.'))
                {
                    AddFiles(subdirectory, files);
                }
            }
        }

        private static IEnumerable<string> ListFolder(string folder)
        {
            try
            {
                return Directory.Exists(folder) ? Directory.GetFileSystemEntries(folder) : Array.Empty<string>();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return Array.Empty<string>();
            }
        }

        private static string? ReadCached(string path, Dictionary<string, string?> cache)
        {
            if (cache.TryGetValue(path, out string? text))
            {
                return text;
            }

            try
            {
                text = File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                text = null;
            }

            cache[path] = text;
            return text;
        }
    }

    /// <summary>One scanned file: the path as the user gave it, and what was found.</summary>
    internal sealed record ScannedFile(string Path, IReadOnlyList<Diagnostic> Diagnostics);
}
