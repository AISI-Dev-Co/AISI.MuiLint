# Contributing

Thanks for having a look. MuiLint is small on purpose: each rule catches one Modern UI merge mistake that
costs somebody an afternoon, and nothing gets reported unless it really is a mistake.

## Reporting things

- **False positive?** That's a bug, and the most useful kind of report. Use the *False positive* template and paste
  the smallest HTML that triggers it.
- **Missed a mistake?** Use *New rule idea*. Say what you wrote, what the merge did, and which Acumatica version.
- **Anything else** goes under *Bug*.

Please strip customer names and anything confidential from snippets. Stock field names are fine.

## Building

You need the .NET 8 SDK. Everything except packing the VSIX works on Linux and macOS.

```sh
dotnet test tests/AISI.MuiLint.Tests/AISI.MuiLint.Tests.csproj -c Release
dotnet run --project src/AISI.MuiLint.Cli -- examples/src/development/screens
```

CI also runs Roslynator 1.0.0 over `src/AISI.MuiLint`, `src/AISI.MuiLint.Cli` and `src/AISI.MuiLint.Site`, and packs the VSIX on Windows.
Warnings are errors throughout.

## Adding a rule

1. Add the id to `src/AISI.MuiLint/DiagnosticIds.cs` and a `Rule` to `Rules.All` in `Rules.cs`: title, default
   severity and a one-paragraph description. The Roslyn analyser, SARIF output and Error List all pick it up from there.
2. Write the check. HTML rules go in `HtmlMergeScanner`: ones that only need the file's text in
   `HtmlMergeScanner.cs`, ones that read the stock screen or neighbouring files in `HtmlMergeScanner.Extensions.cs`
   (they only run when the host passes `readFile`). TypeScript rules go in `TypeScriptScanner`, and need adding to
   `TypeScriptRules` in `HtmlMergeScannerTests` so their fixtures are `.ts` files.
3. Add at least one fixture to `tests/AISI.MuiLint.Tests/Fixtures/fail/<id>/` and one to `pass/<id>/`. The fixture
   test fails until both exist.
4. Write `docs/rules/<id>.md`: why it breaks, a wrong and a right snippet, and how to turn it off. Tests check the page
   exists.
5. If there's an obvious fix, add it to `MuiLintFixes` as a `TextEdit` with a test, and offer it in
   `src/AISI.MuiLint.Vsix/Editor/HtmlSuggestedActionsSource.cs`.
6. Add the rule to the tables in `README.md`, `marketplace/overview.md` and `examples/…/SO301000_Broken.html`, and add
   a line to `CHANGELOG.md`.

Choosing a severity: **error** when the merge definitely breaks, **warning** when it's almost always wrong but
there are legitimate exceptions, **suggestion** when it's worth a second look.

## Style

Match the code around you: plain loops over LINQ in the scanner, `string.Format` with `CultureInfo.InvariantCulture`
for messages, and comments only where the why isn't obvious. Messages should say what's wrong and what the merge will
do about it, in plain words.

## Releasing

1. Run **Release VSIX** by hand (Actions → Release VSIX → Run workflow) with the version you're about to tag. It
   stamps that version, packs the VSIX, checks it with `.github/scripts/verify-vsix.ps1` and leaves it in the run's
   artifacts. Install it in VS and poke at it before going further.
2. Bump `CHANGELOG.md`, then publish a GitHub release tagged `v<version>`. The same workflow runs again and attaches
   `AISI.MuiLint-<version>.vsix` to the release.

## Pull requests

Keep them to one change, make sure `dotnet test` passes, and say which Acumatica version you checked the behaviour
against if the PR touches a rule.
