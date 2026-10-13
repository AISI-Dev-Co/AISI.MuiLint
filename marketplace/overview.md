# AISI MuiLint

Reports mistakes in Acumatica Modern UI HTML and TypeScript as you type, instead of when the site build fails or a field doesn't show. For Visual Studio 2022 and 2026.

Modern UI customisations are HTML extension files under `development/screens`, merged into the stock screen at build time, plus the TypeScript behind them. A selector that matches nothing fails the build on the server; MuiLint flags it in the editor. Acuminator covers your C#; MuiLint covers the front end. Each rule rests on Acumatica's developer guide and has been run over Acumatica's own screens.

## Rules

| Id | Catches |
| --- | --- |
| AISI0001 | Self-closing `<field/>` or `<qp-*/>` tags |
| AISI0003 | Customisations saved under the stock `src/screens` tree |
| AISI0004 | `SO301000/extensions/SO301000.html` (an extension not named `<ScreenID>_<postfix>`) |
| AISI0005 | An empty `qp-fieldset` that nothing adds to (a hint) |
| AISI0006 | Malformed selectors: unclosed `[`, `(` or quotes |
| AISI0008 | The same id, or the same field, twice in one container |
| AISI0009 | Selectors naming a `[name]` or `#id` the stock screen and its extensions don't have |
| AISI0011 | `view.bind`, fields, `state.bind` or `control-state.bind` that the screen's TypeScript doesn't declare |
| AISI0012 | `qp-*` controls without an `id` (a hint) |
| AISI0013 | A `config.bind` with an unclosed brace, bracket or quote |
| AISI0014 | Half a TypeScript extension: the interface without its class, or the other way round |
| AISI0015 | `primaryView` or `@handleEvent` naming a view the screen doesn't have |
| AISI0016 | A view created from a class that isn't a `PXView` |
| AISI0017 | An extension saved outside the screen's `extensions` folder |
| AISI0018 | A customising tag that isn't directly in the top-level `<template>` |
| AISI0019 | A `qp-include` missing a required parameter, or passing an undeclared one |
| AISI0020 | A `qp-template` name that isn't a predefined layout (from your site's client-controls) |
| AISI0021 | A `config.bind` key the control doesn't have (from your site's client-controls) |
| AISI0022 | A `control-type` that isn't a control (from your site's client-controls) |
| AISI0023 | A Modern UI tag that's never closed, or a stray closing tag |
| AISI0024 | A selector matching more than one element of the stock screen |
| AISI0025 | `@graphInfo` without a `graphType` |
| AISI0026 | `@gridConfig` without a `preset` (a hint) |
| AISI0027 | A `graphType` your site's `Bin` doesn't define |
| AISI0028 | A view or action the graph doesn't have (from your site's `Bin`) |
| AISI0029 | A field the view's DAC doesn't have (from your site's `Bin`) |
| AISI0030 | `@linkCommand` naming an action the graph doesn't have |
| AISI0031 | `@featureInstalled` naming a feature your site doesn't have |

Each id in the Error List links to the rule's page, with a wrong and a right example.

## Features

- **Extensions › AISI MuiLint › Lint Modern UI Screens** checks every file under your site's `development/screens`, open or not, and lists the findings in the Error List.
- **Lightbulb fixes** (Ctrl+.): expand a self-closing tag, remove an empty fieldset, declare a missing field in your TypeScript extension (creating the extension and the `.ts` if needed), or suppress a finding on the line or in the file.
- **Completions** for Modern UI tags and attributes; the stock screen's `#ids` and `[name='…']` values in merge selectors, scoped to the container you've named; the views, fields and actions your TypeScript declares; and the fields of the C# DAC extensions in your solution.
- **Go to definition** (F12) and **hover** from a selector into the stock screen HTML, and from a view, action or field to its TypeScript declaration.
- **TypeScript snippets** for fields, actions, views, view and screen extensions, and imports: type `muifield`, `muiext`, `muiscreenext` and so on, then Tab twice.
- **Severities from `.editorconfig`**, using the same `dotnet_diagnostic.AISI0008.severity = error` keys as Roslyn.
- A **command-line tool and GitHub Action** that run the same rules in CI, with SARIF output for code scanning.

Free and MIT licensed. Source, issues and the CLI: [github.com/AISI-Dev-Co/AISI.MuiLint](https://github.com/AISI-Dev-Co/AISI.MuiLint).
