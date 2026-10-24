# Upgrading Ant Design Blazor, htmx, Alpine.js and the plugin libraries

Modulus pins every third-party UI dependency in one place, so upgrades are deliberate and checked.

| Dependency | Pinned in | Current |
|---|---|---|
| `AntDesign`, `AntDesign.ProLayout` | `Directory.Packages.props` (keep both on the same version) | 1.6.2 |
| htmx | `src/Modulus.Ui.Assets/wwwroot/lib/htmx/` | 2.0.11 |
| Alpine.js | `src/Modulus.Ui.Assets/wwwroot/lib/alpinejs/` | 3.17.4 |
| MapLibre GL JS (tile maps) | `src/Modulus.Ui.Assets/wwwroot/lib/maplibre/` | 5.24.0 |
| Tiptap (WYSIWYG editor; a prebuilt bundle, see below) | `src/Modulus.Ui.Assets/wwwroot/lib/tiptap/` | 3.31.4 |
| Univer (spreadsheet; a prebuilt bundle, see below) | `src/Modulus.Ui.Assets/wwwroot/lib/univer/` | 1.0.3 |
| pdf.js (PDF viewer) | `src/Modulus.Ui.Assets/wwwroot/lib/pdfjs/` | 6.3.289 |
| Uppy (drop area; a prebuilt bundle, see below) | `src/Modulus.Ui.Assets/wwwroot/lib/uppy/` | core 6.0.2, Dashboard and XHR upload 6.0.0 |
| Cropper.js (image cropper; a prebuilt bundle, see below) | `src/Modulus.Ui.Assets/wwwroot/lib/cropper/` | 2.2.0 |
| CodeMirror 6 (code editor; a prebuilt bundle, see below) | `src/Modulus.Ui.Assets/wwwroot/lib/codemirror/` | view 6.43.13 and the language packs listed in its LICENSE.md |
| jsVectorMap (vector map, with `world.js`) | `src/Modulus.Ui.Assets/wwwroot/lib/jsvectormap/` | not recorded; its build has no version banner, so write it here at the next upgrade |

## What protects you

- **Aliases, not wrappers.** `App*` types are empty subclasses, so upstream parameter changes need no Modulus changes.
  Only a removed or renamed component breaks them, and the compiler reports it.
- **`tools/antd-drift`** (runs in CI on every push) compares the pinned AntDesign assembly with
  `manifest/antd-coverage.json`:
  - component types added or removed since the recorded baseline;
  - every `alias` entry has an `App*` type that really derives from that component;
  - every `App*` type is listed in the manifest;
  - every `app-*` tag named in an entry's `mvc` field is a real tag helper in `Modulus.AspNetCore.Mvc`;
  - `antd-dark-brand.css` still matches the pinned package's dark stylesheet (the brand colour in dark mode).
- **Weekly drift workflow** (`.github/workflows/antd-drift.yml`) builds against the **latest** AntDesign release and opens
  or updates an `antd-drift` issue when something needs attention, before anyone bumps the pin.
- **Tests** cover palette generation against the official antd palettes, alias rendering, Pro components, tag helpers
  and the `AntIcons` reflection hook.

## Upgrading AntDesign

1. Bump both versions in `Directory.Packages.props`.
2. Build and test:
   ```bash
   dotnet build Modulus.slnx -c Release
   dotnet test --solution Modulus.slnx -c Release --no-build
   ```
   - **`AntIconsTests` fails**: AntDesign moved `IconService.GetIconImg`. Update the lookup in
     `src/Modulus.AspNetCore.Mvc/AntIcons.cs`, or switch to a public API if one now exists.
   - **An alias does not compile**: the component was renamed or made generic. Update the alias and its manifest entry.
3. Run the drift check:
   ```bash
   dotnet run --project tools/antd-drift
   ```
   - *New component types*: for user-facing components, add `public class AppX : AntDesign.X { }` in the matching
     `src/Modulus.Blazor/Components/<Category>/Aliases.cs` and a manifest entry with `"status": "alias"`. Sub-components
     and internals only need the baseline update.
   - *Removed types*: remove the alias and manifest entry, and note the breaking change in the release notes.
   - When everything is classified, accept the new surface:
     ```bash
     dotnet run --project tools/antd-drift -- --update-baseline
     ```
   - *`antd-dark-brand.css` no longer matches*: regenerate it, then check the diff for blue shades that are not in
     `AntDarkBrand.Sources` (they stay antd blue in dark mode until added there):
     ```bash
     dotnet run --project tools/antd-drift -- --write-dark-brand
     ```
