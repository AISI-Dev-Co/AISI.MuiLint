# Changelog

All notable changes to this project are documented here. Versions follow [Semantic Versioning](https://semver.org/).

## [Unreleased]

Every rule was checked against Acumatica's developer guide (the Markdown edition Acumatica publishes in
[Acumatica-AI-Resources](https://github.com/Acumatica/Acumatica-AI-Resources)) and run over Acumatica's own 24R1
screens and extensions as if they were a customisation. Three rules turned out to be wrong and are retired; most of
the rest now say what the docs say. Each rule's page quotes its source.

### Added

- Checks against your site, the way AcuMate checks against a running one. They stay quiet without the site:
  - **AISI0027–AISI0031** (warnings) read the compiled assemblies in the site's `Bin` without loading them: a
    `graphType` no assembly defines, a view or action the graph doesn't have, a `PXFieldState` the view's DAC
    doesn't have, a `@linkCommand` to a missing action, and a `@featureInstalled` feature `FeaturesSet` lacks. Graph
    and cache extensions count, code in `App_RuntimeCode` counts, and anything that can't be read keeps them quiet.
  - **AISI0020–AISI0022** (warnings) read the site's `node_modules/client-controls`: an unknown `qp-template` name, a
    `config.bind` key the control's configuration doesn't have, and a `control-type` that isn't a control.
- **AISI0019** (warning): a `qp-include` missing a `.required` parameter, or passing one the included file doesn't
  declare.
- **AISI0023** (error): a `qp-*`, `field`, `template` or `using` that's never closed, or a closing tag with nothing to
  close. Acumatica's own 24R1 screens have five (FS300100, FS300200, FS305600, PM506000, SM200521).
- **AISI0024** (warning): a merge selector matching more than one element of the stock screen; the docs say the build
  fails.
- **AISI0025** (warning): `@graphInfo` without `graphType`. **AISI0026** (suggestion): `@gridConfig` without the
  `preset` the docs ask for.
- **AISI0011** now also checks `state.bind` and `control-state.bind` on controls other than buttons, as fields
  (`View.Field`, or a bare field of the view around it), and `View.Action` on buttons.
- **AISI0018** (error): a customising tag (`after`, `before`, `append`, `prepend`, `modify`, `remove`, `replace`)
  that isn't directly in the top-level `<template>`. The docs require it; inside a `qp-include` is the exception.
- **AISI0014** (warning): half a TypeScript extension, an empty `interface X extends Y {}` without its `class X`, or a
  class with Modern UI members and no interface.
- **AISI0015** (warning): `primaryView` in `@graphInfo`, or `view` in `@handleEvent`, naming a view the screen doesn't
  have, ignoring case as the backend does.
- **AISI0016** (warning): `createSingle`/`createCollection` given a class that isn't a `PXView`.
- **AISI0017** (warning): a `<ScreenID>_<postfix>` file under `development/screens` or `customizationScreens` that
  isn't in the screen's `extensions` folder.
- The CLI, the Action and the VSIX now check `.ts` files under `screens` and `customizationScreens` folders, with
  `// muilint-disable` comments.
- VSIX lightbulb: declare a field the HTML uses but the TypeScript doesn't (AISI0011), writing the extension class, its
  imports and, for an HTML-only extension, the `.ts` itself.
- VSIX completions:
  - in every merge selector, the fields and ids this file adds above the caret, then the stock screen's `#ids` and
    `[name='…']` values, with names scoped to the container the selector already names;
  - attributes the stock screen uses on a tag, and the values it gives them;
  - fields from the C# DAC extensions in your solution.
- VSIX hover on selectors and bindings.
- VSIX TypeScript snippets: `muifield`, `muifieldcc`, `muiaction`, `muicollection`, `muisingle`, `muiviewclass`,
  `muiext`, `muiscreenext`, `muiimports` and `muiimportscreen`.

### Changed

- Messages for AISI0006, AISI0008 and AISI0009 now say what Acumatica documents: the Modern UI build fails on a
  selector that matches nothing, or more than one element. They used to say the merge skips it quietly.
- AISI0003 recommends `src/development/screens` only. `customizationScreens/<Tenant>` is written by the site on
  publish and deleted on unpublish, so it's no place to edit by hand.
- AISI0004 is a warning: the `<ScreenID>_<postfix>` naming is documented, a failure isn't.
- AISI0005 is a suggestion, and leaves hidden fieldsets, `wg-container`s and fieldsets later elements append to alone.
- AISI0008 only reports an id or field repeated in the same container. Acumatica's screens repeat `btnOK` across
  dialogs and show a field twice in a view on purpose.
- AISI0009 only counts names the file adds above the selector, since elements apply in order.
- AISI0011:
  - fields inside `<using view="...">` or a `qp-panel` are checked against that view, as AcuMate does;
  - `state.bind` is only checked as an action on `qp-button`; elsewhere it names a field;
  - a button's action may be declared on the view it sits in;
  - `qp-panel` ids are no longer required to be views: the docs call `qp-panel` a generic placeholder;
  - an extension that is only HTML is checked against the screen and its other extensions.
- AISI0012 skips the controls Acumatica never gives ids (AcuMate's list, plus `qp-address-lookup`, `qp-hyper-icon`
  and `qp-caption`), and accepts an id given in `config.bind`.
- AISI0009, AISI0011 and the TypeScript rules see every extension of the screen: the stock screen's own `extensions`
  folder, `development/screens`, and every tenant under `customizationScreens`.

### Removed

- **AISI0002** (after/before anchored on a field the same file adds). Elements apply in order, and Acumatica's own
  training anchors on a field added two lines up.
- **AISI0007** (extension HTML with no `.ts`). Acumatica ships dozens of HTML-only extensions.
- **AISI0010** (added field without `Usr`). `Usr` is a database column convention for Acumatica's own tables, not a
  Modern UI one; the docs' own extension example adds `ShowCuryDetail`.
- The ids won't be reused.

## [0.3.0] - 2026-10-05

### Added

- **AISI0011** (warning): a `view.bind`, field name, `state.bind` or `qp-panel` id that the screen's TypeScript
  doesn't declare. Reads the `.ts` beside the HTML and its imports, with extension interfaces merged in. No site
  needed.
- **AISI0012** (suggestion): a `qp-*` control without an `id`.
- **AISI0013** (error): a malformed `config.bind`.
- VSIX: completions for `view.bind`, `state.bind`, field names and `qp-panel` ids, taken from the screen's TypeScript.
- VSIX: go to definition (F12), from a selector into the stock screen HTML and from a binding into the `.ts`.
- A manual dry run for the release workflow, and a check of the packed VSIX's contents in CI and in releases.

## [0.2.0] - 2026-09-30

First public release.

### Added

- **AISI0006** malformed merge selector: an unclosed `[`, `(` or quote.
- **AISI0007** extension HTML with no `.ts` of the same name.
- **AISI0008** duplicate id, or the same field twice in one view (warning).
- **AISI0009** selector target not found in the stock screen HTML, following `qp-include`s (warning).
- **AISI0010** field added by an extension without the `Usr` prefix (suggestion).
- Severities: every finding is an error, warning or suggestion, and `dotnet_diagnostic.<id>.severity` in
  `.editorconfig` overrides it in the CLI and the VSIX, as it already did for the Roslyn analyser.
- `<!-- muilint-disable-next-line ID -->` and `<!-- muilint-disable ID -->` suppression comments.
- CLI `--format json|sarif|github`, `--help` and `--version`.
- GitHub Action (`uses: AISI-Dev-Co/AISI.MuiLint@v0.2.0`) with PR annotations and optional SARIF.
- VSIX lightbulb fixes: add the closing tag (AISI0001), remove an empty fieldset (AISI0005), suppress on the line
  or in the file.
- VSIX Error List severities and help links to `docs/rules/<id>.md`.
- VSIX `[name]` completions now read the real stock screen under `src/screens`; more `qp-*` tags and attributes.
- `examples/` with a stand-in stock screen, a clean extension and a broken one.

### Changed

- AISI0009, AISI0011 and the TypeScript rules now see every extension of the screen: the stock screen's own
  `extensions` folder, `development/screens`, and every project under `customizationScreens`. A field or view another
  extension declares no longer gets reported as missing.

- CLI output is now MSBuild style (`path(line,col): error AISI0001: message`) followed by a summary on stderr.
- CLI exits 1 only for errors; warnings and suggestions are reported but don't fail the run.
- CLI exits 2 if any input is missing or unreadable, even when other files had findings, and no longer crashes on a
  locked file.
- CLI skips `node_modules` and dot-folders when scanning a directory.
- Release workflow stamps the tag's version into the VSIX manifest and assemblies.

## [0.1.0]

- AISI0001 to AISI0005, the VS 2022 VSIX (squiggles, Error List, completions), the CLI and the Roslyn analyser.
