# Dependencies

Every third-party piece Modulus ships or calls, with its version, its licence and why it is there. All of them are
open source except QuestPDF, which is free under its Community licence only (see its row); a commercial licence is needed
for a public-sector body, a publicly traded company, or a business at USD 1,000,000 or more in annual gross revenue. Versions are pinned in one place each (see
[docs/DOCUMENTATION.md#upgrading-ant-design-blazor-htmx-alpinejs-and-the-plugin-libraries](docs/DOCUMENTATION.md#upgrading-ant-design-blazor-htmx-alpinejs-and-the-plugin-libraries) for how to upgrade them).

## NuGet packages

Pinned in `Directory.Packages.props`.

| Package | Version | Licence | Used by | Why |
|---|---|---|---|---|
| `AntDesign` | 1.6.2 | MIT | `Modulus.Blazor`, `Modulus.Ui.Assets` | The Blazor components (`App*` aliases) and the Ant Design v4 CSS both flavours render against. |
| `AntDesign.ProLayout` | 1.6.2 | MIT | `Modulus.Blazor`, `Modulus.Ui.Assets` | `BasicLayout`, `PageContainer`, `GlobalHeader`; kept on the same version as `AntDesign`. |
| `Microsoft.AspNetCore.Components.Web`, `.Authorization` | 10.0.12 | MIT | `Modulus.Blazor`, `Modulus.Ui.Assets` | Razor components and the cascading authentication state. |
| `ClosedXML` | 0.105.1 | MIT | `Modulus.Documents` | Reads and writes xlsx (the spreadsheet's import and download). |
| `DocumentFormat.OpenXml` | 3.1.1 | MIT | `Modulus.Documents` | Writes docx (the document editor's Word download); ClosedXML is built on it. |
| `QuestPDF` | 2026.9.0 | Community licence (proprietary, free under USD 1,000,000 annual gross revenue; not for public-sector or publicly traded companies) | `Modulus.Documents` | Writes PDF from the document model (`DocumentPdf`). The host sets `QuestPDF.Settings.License`. Published 2026-09-14. |
| `Microsoft.AspNetCore.App` (framework reference) | 10.0 | MIT | `Modulus.AspNetCore.Hosting` | Response compression, the health checks and the middleware of the hosting defaults. No package is added. |
| `playwright` (Python) | the version CI installs | Apache-2.0 | `tests/browser` (CI and local gates only) | Drives Chromium for the smoke and parity gates. It is not part of any shipped package. |
| `Fluid.Core` | 2.40.0 | MIT | `Modulus.AspNetCore.Fluid`, `Modulus.Samples.Fluid` | Liquid templates: the parser and the renderer (escaped output, tags and filters). The layout, the Modulus tags and the file cache are Modulus code. Published 2026-08-20. |
| `OpenTelemetry.Extensions.Hosting` | 1.19.1 | Apache-2.0 | `Modulus.AspNetCore.Hosting` | Registers the OpenTelemetry SDK with the host: traces, metrics and logs (`AddModulusObservability`). Published 2026-09-21. |
| `OpenTelemetry.Instrumentation.AspNetCore` | 1.19.0 | Apache-2.0 | `Modulus.AspNetCore.Hosting` | Request spans and request metrics for ASP.NET Core. |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | 1.19.1 | Apache-2.0 | `Modulus.AspNetCore.Hosting` | Sends the signals to an OTLP collector, only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. |

`Modulus.Ui.Abstractions` and `Modulus.AspNetCore.Mvc` take no third-party packages; the pivot's xlsx (`PivotXlsx`) is
written without a library.

Test-only (never shipped): `xunit.v3` 4.0.1 (Apache-2.0), `bunit` 2.11.3 (MIT), `ZXing.Net` 0.16.11 (Apache-2.0,
decodes the QR codes Modulus draws), `Microsoft.EntityFrameworkCore.SqlServer` 10.0.12 (MIT, proves the pivot's
grouped query translates to SQL without a database).

## Scripts, styles and fonts in `Modulus.Ui.Assets`

Vendored under `src/Modulus.Ui.Assets/wwwroot/`; each folder carries its licence. Nothing is loaded from a CDN.

| Library | Version | Licence | Files | Loaded | Why |
|---|---|---|---|---|---|
| htmx | 2.0.11 | 0BSD | `lib/htmx/htmx.min.js` | MVC and Razor Pages, every page | Partial updates, boosted links and forms, SPA navigation. |
| Alpine.js | 3.17.4 | MIT | `lib/alpinejs/alpine.min.js` | MVC and Razor Pages, every page | Small client state of the tag helpers (`modulus-widgets.js`). |
| MapLibre GL JS | 5.24.0 | BSD-3-Clause | `lib/maplibre/` | On first use, pages with `[data-m-map]` | Tile maps (`AppMap` / `app-map`). |
| jsVectorMap | not recorded (no version banner) | MIT | `lib/jsvectormap/` (with `world.js`) | On first use, pages with a vector map | Choropleth maps (`AppVectorMap` / `app-vector-map`). |
| Tiptap | 3.31.4 | MIT | `lib/tiptap/tiptap.js` (bundle built by Modulus) | On first use, pages with an editor | The WYSIWYG editor (`AppEditor` / `app-editor`); the HTML is cleaned on the server by `RichText.Sanitize`. |
| pdf.js | 6.3.289 | Apache-2.0 | `lib/pdfjs/` (`pdf.min.mjs`, `pdf.worker.min.mjs`, as shipped) | On first use, pages with a PDF viewer | The PDF viewer (`AppPdf` / `app-pdf`); the worker is set on `GlobalWorkerOptions`. |
| Uppy | 6.0.2 (core), 6.0.0 (Dashboard, XHR upload) | MIT | `lib/uppy/uppy.js`, `uppy.min.css` (bundle built by Modulus) | On first use, pages with a drop area | The drop area (`AppUploader` / `app-uploader`): previews, progress, type, size and count limits. |
| Cropper.js | 2.2.0 | MIT | `lib/cropper/cropper.js` (bundle built by Modulus; the styles are inside it) | On first use, pages with a cropper | The image cropper (`AppImageCropper` / `app-image-cropper`). |
| CodeMirror | 6 (`@codemirror/view` 6.43.13 and its language packs) | MIT | `lib/codemirror/codemirror.js` (bundle built by Modulus) | On first use, pages with a code editor | The code editor (`AppCodeEditor` / `app-code-editor`): JavaScript, TypeScript, JSON, CSS and HTML. |
| Univer | 1.0.3 (open-source presets only) | Apache-2.0 | `lib/univer/univer.js`, `univer.css` (bundle built by Modulus, ~12 MB, ~3 MB gzipped) | On first use, pages with a spreadsheet or document | The spreadsheet (`AppSpreadsheet`) and the document editor (`AppDocument`). |
| Inter | variable, Latin | SIL OFL 1.1 | `fonts/inter/` | Every page | The UI typeface. |

Tiptap and Univer ship ESM only, so Modulus builds each into one classic script; the exact packages and the esbuild
command are in `lib/<name>/LICENSE.md`.

## Generated icons

`src/Modulus.Ui.Abstractions/Shell/ExtraIconSvg.g.cs`, generated by `tools/icons/build.sh` (not loaded at run time
from anywhere):

| Source | Version | Licence | Why |
|---|---|---|---|
| `@ant-design/icons-svg` | 4.4.2 | MIT | Ant Design icons newer than the `AntDesign` package's set. |
| Tabler Icons (outline) | 3.48.0 | MIT | `tb:` icons Ant Design has no equivalent for (`tools/icons/tabler.txt`). |

## Services called from the browser

| Service | Where | Terms | Notes |
|---|---|---|---|
| OpenStreetMap raster tiles (`tile.openstreetmap.org`) | `osmStyle()` in `modulus-plugins.js` (tile maps) | [OSM tile usage policy](https://operations.osmfoundation.org/policies/tiles/); data ODbL | Fine for samples and light use; production apps with real traffic should point the map at their own or a commercial tile server. |
