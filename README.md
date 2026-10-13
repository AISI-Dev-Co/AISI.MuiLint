# AISI.MuiLint

[![CI](https://github.com/AISI-Dev-Co/AISI.MuiLint/actions/workflows/ci.yml/badge.svg)](https://github.com/AISI-Dev-Co/AISI.MuiLint/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A linter for Acumatica Modern UI HTML and TypeScript, for Visual Studio, the command line and GitHub Actions.

Modern UI customisations are HTML and TypeScript extension files merged into the stock screen when the site builds.
A selector that matches nothing fails that build; a field the TypeScript doesn't declare doesn't show. MuiLint reports
these as you type and in CI. (Acuminator covers your C#; MuiLint covers the front end.)

Each rule's page quotes the part of Acumatica's developer guide it rests on, and the rules have been run over
Acumatica's own 24R1 screens to weed out false positives.

## Rules

| Id | Severity | Catches |
| --- | --- | --- |
| [AISI0001](docs/rules/AISI0001.md) | error | Self-closing `<field/>` or `<qp-*/>` tags. |
| [AISI0003](docs/rules/AISI0003.md) | error | A customisation saved under the stock `src/screens` tree instead of `src/development/screens`. |
| [AISI0004](docs/rules/AISI0004.md) | warning | An extension not named `<ScreenID>_<postfix>`, such as `SO301000/extensions/SO301000.html`. |
| [AISI0005](docs/rules/AISI0005.md) | suggestion | An empty `qp-fieldset` that nothing adds to. |
| [AISI0006](docs/rules/AISI0006.md) | error | A merge selector with an unclosed `[`, `(` or quote. |
| [AISI0008](docs/rules/AISI0008.md) | warning | The same id, or the same field, twice in one container. |
| [AISI0009](docs/rules/AISI0009.md) | warning | A selector naming a `[name]` or `#id` that the stock screen, its extensions and the lines above don't have. |
| [AISI0011](docs/rules/AISI0011.md) | warning | A `view.bind`, field, `state.bind` or `control-state.bind` the screen's TypeScript doesn't declare. |
| [AISI0012](docs/rules/AISI0012.md) | suggestion | A `qp-*` control without an `id`. |
| [AISI0013](docs/rules/AISI0013.md) | error | A `config.bind` with an unclosed `{`, `[`, `(` or quote. |
| [AISI0014](docs/rules/AISI0014.md) | warning | Half a TypeScript extension: the empty interface without its class, or the other way round. |
| [AISI0015](docs/rules/AISI0015.md) | warning | `primaryView` in `@graphInfo`, or `view` in `@handleEvent`, naming a view the screen doesn't have. |
| [AISI0016](docs/rules/AISI0016.md) | warning | `createSingle`/`createCollection` given a class that isn't a `PXView`. |
| [AISI0017](docs/rules/AISI0017.md) | warning | A `<ScreenID>_<postfix>` file outside the screen's `extensions` folder. |
| [AISI0018](docs/rules/AISI0018.md) | error | A customising tag (`after`, `modify`, …) that isn't directly in the top-level `<template>`. |
| [AISI0019](docs/rules/AISI0019.md) | warning | A `qp-include` missing a `.required` parameter, or passing one the included file doesn't declare. |
| [AISI0020](docs/rules/AISI0020.md) | warning | A `qp-template` `name` that isn't a predefined layout. |
| [AISI0021](docs/rules/AISI0021.md) | warning | A `config.bind` key the control's configuration doesn't have. |
| [AISI0022](docs/rules/AISI0022.md) | warning | A `control-type` naming a `qp-` control that doesn't exist. |
| [AISI0023](docs/rules/AISI0023.md) | error | A `qp-*`, `field`, `template` or `using` that's never closed, or a closing tag with nothing to close. |
| [AISI0024](docs/rules/AISI0024.md) | warning | A merge selector matching more than one element of the stock screen. |
| [AISI0025](docs/rules/AISI0025.md) | warning | `@graphInfo` without a `graphType`. |
| [AISI0026](docs/rules/AISI0026.md) | suggestion | `@gridConfig` without a `preset`. |
| [AISI0027](docs/rules/AISI0027.md) | warning | A `graphType` that no assembly in the site's `Bin` defines. |
| [AISI0028](docs/rules/AISI0028.md) | warning | A view or action on the screen class that the graph doesn't have. |
| [AISI0029](docs/rules/AISI0029.md) | warning | A `PXFieldState` that the view's DAC doesn't have. |
| [AISI0030](docs/rules/AISI0030.md) | warning | `@linkCommand` naming an action the graph doesn't have. |
| [AISI0031](docs/rules/AISI0031.md) | warning | `@featureInstalled` naming a feature the site's `FeaturesSet` doesn't have. |

AISI0002, AISI0007 and AISI0010 are retired; their pages say why.

AISI0014–AISI0016 and AISI0025–AISI0031 check `.ts` files, and AISI0017 checks both. TypeScript checks only run on
files under a `screens` or `customizationScreens` folder.

Some rules need more than the file itself and stay quiet without it:

- **AISI0009, AISI0011, AISI0019 and AISI0024** read neighbouring files: the stock screen, the screen's `.ts`, its other extensions and
  included files.
- **AISI0020–AISI0022** read the site's `node_modules/client-controls`, so they match your Acumatica version.
- **AISI0027–AISI0031** read the compiled assemblies in the site's `Bin` (beside `FrontendSources`) without loading
  them, plus source published to `App_RuntimeCode`. Members added in code at run time (`Actions.Add`, a field added in
  an event handler) can't be seen; suppress those.

`examples/` has a stand-in stock screen, a clean extension, and a broken `.html` and `.ts` that trip most rules:

```sh
dotnet run --project src/AISI.MuiLint.Cli -- examples/src/development/screens
```

## Visual Studio 2022 and 2026

Download `AISI.MuiLint-<version>.vsix` from the [latest release](https://github.com/AISI-Dev-Co/AISI.MuiLint/releases/latest),
then **Extensions → Manage Extensions → ⋮ → Install from VSIX…** and restart VS. It works in both the Web Tools
(`htmlx`) and classic (`html`) editors and doesn't need the web workload.

- **Squiggles and Error List entries** for every rule, in HTML and TypeScript. The id in the Error List links to the
  rule's page.
- **Extensions › AISI MuiLint › Lint Modern UI Screens** checks every `.html` and `.ts` under the site's
  `development/screens`, open or not. It finds the site from the open solution or folder: in it, above it (a solution
  in `App_Data/Projects`), or a few folders below it. The same menu has **Clear Results**, **Rule Reference** and
  **Report an Issue**.
- **Lightbulb fixes** (Ctrl+.) in HTML:
  - expand a self-closing tag (AISI0001) or remove an empty fieldset (AISI0005);
  - declare a field the HTML uses but the TypeScript doesn't (AISI0011), writing the view-class extension, its
    imports and the `.ts` file if they don't exist yet;
  - suppress a finding on its line or in the file.
- **Completions:**
  - Modern UI tags and attributes, plus the attributes and values the stock screen uses on that tag.
  - In merge selectors, the fields and ids added above the caret, then the stock screen's `#ids` and `[name='…']`
    values. After a container (`#fsColumnA-Order [name='`), only the fields inside it.
  - In `view.bind`, `state.bind`, a field's `name` and a `qp-panel` id, the views, actions and fields the screen's
    TypeScript declares. Fields come from the view you're in.
  - For a field's `name`, also the properties of every `PXCacheExtension` in your solution (or the site's
    `App_Data/Projects`) that the `.ts` doesn't declare yet. Pick one and the AISI0011 lightbulb declares it.
- **Go to definition** (F12) and **hover**: from a selector to the element in the stock screen HTML, and from
  `view.bind`, `state.bind` or a field's `name` to its TypeScript declaration.
- **TypeScript snippets**. Type the shortcut and press Tab twice, or use Insert Snippet (Ctrl+K, X):

  | Shortcut | Inserts |
  | --- | --- |
  | `muifield` | `Name: PXFieldState;` |
  | `muifieldcc` | `Name: PXFieldState<PXFieldOptions.CommitChanges>;` |
  | `muiaction` | `Name: PXActionState;` |
  | `muicollection` / `muisingle` | `View = createCollection(Class);` / `createSingle` |
  | `muiviewclass` | a `class X extends PXView` with its first field |
  | `muiext` | a view-class extension: the interface and class pair, with a field |
  | `muiscreenext` | the start of a screen extension `.ts`, importing the stock screen |
  | `muiimports` | the `client-controls` import for views, fields and actions |
  | `muiimportscreen` | an import of the screen class and a view class from the stock screen |

## Command line

Needs the .NET 8 SDK:

```sh
dotnet run --project src/AISI.MuiLint.Cli -c Release -- path/to/FrontendSources/screen/src/development/screens
```

Pass files or directories. Directories are searched for `*.html` and `*.ts`, skipping `node_modules` and dot-folders.
Point it at `development/screens`: given the whole `src` folder, it reports every stock screen under AISI0003.

```
examples/…/SO301000_Broken.html(4,3): error AISI0001: <field/> can't be self-closing: HTML only allows that on a few standard tags, so whatever follows ends up inside it. …
examples/…/SO301000_Broken.html(10,35): warning AISI0009: [name='OrderDat'] is not in the stock SO301000.html, any extension of it, or above in this file. The Modern UI build fails on a selector that matches nothing.
muilint: 4 files scanned, 4 errors, 6 warnings, 2 suggestions.
```

| Option | |
| --- | --- |
| `-f`, `--format text` | MSBuild-style lines, clickable in VS, VS Code and most CI logs. The default. |
| `-f json` | A JSON array, one object per finding. |
| `-f sarif` | SARIF 2.1.0, for GitHub code scanning. |
| `-f github` | GitHub Actions workflow commands, shown as annotations on the PR. |
| `--version`, `-h`, `--help` | Print the version or usage. |

Exit codes: **0** no errors, **1** at least one error, **2** bad usage or an unreadable input. Warnings and suggestions
never fail the run; raise one to `error` in `.editorconfig` if you want it to.

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

For GitHub code scanning, add `sarif-file` and upload it (private repositories need GitHub Advanced Security):

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

AISI0009 needs the stock screen in the checkout. Most customisation repositories don't commit `src/screens`, and then
it stays quiet.

## Configuration

### Severities

MuiLint reads the same `.editorconfig` keys as Roslyn, so one file drives the analyser, the CLI, the Action and the
VSIX:

```ini
[*.html]
dotnet_diagnostic.AISI0008.severity = error
dotnet_diagnostic.AISI0012.severity = warning

[**/legacy/**.html]
dotnet_diagnostic.AISI0009.severity = suggestion
```

Values are `error`, `warning`, `suggestion`, `silent`/`none` (off) and `default`. Nearer `.editorconfig` files win, and
`root = true` stops the search.

### Suppressing findings

```html
<!-- muilint-disable-next-line AISI0009 -->
<field name="UsrRush" after="[name='SomethingAnotherPackageAdds']"></field>
```

`<!-- muilint-disable AISI0009 -->` anywhere in a file turns the rule off for that file. List several ids to turn off
several; leave the id out to turn off everything. In `.ts` files use line comments:
`// muilint-disable-next-line AISI0014`, `// muilint-disable AISI0015`.

## How the stock screen and TypeScript are found

For `…/src/development/screens/SO/SO301000/extensions/SO301000_AISI.html` (or the same under
`src/customizationScreens/<Tenant>/screens/`), the stock screen is `…/src/screens/SO/SO301000/SO301000.html` plus the
`qp-include` files it references; if an include can't be read, AISI0009 skips that screen. Every other extension of
the screen, in `src/screens`, `src/development/screens` or a tenant's `customizationScreens`, counts too: what its HTML
adds can be targeted, and what its `.ts` declares can be bound to.

For bindings, MuiLint reads the `.ts` beside the HTML, follows its relative and `src/…` imports, and finds the
`PXScreen` class, its `createSingle`/`createCollection` views and each view class's members, base classes included.
Extension interfaces (`interface SOOrderHeader_AISI extends SOOrderHeader {}`) are merged into the class they extend,
as TypeScript does. If a view's class extends something it can't open, that view's fields aren't checked.

## Roslyn analyser

The core assembly is also a Roslyn analyser for additional files. A C# project that lists `.html` files as
`AdditionalFiles` gets the rules that need only the HTML text (AISI0001, 0003–0006, 0008, 0012, 0013, 0017, 0018 and
0023) as build diagnostics:

```xml
<ItemGroup>
  <AdditionalFiles Include="FrontendSources\screen\src\development\screens\**\*.html" />
</ItemGroup>
```

## Contributing

Bug reports, false positives and rule ideas are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md) for building, adding
rules and releasing, and [SECURITY.md](SECURITY.md) for security issues.

## Licence

MIT. Copyright (c) 2026 AISI Dev Co.
