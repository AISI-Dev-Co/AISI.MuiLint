# AISI.MuiLint

[![CI](https://github.com/AISI-Dev-Co/AISI.MuiLint/actions/workflows/ci.yml/badge.svg)](https://github.com/AISI-Dev-Co/AISI.MuiLint/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A linter for Acumatica Modern UI HTML, for Visual Studio, the command line and GitHub Actions.

**Acuminator lints your C#. MuiLint lints the HTML merge.** Modern UI customisations are HTML extension files that
get merged into the stock screen at build time, and when the merge can't place something it doesn't complain. The
field just isn't there. MuiLint catches those mistakes while you're typing, and again in CI.

It reads raw HTML text. It doesn't convert Classic UI, emit TypeScript, or analyse `view.bind`. It isn't a second
AcuMate, and it contains no GPL code.

## What it catches

| Id | Severity | Catches |
| --- | --- | --- |
| [AISI0001](docs/rules/AISI0001.md) | error | Self-closing `<field/>` or `<qp-*/>` tags. The merge doesn't treat them as a start/end pair. |
| [AISI0002](docs/rules/AISI0002.md) | error | `after`/`before` `[name='X']` where `X` is added by the same file. The merge only sees stock HTML. |
| [AISI0003](docs/rules/AISI0003.md) | error | A customisation saved under the stock `src/screens` tree instead of `development/screens` or `customizationScreens`. |
| [AISI0004](docs/rules/AISI0004.md) | error | `SO301000/extensions/SO301000.html`: an extension named like the screen it extends. |
| [AISI0005](docs/rules/AISI0005.md) | error | An empty `qp-fieldset` (fieldsets that `modify`/`remove`/`replace` are exempt). |
| [AISI0006](docs/rules/AISI0006.md) | error | A merge selector with an unclosed `[`, `(` or quote. |
| [AISI0007](docs/rules/AISI0007.md) | error | Extension HTML with no `.ts` of the same name beside it, so it is never loaded. |
| [AISI0008](docs/rules/AISI0008.md) | warning | The same id twice in a file, or the same field twice in the same view. |
| [AISI0009](docs/rules/AISI0009.md) | warning | A selector naming a `[name]` or `#id` the stock screen doesn't have. Usually a typo. |
| [AISI0010](docs/rules/AISI0010.md) | suggestion | A field the extension adds without the `Usr` prefix. |

AISI0007 and AISI0009 look at neighbouring files, so they run in the CLI, the Action and the VSIX, but not in the
Roslyn analyser.

Want to see them all at once? `examples/` has a made-up stock screen, a clean extension and one that trips every
rule:

```sh
dotnet run --project src/AISI.MuiLint.Cli -- examples/src/development/screens
```

## Visual Studio 2022

Download `AISI.MuiLint-<version>.vsix` from the [latest release](https://github.com/AISI-Dev-Co/AISI.MuiLint/releases/latest),
then **Extensions → Manage Extensions → ⋮ → Install from VSIX…** and restart VS.

In the HTML editor you get:

- **Squiggles and Error List entries** for every rule, coloured by severity. The id in the Error List links to the rule's page.
- **Lightbulb fixes** (Ctrl+.): add the missing closing tag (AISI0001), remove an empty fieldset (AISI0005), or
  suppress any finding on its line or in the file.
- **Completions** for Modern UI tags and merge attributes, and `[name='…']` values inside `after`/`before`. The values
  come from the stock screen first, then `PXFieldState` fields in the sibling `.ts`, so you anchor on something that exists.

It hooks both the VS 2022 Web Tools editor (`htmlx`) and the classic HTML editor (`html`), and doesn't need the full
web workload.

## Command line

The CLI needs the .NET 8 SDK:

```sh
dotnet run --project src/AISI.MuiLint.Cli -c Release -- path/to/FrontendSources/screen/src/development/screens
```

Give it files or directories. Directories are searched for `*.html`, skipping `node_modules` and dot-folders. Point it
at `development/screens` or `customizationScreens`; if you point it at the whole `src` folder, every stock screen is
reported under AISI0003.

```
examples/…/SO301000_Broken.html(4,3): error AISI0001: Self-closing <field> is not valid for Acumatica Modern UI merge. Use <field ...></field>.
examples/…/SO301000_Broken.html(13,35): warning AISI0009: [name='OrderDat'] is not in the stock SO301000.html, so the merge has nothing to attach to. …
muilint: 2 files scanned, 5 errors, 2 warnings, 1 suggestion.
```

| Option | |
| --- | --- |
| `-f`, `--format text` | MSBuild-style lines, clickable in VS, VS Code and most CI logs. The default. |
| `-f json` | A JSON array, one object per finding. Easy to feed into a script or an n8n flow. |
| `-f sarif` | SARIF 2.1.0 for GitHub code scanning and other dashboards. |
| `-f github` | GitHub Actions workflow commands, which turn into annotations on the PR. |

Exit codes: **0** no errors, **1** at least one error, **2** bad usage or an input that couldn't be read. Warnings and
suggestions never fail the run on their own; if you want one to, raise it to `error` in `.editorconfig`.

## GitHub Action

```yaml
name: MuiLint
on: [push, pull_request]

jobs:
  muilint:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: AISI-Dev-Co/AISI.MuiLint@v0.2.0
        with:
          path: FrontendSources/screen/src/development/screens
```

Findings appear as annotations on the pull request, and the job fails on errors. `path` takes several paths separated
by spaces or new lines.

To send results to GitHub code scanning as well, add `sarif-file: muilint.sarif` and upload it. Code scanning on a
private repository needs GitHub Advanced Security.

```yaml
    permissions:
      contents: read
      security-events: write
    steps:
      - uses: actions/checkout@v4
      - uses: AISI-Dev-Co/AISI.MuiLint@v0.2.0
        with:
          path: FrontendSources/screen/src/development/screens
          sarif-file: muilint.sarif
      - uses: github/codeql-action/upload-sarif@v3
        if: always()
        with:
          sarif_file: muilint.sarif
```

AISI0009 only runs when the stock screen is in the checkout. Most customisation repositories don't commit the stock
`src/screens` tree, and in that case the rule simply stays quiet.

## Configuration

### Severities

MuiLint reads the same `.editorconfig` keys as Roslyn, so one file drives the analyser, the CLI, the Action and the
VSIX:

```ini
[*.html]
# Fail CI on duplicates
dotnet_diagnostic.AISI0008.severity = error
# We move stock fields about a lot
dotnet_diagnostic.AISI0010.severity = none

[**/legacy/**.html]
dotnet_diagnostic.AISI0009.severity = suggestion
```

Values are `error`, `warning`, `suggestion`, `silent`/`none` (off) and `default`. Nearer `.editorconfig` files win, and
`root = true` stops the search.

### Suppressing a single finding

```html
<!-- muilint-disable-next-line AISI0002 -->
<field name="UsrRush" after="[name='UsrPriority']"></field>
```

`<!-- muilint-disable AISI0009 -->` anywhere in a file switches a rule off for that file. Leave out the id to switch off
everything, and list several ids to switch off more than one. The lightbulb writes these comments for you.

## How the stock screen is found

For an extension at `…/src/development/screens/SO/SO301000/extensions/SO301000_AISI.html` (or the same under
`src/customizationScreens/<Project>/`), the stock screen is `…/src/screens/SO/SO301000/SO301000.html`. MuiLint reads
the names and ids in it and follows its `qp-include url="…"` files. If any include can't be read, AISI0009 skips that
screen rather than guessing.

## Roslyn analyser

The core assembly is also a Roslyn additional-file analyser. A C# project that lists `.html` files as
`AdditionalFiles` gets the text-only rules as ordinary build diagnostics:

```xml
<ItemGroup>
  <AdditionalFiles Include="FrontendSources\screen\src\development\screens\**\*.html" />
</ItemGroup>
```

## Building

```sh
dotnet test tests/AISI.MuiLint.Tests/AISI.MuiLint.Tests.csproj -c Release
```

```
src/AISI.MuiLint         netstandard2.0  scanner, rules, fixes, Roslyn analyser
src/AISI.MuiLint.Cli     net8.0          the muilint command
src/AISI.MuiLint.Vsix    net472          VS 2022 extension: tagger, Error List, lightbulb, completions
tests/AISI.MuiLint.Tests net8.0          fixtures under Fixtures/{fail,pass}/<id>
examples/                               a stand-in stock screen plus a clean and a broken extension
action.yml                              the GitHub Action
marketplace/                            Visual Studio Marketplace listing
```

Packing the VSIX needs Windows and the Visual Studio SDK
(`dotnet build src/AISI.MuiLint.Vsix/AISI.MuiLint.Vsix.csproj -c Release`). On Linux the project still compiles, but
without the editor code or a `.vsix`. CI packs it on `windows-latest`.

### Releasing

Publish a GitHub Release with a tag like `v0.3.0`. The **Release VSIX** workflow stamps that version into the
assembly, the VSIX manifest and the package registration, packs on Windows, and attaches `AISI.MuiLint-0.3.0.vsix` to
the release. Tags must be plain `major.minor.patch`, because VSIX versions can't carry a pre-release suffix.

To publish to the Visual Studio Marketplace, run `VsixPublisher.exe publish -payload AISI.MuiLint-0.3.0.vsix
-publishManifest marketplace/publishManifest.json -personalAccessToken <PAT>`. The publisher id in `publishManifest.json`
has to match your Marketplace publisher.

## Contributing

Bug reports, false positives and new-rule ideas are all welcome; see [CONTRIBUTING.md](CONTRIBUTING.md). For
security issues, see [SECURITY.md](SECURITY.md).

## Licence

MIT. Copyright (c) 2026 AISI Dev Co.
