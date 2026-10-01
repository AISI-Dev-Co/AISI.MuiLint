using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using AISI.MuiLint.Cli;
using Xunit;

namespace AISI.MuiLint.Tests
{
    public sealed class CliTests
    {
        private static readonly string Extensions = Path.Combine(
            TestFiles.RepoRoot, "examples", "src", "development", "screens", "SO", "SO301000", "extensions");

        [Fact]
        public void CleanExtension_ExitsZero()
        {
            (int exit, string output, _) = Run(Path.Combine(Extensions, "SO301000_AISI.html"));
            Assert.Equal(0, exit);
            Assert.Equal(string.Empty, output);
        }

        [Fact]
        public void BrokenExtension_ExitsOneAndReportsInMsBuildFormat()
        {
            (int exit, string output, string summary) = Run(Path.Combine(Extensions, "SO301000_Broken.html"));
            Assert.Equal(1, exit);
            Assert.Contains("SO301000_Broken.html(4,3): error AISI0001: ", output, StringComparison.Ordinal);
            Assert.Contains("warning AISI0009", output, StringComparison.Ordinal);
            Assert.Contains("info AISI0010", output, StringComparison.Ordinal);
            Assert.Contains("1 file scanned, 5 errors, 2 warnings, 1 suggestion", summary, StringComparison.Ordinal);
        }

        [Fact]
        public void BrokenExample_TripsEveryRule()
        {
            (_, string output, _) = Run("--format", "json", Extensions);
            using JsonDocument json = JsonDocument.Parse(output);
            string[] ids = json.RootElement.EnumerateArray().Select(e => e.GetProperty("id").GetString()!).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();
            Assert.Equal(Rules.All.Select(r => r.Id).Where(id => id != DiagnosticIds.StockScreensPath && id != DiagnosticIds.ExtensionBasename), ids);
        }

        [Fact]
        public void Sarif_IsWellFormed()
        {
            (int exit, string output, _) = Run("-f", "sarif", Extensions);
            (_, string json, _) = Run("-f", "json", Extensions);
            Assert.Equal(1, exit);

            using JsonDocument sarif = JsonDocument.Parse(output);
            using JsonDocument findings = JsonDocument.Parse(json);
            Assert.Equal("2.1.0", sarif.RootElement.GetProperty("version").GetString());
            JsonElement run = sarif.RootElement.GetProperty("runs")[0];
            JsonElement rules = run.GetProperty("tool").GetProperty("driver").GetProperty("rules");
            Assert.Equal(Rules.All.Count, rules.GetArrayLength());

            JsonElement[] results = run.GetProperty("results").EnumerateArray().ToArray();
            Assert.Equal(findings.RootElement.GetArrayLength(), results.Length);
            foreach (JsonElement result in results)
            {
                int index = result.GetProperty("ruleIndex").GetInt32();
                Assert.Equal(rules[index].GetProperty("id").GetString(), result.GetProperty("ruleId").GetString());
                string uri = result.GetProperty("locations")[0].GetProperty("physicalLocation").GetProperty("artifactLocation").GetProperty("uri").GetString()!;
                Assert.StartsWith("file:///", uri, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void GitHubFormat_WritesWorkflowCommands()
        {
            (_, string output, _) = Run("--format=github", Path.Combine(Extensions, "SO301000_Broken.html"));
            Assert.Contains(",line=4,col=3,", output, StringComparison.Ordinal);
            Assert.Contains("::error file=", output, StringComparison.Ordinal);
            Assert.Contains("::warning file=", output, StringComparison.Ordinal);
            Assert.Contains("::notice file=", output, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("does-not-exist.html")]
        [InlineData("--format", "xml", "x.html")]
        [InlineData("--nope")]
        [InlineData("--format")]
        public void BadInput_ExitsTwo(params string[] args)
        {
            (int exit, _, _) = Run(args);
            Assert.Equal(2, exit);
        }

        [Fact]
        public void NoArguments_ExitsTwo()
        {
            (int exit, _, _) = Run();
            Assert.Equal(2, exit);
        }

        [Fact]
        public void Directories_SkipNodeModules()
        {
            string root = Path.Combine(Path.GetTempPath(), "muilint-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "node_modules", "pkg"));
            File.WriteAllText(Path.Combine(root, "node_modules", "pkg", "index.html"), "<field name=\"X\"/>");
            try
            {
                (int exit, _, string summary) = Run(root);
                Assert.Equal(0, exit);
                Assert.Equal(string.Empty, summary);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        private static (int Exit, string Output, string Summary) Run(params string[] args)
        {
            TextWriter originalOut = Console.Out;
            TextWriter originalError = Console.Error;
            var output = new StringWriter();
            var error = new StringWriter();
            Console.SetOut(output);
            Console.SetError(error);
            try
            {
                int exit = Program.Main(args);
                return (exit, output.ToString(), error.ToString());
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
            }
        }
    }
}