4. **Check the styling layer.** `modulus.css` refines antd's v4 CSS and depends on its class names. If AntDesign moves to
   antd v5 CSS (CSS-in-JS tokens), review the "v5 refresh" section of `modulus.css`; most of it becomes unnecessary. Also
   review `ThemeCss.Build`, which writes the v4 variable names that `ant-design-blazor.variable.css` reads.
5. **Visual check** in all three samples, in light and dark mode, with the horizontal and the vertical layout: dashboards,
   customers (filter expand, drawer form with validation errors, batch selection), `/forms/validation`, `/tables/advanced`,
   account settings, sign-in and 404. Then open `/ui/components` in the Blazor and MVC samples next to each other: every
   Ant Design category is on one page, so a markup change upstream shows up as a visible difference between them.
   ```bash
   dotnet run --project samples/Modulus.AdminTemplate.Blazor       # :5100
   dotnet run --project samples/Modulus.AdminTemplate.Mvc          # :5101
   dotnet run --project samples/Modulus.AdminTemplate.RazorPages   # :5102
   ```
   The MVC tag helpers emit AntDesign's DOM by hand. If a component's markup changed upstream (compare the Blazor
   sample's HTML), update the matching tag helper in `src/Modulus.AspNetCore.Mvc/TagHelpers/`.

## Upgrading htmx or Alpine.js

1. Replace the file in `wwwroot/lib/<name>/` with the new minified build from the official release, and update the
   version in the table above (the minified files carry no version banner, so this table is the record).
2. Read the release notes for changes to events Modulus relies on:
   - htmx: `htmx:confirm` (`issueRequest`), `htmx:responseError`, `HX-Trigger` JSON events, `hx-headers`
   - Alpine: `Alpine.data`, `Alpine.store`, `x-cloak`, `alpine:init`
3. Run the MVC sample and exercise: create a customer (toast, drawer close, table refresh), delete with the confirm dialog,
   sort, paginate, switch the layout in the settings panel and toggle the theme, then reload to confirm both persist.
   On `/ui/components`, check tabs (click and arrow keys), the dropdown delete (confirm → toast), notification, modal
   (focus, Escape, submit closes it), collapse, switch, segmented, rate, upload and the input clear/reveal buttons.

## Upgrading MapLibre, Tiptap, Univer or jsVectorMap

1. **MapLibre, jsVectorMap:** replace the files in `wwwroot/lib/<name>/` with the new builds from the official release,
   keeping the file names (`modulus-plugins.js` loads them by path on first use), and update the table above.
   **Tiptap** and **Univer** ship as IIFE bundles Modulus builds itself (`lib/tiptap/tiptap.js`,
   `lib/univer/univer.js`), because both are ESM-only; see each `lib/<name>/LICENSE.md` for the esbuild command and
   rebuild after bumping the packages.
2. Read the release notes for the APIs Modulus calls:
   - MapLibre: `maplibregl.Map`, `Marker`, `Popup`, `flyTo`, `fitBounds`, `NavigationControl`
   - Tiptap: `Editor` (`@tiptap/core`), `StarterKit`, `Image`, `Placeholder`, `chain().focus()`, `getHTML`, `isEmpty`
   - Univer: `createUniver`, `UniverSheetsCorePreset` / `UniverDocsCorePreset` and the other presets, `defaultTheme`
     (`@univerjs/themes`), `univerAPI.createWorkbook` / `createDocument`, `disposeUnit` (a reset swaps the workbook or
     document in the running instance), `getActiveWorkbook().save()` / `getActiveDocument().save()`,
     `addEvent(Event.CommandExecuted)` with the command `type` (2 is a mutation) and its `sheet.` / `doc.` id prefix,
     `createMenu(...).appendTo('ribbon.start.others')`, `setTheme`, `toggleDarkMode`, the `en-US` locale. Univer's
     buttons carry no `type`: the script points them (and its inputs) at a form id that does not exist so they never
     submit the page's form; check the sheet list button next to "+" still switches sheets without a download.
   - jsVectorMap: the `world` map in `world.js`, region series (`regions` / `values`), `onRegionTooltipShow`
3. Run a sample and check `/plugins/editor` (format, insert an image and a link, save, and confirm the cleaned HTML
   comes back), `/plugins/spreadsheet` (type a number, watch the Year column and Total row recalculate, add a
   `=SUM(B2:B5)` formula, switch sheets, save, and check the JSON posted), `/plugins/map` and `/plugins/map-fullsize`
   (markers, popups, the place list flying to a place, resizing the window) and `/plugins/map-vector` (fills,
   tooltips, legend), in light and dark mode, with SPA navigation on (leave the page and come back).

## Upgrading Uppy, Cropper.js or CodeMirror

Each is bundled by Modulus into one classic script, because the packages ship ESM only. `lib/<name>/LICENSE.md` lists
the exact packages, their versions and the esbuild command; to upgrade:

1. Pin the new versions with `npm install --save-exact`, rebuild the bundle with esbuild (the entry file is in the
   LICENSE.md), and replace `lib/<name>/<name>.js` (and `uppy.min.css` for Uppy). Keep the file names: the widgets in
   `modulus-plugins.js` load them by path. Update the table above and `DEPENDENCIES.md`.
2. Read the release notes for the APIs Modulus calls:
   - Uppy: `Uppy` (`restrictions`, `autoProceed`, `on('complete')`), `Dashboard` (`inline`, `target`, `theme`,
     `hideUploadButton`, `note`, `setOptions`), `XHRUpload` (`endpoint`, `fieldName`, `formData`, `headers`,
     `getResponseData`). The endpoint must answer 2xx; an empty reply is a success because `getResponseData` returns `{}`.
   - Cropper.js: `new Cropper(image, { container })`, `getCropperSelection()`, `getCropperImage()`, the selection's
     `aspectRatio`, `change`, `$toCanvas()`, and the picture's `$image` (natural size, for the crop in pixels).
   - CodeMirror: `EditorView`, `EditorState` (`readOnly`, `allowMultipleSelections`), `Compartment` (the theme),
     `EditorView.updateListener`, `EditorView.contentAttributes`, the keymaps, and the language packages.
3. Run `/plugins/dropzone` (drop a PDF and an image, see the result line; drop a `.exe` and see it refused), `/plugins/image-cropper`
   (pick 1:1, drag the box, see the readout and the preview) and `/plugins/code-editor` (type in the JSON rule, read the
   form value, switch to dark mode) in all three samples.

## Upgrading Fluid (`Modulus.AspNetCore.Fluid`)

The version is pinned in `Directory.Packages.props` (`Fluid.Core`, 2.40.0, MIT). To upgrade:

1. Change the pin, build, and run `tests/Modulus.AspNetCore.Fluid.Tests`. They cover escaping, the layout, the model types,
   each tag and filter, parse errors and the file cache.
2. Read the release notes for what `ModulusLiquid` and `ModulusLiquidTags` call: `FluidParser.RegisterEmptyTag`,
   `RegisterExpressionTag`, `TemplateOptions.Filters.AddFilter`, `MemberAccessStrategy.Register<T>()`, `TemplateContext`,
   `StringValue(text, encode)` (which prints a value unescaped when `encode` is false) and `FluidTemplate.Render`.
3. Keep the renderer's public members clear of `TryParse`: ASP.NET Core's minimal APIs treat a `TryParse(string, out T)` method
   on a parameter type as a binder and fail when the route is built (`ModulusLiquid.TryParseTemplate` is the name).

## Upgrading QuestPDF (`Modulus.Documents`, PDF output)

The version is pinned in `Directory.Packages.props` (`QuestPDF`, 2026.9.0, Community licence). Before upgrading, check the
licence terms of the new version: the Community licence covers individuals and businesses under USD 1,000,000 in annual gross
revenue, and not public-sector or publicly traded companies. A host that needs another licence sets `QuestPDF.Settings.License`
before its first document (Modulus does not choose it). The samples set it in their `Program.cs` (the Fluid sample and the three admin apps). To upgrade:

1. Change the pin, build, and run `tests/Modulus.Blazor.Tests` (`DocumentPdfTests`, and the DOCX tests beside them).
2. Check the output with `pdfinfo` (one A4 page for a short letter) and `pdftotext -layout` (the text, the lists and the
   footer's page numbers), since the tests only check the PDF's structure.
3. The calls `DocumentPdf` makes are `Document.Create`, `Page`, `MarginHorizontal` and `MarginVertical`, `DefaultTextStyle`,
   `Column`, `Table` with `ColumnsDefinition`, `Cell().Border().BorderColor().Padding()`, `ConstantItem`, `RelativeItem`,
   `CurrentPageNumber`, `TotalPages`, the text spans, and `GeneratePdf`. Read the release notes for those first.

## Hardening

**What is on.** `AddModulusHardening()` and `UseModulusHardening()` (`Modulus.AspNetCore.Hosting`, called before the static
files and the endpoints in each sample) add:

- **Compression**: Brotli and gzip for HTML, CSS, JavaScript, JSON and SVG. Not over HTTPS by default (`EnableForHttps` is off):
  a compressed page that echoes a secret next to attacker-controlled text can leak it (BREACH).
- **Security headers** on every response: `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`,
  `X-Frame-Options: SAMEORIGIN`, `Permissions-Policy` with camera, microphone and location off.
- **The Content-Security-Policy**, with a nonce per request. MVC and Razor Pages **enforce** it (`CspEnforce = true` in their
  `Program.cs`). Blazor sends it **report-only**: its import map is an inline script, and Blazor 10 has no nonce for it, so an
  enforced policy would stop the module loader. The browser console shows that one violation per page. Turning enforcement on
  there needs the framework to nonce its import map, or a hash of it (which changes with every build).
- **Scripts**: `script-src 'self' 'nonce-…' 'unsafe-eval'`. Alpine.js (the standard build, which evaluates `x-data` and
  `x-show` expressions) needs `'unsafe-eval'`. Alpine's CSP build would remove it, at the cost of its expression syntax. Styles
  allow `'unsafe-inline'`, because Ant Design sets styles from script.
- **Connections**: `'self'`, WebSockets (Blazor), and `https:` (map tiles). Narrow it with `ConnectSources` and no `https:`
  when the tile hosts are known. `CspReportUri` sends violations to an endpoint.

**Nonces.** An inline script the app writes gets the request's nonce: `<script nonce="@CspNonce.Of(Context)">…</script>`.
Modulus writes no inline scripts, so its own markup needs none.

**Health.** `MapModulusHealth()` answers `/health` with `Healthy` (200) while the app's health checks pass, and 503 otherwise.
Add checks with `AddHealthChecks()`. The samples report no checks beyond the defaults.

**Observability.** `AddModulusObservability()` (`Modulus.AspNetCore.Hosting`, called beside `AddModulusHardening()` in each
sample) registers OpenTelemetry for the app: ASP.NET Core request traces, request metrics, and the logs with their scopes. The
service name is the entry assembly's name unless `ServiceName` is set.

- **Traces** skip `/health`, `/_content`, `/_framework` and `/_blazor` (the probe, the library's assets and Blazor's live
  connection). **Metrics do not skip them**: the `/health` requests count in the request metrics.
- **Query strings** are recorded with their sensitive values redacted (the instrumentation's default; keep
  `DisableUrlQueryRedaction` off), so a token in a URL is not written to a trace.
- **Export.** The signals stay in the process unless `OTEL_EXPORTER_OTLP_ENDPOINT` is set (or `Otlp = true`). Then they go to
  that collector, with the standard `OTEL_EXPORTER_OTLP_PROTOCOL` and `OTEL_EXPORTER_OTLP_HEADERS` variables.
- **Checked.** The Fluid sample on 5103 with `OTEL_EXPORTER_OTLP_ENDPOINT` pointed at a local listener: `/v1/traces` carried
  the `/invoices/4988` request and not `/health`; `/v1/logs` and `/v1/metrics` arrived (metrics go out every 60 seconds). With no
  endpoint set, the sample runs with no export and no errors.
- **Upgrading.** The three OpenTelemetry packages are pinned in `Directory.Packages.props`; keep them in step, and check the
  signals after an upgrade as above.

**Caching.** The MVC head tags (`modulus-head`, `modulus-scripts`) add each library asset's content hash as `?v=`, so a changed
file gets a new address. A versioned `/_content/` response is sent `Cache-Control: public, max-age=31536000, immutable`.
Other assets, and everything in Blazor (whose head is not MVC), keep `no-cache` with an ETag, so the browser revalidates and
gets 304 when nothing changed.

**Not checked here.** The 500 page: each sample routes exceptions to its error page outside Development, but this was not
triggered in this session. The 404 page and the htmx status codes were checked.

**Upgrading.** Changing the policy: edit `HostingExtensions.PolicyFor`, and keep `tests/Modulus.AspNetCore.Hosting.Tests` green.
Run `tests/browser/smoke.py` against the samples before enforcing a policy anywhere new: a zero-violation run is the evidence.

## Saved views (`MapModulusSavedViews`)

`<app-saved-views>` posts to `/_modulus/views`. An MVC or Razor Pages app that uses it must map that endpoint after
its controllers or pages:

```csharp
app.MapModulusSavedViews();
```

Without it, saving or deleting a view returns 404. Blazor needs nothing extra. `AddModulusMvc` and `AddModulusBlazor`
register `MemoryUiStateStore`, which forgets everything on restart. To keep views, register your own
`IUiStateStore` **before** calling them, e.g. a copy of the samples' `EfUiStateStore`
(`samples/Modulus.Samples.Shared/SampleUiState.cs`, one EF Core table keyed by user and key):

```csharp
builder.Services.AddSampleUiState(builder.Environment.ContentRootPath); // before AddModulusMvc / AddModulusBlazor
var app = builder.Build();
app.Services.EnsureUiStateDatabase();
```

On a Blazor `ProTable`, set `UrlState` (plus `UrlQuery` and `OnUrlChanged` for the page's own filters) so the
layout is in the URL and `AppSavedViews` can save it. Saved views no longer keep the page number (`current` is
dropped like `page`); views saved earlier with one still open, on that page.

## Multi-column sort (`thenBy`)

Nothing breaks. `ProTableRequest` gained `ThenBy` (text, bound from `?thenBy=`) and `Sorts` (the whole list); a
request without `thenBy` sorts exactly as before. To offer Shift+click sorting:

- Blazor: set `MultiSort` on the `ProTable`. Your `Request` callback must apply `request.Sorts`: `TableQuery` does,
  and for in-memory rows use `rows.SortBy(request, keys)` with a field → key dictionary instead of a chain of
  single-field `SortBy` calls (which only read the first sort).
- MVC and Razor Pages: pass `sortAddHref` and `sortRank` to `ColumnHeader.Content` (see the sample's
  `Grid.SortTh(…, multiSort: true)`), and apply `request.Sorts` in the action as above.

A custom `IColumnHeaderHost` keeps compiling: the new members (`SortAsync(key, add)`, `MultiSort`, `SortRank`) have
defaults that keep one sort.

## Group by (`groupBy`)

Nothing breaks. `ProTableRequest` gained `GroupBy` (bound from `?groupBy=`); `ProTableResponse` gained `Groups` and
`Totals`, null unless set. To offer grouping:

- Register the fields and totals on your `TableQuery`: `.Group("department")` (the field must have a `Sort`) and
  `.Total("salary", "Payroll", e => e.Salary)`, and return `page.ToResponse()` so the groups and totals reach the table.
- Blazor: set `GroupFields` on the `ProTable` (or `ModulusDataGrid`) to the fields the menu offers.
- MVC and Razor Pages: write `TableGroups.HeaderRow` before a group's first row, `TableGroups.RowAttribute` on its
  rows, `TableGroups.FooterRow` after its last and `TableGroups.Totals` under the table (see the sample's
  `Tables/Advanced.cshtml`); `modulus-mvc.js` handles the toggles.

A `Request` callback that ignores `GroupBy` still works: the table shows its rows without group headers.

## Multi-level headers (`HeaderGroups`)

Nothing breaks: tables without groups render as before. To add bands over columns:

- Blazor: set `HeaderGroups` on the `ProTable` or `EditableGrid`, e.g.
  `[new HeaderGroup("Job", "Position", "Department"), new HeaderGroup("Pay", "Salary")]`; `{ Level = 1 }` puts a band a row higher. Keys are a
  `PropertyColumn`'s property name or a `GridColumn.Key`, matched without regard to case.
- MVC and Razor Pages: set `header-groups` on `<app-editable-grid>`. In your own tables, write
  `@Html.Raw(HeaderGroups.Rows(shown, groups))` first in the `<thead>`, where `shown`
  is the shown columns in order with null for the checkbox and other untitled columns (see the sample's
  `Tables/Advanced.cshtml`).

A custom script that reads `thead tr` should pick the leaf row with `thead tr:not(.m-header-bands)`.

## Checkbox filter menu (value lists)

A column search of the form `=a|b` or `!=a|b` is now a value list: `TableQuery.SearchText` and `DataGridAuto` keep
the rows whose value is (or is not) exactly one of the listed values (`""` is a blank cell), translated to SQL
`IN` / `NOT IN`. Before, `SearchText` looked for the whole text (`=a|b`) as a substring, and `DataGridAuto` matched
or excluded the text `a|b` exactly. `=x` and `!=x` with a single value now match exactly in `SearchText` too; other
searches behave as before. Such a list may be up to `ColumnSearch.MaxListLength` (2000) characters; other searches are still
cut at `TableQuery.MaxSearchLength`.

To show the menu: Blazor `ColumnHeader Values="…"` (build them with `ColumnSearch.ValuesOf` or
`GridSchema.SearchValues`); MVC `ColumnHeader.Content(…, values: …)`. Editable grids list Select, Tag and Switch
columns without changes.

Custom Alpine components: declare every field the component sets in its data object; an undeclared one lands on the
outermost scope.

## Row states on the editable grid

- `GridSchema.Validate` skips rows whose `State` is `ReadOnly` or `Locked`, and takes an optional row rule
  (`Func<GridRecord, string?>`) whose message becomes a row error (`GridError.ForRow`, `IsRow`).
- MVC apps that set row states should call `GridSchema.Protect(post.Columns, post.Rows, storedRows)` after reading
  the post: it restores read-only and locked rows from the store and puts back a deleted locked row. The
  `delete-row` operation already refuses locked rows.
- In `<app-editable-grid>`, an empty cell that can't be edited now shows "—" instead of "Add".
- The error banner counts cells, rows or (mixed) items: "2 cells need attention before saving." (`GridSchema.ErrorSummary`).
- `SortableHtml.RowHandle` has two new optional parameters, `disabled` and `disabledTitle`.

## Icons

Names Ant Design has no icon for are Tabler outline icons with a `tb:` prefix (`ShellRenderer.TablerPrefix`), listed in
`tools/icons/tabler.txt` and generated into `ExtraIconSvg.g.cs` by `tools/icons/build.sh`. The earlier Remix Icon
fallbacks (`ri:`) are gone and draw an empty icon: rename them, e.g. `ri:check-double` → `tb:checks`,
`ri:expand-up-down` → `tb:selector`, `ri:group` → `tb:users-group`, `ri:lock-2` → `tb:lock`, brands such as
`ri:visa` → `tb:brand-visa`. To add one, put its name from https://tabler.io/icons in `tabler.txt` and run the script.

## SPA navigation and `IsHtmx()`

`Request.IsHtmx()` is now false for boosted requests (`hx-boost`), as it already was for history restores: a boosted
link or form is a page load, so `IsHtmx() ? NoContent() : Redirect(...)` keeps working when
`ModulusThemeOptions.SpaNavigation` is on. Use `Request.IsBoosted()` to detect SPA navigation. To opt in:

1. Set `Modulus:Theme:SpaNavigation` to `true` (and `SpaPrefetch` to `false` to turn off hover prefetch).
2. Move inline page code to `Modulus.page(fn)` (or `Modulus.onPage(fn)` for code every page needs). `DOMContentLoaded`
   fires once, on the first page only.
3. Links that must load the whole page (downloads, pages outside the shell that respond with a shell page) take
   `download` or `data-m-reload`; anything that isn't a shell fragment already falls back to a full load.
