# Contributing

Each rule catches one Modern UI merge mistake, and should only fire when it really is a mistake.

## Reporting issues

- **False positive:** use the *False positive* template with the smallest snippet that triggers it. These are the
  most useful reports.
- **Missed mistake:** use *New rule idea*. Say what you wrote, what the merge did, and which Acumatica version.
- **Anything else:** use *Bug*.

Strip customer names and anything confidential from snippets. Stock field names are fine.

## Building

You need the .NET 8 SDK. Everything except packing the VSIX works on Linux and macOS.

```sh
dotnet test tests/AISI.MuiLint.Tests/AISI.MuiLint.Tests.csproj -c Release
dotnet run --project src/AISI.MuiLint.Cli -- examples/src/development/screens
```

```
src/AISI.MuiLint         netstandard2.0  scanner, rules, fixes, Roslyn analyser
src/AISI.MuiLint.Site    netstandard2.0  reads the site's Bin and App_RuntimeCode (AISI0027–AISI0031)
src/AISI.MuiLint.Cli     net8.0          the muilint command
src/AISI.MuiLint.Vsix    net472          VS extension: tagger, Error List, lightbulb, completions
tests/AISI.MuiLint.Tests net8.0          fixtures under Fixtures/{fail,pass}/<id>
examples/                               a stand-in stock screen plus a clean and a broken extension
action.yml                              the GitHub Action
marketplace/                            Visual Studio Marketplace listing
```

Packing the VSIX needs Windows and the Visual Studio SDK
(`dotnet build src/AISI.MuiLint.Vsix/AISI.MuiLint.Vsix.csproj -c Release`). On Linux the project compiles without the
editor code or a `.vsix`.

CI runs the tests, runs Roslynator 1.0.0 over `src/AISI.MuiLint`, `src/AISI.MuiLint.Cli` and `src/AISI.MuiLint.Site`,
and packs the VSIX on `windows-latest`. Warnings are errors throughout.

## Adding a rule

1. Add the id to `src/AISI.MuiLint/DiagnosticIds.cs` and a `Rule` to `Rules.All` in `Rules.cs`: title, default
   severity and a one-paragraph description. The Roslyn analyser, SARIF output and Error List pick it up from there.
2. Write the check. HTML rules go in `HtmlMergeScanner`: text-only ones in `HtmlMergeScanner.cs`, ones that read the
   stock screen or neighbouring files in `HtmlMergeScanner.Extensions.cs` (they only run when the host passes
   `readFile`). TypeScript rules go in `TypeScriptScanner`, and need adding to `TypeScriptRules` in
   `HtmlMergeScannerTests` so their fixtures are `.ts` files.
3. Add at least one fixture to `tests/AISI.MuiLint.Tests/Fixtures/fail/<id>/` and one to `pass/<id>/`. The fixture
   test fails until both exist.
4. Write `docs/rules/<id>.md`: why it breaks, a wrong and a right snippet, the source it rests on, and how to turn it
   off. A test checks the page exists.
5. If there's an obvious fix, add it to `MuiLintFixes` as a `TextEdit` with a test, and offer it in
   `src/AISI.MuiLint.Vsix/Editor/HtmlSuggestedActionsSource.cs`.
6. Add the rule to the tables in `README.md` and `marketplace/overview.md`, to `examples/…/SO301000_Broken.html`, and
   to `CHANGELOG.md`.

Severity: **error** when the merge definitely breaks, **warning** when it's almost always wrong but has legitimate
exceptions, **suggestion** when it's worth a second look.

## Style

Match the surrounding code: plain loops over LINQ in the scanner, `string.Format` with `CultureInfo.InvariantCulture`
for messages, and comments only where the reason isn't obvious. Messages say what's wrong and what the merge will do
about it.

## Pull requests

Keep each to one change, make sure `dotnet test` passes, and if it touches a rule, say which Acumatica version you
checked against.

## Releasing

1. Run **Release VSIX** by hand (Actions → Release VSIX → Run workflow) with the version you're about to tag. It
   stamps that version into the assembly, the VSIX manifest and the package registration, packs the VSIX, checks it
   with `.github/scripts/verify-vsix.ps1` and leaves it in the run's artifacts. Install it in VS and try it.
2. Update `CHANGELOG.md`, then publish a GitHub release tagged `v<version>`. The same workflow runs again and attaches
   `AISI.MuiLint-<version>.vsix` to the release. Tags must be plain `major.minor.patch`, because VSIX versions can't
   carry a pre-release suffix.
3. To publish to the Visual Studio Marketplace, run `VsixPublisher.exe publish -payload AISI.MuiLint-<version>.vsix
   -publishManifest marketplace/publishManifest.json -personalAccessToken <PAT>`. The publisher id in
   `publishManifest.json` has to match your Marketplace publisher.
