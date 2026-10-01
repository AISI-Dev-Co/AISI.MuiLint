using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace AISI.MuiLint.Cli
{
    /// <summary>The four output formats. Each one writes every finding from every scanned file.</summary>
    internal static class Output
    {
        /// <summary>
        /// MSBuild-style lines, which Visual Studio, VS Code and most CI log viewers already
        /// know how to link: <c>path(line,col): error AISI0001: message</c>.
        /// </summary>
        public static void WriteText(TextWriter output, TextWriter summary, IReadOnlyList<ScannedFile> files)
        {
            int errors = 0;
            int warnings = 0;
            int suggestions = 0;
            foreach (ScannedFile file in files)
            {
                foreach (Diagnostic d in file.Diagnostics)
                {
                    output.WriteLine(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}({1},{2}): {3} {4}: {5}",
                        file.Path,
                        d.Line,
                        d.Column,
                        Word(d.Severity),
                        d.Id,
                        d.Message));

                    switch (d.Severity)
                    {
                        case Severity.Error:
                            errors++;
                            break;
                        case Severity.Warning:
                            warnings++;
                            break;
                        default:
                            suggestions++;
                            break;
                    }
                }
            }

            if (files.Count == 0)
            {
                return;
            }

            string scanned = Plural(files.Count, "file") + " scanned";
            summary.WriteLine(errors + warnings + suggestions == 0
                ? "muilint: " + scanned + ", all clean."
                : "muilint: " + scanned + ", " + Plural(errors, "error") + ", " + Plural(warnings, "warning") + ", " + Plural(suggestions, "suggestion") + ".");
        }

        public static void WriteJson(TextWriter output, IReadOnlyList<ScannedFile> files)
        {
            using var buffer = new MemoryStream();
            using var json = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true });
            json.WriteStartArray();
            foreach (ScannedFile file in files)
            {
                foreach (Diagnostic d in file.Diagnostics)
                {
                    json.WriteStartObject();
                    json.WriteString("path", file.Path);
                    json.WriteNumber("line", d.Line);
                    json.WriteNumber("column", d.Column);
                    json.WriteNumber("endLine", d.EndLine);
                    json.WriteNumber("endColumn", d.EndColumn);
                    json.WriteString("severity", Word(d.Severity));
                    json.WriteString("id", d.Id);
                    json.WriteString("message", d.Message);
                    json.WriteString("helpUri", Rules.Find(d.Id)?.HelpUri);
                    json.WriteEndObject();
                }
            }

            json.WriteEndArray();
            Flush(json, buffer, output);
        }

        /// <summary>SARIF 2.1.0, the format GitHub code scanning and most security dashboards import.</summary>
        public static void WriteSarif(TextWriter output, IReadOnlyList<ScannedFile> files, string version)
        {
            using var buffer = new MemoryStream();
            using var json = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true });
            json.WriteStartObject();
            json.WriteString("$schema", "https://json.schemastore.org/sarif-2.1.0.json");
            json.WriteString("version", "2.1.0");
            json.WriteStartArray("runs");
            json.WriteStartObject();

            json.WriteStartObject("tool");
            json.WriteStartObject("driver");
            json.WriteString("name", "muilint");
            json.WriteString("version", version);
            json.WriteString("informationUri", Rules.RepositoryUrl);
            json.WriteStartArray("rules");
            foreach (Rule rule in Rules.All)
            {
                json.WriteStartObject();
                json.WriteString("id", rule.Id);
                json.WriteString("name", rule.Title);
                json.WriteStartObject("shortDescription");
                json.WriteString("text", rule.Title);
                json.WriteEndObject();
                json.WriteStartObject("fullDescription");
                json.WriteString("text", rule.Description);
                json.WriteEndObject();
                json.WriteString("helpUri", rule.HelpUri);
                json.WriteStartObject("defaultConfiguration");
                json.WriteString("level", SarifLevel(rule.DefaultSeverity));
                json.WriteEndObject();
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteEndObject();
            json.WriteEndObject();

            json.WriteStartArray("results");
            foreach (ScannedFile file in files)
            {
                foreach (Diagnostic d in file.Diagnostics)
                {
                    json.WriteStartObject();
                    json.WriteString("ruleId", d.Id);
                    json.WriteNumber("ruleIndex", IndexOf(d.Id));
                    json.WriteString("level", SarifLevel(d.Severity));
                    json.WriteStartObject("message");
                    json.WriteString("text", d.Message);
                    json.WriteEndObject();
                    json.WriteStartArray("locations");
                    json.WriteStartObject();
                    json.WriteStartObject("physicalLocation");
                    json.WriteStartObject("artifactLocation");
                    json.WriteString("uri", ToUri(file.Path));
                    json.WriteEndObject();
                    json.WriteStartObject("region");
                    json.WriteNumber("startLine", d.Line);
                    if (d.Length > 0)
                    {
                        json.WriteNumber("startColumn", d.Column);
                        json.WriteNumber("endLine", d.EndLine);
                        json.WriteNumber("endColumn", d.EndColumn);
                    }

                    json.WriteEndObject();
                    json.WriteEndObject();
                    json.WriteEndObject();
                    json.WriteEndArray();
                    json.WriteEndObject();
                }
            }

            json.WriteEndArray();
            json.WriteEndObject();
            json.WriteEndArray();
            json.WriteEndObject();
            Flush(json, buffer, output);
        }

        /// <summary>
        /// GitHub Actions workflow commands (<c>::error file=…::message</c>). The runner turns them
        /// into annotations on the PR diff.
        /// </summary>
        public static void WriteGitHub(TextWriter output, IReadOnlyList<ScannedFile> files)
        {
            foreach (ScannedFile file in files)
            {
                foreach (Diagnostic d in file.Diagnostics)
                {
                    string command = d.Severity switch
                    {
                        Severity.Error => "error",
                        Severity.Warning => "warning",
                        _ => "notice",
                    };

                    output.WriteLine(string.Format(
                        CultureInfo.InvariantCulture,
                        "::{0} file={1},line={2},col={3},endLine={4},endColumn={5},title={6}::{7}",
                        command,
                        EscapeProperty(file.Path.Replace('\\', '/')),
                        d.Line,
                        d.Column,
                        d.EndLine,
                        d.EndColumn,
                        EscapeProperty(d.Id),
                        EscapeData(d.Message + " " + Rules.Find(d.Id)?.HelpUri)));
                }
            }
        }

        private static void Flush(Utf8JsonWriter json, MemoryStream buffer, TextWriter output)
        {
            json.Flush();
            output.WriteLine(Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length));
        }

        private static string Word(Severity severity)
        {
            return severity switch
            {
                Severity.Error => "error",
                Severity.Warning => "warning",
                _ => "info",
            };
        }

        private static string SarifLevel(Severity severity)
        {
            return severity switch
            {
                Severity.Error => "error",
                Severity.Warning => "warning",
                _ => "note",
            };
        }

        private static int IndexOf(string id)
        {
            for (int i = 0; i < Rules.All.Count; i++)
            {
                if (Rules.All[i].Id == id)
                {
                    return i;
                }
            }

            return -1;
        }

        private static string ToUri(string path)
        {
            if (Path.IsPathRooted(path))
            {
                return new Uri(Path.GetFullPath(path)).AbsoluteUri;
            }

            // Relative paths stay relative so code scanning can map them onto the repository.
            IEnumerable<string> segments = path.Replace('\\', '/').Split('/')
                .Where(s => s.Length > 0 && s != ".")
                .Select(Uri.EscapeDataString);
            return string.Join("/", segments);
        }

        private static string Plural(int count, string noun)
        {
            return count.ToString(CultureInfo.InvariantCulture) + " " + noun + (count == 1 ? string.Empty : "s");
        }

        private static string EscapeData(string value)
        {
            return value.Replace("%", "%25", StringComparison.Ordinal)
                .Replace("\r", "%0D", StringComparison.Ordinal)
                .Replace("\n", "%0A", StringComparison.Ordinal);
        }

        private static string EscapeProperty(string value)
        {
            return EscapeData(value)
                .Replace(":", "%3A", StringComparison.Ordinal)
                .Replace(",", "%2C", StringComparison.Ordinal);
        }
    }
}
