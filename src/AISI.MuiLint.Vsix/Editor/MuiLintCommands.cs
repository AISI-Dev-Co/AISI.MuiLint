#nullable disable
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using AISI.MuiLint;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;

namespace AISI.MuiLint.Vsix
{
    /// <summary>Extensions › AISI MuiLint. The ids match MuiLintCommands.vsct.</summary>
    internal static class MuiLintCommands
    {
        public static readonly Guid CommandSet = new Guid("a0bc17b0-92d9-4c20-80e8-cd3444633f9c");

        private const int LintScreensId = 0x0100;
        private const int ClearResultsId = 0x0101;
        private const int RuleReferenceId = 0x0102;
        private const int ReportIssueId = 0x0103;

        public static async Task InitializeAsync(AsyncPackage package)
        {
            var commands = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            var componentModel = await package.GetServiceAsync(typeof(SComponentModel)) as IComponentModel;
            if (commands == null || componentModel == null)
            {
                return;
            }

            HtmlErrorTableDataSource errors = componentModel.GetService<HtmlErrorTableDataSource>();
            Add(commands, LintScreensId, () => package.JoinableTaskFactory.RunAsync(() => LintOrSayWhyNotAsync(package, errors)).FileAndForget("AISI.MuiLint/LintScreens"));
            Add(commands, ClearResultsId, () => errors.ReplaceScan(Array.Empty<Diagnostic>()));
            Add(commands, RuleReferenceId, () => VsShellUtilities.OpenSystemBrowser(Rules.RepositoryUrl + "#what-it-catches"));
            Add(commands, ReportIssueId, () => VsShellUtilities.OpenSystemBrowser(Rules.RepositoryUrl + "/issues/new/choose"));
        }

        private static void Add(OleMenuCommandService commands, int id, Action run)
        {
            commands.AddCommand(new MenuCommand((sender, e) => run(), new CommandID(CommandSet, id)));
        }

        private static async Task LintOrSayWhyNotAsync(AsyncPackage package, HtmlErrorTableDataSource errors)
        {
            try
            {
                await LintAsync(package, errors);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                await package.JoinableTaskFactory.SwitchToMainThreadAsync();
                Tell(package, "MuiLint couldn't read the screens: " + ex.Message);
            }
        }

        private static async Task LintAsync(AsyncPackage package, HtmlErrorTableDataSource errors)
        {
            await package.JoinableTaskFactory.SwitchToMainThreadAsync();
            var solution = await package.GetServiceAsync(typeof(SVsSolution)) as IVsSolution;
            var statusBar = await package.GetServiceAsync(typeof(SVsStatusbar)) as IVsStatusbar;
            string start = null;
            solution?.GetSolutionInfo(out start, out string _, out string _);
            if (string.IsNullOrEmpty(start))
            {
                Tell(package, "Open the solution or folder with your Acumatica site first; MuiLint looks for its FrontendSources from there.");
                return;
            }

            statusBar?.SetText("MuiLint: looking for development/screens…");
            await TaskScheduler.Default;
            IReadOnlyList<string> folders = ScreenFolders.FindDevelopmentScreens(start);
            var files = new List<string>();
            foreach (string folder in folders)
            {
                files.AddRange(ScreenFolders.Files(folder));
            }

            var found = new List<Diagnostic>();
            foreach (string file in files)
            {
                string text = MuiLintPackage.TryReadFile(file);
                if (text != null)
                {
                    found.AddRange(MuiLintPackage.Analyze(file, text));
                }
            }

            await package.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (folders.Count == 0)
            {
                statusBar?.SetText(string.Empty);
                Tell(package, "No FrontendSources/screen/src/development/screens found in, above or just below " + start + ".");
                return;
            }

            errors.ReplaceScan(found);
            statusBar?.SetText(Summary(files.Count, found));
            if (await package.GetServiceAsync(typeof(SVsUIShell)) is IVsUIShell shell)
            {
                Guid errorList = new Guid(ToolWindowGuids80.ErrorList);
                if (shell.FindToolWindow((uint)__VSFINDTOOLWIN.FTW_fForceCreate, ref errorList, out IVsWindowFrame frame) == VSConstants.S_OK)
                {
                    frame?.Show();
                }
            }
        }

        private static string Summary(int files, List<Diagnostic> found)
        {
            int errors = 0;
            int warnings = 0;
            foreach (Diagnostic diagnostic in found)
            {
                errors += diagnostic.Severity == Severity.Error ? 1 : 0;
                warnings += diagnostic.Severity == Severity.Warning ? 1 : 0;
            }

            return string.Format(
                CultureInfo.CurrentCulture,
                "MuiLint: {0} files checked, {1} errors, {2} warnings, {3} suggestions.",
                files,
                errors,
                warnings,
                found.Count - errors - warnings);
        }

        private static void Tell(AsyncPackage package, string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            VsShellUtilities.ShowMessageBox(
                package,
                message,
                "AISI MuiLint",
                OLEMSGICON.OLEMSGICON_INFO,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }
    }
}
