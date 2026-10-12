# AISI.MuiLint

[![CI](https://github.com/AISI-Dev-Co/AISI.MuiLint/actions/workflows/ci.yml/badge.svg)](https://github.com/AISI-Dev-Co/AISI.MuiLint/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A linter for Acumatica Modern UI HTML and TypeScript, for Visual Studio, the command line and GitHub Actions.

**Acuminator lints your C#. MuiLint lints the HTML merge.** Modern UI customisations are HTML and TypeScript
extension files that get merged into the stock screen when the site builds. Point a selector at nothing and the build
fails, minutes later and on the server; bind a field the TypeScript doesn't declare and it doesn't show. MuiLint
catches those mistakes while you're typing, and again in CI.

Every rule is checked against Acumatica's developer guide, and each rule's page quotes the part it rests on. The rules
were also run over Acumatica's own 24R1 screens and extensions, treating them as if they were yours, to weed out
false alarms.


## What it catches

| Id | Severity | Catches |
| --- | --- | --- |
| [AISI0001](docs/rules/AISI0001.md) | error | Self-closing `<field/>` or `<qp-*/>` tags, which Acumatica's docs rule out. |
| [AISI0003](docs/rules/AISI0003.md) | error | A customisation saved under the stock `src/screens` tree instead of `src/development/screens`. |
| [AISI0004](docs/rules/AISI0004.md) | warning | `SO301000/extensions/SO301000.html`: an extension not named `<ScreenID>_<postfix>`. |
| [AISI0005](docs/rules/AISI0005.md) | suggestion | An empty `qp-fieldset` that nothing adds to. |
| [AISI0006](docs/rules/AISI0006.md) | error | A merge selector that isn't valid CSS: an unclosed `[`, `(` or quote. |
| [AISI0008](docs/rules/AISI0008.md) | warning | The same id, or the same field, twice in one container, so a selector matches both. |
| [AISI0009](docs/rules/AISI0009.md) | warning | A selector naming a `[name]` or `#id` the stock screen, its extensions and the lines above don't have. |
| [AISI0011](docs/rules/AISI0011.md) | warning | A `view.bind`, field or button `state.bind` that the screen's TypeScript doesn't declare. |
| [AISI0012](docs/rules/AISI0012.md) | suggestion | A `qp-*` control without an `id`, so customisations can't target it. |
| [AISI0013](docs/rules/AISI0013.md) | error | A `config.bind` with an unclosed `{`, `[`, `(` or quote. |
| [AISI0014](docs/rules/AISI0014.md) | warning | Half a TypeScript extension: the empty interface without its class, or the other way round. |
| [AISI0015](docs/rules/AISI0015.md) | warning | `primaryView` in `@graphInfo`, or `view` in `@handleEvent`, naming a view the screen doesn't have. |
| [AISI0016](docs/rules/AISI0016.md) | warning | `createSingle`/`createCollection` given a class that isn't a `PXView`, usually an extension class. |
| [AISI0017](docs/rules/AISI0017.md) | warning | A `<ScreenID>_<postfix>` file saved outside the screen's `extensions` folder. |
| [AISI0018](docs/rules/AISI0018.md) | error | A customising tag (`after`, `modify`, …) that isn't directly in the top-level `<template>`. |

AISI0002, AISI0007 and AISI0010 are retired: Acumatica's own screens and docs showed they were wrong. Their pages say
why.

AISI0014–AISI0016 check `.ts` files, and AISI0017 checks both. The `.ts` rules, and the rules that look at
neighbouring files (AISI0009, AISI0011), run in the CLI, the Action and the VSIX, but not in the Roslyn analyser.

Want to see them all at once? `examples/` has a made-up stock screen, a clean extension, and a broken `.html` and
`.ts` that between them trip nearly every rule (the ones about where a file lives can't fire on a file that lives in
the right place):

```sh
dotnet run --project src/AISI.MuiLint.Cli -- examples/src/development/screens
```

## Visual Studio 2022

Download `AISI.MuiLint-<version>.vsix` from the [latest release](https://github.com/AISI-Dev-Co/AISI.MuiLint/releases/latest),
then **Extensions → Manage Extensions → ⋮ → Install from VSIX…** and restart VS.

You get:

- **Squiggles and Error List entries** for every rule, in the HTML and in the screen's TypeScript, coloured by
  severity. The id in the Error List links to the rule's page.
- **An Extensions › AISI MuiLint menu:**
  - **Lint Modern UI Screens** checks every `.html` and `.ts` under the site's `development/screens`, open or not,
    and lists what it finds in the Error List. It finds the site from the open solution or folder: in it, above it
    (a solution in `App_Data/Projects`), or a few folders below it. A file you open then reports live instead.
  - **Clear Results**, **Rule Reference** and **Report an Issue**.
- **Lightbulb fixes** (Ctrl+.) in the HTML:
  - add the missing closing tag (AISI0001), or remove an empty fieldset (AISI0005);
  - declare a field the HTML uses but the TypeScript doesn't (AISI0011). It opens the `.ts` and adds the field to your
    extension of the view's class, writing that extension and its imports if there isn't one yet, and creating the
    `.ts` if the extension is only HTML so far;
  - suppress any finding on its line or in the file.
- **Completions:**
  - Modern UI tags and attributes, plus any attribute the stock screen uses on that tag, and the values it gives it
    (`slot`, a template's layout name).
  - In any merge selector (`after`, `before`, `append`, `prepend`, `modify`, `remove`, `replace`), the fields and ids
    this file adds above the caret, then the stock screen's `#ids` and `[name='…']` values. Once you've named a container (`#fsColumnA-Order [name='`), you only get the
    fields inside it.
  - Inside `view.bind`, `state.bind`, a field's `name` and a `qp-panel` id, the views, actions and fields the
    screen's TypeScript declares; fields come from the view you're in.
  - For a field's `name`, also the fields of the C# DAC extensions in your solution that the `.ts` doesn't declare
    yet. Pick one, and the AISI0011 lightbulb declares it.
- **Go to definition** (F12): on a selector's `[name='…']` or `#id` it opens the stock screen HTML at that element, and
  on `view.bind`, `state.bind` or a field's `name` it opens the `.ts` at the declaration, extensions included.
- **Hover** over the same things to see what they are and where they're declared.
- **TypeScript snippets.** Type the shortcut and press Tab twice, or use Insert Snippet (Ctrl+K, X):

  | Shortcut | Inserts |
  | --- | --- |
  | `muifield` | `Name: PXFieldState;` |
  | `muifieldcc` | `Name: PXFieldState<PXFieldOptions.CommitChanges>;` |
  | `muiaction` | `Name: PXActionState;` |
  | `muicollection` / `muisingle` | `View = createCollection(Class);` / `createSingle` |
  | `muiviewclass` | a new `class X extends PXView` with its first field |
  | `muiext` | an extension of a view class: the interface and class pair, with a field |
  | `muiscreenext` | the start of a screen extension `.ts`, with the stock screen imported |
  | `muiimports` | the `client-controls` import for views, fields and actions |
  | `muiimportscreen` | an import of the screen class and a view class from the stock screen |

It hooks both the VS 2022 Web Tools editor (`htmlx`) and the classic HTML editor (`html`), and doesn't need the full
web workload.

## Command line

The CLI needs the .NET 8 SDK:

```sh
dotnet run --project src/AISI.MuiLint.Cli -c Release -- path/to/FrontendSources/screen/src/development/screens
```

Give it files or directories. Directories are searched for `*.html` and `*.ts`, skipping `node_modules` and
dot-folders. Point it at `development/screens`; if you point it at the whole `src` folder,
every stock screen's HTML is reported under AISI0003.

```
examples/…/SO301000_Broken.html(4,3): error AISI0001: <field/> can't be self-closing: HTML only allows that on a few standard tags, so whatever follows ends up inside it. …
examples/…/SO301000_Broken.html(10,35): warning AISI0009: [name='OrderDat'] is not in the stock SO301000.html, any extension of it, or above in this file. The Modern UI build fails on a selector that matches nothing.
examples/…/SO301000_Broken.html(23,5): error AISI0018: <field after=...> is inside <qp-fieldset>. Tags that customize the original HTML have to be directly in the top-level <template>.
muilint: 4 files scanned, 4 errors, 6 warnings, 2 suggestions.
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
      - uses: AISI-Dev-Co/AISI.MuiLint@v0.3.0
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
      - uses: AISI-Dev-Co/AISI.MuiLint@v0.3.0
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
# We like our containers tidy
dotnet_diagnostic.AISI0012.severity = warning

[**/legacy/**.html]
dotnet_diagnostic.AISI0009.severity = suggestion
```

Values are `error`, `warning`, `suggestion`, `silent`/`none` (off) and `default`. Nearer `.editorconfig` files win, and
`root = true` stops the search.

### Suppressing a single finding

```html
<!-- muilint-disable-next-line AISI0009 -->
<field name="UsrRush" after="[name='SomethingAnotherPackageAdds']"></field>
```

`<!-- muilint-disable AISI0009 -->` anywhere in a file switches a rule off for that file. Leave out the id to switch off
everything, and list several ids to switch off more than one. The lightbulb writes these comments for you.

In a `.ts` file the same comments are line comments: `// muilint-disable-next-line AISI0014` and
`// muilint-disable AISI0015`.

## How the stock screen is found

For an extension at `…/src/development/screens/SO/SO301000/extensions/SO301000_AISI.html` (or the same under
`src/customizationScreens/<Tenant>/screens/`, which the site writes when it publishes), the stock screen is `…/src/screens/SO/SO301000/SO301000.html`. MuiLint reads
the names and ids in it and follows its `qp-include url="…"` files. If any include can't be read, AISI0009 skips that
screen rather than guessing.

Every extension of the screen counts as well, wherever it lives: `…/src/screens/SO/SO301000/extensions/`,
`…/src/development/screens/SO/SO301000/extensions/`, and `extensions` under each tenant in
`…/src/customizationScreens/`, which holds what the site's published customization projects add. Whatever their HTML adds can be targeted, and whatever their `.ts` declares can be
bound to.

## How the TypeScript is read

AISI0011, the binding completions and F12 read the `.ts` next to the HTML and follow its imports, both relative ones
(`./views`) and `src/…` ones (`src/screens/SO/SO301000/SO301000`), plus every other extension `.ts` of the screen
(see above). They find the class that extends `PXScreen`, its
`createSingle`/`createCollection` views, and the members of each view's class through its base classes. Extension
interfaces such as `interface SOOrderHeader_AISI extends SOOrderHeader {}` are merged into the class they extend, the
way TypeScript does it. AISI0015 and AISI0016 use the same model when they check a `.ts` file.

TypeScript checks only run on files under a `screens` or `customizationScreens` folder, so the rest of your
TypeScript is left alone.

The C# field completions read the `.cs` files in whichever comes first walking up from the HTML: a folder that holds a
`.sln`, or a site's `App_Data/Projects`, where Acumatica keeps extension libraries. They offer every property of each
`PXCacheExtension`, `Usr` prefix or not.

None of this needs a site. In return, MuiLint only knows what's in your files. If a view's class extends something it
can't open, it doesn't check that view's fields rather than guess.

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
