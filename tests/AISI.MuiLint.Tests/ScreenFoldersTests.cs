using System;
using System.IO;
using System.Linq;
using AISI.MuiLint.Vsix;
using Xunit;

namespace AISI.MuiLint.Tests
{
    public sealed class ScreenFoldersTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "muilint-" + Guid.NewGuid().ToString("N"));

        [Fact]
        public void FindsTheSiteAboveASolutionInAppDataProjects()
        {
            string screens = Site("Site");
            string solution = Folder("Site", "App_Data", "Projects", "AISI");

            Assert.Equal(new[] { screens }, ScreenFolders.FindDevelopmentScreens(solution));
        }

        [Fact]
        public void FindsSitesInsideARepository()
        {
            string first = Site("repo", "Site2024");
            string second = Site("repo", "instances", "Site2025");
            Folder("repo", "node_modules", "pkg", "FrontendSources", "screen", "src", "development", "screens");

            Assert.Equal(new[] { first, second }, ScreenFolders.FindDevelopmentScreens(Path.Combine(_root, "repo")));
        }

        [Fact]
        public void FindsNothingWhereThereIsNoSite()
        {
            Assert.Empty(ScreenFolders.FindDevelopmentScreens(Folder("elsewhere", "src")));
        }

        [Fact]
        public void FilesAreTheHtmlAndTsOutsideNodeModulesAndDotFolders()
        {
            string screens = Site("Site");
            string extensions = Folder("Site", "FrontendSources", "screen", "src", "development", "screens", "SO", "SO301000", "extensions");
            foreach (string file in new[] { "SO301000_AISI.html", "SO301000_AISI.ts", "types.d.ts", "notes.md" })
            {
                File.WriteAllText(Path.Combine(extensions, file), string.Empty);
            }

            File.WriteAllText(Path.Combine(Folder("Site", "FrontendSources", "screen", "src", "development", "screens", "node_modules"), "x.html"), string.Empty);
            File.WriteAllText(Path.Combine(Folder("Site", "FrontendSources", "screen", "src", "development", "screens", ".cache"), "y.html"), string.Empty);

            Assert.Equal(
                new[] { "SO301000_AISI.html", "SO301000_AISI.ts" },
                ScreenFolders.Files(screens).Select(Path.GetFileName));
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        private string Site(params string[] parts)
        {
            return Folder(parts.Concat(new[] { "FrontendSources", "screen", "src", "development", "screens" }).ToArray());
        }

        private string Folder(params string[] parts)
        {
            string path = Path.Combine(new[] { _root }.Concat(parts).ToArray());
            Directory.CreateDirectory(path);
            return Path.GetFullPath(path);
        }
    }
}
