Modulus bundles Univer as one classic script (IIFE) because Univer 1.x ships ESM only.

Bundled packages (Apache-2.0, (c) Univer — https://univer.ai), all 1.0.3:
  @univerjs/presets                                createUniver, defaultTheme, darkBlueTheme, LocaleType, mergeLocales
  @univerjs/preset-sheets-core                     UniverSheetsCorePreset (sheets UI, formula bar, formulas)
  @univerjs/preset-sheets-filter, -sort            UniverSheetsFilterPreset, UniverSheetsSortPreset
  @univerjs/preset-sheets-conditional-formatting   UniverSheetsConditionalFormattingPreset
  @univerjs/preset-sheets-data-validation          UniverSheetsDataValidationPreset
  @univerjs/preset-sheets-find-replace             UniverSheetsFindReplacePreset
  @univerjs/preset-sheets-hyper-link, -note, -table
                                                   UniverSheetsHyperLinkPreset, UniverSheetsNotePreset, UniverSheetsTablePreset
  @univerjs/preset-docs-core                       UniverDocsCorePreset (the document editor)
  @univerjs/preset-docs-drawing, -hyper-link       UniverDocsDrawingPreset (images), UniverDocsHyperLinkPreset
  @univerjs/core, engine-formula, engine-render, drawing, network, protocol, rpc, telemetry, themes, ...
                                                   (transitive dependencies of the presets, same version set)
  decimal.js and other transitive helpers          (MIT; their notices are embedded in univer.js)

Only the open-source (Apache-2.0) presets are used; nothing from Univer Pro. The formulas run on the main thread
(no workerURL), so one file is enough.

Built with esbuild; see docs/UPGRADING.md ("Upgrading MapLibre, Tiptap, Univer or jsVectorMap").

  npm install esbuild @univerjs/presets@1.0.3 @univerjs/preset-sheets-core@1.0.3 @univerjs/preset-sheets-filter@1.0.3 \
    @univerjs/preset-sheets-sort@1.0.3 @univerjs/preset-sheets-conditional-formatting@1.0.3 \
    @univerjs/preset-sheets-data-validation@1.0.3 @univerjs/preset-sheets-find-replace@1.0.3 \
    @univerjs/preset-sheets-hyper-link@1.0.3 @univerjs/preset-sheets-note@1.0.3 @univerjs/preset-sheets-table@1.0.3 \
    @univerjs/preset-docs-core@1.0.3 @univerjs/preset-docs-drawing@1.0.3 @univerjs/preset-docs-hyper-link@1.0.3

  univer-entry.mjs: imports every preset above and its en-US locale, then
    window.ModulusUniver = { createUniver, defaultTheme, darkBlueTheme, LocaleType, <the presets>,
                             enUS: mergeLocales(<every locale>) };
  css-entry.css: @import "@univerjs/<preset>/lib/index.css"; for every preset above

  esbuild univer-entry.mjs --bundle --format=iife --minify --charset=utf8 --legal-comments=inline --outfile=univer.js
  esbuild css-entry.css --bundle --minify --outfile=univer.css

The Apache-2.0 licence text is in LICENSE (next to this file) and each bundled package's notice is embedded in
univer.js (legal-comments).

The bundle exposes window.ModulusUniver; modulus-plugins.js starts it for [data-m-sheet] (spreadsheet) and [data-m-doc]
(document) roots. The file is about 12.0 MB (3.0 MB gzipped) and loads only on pages that show one of them.
