# AISI MuiLint

Squiggles for the Acumatica Modern UI HTML and TypeScript mistakes you'd otherwise find when the site build fails, or when a field doesn't show.

Acuminator lints your C#. MuiLint lints the front end of your customisation: the HTML extension files under `development/screens` that get merged into the stock screen at build time, and the TypeScript behind them. Point a selector at nothing and the build fails, minutes later and on the server. MuiLint tells you while you're still typing. Every rule is checked against Acumatica's developer guide and run over Acumatica's own screens.

## What it catches

| Id | Catches |
| --- | --- |
| AISI0001 | Self-closing `<field/>` or `<qp-*/>` tags |
| AISI0003 | Customisations saved under the stock `src/screens` tree |
| AISI0004 | `SO301000/extensions/SO301000.html` (an extension not named `<ScreenID>_<postfix>`) |
| AISI0005 | An empty `qp-fieldset` that nothing adds to (a hint) |
| AISI0006 | Malformed selectors: unclosed `[`, `(` or quotes |
| AISI0008 | The same id, or the same field, twice in one container |
| AISI0009 | Selectors naming a `[name]` or `#id` the stock screen and its extensions don't have |
| AISI0011 | `view.bind`, fields or a button's `state.bind` that the screen's TypeScript doesn't declare |
| AISI0012 | `qp-*` controls without an `id` (a hint) |
| AISI0013 | A `config.bind` with an unclosed brace, bracket or quote |
| AISI0014 | Half a TypeScript extension: the interface without its class, or the other way round |
| AISI0015 | `primaryView` or `@handleEvent` naming a view the screen doesn't have |
| AISI0016 | A view created from a class that isn't a `PXView` |
| AISI0017 | An extension saved outside the screen's `extensions` folder |
| AISI0018 | A customising tag that isn't directly in the top-level `<template>` |

Each id in the Error List links to a page explaining the rule, with a before and after.

## And also

- **Extensions › AISI MuiLint › Lint Modern UI Screens** checks every file under your site's `development/screens`, not just the open ones, and lists the findings in the Error List.
- **Lightbulb fixes** (Ctrl+.): close a self-closing tag, remove an empty fieldset, declare a missing field in your TypeScript extension (writing the extension, and the `.ts`, if there isn't one), or suppress a finding on the line or in the file.
- **Completions** for Modern UI tags and attributes; the stock screen's `#ids` and `[name='…']` values in every merge selector, scoped to the container you've named; the views, fields and actions your TypeScript declares; and the fields of the C# DAC extensions in your solution.
- **Go to definition** (F12) and **hover** from a selector into the stock screen HTML, and from a view, action or field into its TypeScript declaration.
- **TypeScript snippets** for fields, actions, views, view and screen extensions, and their imports: type `muifield`, `muiext`, `muiscreenext` and so on, then Tab twice.
- **Severities from `.editorconfig`**, using the same `dotnet_diagnostic.AISI0008.severity = error` keys as Roslyn.
- A **command-line tool and a GitHub Action** that run the same rules in CI, with SARIF output for code scanning.

Free and MIT licensed. Source, issues and the CLI: [github.com/AISI-Dev-Co/AISI.MuiLint](https://github.com/AISI-Dev-Co/AISI.MuiLint).
