# DOCUMENTATION

This document combines 9 source documents. Contents:

- [Modulus CLI: UI commands](#modulus-cli-ui-commands)
- [Upgrading Ant Design Blazor, htmx, Alpine.js and the plugin libraries](#upgrading-ant-design-blazor-htmx-alpinejs-and-the-plugin-libraries)
- [Modulus UI — design notes](#modulus-ui--design-notes)
- [Modulus UI templates: production-readiness status and remaining work](#modulus-ui-templates-production-readiness-status-and-remaining-work)
- [Plan and progress](#plan-and-progress)
- [Modulus UI Framework - Strategic Roadmaps](#modulus-ui-framework---strategic-roadmaps)
- [Modulus UI CLI Implementation Roadmap](#modulus-ui-cli-implementation-roadmap)
- [Auth/Identity UI Enhancement Layer](#authidentity-ui-enhancement-layer)
- [Modulus UI Professional Enhancement Roadmap](#modulus-ui-professional-enhancement-roadmap)

<!-- source: docs/DOCUMENTATION.md#modulus-cli-ui-commands -->
## Modulus CLI: UI commands

The `modulus ui` commands scaffold UI into a Modulus web app. They read the app's engine from `.modulus.json`
(`ui_engine`, written by `modulus app --ui-engine`) or take `--engine mvc|razor-pages|blazor`. Fluid is not scaffolded yet.
Every command accepts `--dry-run`, `--force`, `--json`, `-v`, `-q` and `-o <app root>`.

Templates come from the `Modulus.Ui.Templates` package, the UI libraries from `Modulus.Ui.*`, `Modulus.AspNetCore.*`,
`Modulus.Blazor` and `Modulus.Documents`.

### Which UI a new app gets

`modulus app --kind webapp+api --ui-engine mvc|razor-pages` builds the **Web project on the Modulus UI framework**:
`AddModulusMvc`, the `_ModulusLayout` shell, `MapStaticAssets`, an auth frame for the sign-in pages and a sign-in
convention (every page needs a signed-in user except `/Account`). The CLI no longer carries any other UI: the older
`Cobytelabs.Modulus.UI.*` modules, the Tabler theme, `ui add/remove/update/eject/diff`, `--ui-modules` and the single-project
`webapp` kind were removed. `blazor`, `fluid` and `none` have page templates but no app host yet, so `modulus app` refuses
them for a web app. `generate-crud` scaffolds the API side only; add pages with the `ui` commands below.

### Packages and feeds

| Where | What the CLI does |
| --- | --- |
| `~/.modulus/cache` | Templates unpacked from the package (re-unpacked when the local `.nupkg` is newer). |
| `~/.modulus/feed` (or `MODULUS_LOCAL_FEED`) | Local feed used until the packages are on nuget.org. |
| nuget.org | Used when the package is in neither of the above. |

Pack the libraries into the local feed: `dotnet pack Modulus.slnx -c Release -o ~/.modulus/feed`.
`modulus app --ui-engine <engine>` references the engine's packages (listed in the package's `ui-kit.json`) and, if the local
feed exists, adds it to `NuGet.config` with source mapping. Delete `~/.modulus/feed` once the packages are published.

### Commands

| Command | Adds |
| --- | --- |
| `ui add-component <names…> \| --all \| --list` | 78 components in nine groups: Inputs (auto-complete, button, cascader, checkbox, checkbox-group, color-picker, date-picker, date-range-picker, form-group, form-input, input-number, money-input, otp-input, radio-group, rate, search-input, segmented, select, slider, switch, textarea, transfer, tree-select, upload), Data display (avatar, badge, calendar, carousel, collapse, countdown, data-table, descriptions, image, list, popover, qr-code, stat-card, statistic, status-badge, tag, timeline, tooltip, tree), Navigation (breadcrumb, dropdown, menu, page-header, pagination, back-top, steps, tabs), Layout (card, divider, grid, space, splitter), Feedback (alert, drawer, empty-state, modal, progress, result, skeleton, spinner) Documents (document, pdf-viewer, spreadsheet), Editors (code-editor, rich-text-editor), Data grids (data-grid, import-wizard, pivot-grid) and Pro (file-manager, gantt, kanban, query-builder, scheduler, schema-form). Run `--list` for descriptions. |
| `ui create-dashboard <overview\|analytics\|reports\|audit> [--module]` | A dashboard page. |
| `ui create-form-from-entity <Entity> [--module]` | A create/edit form whose fields come from the entity class's public properties. |
| `ui add-admin [--pages a,b] [--engine] [--folder Admin] [--no-menu]` | The admin dashboard starter: 14 working pages (dashboard, analytics, reports, users, roles, tenants with settings and members, settings, audit log, system health, profile, notifications and preferences), plus an `AdminController` for MVC and a `Views/_ViewStart.cshtml` that sets the shell layout when the app has none. It adds `AddModulusAdminUi()` (the matching menu: Overview, Settings, System, My account) to `Program.cs` and, for MVC, controller routing. `--pages` limits both the files and the menu. The pages are yours to edit; they read data through `Modulus.Ui.Abstractions.Admin` services and show sample data until you register your own. They need a signed-in user (`--auth openiddict`), and the command warns when the host has no sign-in. |
| `ui add-auth` | Forgot-password and reset-password pages (`/auth/forgot-password`, `/auth/reset-password`). |
| `ui add-2fa` | The two-factor page (`/auth/two-factor`): authenticator setup, recovery codes, turn off. |
| `ui add-session-manager` | Sessions and recent sign-ins (`/auth/sessions`). |
| `ui add-chart --type line\|column\|donut\|heatmap` | A chart page (`/charts/<type>`) built on the framework's chart components, with sample data to replace. MVC shares one `ChartsController`. |
| `ui add-i18n --languages en,es,fr` | Sets `Modulus:Theme:Cultures` (the first is the default) and adds `app.UseModulusLocalization()` before `UseRouting`. With two or more languages the theme settings show a language picker. |
| `ui audit [--level A\|AA\|AAA] [--format json\|markdown\|html] [--out-file f] [--fail-on-issues]` | A static accessibility scan of every `.cshtml`, `.razor` and `.html` file under `src`. Seven rules (image alt, form labels, button/link names, keyboard access, page language, table headers, positive tabindex), all WCAG level A. A markup lint: it does not check colour contrast, focus order or content in the running page. |
| `ui create-permission-matrix <Entity> [--module] [--roles Admin,Sales] [--permissions a:b:c,...]` | A role × permission matrix (`/permissions/<entity>`). It reads grants with `GET /authorization/grants/Role/{role}` and grants or revokes with `POST` / `DELETE`, so the host needs `MapModulusAuthorizationManagement` and the caller the management permission. The API cannot list all roles or permissions, so rows and columns come from the options (default: `Admin,Manager,User` and the entity's `<module>:<route>:manage`). A denied permission shows as a locked box. |
| `ui create-crud <Entity> [--module] [--engine razor-pages\|blazor]` | List, create/edit and delete pages (`/<entities>`) over the entity's API (`generate-crud`) through a typed `<Entity>ApiClient`, plus a menu entry. Needs a `webapp+api` app; set `Api:BaseUrl` in the Web project. |
| `ui add-assistant` | Hosts the AI platform's embedded assistant: `POST /ai/session` (mints the session token with the server-side `Ai:Host:ApiKey`), a `_AiAssistant` layout partial rendering `<ai-assistant>`, the `ai:use` permission and `Ai:Host` settings (disabled by default). Razor Pages and MVC; supply the SDK script under `wwwroot/lib/ai-sdk/`. |
| `ui theme create <name> [--base tabler\|minimal] [--colors]` | `Themes/<name>/theme.json` and `variables.css`. |
| `ui theme set <name>` | Writes the theme to `Modulus:Theme` in `appsettings.json`. |
| `ui theme list` | The app's themes; the active one is marked. |
| `ui theme export <name> --format css\|scss\|tailwind\|json` | The theme in another format. |

The auth pages call the `/account/*` endpoints of Modulus.Identity on the same host. MVC gets a controller beside each
view; Razor Pages and Blazor carry their route in the page.

### Update notices

When a newer `modulus` CLI is on nuget.org, the command you run ends with a short notice and the update command. The check asks nuget.org at most once a day and never delays a command. It stays silent for `--json`, `--quiet`, redirected output and CI. To turn it off, set `MODULUS_NO_UPDATE_CHECK=1`. Run `modulus doctor` to see the installed and latest versions at any time.

### Not built yet

`add-chat`, `add-form-builder`, `add-collaboration`, `add-pwa`, `create-user-dashboard` (Identity has
no endpoint to list users), `add-access-review`. They need template content or
backend endpoints that do not exist yet. Self-service registration is not offered because Identity only supports invitations.

### Known issues

- The identity-backed pages (`add-auth`, `add-2fa`, `add-session-manager`, permission matrix) call same-origin `/account/*` and `/authorization/*`; in a `webapp+api` app the Web host forwards the recovery routes only.
- The `mvc` engine adds MVC to the web host for the landing page only: `HomeController` with `Views/Home/Index.cshtml`, plus `Views/_ViewStart.cshtml` and `Views/_ViewImports.cshtml`. The sign-in and status pages stay Razor Pages. Add your own controllers and views beside them.
- `admin-users`, `admin-settings`, `module-home` and `system-health` templates exist but no command scaffolds them yet.
- The `_content` assets of the UI packages are served by `dotnet run` only in the Development environment; a published app carries them in `wwwroot`.

<!-- source: docs/DOCUMENTATION.md#upgrading-ant-design-blazor-htmx-alpinejs-and-the-plugin-libraries -->
## Upgrading Ant Design Blazor, htmx, Alpine.js and the plugin libraries

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

### What protects you

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

### Upgrading AntDesign

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

### Upgrading htmx or Alpine.js

1. Replace the file in `wwwroot/lib/<name>/` with the new minified build from the official release, and update the
   version in the table above (the minified files carry no version banner, so this table is the record).
2. Read the release notes for changes to events Modulus relies on:
   - htmx: `htmx:confirm` (`issueRequest`), `htmx:responseError`, `HX-Trigger` JSON events, `hx-headers`
   - Alpine: `Alpine.data`, `Alpine.store`, `x-cloak`, `alpine:init`
3. Run the MVC sample and exercise: create a customer (toast, drawer close, table refresh), delete with the confirm dialog,
   sort, paginate, switch the layout in the settings panel and toggle the theme, then reload to confirm both persist.
   On `/ui/components`, check tabs (click and arrow keys), the dropdown delete (confirm → toast), notification, modal
   (focus, Escape, submit closes it), collapse, switch, segmented, rate, upload and the input clear/reveal buttons.

### Upgrading MapLibre, Tiptap, Univer or jsVectorMap

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

### Upgrading Uppy, Cropper.js or CodeMirror

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

### Upgrading Fluid (`Modulus.AspNetCore.Fluid`)

The version is pinned in `Directory.Packages.props` (`Fluid.Core`, 2.40.0, MIT). To upgrade:

1. Change the pin, build, and run `tests/Modulus.AspNetCore.Fluid.Tests`. They cover escaping, the layout, the model types,
   each tag and filter, parse errors and the file cache.
2. Read the release notes for what `ModulusLiquid` and `ModulusLiquidTags` call: `FluidParser.RegisterEmptyTag`,
   `RegisterExpressionTag`, `TemplateOptions.Filters.AddFilter`, `MemberAccessStrategy.Register<T>()`, `TemplateContext`,
   `StringValue(text, encode)` (which prints a value unescaped when `encode` is false) and `FluidTemplate.Render`.
3. Keep the renderer's public members clear of `TryParse`: ASP.NET Core's minimal APIs treat a `TryParse(string, out T)` method
   on a parameter type as a binder and fail when the route is built (`ModulusLiquid.TryParseTemplate` is the name).

### Upgrading QuestPDF (`Modulus.Documents`, PDF output)

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

### Hardening

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

### Saved views (`MapModulusSavedViews`)

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

### Multi-column sort (`thenBy`)

Nothing breaks. `ProTableRequest` gained `ThenBy` (text, bound from `?thenBy=`) and `Sorts` (the whole list); a
request without `thenBy` sorts exactly as before. To offer Shift+click sorting:

- Blazor: set `MultiSort` on the `ProTable`. Your `Request` callback must apply `request.Sorts`: `TableQuery` does,
  and for in-memory rows use `rows.SortBy(request, keys)` with a field → key dictionary instead of a chain of
  single-field `SortBy` calls (which only read the first sort).
- MVC and Razor Pages: pass `sortAddHref` and `sortRank` to `ColumnHeader.Content` (see the sample's
  `Grid.SortTh(…, multiSort: true)`), and apply `request.Sorts` in the action as above.

A custom `IColumnHeaderHost` keeps compiling: the new members (`SortAsync(key, add)`, `MultiSort`, `SortRank`) have
defaults that keep one sort.

### Group by (`groupBy`)

Nothing breaks. `ProTableRequest` gained `GroupBy` (bound from `?groupBy=`); `ProTableResponse` gained `Groups` and
`Totals`, null unless set. To offer grouping:

- Register the fields and totals on your `TableQuery`: `.Group("department")` (the field must have a `Sort`) and
  `.Total("salary", "Payroll", e => e.Salary)`, and return `page.ToResponse()` so the groups and totals reach the table.
- Blazor: set `GroupFields` on the `ProTable` (or `ModulusDataGrid`) to the fields the menu offers.
- MVC and Razor Pages: write `TableGroups.HeaderRow` before a group's first row, `TableGroups.RowAttribute` on its
  rows, `TableGroups.FooterRow` after its last and `TableGroups.Totals` under the table (see the sample's
  `Tables/Advanced.cshtml`); `modulus-mvc.js` handles the toggles.

A `Request` callback that ignores `GroupBy` still works: the table shows its rows without group headers.

### Multi-level headers (`HeaderGroups`)

Nothing breaks: tables without groups render as before. To add bands over columns:

- Blazor: set `HeaderGroups` on the `ProTable` or `EditableGrid`, e.g.
  `[new HeaderGroup("Job", "Position", "Department"), new HeaderGroup("Pay", "Salary")]`; `{ Level = 1 }` puts a band a row higher. Keys are a
  `PropertyColumn`'s property name or a `GridColumn.Key`, matched without regard to case.
- MVC and Razor Pages: set `header-groups` on `<app-editable-grid>`. In your own tables, write
  `@Html.Raw(HeaderGroups.Rows(shown, groups))` first in the `<thead>`, where `shown`
  is the shown columns in order with null for the checkbox and other untitled columns (see the sample's
  `Tables/Advanced.cshtml`).

A custom script that reads `thead tr` should pick the leaf row with `thead tr:not(.m-header-bands)`.

### Checkbox filter menu (value lists)

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

### Row states on the editable grid

- `GridSchema.Validate` skips rows whose `State` is `ReadOnly` or `Locked`, and takes an optional row rule
  (`Func<GridRecord, string?>`) whose message becomes a row error (`GridError.ForRow`, `IsRow`).
- MVC apps that set row states should call `GridSchema.Protect(post.Columns, post.Rows, storedRows)` after reading
  the post: it restores read-only and locked rows from the store and puts back a deleted locked row. The
  `delete-row` operation already refuses locked rows.
- In `<app-editable-grid>`, an empty cell that can't be edited now shows "—" instead of "Add".
- The error banner counts cells, rows or (mixed) items: "2 cells need attention before saving." (`GridSchema.ErrorSummary`).
- `SortableHtml.RowHandle` has two new optional parameters, `disabled` and `disabledTitle`.

### Icons

Names Ant Design has no icon for are Tabler outline icons with a `tb:` prefix (`ShellRenderer.TablerPrefix`), listed in
`tools/icons/tabler.txt` and generated into `ExtraIconSvg.g.cs` by `tools/icons/build.sh`. The earlier Remix Icon
fallbacks (`ri:`) are gone and draw an empty icon: rename them, e.g. `ri:check-double` → `tb:checks`,
`ri:expand-up-down` → `tb:selector`, `ri:group` → `tb:users-group`, `ri:lock-2` → `tb:lock`, brands such as
`ri:visa` → `tb:brand-visa`. To add one, put its name from https://tabler.io/icons in `tabler.txt` and run the script.

### SPA navigation and `IsHtmx()`

`Request.IsHtmx()` is now false for boosted requests (`hx-boost`), as it already was for history restores: a boosted
link or form is a page load, so `IsHtmx() ? NoContent() : Redirect(...)` keeps working when
`ModulusThemeOptions.SpaNavigation` is on. Use `Request.IsBoosted()` to detect SPA navigation. To opt in:

1. Set `Modulus:Theme:SpaNavigation` to `true` (and `SpaPrefetch` to `false` to turn off hover prefetch).
2. Move inline page code to `Modulus.page(fn)` (or `Modulus.onPage(fn)` for code every page needs). `DOMContentLoaded`
   fires once, on the first page only.
3. Links that must load the whole page (downloads, pages outside the shell that respond with a shell page) take
   `download` or `data-m-reload`; anything that isn't a shell fragment already falls back to a full load.

<!-- source: docs/DESIGN.md -->
## Modulus UI — design notes

Why Modulus is built the way it is, and which conventions come from which Ant Design-based project.

### 1. What we studied

| Library | Stack | What it does well | What we took |
|---|---|---|---|
| **Ant Design Pro** (ant-design/ant-design-pro) | React, umi | The reference admin template: side layout, 208 px sider, 56 px header, `PageContainer`, dashboard of `ChartCard`s, list/detail/form/exception/account pages | Page inventory and layout metrics; dashboard composition (KPI row → main chart + ranking → tables); 403/404/500 exception pages; sign-in layout |
| **ProComponents** (ant-design/pro-components) | React | Higher-level blocks: `ProTable` (request → `{data, total}`, toolbar, density, column settings, batch bar), `QueryFilter` (collapsed search form with Expand), `ModalForm`/`DrawerForm` (`onFinish` returns `true` to close), `StatisticCard`, `FooterToolbar` | API shapes, one-to-one: `ProTable.Request(ProTableRequest) → ProTableResponse<T>`, `QueryFilter` + `Advanced`, `OnFinish → Task<bool>`, `StatisticCard` + `Trend` |
| **Ant Design Pro Blazor** (ant-design-blazor/ant-design-pro-blazor) | Blazor | Proves the Pro template in Blazor on `AntDesign.ProLayout` (`BasicLayout`, `MenuDataItem`, `ProSettings`) | We use the **official** `AntDesign.ProLayout` package instead of re-implementing a shell. `ModulusLayout` only feeds it Modulus options and menu contributors |
| **Ant Design Blazor** (AntDesign NuGet) | Blazor | Complete component set, services (`IMessageService`, `ModalService`), antd v4 CSS with CSS-variable theming | The foundation. We alias its components and never re-implement them |
| **antd v5 / token system** | React, CSS-in-JS | Seed → map → alias tokens, `@ant-design/colors` palette algorithm, 6 px radius, softer elevation | Palette algorithm ported to C# (`AntPalette`, tested against official palettes); v5 visual refresh layered over the v4 CSS |
| **Refine** (`@refinedev/antd`) | React | Headless data/resource layer under Ant Design; "list/create/edit/show" resource conventions; the UI never owns data access | Pro components call back into app code (`Request`, `OnFinish`); samples keep data in a store/service, and the UI only receives delegates |
| **ABP Framework** (Blazorise/Ant themes) | .NET | Modular apps: `IMenuContributor`, permission-gated menu items, theme as a swappable module | `IMenuContributor` / `MenuConfigurationContext` / `RequiredPolicy`, so every module contributes menus to both flavors once |
| **Radzen / Syncfusion / DevExpress** (reviewed 2026-10-01) | Commercial .NET component suites | Feature inventories for ERP apps: ERP grids (grouping, multi-level headers, Excel-style filters, saved layouts), query builders, import/export, scheduler, gantt, kanban, reporting, document processing | Their UI capabilities as the checklist for the ERP UI roadmap (PLAN §7.3) — not their product scope. Modulus stays a UI framework; data pipelines, report scheduling and product stores belong to the apps built on it |

#### Findings that shaped the build

1. **The best libraries don't re-wrap Ant Design; they compose it.** The earlier Modulus approach (hand-written
   `App*.razor` wrappers re-declaring each parameter) drifted from upstream immediately and could not keep up. Modulus now
   uses pure subclasses (`public class AppButton : AntDesign.Button { }`). They carry zero maintenance cost, expose
   every upstream parameter, and are verified by `tools/antd-drift`.
2. **The Pro layer carries the "professional" look**, more than the primitives do. Ant Design Pro looks polished
   because of consistent page anatomy: page header + subtitle, a filter card, a table card with a title and toolbar,
   24 px gutters, 16 px card spacing and KPI cards with a chart slot. Modulus reproduces that anatomy with `PageContainer`,
   `QueryFilter`, `ProTable` and `StatisticCard`, and uses the same classes in MVC.
3. **Server-rendered antd stacks usually fail on parity.** The fix is to share structure, not just CSS: the MVC tag helpers
   emit the exact DOM Ant Design Blazor renders (checked side by side in screenshots). Icons come from the same SVG store,
   and chart geometry comes from the same C# code.
4. **Theme switching in `AntDesign.ProLayout`'s SettingDrawer doesn't work in 1.6** (only one theme CSS ships), so we do
   not use it. Modulus switches `media` on two preloaded stylesheets, persists the choice in a cookie and applies it before
   first paint.

### 2. Architecture

```
Modulus.Ui.Abstractions   theme options + palette, menus, ShellRenderer, ProTable request/response, TableQuery,
        │                 Paging, chart geometry, form rules, plugin renderers  (no UI deps)
        │
Modulus.Ui.Assets         modulus*.css (tokens, v5 refresh, shell, pages), antd-dark-brand.css, modulus-*.js
        │                 (theme, shell, mvc, widgets, plugins, spa, virtual), htmx 2.0.11, Alpine 3.17.4,
        │                 MapLibre, Tiptap, jsVectorMap, pdf.js; brings AntDesign + ProLayout static CSS to every flavor
   ┌────┴─────────────┐
Modulus.Blazor         Modulus.AspNetCore.Mvc
App* aliases, ProLayout    tag helpers, _ModulusLayout, htmx response helpers, AntIcons
shell, Pro components,
charts
```

#### Theming

- `ModulusThemeOptions` is the single source. `ThemeCss.Build` emits the antd v4 CSS variables that
  `ant-design-blazor.variable.css` reads (`--ant-primary-color`, `-hover`, `-active`, `-outline`, `--ant-primary-1..10`, …),
  computed with antd's own `registerTheme` rules, plus `--m-*` tokens for the Modulus layer.
- Dark mode uses AntDesign's `dark.css`. Modulus tokens have dark overrides under `html[data-theme='dark']`.
- `dark.css` hard-codes antd's blue shades, so the brand colour would stop at the light theme. `antd-dark-brand.css`
  repeats only the declarations that use one of those shades (`AntDarkBrand.Sources`), each shade as a `--m-adp-*`
  variable, and `ThemeCss.Build` sets the variables from the brand: `AntDarkBrand.Recolor` turns each shade's hue by as
  much as the brand's dark primary differs from antd's. The file is generated from the pinned package
  (`tools/antd-drift -- --write-dark-brand`) and the drift check fails when it no longer matches. Selectors for the
  `-blue` preset keep antd's blue.
- Localization: `ModulusThemeOptions.Cultures` lists the languages. The settings panel writes the standard
  `.AspNetCore.Culture` cookie (`ModulusCulture.CookieValue`) and reloads; the host's `UseRequestLocalization`
  (`app.UseModulusLocalization()` in MVC) sets the request culture, and AntD's `LocaleProvider`, the tag helpers and
  the number and date formatting follow it. `ModulusCulture.Match` picks the closest supported culture.
- Layout settings are attributes on `<html>` (`data-layout`, `data-width`, `data-header`, `data-topbar`, `data-side`,
  `data-sidebar`, `data-fold`, `dir`) that `modulus-theme.js` sets from cookies before first paint, so the shell switches
  without a server round trip; CSS in `modulus-shell.css` does the rest. `ShellRenderer.Header` wraps the header row and
  navbar so header modes and the condensed row can style them as one unit. The shell uses logical properties
  (`inset-inline-start`, `padding-inline`) so right-to-left and end-side sidebars mirror it; page widgets and Ant Design
  components are not mirrored yet.
- The v5 refresh (in `modulus.css`) touches only visual properties: radius, shadow, menu item shape, table header weight
  and control heights. Layout and behaviour stay Ant Design's.

#### Blazor

- `AddModulusBlazor` registers AntDesign, maps options into `ProSettings` and registers the menu pipeline.
- `AppShell` is the default shell: the Tabler layout (header, horizontal navbar or vertical sidebar, footer, settings
  panel). Its markup comes from `ShellRenderer` in `Modulus.Ui.Abstractions`, the same code MVC's `_ModulusLayout` calls,
  so the two flavors cannot drift apart; `modulus-shell.js` adds the behaviour in both.
- The header's app switcher and tenant switcher are data on `ShellContent` (`Apps`, `Tenants`). Modulus doesn't
  own tenancy: the app names the current tenant, or lets the switcher keep it in the `modulus-tenant` cookie. The
  interactive Blazor circuit has no `HttpContext`, so the host reads the cookie while prerendering and passes
  `AppShell TenantId`. Switching reloads the page, so every server-rendered part picks up the new tenant.
- Nested menus are trees, not columns. A navbar dropdown is one list (`.m-tree`): a group directly under it is a
  section (`.m-menu-section`, a bold heading with its entries indented, as in Odoo's app menus) and deeper groups are
  `<details class="m-tree-group">` that fold open in place, indented by `--m-depth`. In the sidebar, groups below the
  top level are folding branches (`.m-snav-branch`). Branches on the path to the current page render open
  (`m-in-path`), so the server paints the right state with no script; the folded sidebar's flyout shows the same tree.
- `ModulusLayout` is the alternative Ant Design Pro shell. It wraps `BasicLayout`, converts `MenuItemDefinition` →
  `MenuDataItem`, hides items the user isn't authorized for and renders the footer via `FooterView`.
- Pro components are plain Razor over Ant Design components. They never touch AntDesign internals.
- Components AntD Blazor 1.6.2 lacks (QRCode, ColorPicker, FloatButton, Tour) live in `Components/Extras` and in
  `ModernTagHelpers.cs`. Both emit the antd v5 DOM, and the logic lives in `Modulus.Ui.Abstractions` so it is shared:
  `QrCode` (a byte-mode QR encoder, checked by decoding with ZXing in tests), `QrSvg` / `QRCodeMarkup`, `HsvColor` and
  `TourStep`. `modulusColorPicker` mirrors `HsvColor` in JS. `modulusShell.tour` positions the tour for both flavours,
  and `modulusShell` drives `[data-m-back-top]` back-to-top buttons for both flavours.
- `AppCreatableSelect` / `app-creatable-select` is a select built in Modulus rather than AntD's `Select` with
  `DropdownRender`, so both flavours share one DOM and the "Add item" row can take focus without closing the list.
  Its dropdown is `position: fixed` (placed by `modulusShell.placeSelect`), so table cells and scrolling cards don't
  clip it. Options are `GridOption`, and `GridSchema.AddOption` checks new ones on both sides. The MVC grid adds an
  option with a round trip (`add-option:{key}:{row}`), because the option changes the posted schema.
  For records that need a real form, the select only asks: Blazor raises `OnAddNew` and the page owns the
  `ModalForm`; MVC opens a named `app-modal`, and the server's `HxOptionAdded` event carries the saved record back.
  The event is broadcast by list name, so two pickers of the same list stay in step.
- `EditableGrid` renders its columns from `GridColumn` data over AntD's `Table` (fixed columns, `ExpandTemplate`).
  Values are invariant strings (`1234.5`, `2026-12-31`, `true`, `a,b`), so Blazor, MVC and the store share
  `GridSchema` for formatting, normalising and validation. Select and DatePicker ignore `Open` on first render and
  InputNumber drops text it cannot parse, so the grid opens popups through `modulusShell.openPopup` and edits numbers
  in a native input.

#### Nested grid

- One component serves every depth: `NestedGrid` renders a level as an `EditableGrid` whose row detail is another
  `NestedGrid` for the next level. There are no per-type child tables; `NestedGridSettings` decides the levels, their
  columns and whether rows expand. A level can take its columns from `ColumnSets` by a field of the parent row, so
  sibling rows can have differently shaped children.
- Children load lazily. Blazor calls `Load` when the child grid first renders (AntD only renders an expanded row's
  template), and MVC puts an `hx-get … hx-trigger="intersect once" hx-target="this"` placeholder in the hidden
  detail row. `hx-target="this"` matters: the placeholder would otherwise inherit the level form's target and replace
  the parent.
- Each MVC level is its own `<form>`, even though it sits inside the parent's form. htmx parses swapped fragments in a
  `<template>`, so the nested form survives, and its inputs belong to it rather than to the parent. The level stops
  `submit` and `grid-change` from bubbling, and names its fields `l{level}`, so a parent never sees a child's rows.
- Save is per level and per page: `NestedGridChanges` carries the page as edited and the ids that were loaded but are
  gone. Search, sort and paging wait for Save or Discard (Blazor), because they reload the page.

#### One table design

- The data table, the editable grid and every nested level share one layout: the title with a count, then search,
  Columns and Export CSV, the table's own actions, and Reload / density; a filter row under the toolbar; a batch bar
  once rows are checked; checkbox and expand columns before the data; the "1–10 of 57" range left of the pager.
- The editable grid searches and filters in the browser, because it holds every row: Blazor filters its view, MVC
  hides the rows that don't match (they still post, so a Save never loses them). Both use `GridSchema.Matches`
  (mirrored in `modulusGrid`), and rows added since the last save stay visible. MVC keeps the search and filters in
  `sessionStorage`, so they survive the grid being rendered again after a post. A nested level pages, so it searches
  and filters on the server instead (`NestedGridRequest.Filters`, `f.{key}` in the MVC URL).
- Selection is client state in both flavours: the MVC checkboxes have no name. "Set value" writes one option (or
  Yes / No) into every selected row and marks the grid dirty; Delete removes the rows until Save. Export writes the
  visible columns of the rows shown or selected with `GridSchema.Csv` (Blazor) or the same rules in JS (MVC), and
  saves them through `modulusShell.download`.
- Column resizing is one grip on `<body>` placed over the header edge under the pointer (`modulus-shell.js`), so it
  never adds markup to tables Blazor or htmx own. Dragging sets the `<col>` width, re-pins sticky cells and raises
  `m-col-resize`; the editable grids keep the width in their schema (Blazor through `OnColumnResized`, MVC in the
  posted `{name}.schema`), and `ProTable` keeps it in the page only.
- Avatars use `GridSchema.Initials` and `GridSchema.Tint` (an unsigned hash, the same in JS), so a cell edited in
  the browser looks exactly like one rendered on the server.

#### Editable grid in MVC

- `app-editable-grid` posts the whole grid on every action: the schema as JSON in `{name}.schema`, row ids in
  `{name}.index`, cells as `{name}[{id}].{key}` (hidden columns included) and the action in `{name}.op`.
  `EditableGridForm.Read` validates ids and the schema (falling back to the server's columns), and `Apply` performs
  row and column operations; save, discard and reset stay with the app. htmx swaps just the grid card.
- Cell editing is client-side (`modulusGrid` in `modulus-widgets.js`) with the same formatting and validation rules
  as `GridSchema`, so edits cost no round trip until a structural action or Save.

#### Business fields

- **One rule set, two front ends.** Each field's parsing and checking is a static class in
  `Modulus.Ui.Abstractions.Forms`: `NumberText` (group separators, `(1,250)` negatives, trailing minus), `DurationText`,
  `Currency` / `UnitOfMeasure` (decimals per code), `InputMask` (`9` digit, `A` letter, `*` either, `\` literal),
  `PhoneNumber`, `LookupSearch` (every word must match; labels starting with the text first), `RichText` (an allow-list
  sanitizer) and `SignatureImage`. Blazor components call them directly; the MVC tag helpers render the same DOM and
  `modulus-shell.js` / `modulus-widgets.js` mirror the rules, and the server re-checks with the C# version.
- **Post plain values.** What the user sees is formatted (`1,234.50`, `1:30`, `(202) 555-0111`); what posts is invariant
  (`1234.5`, `90`, `+1 202 555-0111`) in a hidden input named by `asp-for`, so model binding needs nothing special.
  Companion fields post under their own names (`currency-for`, `unit-for`, `from-for` / `to-for`).
- **Lookups** take local `Items` or a server search. Blazor: `SearchAsync(text, max, ct)`. MVC: `search-href`, called as
  `?q=text&max=n` (the widget asks for one more than it shows, to know there are more) and answering
  `[{ value, label, description, fields }]`. `fields` fill the dialog's extra `Columns`.
- **One-time code**: typed keys are handled in `beforeinput` (replace the box, move on), so fast typing never depends
  on the caret; a paste or an autofill into one box spreads over the boxes. Blazor sees every change as `input`.
- **Hosting** (`Modulus.AspNetCore.Hosting`): one place for compression, the security headers, the CSP with a nonce, `/health`, and OpenTelemetry (`AddModulusObservability`),
  added by each sample's `Program.cs`. The CSP is report-only until a page set shows no violations (see UPGRADING *Hardening*).
- **Quality gates** (`tests/browser`): the samples in a real browser, in CI's `browser` job. The gates fail the job; they do not
  report and continue. `budgets.py` runs in the build job.
- **Dynamic forms** are a `FormSchema` (fields as data, read from JSON with `Parse`). `FormSchemaHtml` writes the MVC `app-schema-form`
  markup; the Blazor `AppSchemaForm` writes the same item classes and messages in Razor, because its controls are Blazor elements.
  A `MarkupString` must hold balanced HTML: a fragment with an open `div` is closed by the browser where it ends, which moves any
  Blazor element that follows it out of its box (the first version did this, and the labels sat far from their inputs).
- **Rich text** is `contenteditable` with `document.execCommand` for the toolbar. Tables are built as markup (Insert table,
  row and column buttons that need the caret in a cell, Tab between cells). The posted HTML is never trusted: pages sanitize
  it before saving (tables keep their cells, with `colspan` and `rowspan` as whole numbers only), and `ToPlainText` backs
  required and length checks.

#### MVC / Razor Pages

- No SPA framework. htmx swaps server-rendered fragments. Alpine holds local UI state (dropdowns, the drawer,
  the confirm dialog and table selection).
- Server → client feedback uses `HX-Trigger` events (`modulus:message`, `modulus:drawer-close`, custom refresh events),
  so controllers stay declarative: `Response.HxMessage(...).HxCloseDrawer().HxTrigger("customers:refresh")`.
- Antiforgery tokens travel in a header (`modulus-antiforgery` attribute) so every htmx POST is protected.
- Validation uses the same DataAnnotations model as Blazor, so both flavors show the same messages.
- **Tag helpers copy the Blazor DOM.** Each `app-*` helper emits the element tree Ant Design Blazor renders, captured
  from a live Blazor page. The `/ui/components` gallery exists in every sample to check this side by side. Compound
  components collect their children through `context.Items` (tabs, steps, timeline, descriptions, collapse, list),
  and named slots (`dropdown-menu`, `modal-footer`, `alert-action`, `result-extra` …) fill fixed regions.
- **Controls wrap a real form element.** `asp-for` supplies name, id, value and error state. Unbound attributes
  (`placeholder`, `hx-*`, `x-*`, `autocomplete`) move to the inner `<input>`; class and style stay on the outer element.
  Native inputs do the work: range for Slider, date/time for pickers, radios for Segmented, a hidden input for Switch
  and Rate. So forms post without JavaScript, and Alpine only adds the visual state.
- **Alpine components are small and named** (`modulusTabs`, `modulusCollapse`, `modulusModal`, `modulusUpload`) and live
  in `modulus-mvc.js`. Keyboard behaviour follows WAI-ARIA: arrow keys on tabs, Escape and focus return on modals.
- **Server feedback extends HX-Trigger**: `HxNotify` (notification), `HxCloseModal`, next to `HxMessage` / `HxCloseDrawer`.
- **The shell is responsive the way Tabler's is.** Below 992 px both layouts hide the navigation behind a menu button
  that slides the sidebar in as a drawer; below 768 px search collapses to an icon. Layout choices live in cookies,
  so the server renders them on the first paint.

#### SPA navigation (MVC / Razor Pages)

`ModulusThemeOptions.SpaNavigation` turns the shell into a single-page app without a client framework:

- `_ModulusLayout` puts `hx-boost` on `<body>`. A boosted request (`Request.IsBoosted()`) gets a fragment: `<title>`,
  the sidebar and header as out-of-band swaps (they show the active item, the app's menu and the breadcrumb), the app
  launcher only when the app changed (`X-Modulus-App`), and `<main>` with the page's `Head` and `Scripts` sections.
  `SpaNavigation.PrepareFragment` marks it (`X-Modulus-Fragment`). The client swaps a page navigation (nearest `hx-boost`
  is `<body>`) into `#main`; a boost area of its own (a list's filters and pager) keeps its `hx-target` and `hx-select`.
- `Request.IsHtmx()` is false for boosted requests: controllers answer them like a browser (redirects, pages).
- `modulus-spa.js` loads anything that isn't a fragment (a page outside the shell, an error page, a file) as a whole
  page, aborts a navigation still in flight, shows a delayed progress bar, prefetches on hover (one use, 30 s),
  restores the sidebar and scroll positions, closes overlays and moves focus to `#main`.
- Back and forward reload `.m-page` (the `hx-history-elt`) from the server: htmx keeps no snapshots, because
  DOM that Alpine rendered would be initialised twice.
- Page scripts: `Modulus.onPage(fn)` runs on every page view, `Modulus.page(fn)` for this page only; either may
  return a cleanup that runs when the user leaves. External scripts a fragment brings are skipped when the page
  already has them.

#### Large data

- `TableQuery<T>` (`Modulus.Ui.Abstractions.Data`) turns a `ProTableRequest` into an `IQueryable` the database runs:
  registered sorts and column searches only (anything else in the query string is ignored), order by the sort key then
  the id, `Skip`/`Take` for numbered pages, and a keyset cursor (`WHERE key > v OR (key = v AND id > i)`) for the rows
  after a page. `CountMode.None` skips `COUNT(*)` and reads one extra row to know whether more follow. Nullable keys and
  types SQL can't compare fall back to the cursor's offset. Values go into the query as parameters.
- MVC: `<app-load-more>` (infinite scroll: replaces itself with the next rows when revealed) and `<app-virtual-table>`
  with `modulus-virtual.js` (only the rows in view are in the DOM; server-rendered `<tr>` blocks by page number, cached,
  cancelled when scrolled past, the first block seeded with the page). Blazor uses `Virtualize` over the same query.
- A page's own boost area (`hx-sync="#area:replace"`) cancels a filter or sort request still running when another starts.

#### Pivot

In the manner of Odoo's pivot view, copied as it is: collapsible "Total" headers on both axes, a group-by dropdown on
a closed last-level header (dates offer Year, Quarter, Month, Week and Day), and a toolbar with Measures, Flip axis,
Expand all and Download.

- **Only registered fields reach the query.** `PivotQuery<T>` (`Modulus.Ui.Abstractions.Pivot`) is built like
  `TableQuery<T>`: `Dimension` (text or enum), `Date` and `Measure` (sum, average, min or max, fixed per measure like
  Odoo's `aggregator`; count is built in). `State(query)` drops unknown group-bys and measures.
- **Only what shows is read, as in Odoo.** `Run` issues one `GroupBy` per pair of depths (row depth × column depth)
  that has open headers, filtered to the records under those open headers (the header keys go in as parameters). A
  closed header is one group, however much lies under it, so City → Customer with only Berlin open reads Berlin's
  customers, not every city's. The key is a `PivotKey` with eight text and eight number slots; dates group by numbers
  such as `202407`. The database returns the count and one sum, min or max per measure for each group, so every
  total comes from the database; averages are sum/count. `MaxGroups` (10,000 by default) caps the groups of all the
  queries together, roughly the cells the table would show, and turns a bigger table into a message.
- **The layout is the URL.**
  - `PivotState` holds the group-bys (`rows`, `cols`), the measures, the open headers and the sort.
  - Open headers are paths such as `*/Berlin/2024`, in `rx`/`cx`. *Expand all* records a depth (`ra`/`ca`), and the
    list then names the closed headers, so a group-by added later still opens only its own header.
  - Every control is a link to the next state, so the pivot needs no script: htmx boost (MVC, Razor Pages) or the
    Blazor router follows it, and back and forward step through the layouts.
  - `PivotState.Link` keeps the page's own filters in the query.
- **One renderer.** `PivotHtml` draws the toolbar and the AntD bordered table (column header rows with colspans, row
  headers indented by `--m-depth`). `AppPivotGrid` and `<app-pivot-grid>` both call it. Header dropdowns are
  `details.m-dropdown`; `modulus-shell.js` floats their panel out of the scrolling table (`placeDropdown`).
- **Download** is `PivotXlsx`: SpreadsheetML written into a zip, with no library. It has the same headers (merged),
  indented row labels and bold totals.

#### User state, saved views and field access

- **`IUiStateStore`** (`Modulus.Ui.Abstractions.State`) keeps short per-user strings under keys such as
  `views:/tables/pivot`. The user is `UiUser.Key(principal)`: the name identifier, else the name. Anonymous visitors
  keep nothing, because one key would be shared by all of them. `AddModulusMvc` and `AddModulusBlazor` register
  `MemoryUiStateStore` (in memory, lost on restart, 64 KB per value, 500 keys per user) unless the app registered its
  own store first. A database store is one table keyed by user and key: the samples' `EfUiStateStore`
  (`Modulus.Samples.Shared/SampleUiState.cs`, EF Core on SQLite) is one to copy.
- **Saved views are named query strings** (Odoo's favourites). Modulus pages keep their whole layout in the URL, so
  `SavedViews` stores the canonical query:
  - empty values and the position keys (`page`, `current`, `cursor`, `after`, `before`, `handler`) are dropped;
  - keys are in ordinal order.

  That lets the dropdown mark the view the current URL shows. A page keeps at most 20 views, and one view can be the
  default: a visit whose URL names no layout goes there (`DefaultUrlAsync`; `AppSavedViews` does it with
  `ApplyDefault`).
- **One markup.**
  - `SavedViewsHtml` draws the dropdown.
  - `<app-saved-views>` (MVC) submits a hidden form through `form="…"` attributes, so the dropdown nests no form. It
    posts to `MapModulusSavedViews()`, which validates the antiforgery token, accepts only a local path, and redirects
    back with any error in `views-error`.
  - `AppSavedViews` (Blazor) renders the same DOM with buttons.
- **A Blazor table in the URL.** `ProTable UrlState` writes its page, page size, sort and column searches to the URL
  with the MVC grids' keys (`ProTableUrl`: `current`, `pageSize`, `sortField`, `sortOrder`, `s.{key}`), beside the
  page's own keys (`UrlQuery`, read back in `OnUrlChanged`). Each change adds a history entry, and the table follows
  the URL when it changes from outside (back, forward, a saved view). So a saved view of `/tables/advanced` is the
  same query string in all three samples. The editable grid has no URL state (its layout is a saved schema), so it
  has no Views dropdown.
- **Field access is enforced before the query, not only in the view.** `FieldAccess.Create().Require(field, roles…)`
  (or a test on the principal) and `.For(user)` give a `Func<string, bool>`.
  - It goes to `TableQuery.Restrict` (sorts and searches), `PivotQuery.Restrict` (group-bys and measures; a date
    interval `date:month` follows `date`) and `GridSchema.Allowed` (the columns rendered and read back).
  - A field the URL names but the user may not see is dropped like an unknown one.
  - The sample pivot's *Largest* measure needs the Admin or Editor role, for the page and the download alike.

#### Group by (phase 23)

- **Groups come from the server, a page at a time.** As in Odoo's list view grouped by a field, the server sorts by
  the group field first, so a page holds whole groups or the start or end of one. Each group's row count and totals
  are over all its rows, not just those on the page, read with one `GROUP BY` over the page's group values; the page
  reads one row before and after it to tell whether its first group started earlier ("continued") and whether its
  last ends later (no subtotal row yet). Paging stays the page size the user picked, with no group-level paging.
- **Ant Design's own row grouping is not used:** it groups the rows it has, client-side. ProTable writes the header
  and subtotal rows itself through `RowTemplate`, around Ant Design's `TableRow`, so selection, row detail and
  columns behave as before; a group row never reaches the columns (an `ActionColumn` reading the row is safe).
- **One markup for both flavours** (`TableGroups`): the header and footer cells are the same HTML in Blazor and MVC;
  MVC's toggle is a class on the group's rows (`m-group-collapsed`), so an open row detail keeps its own state.

#### Multi-column sort and edit states (phase 23)

- **One sort list, in the URL.** `ProTableRequest.Sorts` is the first sort (`sortField`, `sortOrder`) followed by
  `thenBy` (`-salary,name`; a minus is descending), at most four. Keeping the first sort in its old keys means old
  links and saved views still mean the same thing, and an MVC action binds `ThenBy` like any other string.
- **One click rule** (`SortKeys.Click`, shared by both flavours): a plain click sorts by that column alone; Shift
  (or Ctrl/Cmd) + click adds it last, turns it, then drops it, as in Excel and AG Grid. Each sorted column shows its
  place by the carets once there are two or more. The MVC header is still a plain link; the Shift+click URL is
  `data-sort-add-href`, which `modulus-shell.js` follows (a modified click on a link would otherwise open a window).
- **The server applies only what it registered.** `TableQuery` orders by each registered sort and then the id (one
  `ORDER BY`), skipping unknown fields. Keyset cursors need a single sort key; several sorts page by offset.
- **Edit states** in the editable and nested grids: changed rows amber with a bar at their start, changed cells
  stronger with a corner mark, added rows blue, the row being edited with a primary bar. A cell error stays red on
  top. The marks live in the page (Blazor state, or classes `modulus-widgets.js` adds) until a save or discard.

#### Multi-level headers (phase 23)

- **Bands are drawn from the shown columns.** A `HeaderGroup` names its column keys and a level; `HeaderGroups.Bands`
  walks the shown columns in order and merges neighbours under the same group, so a band never spans a column it
  doesn't own: hiding one narrows it, a column moved between two of its own splits it, and a band with nothing shown
  is gone. Checkbox, expand, drag and delete columns are untitled cells.
- **Ant Design's column groups are not used:** they nest the column components, so a band would be part of the
  column definitions instead of a layer over them, and the column picker, resizing and saved layouts would have to
  know about it. ProTable and EditableGrid use `HeaderTemplate` instead: the band rows (one per level, outermost
  first) above Ant Design's own header row. ProTable reads its shown columns after the first render (the columns
  register while it renders) and renders the bands again when they change.
- **One markup for both flavours** (`HeaderGroups.Rows`). Each band carries its full span in `data-m-span`, so the
  MVC column picker (`modulus-shell.js`) can narrow or hide it in the browser without a round trip; the picker,
  resizing and sticky headers all read the leaf row (`tr:not(.m-header-bands)`).

#### Checkbox filter menu (phase 23)

- **A pick is a column search, not a new filter model.** The ticked values go into the same `s.{key}` search as
  typed text, as a value list: `=a|b` (only these) or `!=a|b` (all but these), `""` for a blank cell.
  `ColumnSearch.ValueQuery` writes the shorter form, so unticking two of 300 values is a short `!=` and the URL
  stays readable; ticking everything clears the search. Saved views, `ProTableUrl` and the MVC binders needed no
  change.
- **Exact and set-based on the server.** `TableQuery.SearchText` and `DataGridAuto` translate a list to
  `values.Contains(x)` over a boxed `List<string>`, which EF Core sends as `IN` / `NOT IN` with parameters, never as
  `LIKE` or literals. A list is kept whole up to `MaxListLength` (cutting at `MaxSearchLength` would split a value).
- **The icon stays in filter-row mode** for list columns: the filter row's select picks one value, the list several.
- **Alpine fields are declared.** An Alpine component that sets a property it didn't declare writes it on the
  outermost scope, so nested or neighbouring components share it. Every component in `modulus-*.js` declares its
  private fields (timers, listeners, caches) in its data object.
- **The menu survives its own focus.** It focuses with `preventScroll` and adds its close-on-scroll listener two
  animation frames after opening, so the scroll that opened it (a phone scrolling the header into view) doesn't
  close it.

#### Row states (phase 23)

- **The state is data on the row.** `GridRecord.State` (`Editable`, `ReadOnly`, `Locked`) and `StateReason` come
  from the app, derived from the record (a closed period, a posted invoice, another team's row); the grid only
  shows and enforces them. Read-only means the cells are fixed but the row is still the user's to move or delete;
  locked means nothing about the row changes.
- **Enforced in three places.** The UI disables editors, handles and delete buttons; batch Delete and Set value
  skip the rows; and the server holds a post to the store (`GridSchema.Protect`), because a disabled button is no
  guarantee. Blazor runs in-process, so its grid simply refuses the change.
- **Shown without colour alone.** A lock or eye mark in the first cell carries the reason as its tooltip and as
  screen-reader text; colour (muted text, grey or red bar) only repeats it. An empty cell that can't be edited
  reads "—", not the "Add" prompt.
- **Row errors are separate from cell errors.** `GridError.ForRow` has an empty key; it is for rules across cells
  (Due before Start) or the server refusing a row. The rule runs only once the row's cells are valid, so one
  mistake doesn't produce two messages, and in Blazor it re-runs as the row is edited, so the error clears when fixed.

#### The ERP UI map

The ERP suites in §1 were reviewed as feature inventories. This is the UI surface they imply and where Modulus
stands (2026-10-01; PLAN §7.3 has the plan):

| ERP UI surface | Modulus today | Planned |
|---|---|---|
| Data grid: grouping, aggregates, multi-level headers, saved layouts | `ProTable`, `EditableGrid`, `NestedGrid`, column picker, density; single-column sort | Phase 23 grid engine: multi-sort, group headers/footers/aggregates, multi-level headers, checkbox filter menu, row states |
| Pivot analysis | `PivotQuery` / `AppPivotGrid` (§7.1); derived measures (`PivotQuery.Ratio`) | Done (phase 23) |
| Tree / BOM grids | `NestedGrid` (parent → child levels, lazy load); TreeGrid mode (`NestedGridSettings.Tree`): one level repeated to `MaxDepth`, child-count expanders, per-level rollup totals | TreeGrid sample at `/tables/tree` (done) |
| Query builder / advanced filtering | `QueryFilter`, filter row, `ColumnSearch`; `QueryBuilder<T>` with AND/OR groups (phase 24, `TableQuery.Filters` + `ProTableRequest.Where`) | Done (phase 24) |
| Import | `Modulus.Ui.Abstractions/Import` (`CsvReader`, `ImportMapping`, `ImportValidator`, `ImportFlow`); `ImportWizard` (Blazor), `app-import-wizard` (MVC) | Done (phase 25): upload, map columns, check rows, import; the import is the app's callback |
| Scheduler, gantt, kanban | Kanban, Scheduler and Gantt are library components: `KanbanBoard` / `app-kanban` (`Modulus.Ui.Abstractions/Kanban`), `Scheduler` / `app-scheduler` (`Modulus.Ui.Abstractions/Scheduler`) and `Gantt` / `app-gantt` (`Modulus.Ui.Abstractions/Gantt`, read only) | Promoted (P1); Gantt editing (drag to reschedule, dependencies) not yet |
| Lookups | `AppLookup` (type-ahead, "Search more…" dialog) | P1: multi-column, paged dialog |
| Workflow UI | `AppStatusBar`, `WorkflowStage` | P1: `ApprovalTimeline`, history/audit drawer, `StatusBadge` |
| Saved layouts per user | grid/pivot state in the URL; column picker in localStorage | Phase 22 `IUiStateStore` + saved views |
| Printing | `Modulus.Ui.Abstractions/Printing` (`PrintCss.Page`, `PrintTableHtml`, `LabelHtml`); `LabelSheet`, `PrintTable` (Blazor), `app-label-sheet`, `app-print-table` (MVC); print views on the blank layout | Done (phase 26): page setup, repeating table headers, label sheets, invoice print |

Out of scope for the framework, on purpose: report definitions and scheduling, document pipelines beyond phase 21,
and any product data store. The ERP Report Studio reviewed on 2026-10-01 is a separate future solution that will
consume Modulus packages for this UI surface.

#### Component architecture rules

Adapted from the 2.0 proposal; new components follow them.

1. **One renderer, two front ends.** Markup is a static `*Html` class in `Modulus.Ui.Abstractions`; the Blazor
   component and the tag helper are thin adapters, and a test proves they emit the same DOM.
2. **State lives in the URL or in form fields**, not in the component. A control that changes the view is a link or
   a GET form; a control that changes data posts a form.
3. **The server decides what runs.** Fields, sorts, searches and measures are registered; values from the query
   string are checked against them and go into queries as parameters.
4. **Large data stays on the server.** Page, group and aggregate in the query (`IQueryable`); send only what is
   shown, and cap what one request can ask for (`MaxGroups`, page sizes).
5. **AntD DOM and tokens.** Emit Ant Design class names and use the `--m-*` tokens, so a component works in light,
   dark and compact density, and at 390 px.
6. **One `[data-m-*]` root per script widget**, started and torn down by the `modulus-plugins.js` observer, so it
   works after htmx swaps, Blazor renders and SPA navigation alike. Libraries load on first use.
7. **No new dependency without a reason.** Prefer a small piece of Modulus code (like the xlsx writer) to a package,
   and record each vendored library with its licence.

#### Plugins

- **One renderer, two front ends.** Each plugin's markup is a static class in `Modulus.Ui.Abstractions.Plugins`
  (`EditorHtml`, `MapHtml`, `MediaPlayerHtml`, `SortableHtml`, `SpreadsheetHtml`); the Blazor component and the tag
  helper both call it, so they emit the same DOM. `modulus-plugins.js` finds the `[data-m-*]` roots (`data-m-editor`,
  `data-m-map`, `data-m-vector-map`, `data-m-player`, `data-m-sortable`, `data-m-sheet`) and wires them in both
  flavours, after htmx swaps and Blazor renders alike.
- **Libraries load on first use.** Tiptap, MapLibre, Univer, jsVectorMap, pdf.js, Uppy, Cropper.js and CodeMirror are vendored in
  `Modulus.Ui.Assets/wwwroot/lib` and loaded only when a page has a matching root, so pages without a plugin pay nothing.
  Maps, the editor, the spreadsheet and the PDF viewer wrap the library. Tiptap and Univer ship as prebuilt IIFE bundles
  (`lib/tiptap`, `lib/univer`, and `lib/uppy`, `lib/cropper`, `lib/codemirror` for the three new ones), because they are ESM-only. pdf.js ships ESM as it is: its two `.mjs` files are imported
  with `import()` and the worker is set on `GlobalWorkerOptions`. The media player and sortable are Modulus code.
- **Values post as form fields.** The editor writes its HTML into a hidden input (`[data-m-editor-value]`) and raises
  `change`; Sortable writes the order; the spreadsheet writes its sheets as JSON (`[data-m-sheet-value]`, flushed on
  submit) and raises `change` debounced while editing. Servers clean editor HTML with `RichText.Sanitize(images: true)`.
- **The spreadsheet's data model is plain rows.** A sheet is `SpreadsheetSheet(Name, Rows)` with rows of numbers,
  strings, booleans and `"=SUM(…)"` formula strings; `modulus-plugins.js` maps them to and from Univer's cell data
  (a formula is the `f` field without the leading `=`), trimming trailing empty cells and rows. The theme follows the
  app: the sheet starts in `darkBlueTheme` when the shell is dark and switches in place on `modulus:theme`. A
  read-only sheet drops the toolbar, formula bar, header, footer and context menu.
- **Printing the spreadsheet and the document is ours.** Univer's print plugin is Pro (not open source), and the
  browser printing the page would print the canvas: a clipped screenful with the ribbon. So `modulus-plugins.js`
  turns the content into plain HTML in a hidden frame and prints that. A workbook becomes one table per visible
  sheet (the values as the grid displays them, so formula results and number formats; cell styles, borders, merges,
  column widths, hidden rows and columns; the frozen rows repeat on every page; a wide sheet shrinks to the page). A
  document keeps its page size and margins, title and headings, lists with their numbering, tables, links and
  images. The ribbon gets a Print button (a printer icon registered in Univer's icon registry, which its facade does
  not expose), Ctrl+P inside an editor prints it, and `data-m-print="{id}"` prints from any button, read-only
  editors and editors not started yet included (from the JSON in the markup).
- **One sortable engine** moves list items, cards and table rows. On a table (`data-m-sort-rows` around it,
  `data-m-sort-row` on the handle) it drags by the handle only, carries expanded and nested rows with their row, uses
  a one-row table as the ghost and skips hidden rows. Up, Down, Home and End on a focused handle move the row, and a
  live region reads out the move. Blazor gets the move as a `RowMove` and reorders itself (`EditableGrid`,
  `ProTable.OnRowMove`); in MVC the DOM moves, so the grid posts its rows in the new order. Only the page's rows swap
  slots (`GridSchema.Reorder`, `SortMove.Within`), and handles lock while a column sort is on, because a sorted view
  has no order to change.

#### Samples

- One page, three hosts. MVC and Razor Pages render the same page bodies from `Modulus.Samples.Web/Views/Shared/Ui/**`:
  a controller action or a page model binds the input, validates it and picks the view; the view never knows which
  host it runs in. URLs are the same in all three samples, so a page can be compared across them by changing the port.
- Data, validation rules and page logic that Blazor also needs (stores, `WizardInput.Validate`, `EmployeeDirectory`,
  chart series) live in `Modulus.Samples.Shared`, because the Blazor sample does not reference `Samples.Web`.
- Blazor pages reproduce the MVC markup where a component would differ (for example `FieldItem`, which emits the same
  form-item DOM as `app-form-item`), so screenshots of the same URL match.
- Sign-in is a real cookie sign-in against the in-memory sample users (`SampleAuth`: check, principal, safe return
  URL). Blazor posts the form to `/auth/sign-in` through `modulusShell.submit`, because an interactive circuit cannot
  set cookies. `Samples:RequireSignIn` requires sign-in on every page except the auth and error pages.

### 3. Conventions

| Area | Convention |
|---|---|
| Page | `PageContainer` / `app-page-container` with title and one-line subtitle; content is a vertical stack of cards (`m-stack`, 16 px) |
| Lists | Filter card (`QueryFilter`: the 2–3 most used fields visible, the rest under `Advanced` behind `Expand`) above a table card (title left, primary action right, reload/density icons) |
| Create/edit | `DrawerForm` for records with up to about 10 fields; `ModalForm` for short confirmations; a full page for long forms, with `FooterToolbar` |
| Delete | Always confirmed (`Popconfirm` / `hx-confirm` + `data-confirm-danger`); batch delete through the selection bar |
| Feedback | Toast (`message`) for success; inline form errors for validation; `Result` pages for 403/404/500 |
| KPIs | `StatisticCard`: label, headline value, 46 px chart slot, divider, footer metric; trends always carry text, never colour alone |
| Charts | One series per chart, one axis from zero, recessive grid, hover tooltips, a screen-reader table |
| Icons | Ant Design icons only, drawn inline. Page content and navigation name an Ant Design outline icon (`<Ti Name="team" />` / `<app-ti name="team" />`, menu items, apps and links alike; an `ant:` prefix is optional). Ant Design icons newer than the AntDesign package, and the concepts Ant Design has no icon for (Tabler outline icons at a 1.5 stroke, `tb:`), are bundled in `ExtraIconSvg` by `tools/icons/build.sh` |
| Menus | Modules add items through `IMenuContributor`; `Order` 0–99 for app sections, 90+ for settings. In a menu, plain entries come first, then sections |
| Page scripts (MVC) | `Modulus.page(fn)` or `Modulus.onPage(fn)`, returning a cleanup if needed, never `DOMContentLoaded`, so pages work with SPA navigation |

### 4. Roadmap (not built yet)

See [PLAN.md](#plan-and-progress) for status. Not started:

- Hardening: error policy, CSP nonce, compression and caching, observability.
- Quality gates in CI: parity snapshots, Playwright flows, performance budgets.
- Replace `AntIcons` reflection if AntDesign makes its icon store public.
- Modulus 2.0: Spreadsheet (with a widget init/destroy registry), document templates and Document Processing,
  `IUiStateStore` with saved views, and permission-aware columns. See PLAN.md §7 for the decisions and order.
- ERP UI roadmap (adopted 2026-10-01): the grid engine, a QueryBuilder component, an import wizard UI and
  printing components (phases 22–26), then the P1 component tier. Scope rule: UI capabilities only — no product
  data layer, scheduling or document pipelines in this framework. See PLAN.md §7.3.

<!-- source: docs/TEMPLATES_PRODUCTION_READINESS.md -->
## Modulus UI templates: production-readiness status and remaining work

Status: **in progress.** All 141 templates parse, the mvc, razor-pages and blazor pages compile, and the Blazor account pages have been run in a browser against stand-in endpoints (section 3, item 3). The Playwright CSP gate (`tests/browser`) and the layout checks have not been run.
Scope: `src/Modulus.Ui.Templates/templates/` for the four engines (`mvc`, `razor-pages`, `blazor`, `fluid`).

### 1. What changed so far

Security and correctness fixes, applied to both `mvc` and `razor-pages` unless noted:

- **Self-recursive `newRow()`** (`session-manager`): it called itself and overflowed the stack, so the page never loaded. Now uses `document.createElement('tr')`.
- **Un-nonced inline scripts** (`session-manager`, `two-factor`, `forgot-password`, `reset-password`, `permission-matrix`): these are blocked by the framework's enforced CSP. Each script now has `nonce="@Modulus.AspNetCore.Hosting.CspNonce.Of(Context)"` and is wrapped in an IIFE.
- **Inline event handlers** (`two-factor`: `onsubmit="return false"`): removed. Forms prevent submit in JavaScript.
- **Delete without confirmation** (`razor-pages/crud-index`): `onclick="return confirm(...)"` was blocked by the CSP, so Delete submitted with no prompt. Replaced with `data-confirm` on the form and a nonced handler.
- **Missing antiforgery header on `fetch` writes**: the POST and DELETE calls in `session-manager`, `two-factor`, `forgot-password`, `reset-password` and `permission-matrix` now send `RequestVerificationToken`, read from a hidden `#csrf` input rendered with `Antiforgery.GetAndStoreTokens(Context)`.
- **`Html.Raw` on caller data** (`card`, `tabs`): values are now rendered through Razor's normal encoding. Strings are encoded as text, and callers pass an `IHtmlContent` to insert markup.
- **`[Authorize]` added** as `@attribute [Microsoft.AspNetCore.Authorization.Authorize]` (razor-pages and blazor) on every routable page except `forgot-password`, `reset-password` and `entity-form`. `entity-form` is also unprotected in both engines; it is listed in section 3. The `mvc` views cannot carry the attribute, so the controller that renders each view must enforce authorization (see section 4).

Endpoint contracts were checked against `E:\Personal\framework\modulus\src` for the pages that have a backend. These match the real routes and JSON field names:

- `forgot-password`, `reset-password`
- `sessions` (list, revoke, revoke-all) and `login-history`
- `2fa` setup, enable, disable
- `permission-matrix` (`/authorization/grants`)

### 2. Findings that are not template fixes

- **Mock data without a backend.** Most admin and account pages (user admin, role CRUD, tenants, notifications, audit log, profile update, analytics, reports, admin settings) have hardcoded sample data, and some forms POST to pages with no handler (405). The framework has no HTTP endpoints for them. **Decision made:** show honest placeholders, not fake data. This is not yet implemented.
- **Blazor `HttpClient` calls to same-origin paths** (`/account/...`, `/api/...`) — ✅ fixed. In Blazor Server these ran on the server without the user's cookies (and no host registers an `HttpClient`, so injection failed first). The five pages now use `Modulus.Blazor.ModulusHttp`, which makes the call in the browser (see section 3, item 3).
- **`forgot-password` sends the email in the query string** (`POST /account/forgot-password?email=`). The framework's endpoint takes it from the query, so the template matches the server, but the address lands in access logs. Changing it means changing the endpoint to read a JSON body.
- **Blazor `two-factor`, `session-manager`, `permission-matrix`, `forgot-password`, `reset-password`** — ✅ now use `ModulusHttp`, so the cookie and the antiforgery header are sent by the browser.
- **CLI renderer contract.** `E:\Personal\framework\modulus\cli\Services\ScribanRenderer.cs` only replaces camelCase tokens (`{{ entityPlural }}`), while templates use snake_case (`{{ entity_name_plural }}`). The CRUD path uses real Scriban through `TemplateEngine`, which is correct. The legacy `UiFileGenerator` path does not. `UiFileGenerator.cs` also silently skips missing templates (`ApiClient`, `Menu`).
- **`ui-kit.json` maps output files to templates by name.** Entries used to be plain paths, and the CLI guessed the template by substring. The `crud` entries now name their template explicitly (`{ "path", "template" }`), and `template: null` marks a file this path does not make (the PageModels). The CLI still accepts plain paths, so published packages keep working. The `shell`, `auth`, `errors` and `state-store` entries still point at files that do not exist.
- **`CspNonce` requires `Modulus.AspNetCore.Hosting`.** The new auth templates use it, so apps generated from them must reference that package. The `shell` feature lists it for every engine, and the scaffolded Web project gets it from there.
- **Templates are not in a consistent Scriban/verbatim state.** Templates with `{{ }}` placeholders (CRUD, permission matrix, card/crud pages) are Scriban; the rest are copied verbatim. Keep this distinction when adding templates.

### 3. Remaining work

1. **Placeholders for pages with no backend** — ✅ done for `mvc` and `razor-pages` (14 pages each) and for `blazor` (14 pages, including `admin-settings` and `admin-users`, added in item 6). Each page has a "Not connected" alert under its header (`app-alert` in MVC and Razor Pages, the `ant-alert` markup in Blazor), sample rows are replaced with the `m-empty` empty state, statistics show "—" instead of invented figures, unconnected buttons and search boxes are disabled, and forms are wrapped in `<fieldset disabled>` with their sample values removed. `tenant-settings` links "Back to tenants" to `/admin/tenants`. The `razor-pages` versions are the same bodies with their existing `@page` and `@attribute` lines. The Blazor versions no longer call `HttpClient`, so they are not affected by the cookie problem in section 2. Not yet browser-checked: the disabled controls and the 390 px layout.
2. **Blazor stubs** — ✅ done: `role-management`, `tenant-members`, `tenant-list` and `reports-dashboard` now show their unconnected actions as disabled buttons (`title="Not connected yet"`), with no empty handlers and no `href="#"`. `pagination` is left alone: its `href="#"` links call `GoToPage` and prevent default, so they work.
3. **Pages with a backend** — ✅ done for all three engines. `session-manager`, `two-factor`, `forgot-password`, `reset-password` and `permission-matrix` are wired. The Blazor versions use `ModulusHttp` (decision: browser-side `fetch`). `ModulusHttp` (`Modulus.Blazor`, scoped, registered by `AddModulusBlazor`) imports `_content/Modulus.Ui.Assets/js/modulus-http.js`, which calls `fetch` with `credentials: 'same-origin'` and a `RequestVerificationToken` header read from `<meta name="csrf-token">`. `<ModulusHead />` renders that meta through the new `ModulusAntiforgery` component, so hosts need no change. `Modulus.Blazor` now references the `Microsoft.AspNetCore.App` shared framework for `HttpContext`/`IAntiforgery`. JS interop is not available while prerendering, so the pages that load data do it in `OnAfterRenderAsync`. Verified in Chromium against a scratch Blazor Server host with cookie sign-in and stand-in `/account/sessions` endpoints: the list loaded with the user authenticated, Revoke sent the antiforgery header and the server accepted it, and the list refreshed. Bunit tests: `ModulusHttpTests`. Earlier findings that led here: Findings: `forgot-password` and `reset-password` are `[AllowAnonymous]`, so they need no cookie, only a base address. Keep their HTTP calls: the rate limiter sits on those endpoints. `session-manager`, `two-factor` and `permission-matrix` are `[Authorize]`. Calling the identity services in-process (`IUserSessionService`, `IUserTwoFactorService`, both scoped) is not safe for everything: 2FA disable checks the password through `SignInManager<TUser>`, which the template cannot name, and revoke-all signs the user out, which a Blazor circuit cannot do. The `IHttpContextAccessor` forwarding idea is unreliable inside a circuit. The clean option is browser-side `fetch`, which sends the cookies on same-origin calls; that is what was built.
4. **`razor-pages/entity-form`** — ✅ done: it is now a form partial with no `@page`, so `@model {{entity_name}}Dto` is valid. It is not referenced by any manifest or CLI path in this repo, so it is an orphan until a page renders it. `blazor/entity-form` is routable and now has `[Authorize]`.
5. **`mvc` CRUD** — decided by the CLI, not done: `modulus ui create-crud` refuses `mvc` ("The MVC engine has no CRUD pages yet"), so no `mvc` `crud-*` templates are added. Revisit only if the CLI guard is removed.
6. **`blazor` parity** — ✅ done: `admin-settings` (`/admin/settings`) and `admin-users` (`/admin/users`) are added as placeholders matching the mvc and razor-pages versions. Both are `[Authorize]`.
7. **`fluid` parity** — deferred: the CLI refuses to scaffold fluid (`UiScaffold.ResolveEngine`: "their Liquid placeholders clash with the template syntax"), so the missing templates are not needed yet. Adding them waits for the Liquid/Scriban decision. Checked with Fluid.Core 2.40.0: all 8 `fluid/*.liquid.sbn` bodies parse as Liquid. One failed only because its usage comment contained a `{% include %}` example; that comment is reworded. Still broken: `shared/table.sbn` (uses `&&`, which Liquid lacks) and `shared/form-input.sbn` (uses Scriban's `string.downcase` inside `{{ }}`, and its comment has tag syntax). Neither is valid Liquid or Scriban as written, and nothing includes them. The `.sbn` extension also makes the CLI render them as Scriban. Fix them, or delete them, once the decision is made.
8. **`ui-kit.json`** — ✅ done. The `crud` entries name their template explicitly (`{ "path", "template" }`), and the CLI reads that form (`UiFileGenerator.ReadFileEntry`). This also fixed a bug: the legacy path rendered `crud-index.cshtml.sbn` into `Index.cshtml.cs`, covered by `GenerateAsync_RazorPages_WritesViewWithExplicitTemplate_AndNoPageModelContent`. The four unused features were removed, because nothing read them: the CLI reads only `shell` (`UiFrameworkPackages`). `auth`, `errors` and `state-store` pointed at templates that do not exist. Their `Modulus.AspNetCore.Hosting` need is met by `shell`, which every engine's scaffold uses. All 523 CLI unit tests pass. Still open: the `api-client.cs.sbn` and `menu-contributor.cs.sbn` templates do not exist (those files are skipped, as before). The `notifications`, `tenant-management` and `user-management` features are unread by the CLI too; they are out of this decision and not yet reviewed.
9. **Docs** — ✅ done: the README's CRUD example used `modulus generate-crud --ui-engine mvc`, but `generate-crud` has no such flag and reads the engine from `.modulus.json`. It now shows `modulus ui create-crud` (the supported path, `razor-pages` and `blazor` only). `ui-kit.json`'s `create-crud` was already correct.
10. **Verification** — partly done: a Scriban parse of all 139 `.sbn` files passes (141 after item 6). It found one real error, `shared/form-fields.sbn` (a nested `{{ include }}` inside its header comment). A Razor compile of 118 mvc, razor-pages and blazor pages, against the framework 0.9.0 packages from the NuGet cache, has 0 errors. It found two real errors, now fixed: an unescaped `@media` in `<style>` (mvc and razor-pages `user-profile`, now `@@media`), and `Context` in six razor-pages files, which do not exist on Razor Pages (now `HttpContext`, which `CspNonce.Of` and `GetAndStoreTokens` take). The 13 Scriban-only pages (CRUD, permission matrix, entity form, module home) rendered with sample values fail only on generated types (`ProductDto`, `Catalog`, `ProductApiClient`). The full check is the CLI's `CliScaffoldBuildTests` (packs the framework and builds a scaffolded app). It needs network and a full pack, so it runs in CI. Liquid parse: 8 of 8 fluid bodies pass; the two `shared` files do not (see item 7). Not done: a Playwright run with the CSP enforced (the `tests/browser` gate).

**Still open after the latest pass:** item 10 (Playwright run, and the two `shared` fluid files). Items 5 and 7 are deferred by the CLI, and item 9 is done. Also found in this pass: an unescaped `@media` inside `<style>` in `mvc` and `razor-pages` `user-profile` was parsed as Razor code. It is now `@@media`. Razor compile is still needed to confirm no other cases exist.

### 4. Known limits of the current fixes

- `mvc` views rely on their controller for `[Authorize]`, which the templates do not generate.
- The Blazor templates that call `/account/...` need those endpoints on the same origin as the page, and the host must map antiforgery validation for writes (the browser sends `RequestVerificationToken`). In a `webapp+api` app the Web host forwards only the recovery routes (see `docs/DOCUMENTATION.md#modulus-cli-ui-commands`, known issues). Not run against the real Modulus.Identity endpoints, only against stand-ins with the same routes.
- The `#csrf` input is rendered per page. It works only if the page is served through `Antiforgery`-enabled MVC or Razor Pages.
- The `permission-matrix` role list and API prefix are substituted by the Scriban generator. A role name containing a quote would break the generated JavaScript; the generator should escape it.

### 5. Admin dashboard starter

The `admin` feature in `ui-kit.json` maps 14 working admin pages to output paths for mvc, razor-pages and
blazor, plus `admin-controller.cs.sbn` for MVC. Verified: all templates parse and render with Scriban, compile in scratch hosts, and the
flows run (RP and MVC over HTTP, Blazor in Chromium). Menu: `AddModulusAdminUi()` (tested in `AdminModuleTests`). Not done: CLI commands that
read the `admin` feature (the `modulus` repo, untouched), adapters from the seam interfaces to the real `Modulus.Platform` stores, and
unit tests for `SampleAdminData`.

### 6. Components added for production readiness

| Component | Blazor | MVC and Razor Pages | Notes |
| --- | --- | --- | --- |
| Tag input | `AppTagInput` | `<app-tag-input asp-for="Labels" max="5" />` | Free-form tags in one box. Enter or a separator (`,;` by default) adds a tag, Backspace in the empty field removes the last, a paste is split at the separators. MVC posts one hidden input per tag, so `List<string>` and `string[]` bind back. Both flavours use `Forms.TagInput` (Abstractions) for the rules (trim, no case-insensitive duplicates, `max`, `max-length`); `modulus-shell.js` mirrors them in the browser. Raises `m-tags-change` in MVC. |
| Popconfirm | `AppPopconfirm` (AntD) | `<app-popconfirm title="Delete?" ok-danger hx-delete="/x">…</app-popconfirm>` | An inline question on a control. Attributes other than `class` and `style` go on the OK button (htmx, `form`, `formaction`), so `id` goes there too. OK also raises `modulus:popconfirm-ok`. For a modal question use `hx-confirm`, which the framework turns into its own dialog. |
| Time picker | `TimePicker` (AntD) | `<app-time-picker asp-for="Opens" format="HH:mm" />` | The same control as `app-date-picker picker="time"`. |

Also fixed: the Blazor sample did not build (two pages named like components, `Gantt` and `DatePicker`, and a boolean `ShowIcon` with no value). The pages are now `GanttDemo` and `DatePickerDemo` and keep their routes.

CLI (the `modulus` repo, branch `claude/blissful-cori-5q9txc`): `ui add-component` now lists `tag-input`, `popconfirm` and `time-picker` (81 components). Their templates are in `templates/mvc`, `razor-pages` and `blazor`, and ship in the next Modulus UI release; until the CLI's pinned `DefaultVersion` (1.8.2 at the time of writing) is raised to that release, `--all` skips them and naming one says which version to use. `Blazor` tag input is a controlled field, so keystrokes typed faster than the server round trip can be dropped in Blazor Server; normal typing is not affected.

### 7. Advanced visuals (radar, candlestick, Sankey, org chart)

Four components that the paid suites (Syncfusion, DevExpress, Telerik) sell and ERP screens need. They follow the other charts: one renderer in `Modulus.Ui.Abstractions.Charts.ChartHtml` that Blazor, MVC and Razor Pages all call, so the markup is identical. Server-rendered HTML and SVG, hover tips and layout in CSS, no JavaScript, a screen-reader table (the org chart is a nested list), light and dark mode from the same tokens.

| Visual | Blazor | MVC and Razor Pages | Data | Use it for |
| --- | --- | --- | --- | --- |
| Radar | `RadarChart Axes Series Max` | `<app-radar-chart axes series max>` | `ChartSeries` (3 or more axes) | Supplier scorecards, skills, KPIs per plant |
| Candlestick | `CandlestickChart Candles` | `<app-candlestick-chart candles>` | `Candle(Label, Open, High, Low, Close, Volume)` | Prices, FX, commodities, min / max ranges; a volume strip appears when any volume is set |
| Sankey | `SankeyChart Flows` | `<app-sankey-chart flows>` | `SankeyLink(From, To, Value)` | Material flow, budget allocation, cash from revenue to profit |
| Org chart | `OrgChart Nodes` | `<app-org-chart nodes>` | `OrgNode(Id, ParentId, Title, Subtitle, Badge, Href)` | Reporting lines, cost centres, chart of accounts, bill of materials |

Behaviour worth knowing:

- **Sankey** puts each node in the column of its longest path and gives every column one vertical scale, so equal flows are equally thick. Zero and negative flows are dropped, and so is the one link that closes a loop (the rest of the graph stays). The parameter is `Flows`, not `Links`, because `ChartBase` already has a `Links` drill-down parameter.
- **Org chart** treats a node whose `ParentId` is null or unknown as a root (so a forest draws several trees), draws each node once even if the data has a loop, and shows the number of direct reports. `Href` accepts relative, `http` and `https` URLs only; anything else (`javascript:`, `data:`, `//host`) is dropped. Text is always encoded.
- **Candlestick** hugs the data rather than starting at zero, and marks "up" (close at or above open) with the blue and "down" with the red of the diverging pair, so the meaning does not rest on red and green.
- Release note: the four chart pages and three component templates are new, and `otp-input` and `textarea` (Blazor) gained an `aria-label` so the CLI's accessibility audit passes. The CLI's `DefaultVersion` has to move to the release that contains them.
- The CLI templates are `chart-radar`, `chart-candlestick`, `chart-sankey` and `chart-org` (the pages `modulus ui add-chart --type radar|candlestick|sankey|org` scaffolds) in `templates/mvc`, `razor-pages` and `blazor`. The `modulus` repo's `ui add-component` list needs the four names added.
- `modulus-pages.css` grew by about 540 bytes gzipped, and its budget in `tests/browser/budgets.json` was raised on purpose (29,696 to 33,792).

Not built, and why: a free-form node-and-edge **diagram editor** (drag, connect, route) and a **Gantt resource view with dependencies drawn as arrows** need an interactive canvas, which is a product of its own rather than one more chart. Rich **stock charts** (indicators, zoom, crosshair sync) are the same. The **icon sprite** and other items are in section 8.

### 8. Performance

Measured with Chromium (Playwright) against the samples on the same machine; Debug numbers include JIT and unminified assets, so judge production by the Release rows.

| Page (MVC sample) | Debug, cold | Release publish, cold |
| --- | --- | --- |
| Dashboard first load, transferred | 1,981 KB | **483 KB** (document 50 KB, styles 191 KB, scripts 147 KB, font 96 KB) |
| First contentful paint / largest contentful paint | 1,648 / 1,740 ms | **156 / 224 ms** |
| Total blocking time | 97 ms | **1 ms** |

What the framework already does, and what to keep doing when you add pages:

- **Pre-compressed assets.** `MapStaticAssets` serves scripts and styles pre-compressed at publish time (Brotli: `modulus.css` 222 KB raw, 35 KB sent). Use `app.MapStaticAssets()`, not `UseStaticFiles()`, and always check performance on a `-c Release` publish: a Debug build stores gzip uncompressed.
- **Versioned, immutable assets.** `_content` files requested with `?v=` get `Cache-Control: public, max-age=31536000, immutable`. The Inter font is preloaded, scripts are `defer`red, and htmx / Alpine.js are served locally.
- **Fast navigation (MVC and Razor Pages).** `SpaNavigation` swaps only the page body with a view transition; a link the pointer rests on for 65 ms (or touches) is prefetched and used once within 30 s; loaders appear only after 120 to 300 ms, so quick loads do not flicker. Turn it on in `ModulusThemeOptions`.
- **Server paging and virtual rows.** `ProTable` pages on the server (`RemoteDataSource`); the MVC large-table helpers use `modulus-virtual.js`. Do not load every row to the page: page, filter and sort in the query.
- **Size budgets.** `python tests/browser/budgets.py` fails when a script or stylesheet grows past its gzip budget.

Changed for performance:

- **Dynamic compression level.** `ModulusHardeningOptions.CompressionLevel` defaults to `Optimal` for Brotli and gzip. The framework default (`Fastest`) sent the 470 KB dashboard page at 115 KB (Brotli) / 97 KB (gzip); `Optimal` sends 50 KB / 81 KB for about 2 ms more CPU.
- **Document-wide observer.** The select/lookup placement observer ran a subtree query for every class change on the page; on the Blazor components page it cost 87 ms of script at load. It now finds the few select roots once per batch and compares nodes with `contains`, and does nothing when the page has none.
- **Scroll and resize handlers.** Placing open selects, the sticky gallery nav and the back-to-top buttons used document-wide selector queries on every scroll event. They now use live `getElementsByClassName` collections (or a cached list refreshed when the DOM changes) and run at most once per frame.
- **`dotnet publish` of the samples failed** (NETSDK1152: the Mvc library and the sample views both published `Views/_ViewImports.cshtml`). Views are compiled into the assemblies, so `.cshtml` sources are no longer copied to publish output.
- **ReadyToRun.** The three admin samples set `PublishReadyToRun` and `TieredPGO` when you publish with a runtime identifier (`dotnet publish -c Release -r linux-x64`). First requests got 32 to 44 % faster (560 to 379 ms, 746 to 419 ms, 290 to 188 ms), warm ones 47 to 34 ms. Do the same in your app.

Deployment checklist:

1. Publish with `-c Release -r <rid>` (ReadyToRun) and serve with HTTP/2 or HTTP/3 behind your proxy or CDN.
2. Keep response compression on. `EnableForHttps` is off by default (BREACH); if TLS ends at Kestrel and no proxy compresses, pages go out uncompressed, so either compress at the proxy (preferred) or turn it on after weighing BREACH for your pages: they carry an antiforgery token, which ASP.NET Core encrypts afresh for each response, but that is your call to make, not a guarantee this framework gives.
3. Cache `/_content/*?v=*` at the CDN for a year; never cache the HTML (`no-store` is what the pages send).
4. Blazor Server: keep the circuit near the user, raise `MaximumReceiveMessageSize` only if editors need it, and prefer server paging over large in-memory lists.
5. Measure after each change with `tests/browser/budgets.py` and a Release publish.

Known limits and ideas not done:

- A dashboard page is about 470 KB of HTML (5,000 DOM nodes). More than half is inline SVG, and 132 KB of that is the same icons repeated. A `<symbol>` / `<use>` sprite would shrink the DOM and the document, but it changes the markup the CSS, the scripts and both flavours' tests rely on, so it is a separate change. Compression already removes most of it from the wire (about 5 to 1).
- The Ant Design stylesheet (563 KB raw, 54 KB sent) is render-blocking on every page, and the Ant Design Blazor script spends about 100 to 200 ms in `getBoundingClientRect` on pages with many components; both are upstream.
- The Blazor tag input is a controlled field, so keys typed faster than the server round trip can be dropped.
- Minifying the Modulus scripts and styles was not done: with Brotli it would save an estimated 15 to 20 % of about 100 KB, which does not justify a build step.

<!-- source: docs/PLAN.md -->
## Plan and progress

The plan for the Modulus UI admin template and where each piece stands. Update the status tables in the same commit
as the work they describe.

Last updated: 2026-10-12 · release gates green: Release build 0 warnings, 1105 tests, smoke and parity gates, budgets, and a route sweep of 105 pages × 3 apps at 390 px, 1280 px and dark (864 loads; the one failure left is an external-tile timeout, see the change log) · phases 20 and 22 checked in the browser; phase 24 (QueryBuilder) built and browser-checked on MVC and Blazor, with the 390 px and dark-mode checks done; phase 25 (import wizard) built and browser-checked in all three samples; phase 23 (grid engine) done: multi-column sort, group-by, multi-level headers, the checkbox filter menu, row states, the ERP batch toolbar, TreeGrid mode (sample at `/tables/tree` in all three) and pivot derived measures, plus print for the spreadsheet and document · **resume here:** phase 26 printing components (see the resume note under §3) · the Modulus 2.0 roadmap is in §7, the ERP UI roadmap in §7.3

**Legend:** ✅ done and verified · 🟡 in progress · ⬜ not started · ❌ failing · ➖ not applicable

### 1. Goal

A complete admin template in the style of [Tabler](https://tabler.io/admin-template) and
[Vuexy](https://pixinvent.com/vuexy-bootstrap-html-admin-template/). It ships as three sample apps that render the
same pages at the same URLs, on the same design tokens:

| Sample | Project | Port |
|---|---|---|
| Blazor (Interactive Server) | `samples/Modulus.AdminTemplate.Blazor` | 5100 |
| ASP.NET Core MVC | `samples/Modulus.AdminTemplate.Mvc` | 5101 |
| Razor Pages | `samples/Modulus.AdminTemplate.RazorPages` | 5102 |

**Scope:** dashboards, apps, pages and auth, UI components, forms, tables and charts, on the Tabler shell and design.

**Definition of done for a page:**
- It works in all three samples.
- Screenshots of the same URL match between Blazor and MVC (see §5).
- Its interactions pass a flow test.
- It is clean at 390 px width and in dark mode.

### 2. Architecture (summary)

Details are in [DESIGN.md](#modulus-ui--design-notes).

- **Shell:**
  - `ShellRenderer` (in `Modulus.Ui.Abstractions`) renders the Tabler shell: header, search, notifications, navbar or
    sidebar, footer and settings panel.
  - Blazor wraps it in `AppShell`; MVC and Razor Pages wrap it in `_ModulusLayout`.
- **Page bodies:**
  - MVC and Razor Pages share the page views in `samples/Modulus.Samples.Web/Views/Shared/Ui/**`.
  - Controllers and page models only bind input, validate it and pick the view.
- **Blazor pages** reproduce the same markup, with AntD components where they render identical DOM.
- **Shared code:** data, validation and page logic used by all three samples live in `Modulus.Samples.Shared`.
- **Styling and icons:**
  - Styling uses AntD v4 CSS with a refresh layer: `modulus.css`, `modulus-pages.css` and `modulus-shell.css`.
  - Icons are Ant Design icons drawn inline (`app-ti` / `Ti` take an Ant Design icon name; `tb:` Tabler outline names for what Ant Design lacks).

### 3. Phases

| # | Phase | Status |
|---|---|---|
| 1 | Library rebuild on AntDesign 1.6.2: `App*` aliases, Pro components, theming, drift check | ✅ |
| 2 | MVC tag helpers emitting AntD DOM (90+), htmx and Alpine behaviour | ✅ |
| 3 | Tabler shell shared by Blazor and MVC (`ShellRenderer`) | ✅ |
| 4 | Razor Pages sample and the shared `Samples.Web` view library | ✅ |
| 5 | Dashboards | ✅ |
| 6 | Apps | ✅ |
| 7 | Pages, auth and status pages | ✅ |
| 8 | UI components, cards, charts | ✅ |
| 9 | Forms and data table | ✅ |
| 10 | Cross-sample parity, mobile and dark-mode sweeps | ✅ |
| 11 | Tests, docs, screenshots, Release build with 0 warnings | ✅ |
| 12 | Repository published to GitHub, CI green | ✅ |
| 13 | Follow-up work (§6) | 🟡 |
| 14 | Pro: M1 SPA navigation (MVC, Razor Pages) — `SpaNavigation`, fragment layout, `modulus-spa.js` | ✅ built and checked in the browser on MVC and Razor Pages (2026-10-09): a menu click swaps the page inside the same document, the title and URL follow, back and forward work, no console errors |
| 15 | Pro: M2 large data — `TableQuery` pipeline (keyset cursors, optional count), `app-load-more`, `app-virtual-table`, `/tables/large` | ✅ checked in the browser (2026-10-09): MVC and Razor Pages infinite scroll grows the rows and the virtual table keeps about 32 rows in the DOM after a far scroll; Blazor's Virtual mode keeps 35 and shows data at the scrolled position. Gap: Blazor has no infinite-scroll mode (Pages and Virtual only) |
| 16 | Pro: M3 Fluid (Liquid) — `Modulus.AspNetCore.Fluid`, tag expander, Liquid layout, sample on 5103 | ✅ `ModulusLiquid` (Fluid.Core 2.40.0, escaped output, `LiquidHtml`, page layout, file cache), tags `modulus_head`, `icon`, `barcode`, filters `status_badge` and `money`, `StatusBadgeHtml` in Ui.Abstractions; sample `samples/Modulus.Samples.Fluid` on 5103 (`/` and `/invoices/{id}`); 13 tests; browser-checked at 1280 and 390 px, no console errors (2026-10-09) |
| 17 | Pro: M4 hardening — error policy, CSP nonce, compression and caching, observability | ✅ `Modulus.AspNetCore.Hosting`: Brotli and gzip (not over HTTPS, BREACH), security headers, the CSP with a nonce (enforced on MVC and Razor Pages, report-only on Blazor: its import map has no nonce), `/health`; versioned asset URLs (`?v=`) cached for a year. Error policy: the 404 page and htmx status codes checked (2026-10-09). The 500 page checked on 2026-10-09 in Production: MVC, Razor Pages and Blazor return the Modulus "Server error" page with no exception text or stack; Fluid had no exception handler (an empty 500); it now has one, `Templates/error.liquid`, checked in Production the same way (2026-10-09): the Modulus "Server error" page, no details. Observability: `AddModulusObservability()` (OpenTelemetry 1.19, Apache-2.0): ASP.NET Core traces (no `/health`, `/_content`, `/_framework`, `/_blazor`), request metrics, logs; OTLP only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set; on all four samples. Checked on the Fluid sample (5103) against a local OTLP listener (2026-10-09): traces hold the invoice request and no `/health`, logs and metrics arrive; with no endpoint, no export and no errors. The 500 page is checked in all four samples. 15 tests |
| 18 | Pro: M5 quality gates — parity snapshots, Playwright flows, performance budgets in CI | ✅ `tests/browser`: `smoke.py` (18 pages per app (`/forms/elements` added) answer 200, no console errors, no CSP violation beyond Blazor's import map, compression, hardening headers, `/health`, SPA navigation), `parity.py` (the pivot markup, 83,811 characters, identical in MVC, Razor Pages and Blazor), `budgets.py` (gzipped Modulus assets within budget, in the build job); CI job `browser` starts the four samples and runs the gates. The CI job has not run on GitHub yet: the gates pass locally. Visual snapshots are not in (parity is markup, not pixels) |
| 19 | 2.0: PivotGrid in the manner of Odoo's pivot view — `PivotQuery`/`PivotState`/`PivotResult`/`PivotHtml`/`PivotXlsx`, `AppPivotGrid`, `app-pivot-grid`, `/tables/pivot` (verified 2026-10-02; see §7.1) | ✅ |
| 20 | 2.0: Spreadsheet and document editor on Univer 1.0.3 (open-source presets), with the widget init/destroy registry; xlsx and docx in and out via `Modulus.Documents` (§7.2) | ✅ checked in the browser × 3 (2026-10-04) |
| 21 | 2.0: Document templates on Fluid (after phase 16), then Document Processing: PDF, Word and Excel output (§7.2) | ✅ Liquid template → HTML → blocks → PDF or docx (`DocumentPdf.WriteHtml`, `DocumentDocx.WriteHtml`, `HtmlBlocks.FromHtml`); mail merge as one PDF (`DocumentPdf.WriteMerged`); Excel was already in place (ClosedXML). Checked on the Fluid sample (5103): `/documents/invoices/4988.pdf` (one page), `.docx` (text read back), `/documents/mail-merge.pdf` (48 invoices, 48 pages); and a PDF button on the document page in the three admin apps, each POSTed (contract, one page). Tests: 12 new (`DocumentPdfTests`, `HtmlBlocksTests`). Not clicked in a browser: the new button. |
| 22 | 2.0: User state — `IUiStateStore`, saved views (data table and pivot layouts, ProTable `UrlState`), a database store sample, permission-aware columns (§7.2) | ✅ checked in the browser × 3 (2026-10-04) |
| 23 | 2.0: Grid engine — multi-column sort, group-by with group headers, footers and aggregates, multi-level headers, Excel-style checkbox filter menu, row states (dirty, error, locked), ERP batch toolbar (Approve / Reject / Print), TreeGrid mode on `NestedGrid` (self-referencing rows: BOMs, chart of accounts), pivot UI extensions (§7.3) | ✅ multi-column sort, the edit-state colours, group-by and multi-level headers (2026-10-04), the checkbox filter menu, row states and the ERP batch toolbar (2026-10-05), TreeGrid mode (library, tests, sample page `/tables/tree` in all three, 2026-10-09), pivot derived measures (2026-10-09) | ✅ |
| 24 | 2.0: QueryBuilder UI component — visual WHERE builder over registered fields, one serialisable filter model feeding `ColumnSearch` / `TableQuery` (§7.3) | ✅ `Modulus.Ui.Abstractions/Query` (model, `QueryBuilder<T>`, form, renderer), `TableQuery.Filters` + `ProTableRequest.Where`, `QueryBuilder` (Blazor), `app-query-builder` (MVC), `/tables/query` in all three; 21 + 2 tests; the 390 px and dark-mode checks are done (the query table now scrolls inside its card at phone width) |
| 25 | 2.0: Import wizard UI — upload, column mapping, validation preview and commit as components, the import itself in a server callback (§7.3) | ✅ `Modulus.Ui.Abstractions/Import` (`CsvReader`, `ImportMapping` with header suggestions, `ImportValidator`, `ImportFlow` steps, `ImportWizardHtml`), `ImportWizard` (Blazor, `InputFile`), `app-import-wizard` (MVC, a step per post), `/tables/import` in all three; 18 library + 6 bUnit + 3 MVC tests; browser-checked on all three |
| 26 | 2.0: Printing components — `@page` setup, repeated table headers, grid, invoice and label print styles (§7.3) | ✅ `Modulus.Ui.Abstractions/Printing` (`PrintCss.Page`, `PrintTableHtml`, `LabelLayout`, `LabelHtml`), `LabelSheet` / `PrintTable` (Blazor), `app-label-sheet` / `app-print-table` (MVC), print views at `/tables/print/sheet` and the overview `/tables/print` in all three; 9 library + 2 bUnit + 2 MVC tests; PDF output checked (page counts, repeated header, total once) |

**Resume note (2026-10-12).** Phases 1 to 26 are complete and the release gates are green (§8, 2026-10-12). Remaining: the 🟡 items in §3–§6 need a GitHub CI run, and the Pro items in §6 are written but not yet browser-swept.

**Resume note (2026-10-09, earlier).** Phases 23 to 26 are complete, so P0 is done. The first two P1 items are done: Kanban
(`Modulus.Ui.Abstractions/Kanban`: Blazor `KanbanBoard`, MVC `app-kanban`) and the Scheduler (`Modulus.Ui.Abstractions/Scheduler`:
Blazor `Scheduler`, MVC `app-scheduler`) and the Gantt timeline (`Modulus.Ui.Abstractions/Gantt`: Blazor `Gantt`, MVC
`app-gantt`; read only), and the PDF viewer (pdf.js 6.3.289 in `lib/pdfjs`; `AppPdf` / `app-pdf`), and the file
manager (`Modulus.Ui.Abstractions/Files`: Blazor `FileManager`, MVC `app-file-manager`) and the dashboard layout
(`Modulus.Ui.Abstractions/Dashboard`: Blazor `DashboardGrid`, MVC `app-dashboard`). Barcode and the workflow kit are done too (§8). The multi-column lookup is done too (§8). Rich-text tables and the dynamic form (`FormSchema`, 2026-10-09, §8) are in too, so P1 in §7.3 is complete. Cropper, CodeMirror and Uppy (open from phase 23) are in too, 2026-10-09 (§8). Phases 20 and 22 were checked in the browser on 2026-10-04.

### 4. Page inventory

Every route below exists in all three samples. Status columns: **B** Blazor · **M** MVC · **R** Razor Pages.

#### Dashboards

| Page | Route | B | M | R |
|---|---|---|---|---|
| Analytics | `/` | ✅ | ✅ | ✅ |
| CRM | `/dashboards/crm` | ✅ | ✅ | ✅ |
| eCommerce | `/dashboards/ecommerce` | ✅ | ✅ | ✅ |
| ERP analytics | `/dashboards/erp` | ✅ | ✅ | ✅ |

#### Apps

| Page | Route | B | M | R |
|---|---|---|---|---|
| Email (list, read) | `/apps/email`, `/apps/email/{id}` | ✅ | ✅ | ✅ |
| Packing list templates (merchandising, RMG ERP): four steps, the Excel editor with the Insert ERP Field panel, the template list, a preview filled from a PO with validation, the generate form (Excel or PDF) and the output; the download fills the template (`PackingListDemo`) | `/merchandising/packing-lists`, `/merchandising/packing-lists/download` | ✅ | ✅ | ✅ |
| Chat | `/apps/chat`, `/apps/chat/{id}` | ✅ | ✅ | ✅ |
| Calendar (month, agenda, category filter, event form) | `/apps/calendar` | ✅ | ✅ | ✅ |
| Kanban (drag, move, add, delete) | `/apps/kanban` | ✅ | ✅ | ✅ |
| Gantt (read-only project timeline) | `/apps/gantt` | ✅ | ✅ | ✅ |
| Invoices (list, preview, print, add, edit) | `/apps/invoice/*` | ✅ | ✅ | ✅ |
| Users (list, view) | `/apps/users/list`, `/apps/users/view/{id}` | ✅ | ✅ | ✅ |
| Users grid (cards, role filter, search) | `/apps/users/grid` | 🟡 | 🟡 | 🟡 |
| Roles | `/apps/roles` | ✅ | ✅ | ✅ |
| Permissions | `/apps/permissions` | ✅ | ✅ | ✅ |
| Customers (CRUD, detail) | `/apps/customers`, `/apps/customers/{id}` | ✅ | ✅ | ✅ |
| eCommerce products | `/apps/ecommerce/products` | ✅ | ✅ | ✅ |
| eCommerce orders (list, detail) | `/apps/ecommerce/orders`, `/apps/ecommerce/orders/{id}` | ✅ | ✅ | ✅ |

#### Pages, auth and status

| Page | Route | B | M | R |
|---|---|---|---|---|
| Profile | `/pages/profile` | ✅ | ✅ | ✅ |
| Account settings | `/pages/account-settings` | ✅ | ✅ | ✅ |
| Pricing | `/pages/pricing` | ✅ | ✅ | ✅ |
| FAQ | `/pages/faq` | ✅ | ✅ | ✅ |
| Help center (home, article) | `/pages/help-center`, `/pages/help-center/{slug}` | ✅ | ✅ | ✅ |
| Changelog (release timeline, change kinds) | `/pages/changelog` | 🟡 | 🟡 | 🟡 |
| Sign in / register (sign in and sign out are real cookie sign-ins, see `SampleAuth`) | `/auth/login`, `/auth/logout`, `/auth/register` | 🟡 | 🟡 | 🟡 |
| Forgot / reset password | `/auth/forgot-password`, `/auth/reset-password` | ✅ | ✅ | ✅ |
| Verify email / two steps | `/auth/verify-email`, `/auth/two-steps` | ✅ | ✅ | ✅ |
| 404 / 403 / 500 | `/misc/not-found`, `/misc/not-authorized`, `/misc/server-error` (plus real 404 and error handling) | ✅ | ✅ | ✅ |
| Maintenance / coming soon | `/misc/maintenance`, `/misc/coming-soon` | ✅ | ✅ | ✅ |

#### UI, forms, tables and charts

| Page | Route | B | M | R |
|---|---|---|---|---|
| Component gallery (every AntD category) | `/ui/components` | ✅ | ✅ | ✅ |
| Cards | `/ui/cards` | ✅ | ✅ | ✅ |
| Charts | `/ui/charts` | ✅ | ✅ | ✅ |
| Icons (every Ant Design icon, three themes, search, copy markup) | `/ui/icons` | 🟡 | 🟡 | 🟡 |
| Typography | `/ui/typography` | 🟡 | 🟡 | 🟡 |
| Colours (theme tokens, Ant Design palette, copy value) | `/ui/colors` | 🟡 | 🟡 | 🟡 |
| Form layouts | `/forms/layouts` | ✅ | ✅ | ✅ |
| Form elements (every control on the Tabler form-elements page: text, password, disabled, readonly, rounded, flush, icons, sizes, search with button, add-ons (text, HTML link and shortcut hint), validation states, floating label, loaders in the field, textarea, select, datalist, switches, radios and checkboxes (row and stacked), checkbox with description, range, date, date and time, time (Blazor), colour, file, horizontal form and footer). Every control on the Tabler page has a Modulus control in both flavours; the MVC time input is the browser's native `<input type="time">` (the Blazor one is AntD's `TimePicker`) | `/forms/elements` | ✅ | ✅ | ✅ |
| Wizard (per-step validation) | `/forms/wizard` | ✅ | ✅ | ✅ |
| Validation showcase | `/forms/validation` | ✅ | ✅ | ✅ |
| Business fields (money, quantity, duration, range, phone, mask, one-time code, lookup, status bar, rich text, signature) | `/forms/business` | ✅ | ✅ | ✅ |
| Dynamic form: a whole form drawn from a JSON `FormSchema` (text, multiline, number, money, date, choice, switch), checked on the server | `/forms/dynamic` | ✅ | ✅ | ✅ |
| Data table (search, filters, sort, paging, columns, selection, CSV) | `/tables/advanced` | ✅ | ✅ | ✅ |
| Editable grid (schema-driven columns, in-place editors, expandable rows, pinned columns, add/remove columns) | `/tables/editable` | ✅ | ✅ | ✅ |
| Nested grid (lazy child levels, per-level columns, 1–4 levels) | `/tables/nested` | ✅ | ✅ | ✅ |
| Tree grid (bill of materials: one self-referencing list, child-count expanders, per-level extended-cost totals) | `/tables/tree` | 🟡 | 🟡 | 🟡 |
| Query builder (visual filter: rules and groups over the shipments, applied through `TableQuery`) | `/tables/query` | ✅ | ✅ | ✅ |
| Import data (CSV wizard: upload, map columns, check every row, import the valid file; customers sample) | `/tables/import` | ✅ | ✅ | ✅ |
| Printing (parts list with a repeating header and total; bin labels on A4 sheets; print views on a blank page) | `/tables/print`, `/tables/print/sheet` | ✅ | ✅ | ✅ |
| Data grid (one tag, two engines: typed auto-columns over an IQueryable, or the nested schema grid; `DataGridAuto`) | `/tables/data-grid` | ✅ | ✅ | ✅ |
| Large data (100,000 rows: pages, infinite scroll, virtual table; `TableQuery`); Blazor has pages and virtual only | `/tables/large` | ✅ | ✅ | ✅ |
| Pivot (Odoo pivot view over the 100,000 shipments: header group-bys with date intervals, Measures, Flip axis, Expand all, Download xlsx, sort by a measure, drill-down to Large data) | `/tables/pivot`, `/tables/pivot/download` | ✅ | ✅ | ✅ |

#### Extra (Tabler extra pages)

Menu group *Extra*. Page bodies are in `Views/Shared/Ui/Extra`, data and page logic in `ExtraContent` (`Samples.Shared`). Marketing and Page loader use the blank layout.

| Page | Route | B | M | R |
|---|---|---|---|---|
| Billing (plan, usage meters, payment methods, history) | `/extra/billing` | ✅ | ✅ | ✅ |
| Settings plan (billing cycle, plan, seats, quote) | `/extra/settings-plan` | ✅ | ✅ | ✅ |
| Checkout / pay (card, PayPal, bank transfer, order summary) | `/extra/pay` | ✅ | ✅ | ✅ |
| Pricing table | `/extra/pricing-table` | ✅ | ✅ | ✅ |
| Trial ended | `/extra/trial-ended` | ✅ | ✅ | ✅ |
| License (table of contents) | `/extra/license` | ✅ | ✅ | ✅ |
| Activity (type filter, grouped by day) | `/extra/activity` | ✅ | ✅ | ✅ |
| Tasks (views, groups, progress) | `/extra/tasks` | ✅ | ✅ | ✅ |
| Logs (level filter, search) | `/extra/logs` | ✅ | ✅ | ✅ |
| Uptime monitor (60-day bars, response times, incidents) | `/extra/uptime` | ✅ | ✅ | ✅ |
| Music (player, albums, tracks) | `/extra/music` | ✅ | ✅ | ✅ |
| Job listing (search, type, department, remote filters) | `/extra/jobs` | ✅ | ✅ | ✅ |
| Search results (tabs, highlighted matches) | `/extra/search` | ✅ | ✅ | ✅ |
| Gallery (category filter, lightbox) | `/extra/gallery` | ✅ | ✅ | ✅ |
| Photogrid | `/extra/photogrid` | ✅ | ✅ | ✅ |
| Signatures | `/extra/signatures` | ✅ | ✅ | ✅ |
| Widgets (weather, mini calendar, visitors, goals, storage, to-do) | `/extra/widgets` | ✅ | ✅ | ✅ |
| Text features | `/extra/text-features` | ✅ | ✅ | ✅ |
| Marketing landing page | `/extra/marketing` | ✅ | ✅ | ✅ |
| Cookie banner (per-category consent) | `/extra/cookie-banner` | ✅ | ✅ | ✅ |
| Empty page and empty states | `/extra/empty` | ✅ | ✅ | ✅ |
| Page loader | `/extra/page-loader` | ✅ | ✅ | ✅ |

#### Plugins

Menu group *Plugins* in `AdminMenuContributor` (Data, Inputs, Interface), with one page per plugin in all three samples. Page bodies are in `Views/Shared/Ui/Plugins`, data and page logic in `PluginsContent` (`Samples.Shared`); MVC routes in `PluginsController`. Charts, Datatables and Fullcalendar link to their existing pages. Pages use AntD icons only; the Tabler sprite is unchanged. 🟡 means written but not yet built, run or swept.

| Page | Route | Today | B | M | R |
|---|---|---|---|---|---|
| Charts | `/ui/charts` | Done: 18 chart components | ✅ | ✅ | ✅ |
| Charts advanced (heatmap, treemap, pareto, waterfall, variance, bullet, gauge, funnel, combo, plus interaction: zoom, legend toggle, drill-down) | `/plugins/charts-advanced` | Legend toggle (`ChartOptions.LegendToggle`), drill-down links (`ChartOptions.Links`), zoom by range links (`?days=`) | 🟡 | 🟡 | 🟡 |
| Color picker (presets, formats, alpha, sizes, inside a form) | `/plugins/color-picker` | Page with a GET form (`?primary=&accent=`) and a brand preview | 🟡 | 🟡 | 🟡 |
| Date picker: date, time, range, presets, disabled dates, week/month/quarter/year | `/plugins/date-picker` | MVC: `picker="quarter"` / `"year"`, `presets` on `app-range-picker`, `min`/`max`; `DatePeriods` shared | 🟡 | 🟡 | 🟡 |
| Datatables | `/tables/advanced` | Done, plus the editable and nested grids | ✅ | ✅ | ✅ |
| Dropzone: multiple files, previews, progress, type and size limits, remove | `/plugins/dropzone` | The drag-and-drop area is Uppy (`AppUploader` / `app-uploader`, since 2026-10-09); the button upload is `app-upload` (`action`, `max-size`, `max-count`, `list-type="picture"`); `POST /plugins/dropzone/upload` in all three samples; Uppy checked in the browser × 3 (2026-10-09) | ✅ | ✅ | ✅ |
| Image cropper: a crop box over a picture, aspect ratio buttons (free, 1:1, 4:3, 16:9), a preview, the crop as JSON in a form field | `/plugins/image-cropper` | `AppImageCropper` / `app-image-cropper` on Cropper.js 2.2.0 (`CropperHtml`; the crop in the picture's natural pixels); checked in the browser × 3 (2026-10-09) | ✅ | ✅ | ✅ |
| Code editor: highlighting, folding, search; JavaScript, TypeScript, JSON, CSS, HTML; read only; the text as a form field | `/plugins/code-editor` | `AppCodeEditor` / `app-code-editor` on CodeMirror 6 (`CodeEditorHtml`), in dark mode with the theme; checked in the browser × 3 (2026-10-09) | ✅ | ✅ | ✅ |
| Sortable (NEW): reorder a list, drag between two lists, handles, sortable grid of cards | `/plugins/sortable` | `AppSortable` / `app-sortable` + `app-sortable-item`, keyboard moves, posts the order | 🟡 | 🟡 | 🟡 |
| Fullcalendar | `/apps/calendar` | Done | ✅ | ✅ | ✅ |
| Inline player: video and audio with custom controls, poster, captions | `/plugins/player` | `AppMediaPlayer` / `app-media-player` (`MediaPlayerHtml`); audio track made on the server | 🟡 | 🟡 | 🟡 |
| Lightbox: image grid with full-screen preview, captions, keyboard paging | `/plugins/lightbox` | Captioned grid, square tiles, single image, keyboard list | 🟡 | 🟡 | 🟡 |
| Map: markers with popups, zoom | `/plugins/map` | `AppMap` / `app-map` on MapLibre GL JS (`MapHtml.Tile`) | 🟡 | 🟡 | 🟡 |
| Map fullsize: the map fills the content area, with a floating side panel | `/plugins/map-fullsize` | Full-height map with a searchable place list | 🟡 | 🟡 | 🟡 |
| Map vector: world/region choropleth with hover values and a legend | `/plugins/map-vector` | `AppVectorMap` / `app-vector-map` on jsVectorMap (`Choropleth`, `MapHtml.Vector`) | 🟡 | 🟡 | 🟡 |
| Tour: multi-step tour over a real page, placements, restart | `/plugins/tour` | Mock dashboard; steps for bottom / left / top / right plus centred intro and wrap-up; Start and Restart | 🟡 | 🟡 | 🟡 |
| WYSIWYG editor: toolbar, headings, lists, links, images, HTML output bound to a form field | `/plugins/editor` | `AppEditor` / `app-editor` on Tiptap (`EditorHtml`), cleaned with `RichText.Sanitize(images: true)` | 🟡 | 🟡 | 🟡 |
| Spreadsheet: multi-sheet grid with formulas, editable and read-only, posting its sheets as JSON; print | `/plugins/spreadsheet` | `AppSpreadsheet` / `app-spreadsheet` on Univer (`SpreadsheetHtml`), debounced change events, light/dark theme, print (ribbon, Ctrl+P, `data-m-print`) | 🟡 | 🟡 | 🟡 |
| Document: a Word-like editor (headings, lists, tables, images, links, fullscreen), editable and read-only, posting the document as JSON; Word download; print | `/plugins/document` | `AppDocument` / `app-document` on Univer Docs (`DocumentHtml`), `DocumentDocx` (`Modulus.Documents`), light/dark theme, print (ribbon, Ctrl+P, `data-m-print`) | 🟡 | 🟡 | 🟡 |

**Decisions** (2026-09-28; libraries revisited 2026-10-03)
- **Maps:** MapLibre GL JS for the tile maps (Map, Map fullsize; was Leaflet until 2026-10-03) and jsVectorMap for the vector map.
- **WYSIWYG editor:** wrap Tiptap (was Quill until 2026-10-03); clean its HTML on the server with `RichText.Sanitize`.
- **Menu placement:** a new Plugins group; Charts, Datatables and Calendar keep their entries and are linked from Plugins.
- **Icons:** plugin pages use AntD icons (`app-icon` / `Icon`); no new Tabler sprite icons.

**Order of work**
1. ✅ Plugins menu group and pages for the gallery-only items: Color picker, Date picker, Dropzone, Lightbox, Tour, Charts advanced (route sweep 2026-10-04).
2. 🟡 Sortable: `modulus-plugins.js`, a Blazor component and an `app-sortable` tag helper (written).
3. 🟡 Inline player and WYSIWYG editor (Tiptap) (written).
4. 🟡 Map, Map fullsize and Map vector (MapLibre, jsVectorMap) (written).
5. ✅ Route sweep (desktop, 390 px, dark mode) over the new routes (2026-10-04), and tests for any shared logic in `Modulus.Ui.Abstractions`.

### 5. Verification

| Check | How | Last result |
|---|---|---|
| Unit and component tests | `dotnet test --solution Modulus.slnx` (if it reports "Zero tests ran", run the test DLLs directly: `dotnet tests/<project>/bin/Debug/net10.0/<project>.dll`) | ✅ 630 passed (2026-10-04) |
| Release build | `dotnet build Modulus.slnx -c Release` | ✅ 0 warnings, 0 errors |
| AntD drift | `dotnet run --project tools/antd-drift` | ✅ no drift (AntDesign 1.6.2) |
| CI | GitHub Actions `CI` on `main` | ❌ red since `9579950`: `EditableGridTests.RendersColumnsFromSchema_WithPinnedCellsAndRequiredMarks` expects `$1,200`, but the runner's invariant culture renders `¤1,200`. Fixed locally (the test class pins `en-US`); green once pushed. |
| Route sweep, desktop | Every sidebar route in each sample: HTTP 200, no console errors | ✅ 44 × 3 (2026-09-27); the 22 `/extra/*` routes × 3 on 2026-09-28; `/tables/pivot` × 3 on 2026-10-02; all 96 menu routes × 3 on 2026-10-04 |
| Route sweep, 390 px | Same, plus no horizontal page scroll | ✅ 44 × 3 (2026-09-27); the 22 `/extra/*` routes × 3 on 2026-09-28; all 96 menu routes × 3 on 2026-10-04 |
| Route sweep, dark mode | Same, with the dark theme | ✅ 44 × 3 (2026-09-27); the 22 `/extra/*` routes × 3 on 2026-09-28; all 96 menu routes × 3 on 2026-10-04 |
| Page scrolling | Mouse-wheel scroll on long pages; the page locks only while the drawer is open | ✅ all three samples |
| Visual parity | Screenshot diff of Blazor vs MVC and Razor Pages at 1440 px | ✅ identical or accepted diffs only; `/tables/pivot`: the rendered pivot markup is byte-identical in all three samples (2026-10-02) |
| Icons | Every bundled icon draws an SVG; `tb:` names draw the Tabler stroke group, unknown or old `ri:` names draw nothing (`ShellIcon_EveryBundledIcon_DrawsAnInlineSvg`, `PluginsBatch2Tests`) | ✅ (2026-10-03) |
| CSV export | Same bytes and headers from all three samples | ✅ |
| Pivot | `PivotTests` (state round trip, expand/collapse/flip/expand all, totals = LINQ sums, averages and maxima, `MaxGroups`, only open headers read, date labels, sort, markup, bUnit parity, xlsx, SQL `GROUP BY`s with parameters via EF Core `ToQueryString`) and `PivotTagHelperTests` (MVC = Blazor markup, `PivotState.Link`) | ✅ green, part of the 545 (2026-10-02) |
| Pivot download | The xlsx from all three samples opens in Excel and LibreOffice, with merged headers and bold totals | ✅ all three samples return identical valid xlsx (same size, workbook "Shipments"); opens and converts in LibreOffice with the merged year/measure headers and the bold Total row (2026-10-02) |

**Accepted visual differences:** these come from the controls, not the markup.
- Text rendering in a native `<select>` vs an AntD `Select`.
- The AntD password-reveal glyph.
- Subpixel antialiasing fringes.

### 6. Follow-up work

| Item | Area | Status |
|---|---|---|
| Column picker and density control built into the library: `app-table-columns`, `app-table-settings`, `app-table hidden-columns / density / scroll-x / table-footer` | MVC library | ✅ |
| Keep filters in the URL (`hx-push-url` with the query) on the MVC list pages: all ten already push the URL; empty filter fields are now left out of it | MVC samples | ✅ |
| MVC equivalents for Blazor-only widgets: `app-cascader`, `app-tree-select`, `app-transfer`, `app-tree`, `app-calendar`, `app-carousel`, `app-anchor` (Alpine, `modulus-widgets.js`), with gallery cards in all three samples | MVC library | ✅ |
| Editable data grid: schema-driven columns (text, number, currency, percent, select, multi-select, tag, date, switch, progress, rating) with in-place editors and validation, expandable rows, left/right pinned columns, add/delete rows, add/remove/pin/hide/reorder columns, save. `EditableGrid` (Blazor), `app-editable-grid` + `EditableGridForm` (MVC), `GridSchema` shared | Both | ✅ |
| Complete the AntD gallery: Link, Flex, Splitter, Affix, BackTop, DropdownButton, AutoComplete, InputGroup, CheckboxGroup, Mentions, Image, CountDown, Drawer, Watermark, Comment, plus Modulus ColorPicker, FloatButton and QRCode where AntD Blazor 1.6.2 has none | Both | ✅ AntD components in both flavours, plus Modulus QRCode, ColorPicker, FloatButton and Tour |
| Plugin pages: all twelve written (batch 1: Charts advanced, Color picker, Date picker, Dropzone, Lightbox, Tour; batch 2: Sortable, Inline player, Map, Map fullsize, Map vector, WYSIWYG editor); route sweep × 3 on 2026-10-04 (see §4 *Plugins*) | Both | ✅ |
| Tabler-parity pages: gallery, search results, activity, tasks, logs, license, job listing, uptime, trial ended, cookie banner and widgets are done (§4 *Extra*); icons, typography, colours, changelog and users grid: run in the 2026-10-12 route sweep (HTTP 200, no console errors, no page overflow at 390 px, dark mode) in all three samples | Samples | ✅ |
| Localization: `ModulusThemeOptions.Cultures` adds a language picker to the theme settings panel (culture cookie + reload); `app.UseModulusLocalization()` (MVC) and `UseRequestLocalization` (Blazor sample) set the request culture, which AntD's `LocaleProvider` and the tag helpers follow. Sample texts stay English | Both | ✅ checked 2026-10-12: with a `de-DE` culture cookie the pivot page's numbers and dates switch to German format in MVC and Blazor (`36.449`, `93.183.946,16`), and `<html lang>` follows the culture (`de-DE`; it was hard-coded `en`, now fixed in `_ModulusLayout` and `App.razor`) |
| Brand colour in dark mode (AntD dark CSS hard-codes blue): `AntDarkBrand` + `antd-dark-brand.css` swap the blue shades for the brand's dark palette | Theming | ✅ checked 2026-10-12 in MVC and Blazor: the primary button is `rgb(6,111,209)` in light and `rgb(8,97,181)` in dark (the brand's palette), not AntDesign's default blue |
| Replace `AntIcons` reflection if AntDesign makes its icon store public | MVC library | ⬜ |
| Less Blazor JS interop: `ThemeToggle` uses the shell's `[data-m-theme-toggle]`; `AppRichText` and `AppSignaturePad` report through a hidden input's change event (no `DotNetObjectReference`; the signature pad starts from `[data-m-signature]` in both flavours); `AppColorPicker` drag capture, and the `AppLookup` / `AppCreatableSelect` list placement and active-row scrolling, follow the DOM in `modulus-shell.js`. Left on interop: `AppTour` (measuring), `EditableGrid` (CSV download, resize watch, popup) and the built-in `FocusAsync` | Blazor library | 🟡 tests green; browser check pending |
| bUnit tests for `ProTable` (`TitleExtra`, `RowSelectable`) and tests for sample logic (`EmployeeDirectory`, `WizardInput`, `ModelValidation`, reference pages, `SampleAuth`); `Modulus.Blazor.Tests` now references `Samples.Shared` | Tests | ✅ green (579, 2026-10-03) |
| Real authentication in the samples: cookie sign-in and sign-out against the sample users (password `modulus`), the signed-in user in the header; `Samples:RequireSignIn` (off by default) requires sign-in on every page but the auth and error pages | Samples | 🟡 MVC checked (2026-10-09): sign-in as a sample user, the user's name in the header, sign-out back to the login page, a wrong password refused with the field marked invalid; Blazor and Razor Pages: sign-in with the demo password lands on the home page, a wrong password returns to the login page with the error; sign-out clears the auth cookie and returns to the login page (checked 2026-10-09) |
| Make `EditableGridTests` culture-independent: the class sets `en-US` in its constructor and restores it in `Dispose` | Tests | ✅ passes locally (2026-10-12, 1105 tests and three more runs of the Blazor suite); CI run pending. Known limit: the test suite needs ICU, so it does not run with `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` (see §8) |
| Update GitHub Actions off Node 20: `checkout@v7`, `setup-dotnet@v6`, `upload-artifact@v7`, `github-script@v9` in both workflows | CI | ✅ the tags are in both workflows and exist on GitHub (checked 2026-10-12: checkout v7.0.1, setup-dotnet v6.0.0, upload-artifact v7.0.2, github-script v9.0.0); a CI run is still pending |
| NuGet packaging and versioning: `src/Directory.Build.props` (one `VersionPrefix`, SourceLink, symbols, README), other projects not packable; CI packs `-ci.<run>` prereleases as the `packages` artifact | Release | ✅ `dotnet pack Modulus.slnx -c Release --no-build` writes 15 packages (`.nupkg` and `.snupkg`, version 0.9.0) on 2026-10-12; the `-ci` prerelease upload runs in CI |
| Nested menus organised like Odoo: navbar dropdowns are one narrow column with bold section headings (entries indented under them) and groups inside a section folding open in place; sidebar groups nest as folding branches, to any depth, instead of the multi-column dropdown. Every sample page belongs to a launcher app (Plugins and Extra added), so the current-app bar never falls back to the whole menu | Shell | 🟡 written |
| Structure tidy-ups (namespaces unchanged, no consumer break): the `ProTable`, `EditableGrid` and `NestedGrid` code (with the shared `ColumnSearch` and `Paging`) into a `Grids/` folder in `Ui.Abstractions`, the form binding (`EditableGridForm`, `NestedGridForm`, `ColumnSearchQuery`) into `FormBinding/`; test files foldered to match | Layout | ✅ |
| Pin the SDK in `global.json` (`version` 10.0.400, `rollForward: latestFeature`) so local builds and CI agree; `TestResults/` stays ignored | Repo | ✅ |
| Reusable PDF component: one Blazor and MVC component (`AppPdfDownload` / `app-pdf-download`) that takes a document (editor JSON, blocks or HTML) and a file name, posts it to a server endpoint and downloads the `DocumentPdf` output, so the document pages stop wiring the PDF button by hand; both flavours from one renderer, a sample on the document page, tests | Documents | ✅ `PdfDownloadHtml.Button` (same-site action only, the form's id required), `AppPdfButton` (Blazor), `app-pdf-button` (MVC), used on the document page in all three admin apps; 9 Blazor + 2 MVC tests; the MVC form posted to `/plugins/document/pdf` returns a one-page PDF and the Blazor and MVC pages render the button |
| Banked for a major version, with §7.2's no-renames decision: `Modulus.Ui.Abstractions` → `Modulus.Core`; splitting `modulus.css` and `modulus-widgets.js` behind a pack-time concat | 2.0+ | ⬜ |

### 7. Modulus 2.0 roadmap

From the "Modulus 2.0" proposal reviewed on 2026-09-30. The proposal fits the framework (one renderer for Blazor and
MVC, state in the URL, `[data-m-*]` roots started by `modulus-plugins.js`), but it was not adopted as written. This
section is the to-do list: what is done, what is left, and what was dropped and why.

#### 7.1 PivotGrid (phase 19), written 2026-10-01, verified 2026-10-02

| Item | Where | Status |
|---|---|---|
| Registered fields and measures; one grouped query per row depth × column depth that has open headers, filtered to them (Odoo's way: closed headers are not read); `MaxGroups` cap over what shows | `Ui.Abstractions/Pivot/PivotQuery.cs`, `PivotResult.cs` | ✅ tested (incl. EF SQL) |
| State in the query string (`rows`, `cols`, `measures`, `rx`/`ra`, `cx`/`ca`, `sort`, `sortcol`, `desc`); expand, expand by, collapse, flip, expand all, measures, sort; `PivotState.Link` keeps the page's own query values | `PivotState.cs` | ✅ tested |
| Shared markup: toolbar, nested column headers, indented row headers, group-by dropdowns with a date-interval submenu, sortable measure headers, drill-down links, empty and too-many-groups states | `PivotHtml.cs` | ✅ tested; byte-identical in all three samples |
| xlsx download without a library (SpreadsheetML in a zip) | `PivotXlsx.cs` | ✅ tested; opens in LibreOffice |
| Blazor `AppPivotGrid`, MVC `<app-pivot-grid>` | `Modulus.Blazor/Components/Pro/AppPivotGrid.razor`, `TagHelpers/PivotTagHelpers.cs` | ✅ tested (bUnit parity, MVC = Blazor markup) |
| Header dropdowns float out of the scrolling table: `modulus-shell.js` calls `placeDropdown` when a `details.m-dropdown` with a `.m-dropdown-panel` opens (and again when a date submenu opens) | `modulus-shell.js` | ✅ checked in the browser × 3, 1300 and 390 px (2026-10-04) |
| Busy state and scroll: a pivot link marks the pivot `m-pivot-busy` (a bar runs along the table, values fade) until the new markup replaces it; the table's scroll position and the page's scroll survive the replacement; MVC swaps with `show:none` | `modulus-shell.js`, `Views/Shared/Ui/Tables/Pivot.cshtml` | ✅ checked in the browser × 3 (2026-10-04) |
| Pivot CSS (toolbar button group, table scrolling inside the card with sticky column and row headers, shaded row headers and totals, row hover, open menus above the sticky headers, indents) | `modulus-pages.css` *Pivot table* | ✅ written; 390 px and dark-mode checked on all three (2026-10-09) |
| Blazor filters without flicker: the Status, City and keyword boxes show their value at once instead of reverting until the navigation lands; Blazor `/tables/large` reads `?status=&city=&keyword=` and gets the City select and search MVC has, so the Blazor pivot drills down too (`CellHref`) | Blazor `Pages/Tables/Pivot.razor`, `Large.razor` | ✅ written; `Large` reads its filters from the query (prerender shows the shell, the filtered load runs on the circuit) |
| Sample `/tables/pivot` and `/tables/pivot/download` in all three apps; menu *Tables → Pivot* | `Samples.Shared/ShipmentPivot.cs`, `Views/Shared/Ui/Tables/Pivot.cshtml`, `TablesController`, `Pages/Tables/Pivot.cshtml`, Blazor `Pages/Tables/Pivot.razor` + `Program.cs` | ✅ route sweep + download verified × 3 |
| Tests | `tests/Modulus.Blazor.Tests/PivotTests.cs`, `tests/Modulus.AspNetCore.Mvc.Tests/PivotTagHelperTests.cs` | ✅ green |

**Still to do or to check for the PivotGrid**
1. ✅ Build with 0 warnings and run the tests: `dotnet build Modulus.slnx -c Release` (0 warnings, 0 errors), `dotnet test --solution Modulus.slnx` (545 passed), `dotnet run --project tools/antd-drift` (no drift, 2026-10-02).
2. ✅ The `Microsoft.EntityFrameworkCore.SqlServer` test pin (`10.0.12` in `Directory.Packages.props`) restored and the EF tests run.
3. ✅ EF Core translation (`Queries_TranslateToGroupBysInSql_WithTheOpenHeadersAsParameters`): the `PivotKey` member-init key (also the empty key of the grand-total query) translates — `PivotQuery.Group` uses `Expression.MemberInit` for bound keys and `Expression.New(typeof(PivotKey))` for the empty one; the open-header filters and the enum `long` casts come out as `@p…` parameters (`ToQueryString` asserts, and the slice SQL asserts the values are parameters, not literals). `DateOnly.DayNumber` (the week interval) translates.
4. ✅ Blazor drill-down: the drill links are in the markup (`CellHref` → `/tables/large?status=…&city=…`), Blazor `Large` reads `?status=&city=&keyword=` (`[SupplyParameterFromQuery]`; the static prerender shows the shell, the filtered load runs on the circuit), and the MVC target filters (`/tables/large?city=Amsterdam` → Amsterdam rows only). The interactive click-through (Pages and Virtual mode, back returns to the same layout) belongs to the browser sweep in item 6.
   - The busy bar clears on every link (the 15 s timer is only a fallback); checked in the browser with item 6 (2026-10-04).
5. ✅ Blazor download link: the anchor carries `download`, `hx-boost="false"` and `data-enhance-nav="false"`, so neither the interactive router nor htmx intercepts it; the endpoint returns the workbook on all three ports.
6. ✅ Browser sweep of `/tables/pivot` (done 2026-10-04 on all three samples) (desktop HTTP 200 × 3 verified 2026-10-02, markup byte-identical): header actions, back and forward, keyword/filters keeping the layout, 390 px, dark mode, the header dropdown not clipped, the busy bar and the table's scroll after expanding far down or right.
7. ✅ Screenshot parity, Blazor vs MVC at 1440 px (`/tables/pivot`): the whole `<div class="m-pivot">` fragment is byte-identical across Blazor, MVC and Razor Pages (84,033 bytes each), which is stronger than a screenshot for a server-rendered table.
8. ✅ The downloaded xlsx opens in LibreOffice (`soffice --headless --convert-to csv`): merged year/measure headers, the bold Total row (100,000 / 254,093,087.51) and the per-city rows; the three samples return identical workbooks. Excel itself was not available to try; the file is valid SpreadsheetML/ZIP.
9. ✅ README: the pivot is in the feature list and `app-pivot-grid` in the tag-helper table.
10. ⬜ Later, only if asked (Odoo has these; the first cut does not): a "Compare" period, the graph view switch, "Insert in spreadsheet". Per-user saved layouts are done (phase 22, `AppSavedViews`).

#### 7.2 The rest of the proposal

| Proposal item | Decision | Status |
|---|---|---|
| Spreadsheet (formulas, multi-sheet, xlsx in and out) | **Univer 1.0.3** (Apache-2.0; the engine review of 2026-10-03: Handsontable/HyperFormula are commercial, Jspreadsheet CE and Fortune-sheet lack the formula engine or xlsx round-trip). Vendored as a prebuilt IIFE bundle (`lib/univer`, like Tiptap, because Univer ships ESM-only); `SpreadsheetHtml` / `AppSpreadsheet` / `app-spreadsheet` at `/plugins/spreadsheet` in all three samples; sheets are plain rows with `=SUM(…)` formula strings, posted as JSON through the widget registry, theme-following. xlsx in and out via `Modulus.Documents` (ClosedXML, `SpreadsheetXlsx`); the document editor (`AppDocument`, Univer Docs) downloads as docx (`DocumentDocx`). | ✅ checked in the browser × 3 (2026-10-04) |
| Widget registry (`init`/`destroy` per `[data-m-*]` root) | Built 2026-10-03, ahead of the Spreadsheet: `widget(selector, init)` registers a root and `keepAlive(root, destroy)` releases it when the element leaves the page; `scan()` walks the registry instead of hardcoding widgets. First users: Tiptap editor, MapLibre maps and the Univer spreadsheet. | ✅ |
| Document templates and Document Processing (PDF, Word, Excel output, mail merge) | After Fluid (phase 16, done 2026-10-09): templates in Liquid, rendered to HTML, then to PDF or DOCX. PDF engine chosen 2026-10-09: QuestPDF (Community licence, free under USD 1M revenue; not for public-sector or listed companies). It draws from the document model, not HTML, so Liquid templates reach PDF through a document model, not directly. | ✅ (PDF, Word and Excel output; templates on Fluid; mail merge; PDF button on the document page) |
| `IUiStateStore` and saved views | `IUiStateStore` in `Ui.Abstractions.State` (per user, per key) with an in-memory default (`MemoryUiStateStore`, not a cookie: views can outgrow one). `SavedViews` keeps named canonical query strings, at most 20 per page, one default. `AppSavedViews` / `app-saved-views` share `SavedViewsHtml`; MVC posts to `MapModulusSavedViews()`. Wired on `/tables/pivot` and `/tables/advanced` in all three samples (MVC and Razor Pages redirect to the default view; Blazor's ProTable keeps its state in the URL with `UrlState`, using the MVC keys via `ProTableUrl`). The samples keep views in SQLite (`EfUiStateStore`, `App_Data/ui-state.db`, git-ignored). The editable grid is left out: its layout is a schema saved on the server, not URL state. Checked in the browser × 3 on 2026-10-04 (save, star, default view, delete, back). | ✅ |
| Permission-aware columns | `FieldAccess` (roles or a test per field) → `Func<string, bool>`, applied by `TableQuery.Restrict`, `PivotQuery.Restrict` and `GridSchema.Allowed` before querying and rendering. The sample pivot's *Largest* measure needs Admin or Editor, on the page and the download. | ✅ |
| `DEPENDENCIES.md` | One page listing every vendored script (htmx, Alpine, MapLibre, Tiptap, Univer, jsVectorMap), font, icon set, NuGet package and browser-called service, with its version, its licence and why it is used. | ✅ 2026-10-03 |
| Architecture rules (proposal §48) | Adopted in DESIGN.md *Component architecture rules*, adapted: rule 6 uses the `modulus-plugins.js` observer, not htmx `afterSwap`. | ✅ written |
| htmx `afterSwap` lifecycle | Dropped: the MutationObserver in `modulus-plugins.js` already covers htmx swaps, Blazor renders and SPA navigation. | ➖ |
| Package renames and a JS folder restructure | Dropped: no gain for the cost of breaking every consumer. Revisit only at a major version. | ➖ |
| `IModulusPlugin` registry | Dropped for now: plugins are plain components and tag helpers, and a registry adds indirection without a use case. | ➖ |
| Choosing the aggregation per request | Dropped: each measure has one fixed aggregate (`PivotAggregate`), like Odoo's field `aggregator`. Anything else lets the URL pick work the server did not plan for. | ➖ |

**Order of work:**
1. Finish §7.1 (build, tests, sweep).
2. Stabilise the 🟡 work in §3–§6.
3. Spreadsheet, with the widget registry.
4. Fluid (phase 16), done 2026-10-09. Hardening and quality gates (phases 17 and 18) done the same day, except OpenTelemetry and the 500 page check (see the rows).
5. Document templates.
6. Document Processing.

`IUiStateStore` and saved views can start any time after §7.1.

#### 7.3 ERP UI roadmap (adopted 2026-10-01)

Radzen, Syncfusion and DevExpress were reviewed as feature inventories — which ERP screens and components ship
there — and an ERP Report Studio design as the product side of the same ground. **Modulus adopts the UI
capabilities only.** The scope rule stays as it has always been: Modulus is a UI framework — components, tag
helpers, theming, markup, and the query shaping the grids need (`TableQuery`, `PivotQuery`). Product-level work
(report definitions and scheduling, document pipelines beyond phase 21, any data store of its own) belongs to the
apps built on Modulus; the ERP Report Studio is a separate future solution that will consume Modulus packages for
its UI. Nothing of it is planned in this repo.

**P0 — the ERP interaction spine (phases 22–26 in §3):**

- **Grid engine (23):** multi-column sort; group-by with group headers, footers and aggregates; multi-level
  headers; an Excel-style checkbox filter menu per column; row states (dirty, error, locked, read-only) on the
  editable grid; an ERP batch toolbar (Approve / Reject / Print on the selection); TreeGrid mode on `NestedGrid`
  for self-referencing flat rows (BOMs, chart of accounts) with per-level aggregates. Pivot UI extensions
  (derived-measure display) ride along.
- **QueryBuilder component (24):** a visual WHERE builder over registered fields (field, operator, value, and/or
  groups) that emits one serialisable filter model, plugs into `ColumnSearch` / `TableQuery`, and renders in both
  flavours from one renderer, like every other component.
- **Import wizard (25):** upload → column mapping → validation preview → commit, as components; the server
  callback owns the actual import.
- **Printing (26):** `@page` setup, repeated table headers, and grid, invoice and label print styles; the
  invoice sample page already sketches the invoice part.
- **User state (22):** `IUiStateStore`, saved views, permission-aware columns (§7.2, unchanged).

**P1 — the component tier** (each: one renderer, both flavours, a sample page, tests): Kanban and Scheduler (both done
2026-10-09: `KanbanBoard` and `Scheduler` in the library, drawn by the Blazor components and the MVC `app-kanban` and
`app-scheduler`); Gantt (done 2026-10-09, read only: bars, groups, progress, late marks, today; drag to reschedule and
dependency links are not in it); a PDF viewer (done 2026-10-09, pdf.js 6.3.289, Apache-2.0: one page at a time, zoom,
download; text is not selectable yet); file manager (done 2026-10-09);
dashboard layout (tiles on a 12-column grid, moved and resized, saved through `IUiStateStore`; done 2026-10-09 with
buttons rather than pointer-drag resizing, see the change log); barcode (done 2026-10-09, Code128 and EAN-13, beside the QR
encoder); rich-text tables in `AppRichText` (done 2026-10-09, §8); a workflow and audit UI kit (done 2026-10-09: `ApprovalTimeline`, `AuditTrail`, `StatusBadge`); a multi-column, paged `AppLookup`
(dialog and dropdown); dynamic schema-driven forms (done 2026-10-09: `FormSchema` in `Modulus.Ui.Abstractions/Forms`, `AppSchemaForm` / `app-schema-form`; §8).

**P2:** diagramming, AI-assist hooks (natural-language filters compiling to the QueryBuilder's model), chat UI.
None planned yet.

**Order of work:** the §7.2 order stands (finish §7.1, stabilise the 🟡 work, Spreadsheet, Fluid, document
templates, Document Processing); the P0 phases follow that, P1 items as needed.

### 8. Change log

| Date | Change |
|---|---|
| 2026-10-13 | **Blazor sign-in and CRUD against a live API.** Supersedes the Blazor limits in the 2026-10-12 rows below. `modulus app --ui-engine blazor --auth` (OpenIddict password) now generates the sign-in path: `Program.Blazor` configures cookies with `LoginPath = /account/login`, `AddModulusBff` with a default web client, `POST /account/login` and `GET /account/logout`; `Routes` uses `AuthorizeRouteView` with `RedirectToLogin` for anonymous users; the `Account/Login` page posts a native form with an antiforgery token and sits on a `BlankLayout`; the Home page carries `[Authorize]`. Sign-in was verified end to end with curl against a running OpenIddict API. The MVC app's CRUD (list, create, edit, delete) was run against the live API. The Blazor entity form saves through the generated typed client, but Blazor CRUD against a live API was not separately run. With `--auth`, the Blazor host also runs the startup security guard, like the MVC and Razor Pages hosts: the component endpoints require sign-in, the sign-in, sign-out, health and static-file endpoints carry `.Loosen(reason)`, and `security/loosening-allowlist.json` lists the routes. Checked by starting the generated host with `ASPNETCORE_ENVIRONMENT=Production`: the guard reports 0 findings, an anonymous `/` redirects to `/account/login`, and the sign-in page, health and a static file return 200. The generated Blazor host has a `wwwroot/.gitkeep`, so the static file middleware no longer warns about a missing web root. The Development settings set `Bff:RequireHttpsMetadata` to false (as the BFF host does), so a plain-HTTP issuer works in development. External providers: a Keycloak-style Blazor host, with its authority pointed at the local OpenIddict API, starts with 0 guard findings and redirects anonymous users to the issuer's `/connect/authorize` with the client ID, `/signin-oidc` redirect, PKCE S256, scope, nonce and state. The generated OpenIddict API is password-only by default, so the full code flow was run against a scratch copy of it with `Identity:AllowAuthorizationCodeFlow` on, a public client that redirects to `/signin-oidc`, a test user and a minimal login page (test code only, not in the templates). The whole flow completes: the host's sign-in redirect, the issuer's login, the authorization code posted to `/signin-oidc`, the token exchange at `/connect/token` (200), `/connect/userinfo` (200), and then the signed-in home page (200, showing the signed-in user). An anonymous visit still redirects to sign-in. Keycloak and Authentik themselves were not run here (they need their own servers). The CLI's 530 unit tests pass. |
| 2026-10-12 | **CLI gaps closed.** `modulus app --ui-engine blazor` creates a Blazor app host (App, Routes, the shell layout from `AppShell`, a home page, the typed API clients); `--auth` other than `none` is refused for Blazor until its sign-in pages exist. `ui create-crud` works for MVC (Controllers/<Entities>Controller.cs with list, delete, create and edit, Views/<Entities>/Index and Edit, from the new `mvc/crud-*.cshtml.sbn` templates). `ui create-dashboard` writes a controller at the dashboard route that passes the figures from IMetricsSource or IAuditTrail, so the page renders. The Blazor entity form saves through the entity's typed client (`create-crud` first). Fixed on the way: Blazor admin pages injected services under their page's own name (CS0542); the Blazor UI packages were not referenced; the admin sign-in warning said pages would be refused when they are open. Verified: all three engines scaffold, build with 0 errors, and serve their generated routes (MVC and Razor Pages with no API running; Blazor pages render while the API is down and log the failed calls). CRUD against a running API is not yet tested. Modulus.Cli tests: 530 pass.
| 2026-10-12 | **NuGet packages and CLI scaffolding.** The UI packages use the Modulus pattern `Synvro.<project>` (`src/Directory.Build.props`), version 0.9.0, Apache-2.0, author `synvro`; `dotnet pack` writes 8 packages (and symbol packages) to `nupkg/` (git-ignored). The static assets of `Synvro.Modulus.Ui.Assets` are served under `_content/Modulus.Ui.Assets` (pinned with `StaticWebAssetBasePath`, so project and package consumers get the same URL; the tag helpers write that prefix). Generated apps: `modulus app` (webapp+api, mvc or razor-pages) with `--package-source`; `add-module`, `generate-crud`, `ui add-component`, `ui add-admin`, `ui create-dashboard`, `ui create-form-from-entity`, and `ui create-crud` (razor-pages) all build with 0 errors. Fixed on the way: the Program template path for `ui create-form-from-entity` now references the module's Application project and imports its DTOs; `ApiClientExtensions` no longer imports a namespace that `--no-example` never creates; and `[Authorize]` appears in the generated pages only when the app signs users in (`use_auth`, read from the app's `Program.cs`), because without an auth scheme an authorized page answers 500. Known gaps: `ui create-crud` is refused for mvc and blazor has no app host in the CLI yet; `ui create-dashboard` writes a view with no action, so that page 404s until an action is added. Modulus.Cli tests: 530 pass.
| 2026-10-12 | **Open follow-ups closed or measured.** `<html lang>` follows the UI culture (MVC layout and Blazor `App.razor`; was hard-coded `en`). Localization and the dark-mode brand colour checked in the browser (§6). The sample formatter `Fmt` uses the invariant culture: it threw in globalization-invariant containers (`DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`) and prints the same US-style text. Measured in invariant mode: the MVC sample runs; the Blazor sample's `/tables/large` returns 500 because AntDesign's `LocaleProvider` resolves `en-US` by name (an upstream AntDesign 1.6.2 limit, not changed here). The unit tests pin named cultures (`en-US`, `de-DE`), so they need ICU and fail in invariant mode; that is expected. `dotnet pack` writes 15 packages. The Actions tags were checked against GitHub. Route sweep: 864 loads, one failure, a `networkidle` timeout on a map page that waits on OpenStreetMap tiles (the page loads in under 2 s with `load`). Three Blazor tests (`ModulusDataGridTests`, `ProTableUrlTests`, `ColumnValuesTests`, and once `EditableGridTests`) failed only while the sweep loaded the CPU, with bUnit wait timeouts and a stale event-handler id. Three full runs on an idle machine passed. Smoke, parity and budgets pass. Not checked: the Plugins interactions (sortable reorder, the map popup, the editor) beyond loading, and the nested navbar menus beyond their header dropdowns. |
| 2026-10-12 | **Release gates: sweep, print CSP, 390 px overflow.** A route sweep of the §4 inventory (105 routes, dropping `{id}`, `*` and the two download endpoints) in MVC, Razor Pages and Blazor at 390 px, 1280 px and dark mode found three real problems, all fixed: (1) `/tables/print/sheet` ran an inline script with no nonce, blocked by the enforced CSP in MVC and Razor Pages; the view now sets `nonce="@CspNonce.Of(Context)"`, and `Modulus.Samples.Web` references `Modulus.AspNetCore.Hosting`. (2) Closed breadcrumb panels laid out 240 px wide past the phone edge: `.m-crumb-switch:not([open]) > .m-crumb-panel { display: none }`. (3) Chart x-axis labels (`.m-xy-x span, a`) spilled past the chart on `/ui/charts`, `/dashboards/erp` and `/plugins/charts-advanced`; they now clip with an ellipsis. After the fixes every page measures `scrollWidth` = viewport at 390 px. The sweep's only remaining failure is Blazor `/plugins/map-fullsize`, which times out at `networkidle` because the map waits on `tile.openstreetmap.org` tiles; it loads (canvas drawn) in under 2 s with `load`, and MVC's copy of the page passes. Budgets, smoke, parity, and 1105 tests pass. `modulus-pages.css` is at 28,940 of 29,696 gzipped bytes. |
| 2026-10-12 | **Release build with 0 warnings (§1 goal 11).** The 35 analyzer warnings in `tests/Modulus.Blazor.Tests` are gone: `SampleAdminDataTests` passes `TestContext.Current.CancellationToken` to every awaited call (xUnit1051), and `MultiSortTests` builds its columns with literal sequence numbers in regions (ASP0006). `dotnet build Modulus.slnx -c Release`: 0 warnings, 0 errors. `dotnet test Modulus.slnx -c Release`: 1105 passed, 0 failed. The browser gates (`tests/browser`) were not run in this change. |
| 2026-10-09 | **Forms elements, breadcrumb text, kept page state.** The multiple select is a list box that shows its options (it was a one-line box that hid them); the Sizes inputs have a gap; the file card fills its column (MVC and Blazor; Blazor's drag area was missing its content and now has the same icon and text). The Time field uses the framework date picker in time mode. Breadcrumb levels are text only, no icons (Odoo style), carets stay. Kept state: a page marked `data-m-keep-state` (the packing list template list) remembers its query for the tab (sessionStorage); a menu or breadcrumb link to it gets that query, and a page that opens with no query takes it. Blazor keeps the column order in the URL. Checked in all three apps: PO and template come back after visiting another page. Tests: Blazor 805, MVC suite passes, smoke gate passes. Not done: sheet edits are not kept across a full navigation (the editor keeps its content only while the page stays in the session). |
| 2026-10-09 | **Navigation states and breadcrumb panels.** The end-of-trail breadcrumb panels open to the left of their caret, so the last levels stay on screen at 1280 px (they were cut off). The module bar's hover underline is a tint of the app colour (it was a grey bar). Checked in MVC and Blazor: rest, hover, open and dark theme, and no navbar dropdown runs past the viewport at 1280, 1024 or 390 px. The 23 yellow pages of §4 return 200 in all three apps with no console errors. Finding: once, MVC and Blazor served an empty gzip copy of `modulus-shell.css` (browsers got no shell CSS, so the sidebar showed inline); a restart fixed it and it did not come back. The cause is not identified; if it recurs, check the response-compression path for static assets. |
| 2026-10-09 | **Packing list workspace: Insert ERP field on the right, framework components.** The Excel sheet sits beside the Insert ERP field panel, an accordion of field groups (stacked below 992 px). The KPIs are statistic cards, the four steps are the framework steps, the template actions are buttons and the icon buttons are icons. MVC uses the framework tag helpers (`app-statistic-card`, `app-steps`, `app-collapse`, `app-button`); Blazor uses the same cards and steps, with `ant-btn` elements for links and for the field buttons, because the AntDesign button takes no `href`, `title` or `data-*` attributes. Column letters come from one CSS rule in both. Checked in all three apps: the panel is right of the sheet, a field click writes into the sheet, no console errors; the Blazor suite and the smoke gate pass, the solution builds with 0 warnings. |
| 2026-10-09 | **Breadcrumb: related pages from every level.** Each level of the trail has a caret that opens its related pages: the app level lists the other apps, a section lists its sibling sections (a group opens its first page) and the page on screen lists the pages beside it, with the current one marked. The dropdown is the shell's `m-dropdown`, so it closes on outside click, Escape and link click. The design is pill levels with a quiet caret, a panel with a section heading, and focus rings. Checked in all three apps: the switcher opens and a related link navigates. Tests: 1 new switcher test set, the long-trail test updated. The browser gate passes; the sign-out check on Blazor and Razor Pages is done. |
| 2026-10-09 | **Packing list: letterhead, checks, UI polish.** The preview paper opens with a letterhead (logo, company name, address, phone and email) and the buyer's name and address. The H&M sheet carries the shipper block (company name, address, phone) and the Insert ERP Field panel has company, buyer address and shipping fields (invoice, container, terms). Checks on a list: a carton in two lines or missing from the ranges, the last carton against the order's carton count, the shipped quantity against the ordered one, gross below net, a missing weight; errors block, warnings show. Icons replace the emoji, a Reset order control restores the template's column order, and the preview table wraps inside the paper. Not done: the logo and the letterhead are not in the Excel and PDF files (the document model has no image block yet). Browser gate passes (the Razor host was stale after CSS changes, which caused the earlier failure). |
| 2026-10-09 | **Packing list: drag fixed on MVC and Razor Pages, ERP document chrome.** MVC and Razor Pages letters now follow each column's place (CSS `nth-child`), so a drop re-letters the row; MVC still needs Apply order. The page gets an object header (template, buyer, version, order, status), an action toolbar (Save, Preview, Generate) and a status bar, in all three apps. Checked: drag in Blazor, MVC and Razor Pages in the browser, no overflow at 390 px, solution 0 warnings. |
| 2026-10-09 | **Packing list: column drag in the editor, polished page.** The column bar sits above the Excel editor: each column is a letter chip, dragged to a new place (Blazor on the drop; MVC with Apply order). The sheet, the preview and the downloads follow. The page gets summary figures, a step strip, a bar of template status pills, the preview as a sheet of paper, format cards for Excel and PDF and a file card for the output. Checked: MVC and Blazor column moves in the browser, the browser gate on the three apps. |
| 2026-10-09 | **Real authentication checked on MVC.** Sign-in with a sample user and the demo password, the name in the header, sign-out to the login page, and a wrong password refused (the password field is marked invalid). `RequireSignIn` is off by default, so an unsigned visit to a page answers 200. Blazor and Razor Pages are still to be checked. |
| 2026-10-09 | **Packing list: drag and drop columns.** The preview shows the template's columns as a horizontal sortable (`app-sortable` / `AppSortable`): drag a heading to move the column. MVC applies the order with its Apply button (a GET form posts `cols` in order); Blazor applies it on the drop. The order reaches the preview, the editor sheet and the Excel and PDF downloads. The fill keeps the header block and drops unknown names. Tests: Blazor 796 (3 new). Checked: the keyboard move and Apply in the browser, the preview header after a move, and the browser gate on the three apps. |
| 2026-10-09 | **Breadcrumb in the page header.** The breadcrumb stays in the module bar. A page can now show its own trail above its title: the Blazor `PageHeading` takes `Crumbs`, and the MVC `app-page-header` takes `app-crumb` children (the same tag as in `app-crumbs`). Both render `CrumbHtml`, so the framework has one breadcrumb. Tests: MVC 245, Blazor 793 (2 bUnit and 2 MVC new); the browser gate passes on the three apps. |
| 2026-10-09 | **Packing list: separate layouts per buyer.** Each template has its own sheet (`PackingTemplate.Layout`): H&M (the design), Zara (no remarks, the season in the header), Walmart (a UPC column, the department in the header) and Basic (a quantity line, no weights). The generate form's template chooses the layout in the preview, the Excel editor and the download. A Walmart order (PO-2025-007) with UPCs is added. Every layout keeps the same row structure (five header rows, a blank, the headings, the items, the total). The PDF table sizes its columns in points, so 13-digit UPCs do not wrap. Blazor 791 tests; the browser gate passes. |
| 2026-10-09 | **Insert ERP Field writes into the sheet.** Each field of the panel is a button (`data-m-sheet-insert`, `data-m-sheet-insert-target`); `modulus-plugins.js` sends its token to the sheet, which writes it into the active cell (the sheet is editable now). Checked in the three apps: a click on "Order no" puts `{{PO.Number}}` in the sheet's saved value. Next moves to the preview. Browser gate passes; the solution builds with 0 warnings. |
| 2026-10-09 | **Form layouts: the textarea gap, and the breadcrumb on phones.** The gap above the Message textarea came from the affix wrapper's `::before` (a space, one line tall) and an inline textarea; the wrapper's padding was also overridden by the compact density rule. The rule now targets the wrapper directly, drops the space and makes the textarea a block; checked in all three apps (the textarea starts under its label); a 390 px check of the page has no overflow. The shell's breadcrumb (`CrumbHtml`, the same renderer as `AppCrumbs` and `app-crumbs`) also shows below 992 px, in a strip under the header, with the middle levels in a "…" menu; the module bar keeps the full trail on desktop. Tests: Blazor 789, MVC 243; the browser gate passes on the three apps. |
| 2026-10-09 | **Packing list templates (merchandising).** `/merchandising/packing-lists` in all three admin apps, from the RMG ERP design: the four steps, the Univer editor on the template sheet with the Insert ERP Field panel, the template list (search and filters are GET forms), a preview filled from a sample PO with validation (any unmapped `{{field}}` is listed), the generate form (Excel or PDF) and the output with its download. `PackingListDemo` (Samples.Shared) holds the orders, templates, fields and the fill; `DocumentPdf` now sizes table columns by their content and sets table text at 9 pt. Checked: the page is 200 in all three apps, the browser gate passes, the Excel and PDF downloads are filled with no unmapped tokens, the PDF is one page with no wrapped cells; Blazor 789 tests. Not done: the template and company in the form do not change the output (one layout in the demo), and saving a template (step 4) needs storage the sample does not have, so Save stays disabled. Next moves to the preview. |
| 2026-10-09 | **Form elements: the gaps closed.** New in the MVC library: `addon-before-html` / `addon-after-html` (trusted markup in the add-ons), `vertical` on `app-radio-group` and `app-checkbox-group` (stacked choices). Shared CSS: floating label, loader in a field, stacked groups, checkbox description. Also `multiple` on MVC `app-select` (a bound list marks its chosen options), and the Blazor multi-select (AntD `Select` in multiple mode); a native time input on the MVC page. Tests: 4 new MVC (241 in all); Blazor 776. The browser gate passes on the three admin apps. |
| 2026-10-09 | **Phase 17 closed: the Fluid 500 page.** The Fluid sample had no exception handler, so an unhandled error gave an empty 500. It now renders `Templates/error.liquid` through the layout with status 500 outside Development. Checked on a scratch copy with a route that throws, run in Production: the "Server error" page, no exception text or stack; `/` still 200, a missing invoice still 404. The MVC, Razor Pages and Blazor 500 pages were checked the same way. |
| 2026-10-09 | **Form elements page (`/forms/elements`).** Every control of the Tabler form-elements page, one Modulus control each, in all three admin apps (Blazor page, MVC and Razor Pages through the shared `Ui/Forms/Elements` view), with a menu entry under Forms and a browser-gate page. Items the library lacks in either flavour are listed in §4, not faked. Also: the 500 page checked in Production (MVC, Razor Pages, Blazor: the Modulus error page, no details); Fluid's 500 is an empty body, left open. Verified: the three pages return 200 with no console errors and no horizontal scroll at 1280 and 390 px; `tests/browser/smoke.py` passes on the three admin apps. |
| 2026-10-09 | **Reusable PDF button.** `PdfDownloadHtml.Button` in `Ui.Abstractions/Plugins` renders a submit button that posts a form to a same-site action (the server answers with the PDF). `AppPdfButton` (Blazor) and `app-pdf-button` (MVC) wrap it, and the document page in the three admin apps uses the component instead of the hand-written button. Tests: 9 Blazor, 2 MVC. |
| 2026-10-09 | **Observability (phase 17).** `AddModulusObservability()` in `Modulus.AspNetCore.Hosting` registers OpenTelemetry (`OpenTelemetry.Extensions.Hosting` and `Instrumentation.AspNetCore` 1.19.x, `Exporter.OpenTelemetryProtocol` 1.19.1, all pinned in `Directory.Packages.props`): request traces that skip `/health`, `/_content`, `/_framework` and `/_blazor`, request metrics, and logs with scopes. OTLP export starts only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. Query strings keep the instrumentation's redaction. Wired into the four samples. Metrics still count `/health` (the filter covers traces only). Checked with the Fluid sample against a local OTLP listener: `/v1/traces`, `/v1/logs` and `/v1/metrics` POSTs arrive, and the traces contain the invoice request and not `/health`; with no endpoint, the sample starts and serves with no errors. Tests: 11 new (`ObservabilityTests`); Hosting 15 passing. The 500 page check stays open. |
| 2026-10-09 | **Hardening and quality gates (phases 17 and 18).** New `src/Modulus.AspNetCore.Hosting`: `AddModulusHardening()` / `UseModulusHardening()` and `MapModulusHealth()`, wired into the MVC, Razor Pages, Blazor and Liquid samples. Brotli and gzip for text (`EnableForHttps` off). Security headers on every response. The CSP is built per request with a nonce (`CspNonce.Of(Context)`): **enforced on MVC and Razor Pages** after a survey of 17 pages each reported zero violations (a control inline script was caught, so the listener works); **report-only on Blazor**, whose import map is an inline script with no nonce in Blazor 10, so its one violation per page is accepted and documented. Scripts keep `'unsafe-eval'` for Alpine's expressions. MVC head tags add each library asset's content hash as `?v=`; a versioned `/_content/` response is `immutable` for a year; unversioned ones revalidate (ETag, 304 checked). Gates in `tests/browser`: `smoke.py`, `parity.py` (the pivot markup is identical across the flavours), `budgets.py` (gzipped sizes of the 11 shipped Modulus scripts and stylesheets, each at 15% over today, in `budgets.json`). The CI workflow gets an asset-budget step in the build job and a `browser` job that starts the four samples and runs the gates; **that job has not run on GitHub**, only the same scripts locally. Fixed on the way: the Blazor gate read the first CSP header (Blazor's own `frame-ancestors`) and missed the report-only one. Not in it: OpenTelemetry, visual snapshots, a 500-page check, Blazor's enforced CSP. Tests: 4 new (policy, connect sources and report URI, nonce, defaults); Blazor 748, MVC 234, Fluid 13, Hosting 4, all passing; Release build 0 warnings, 0 errors. |
| 2026-10-09 | **Phase 21 completed: templates, mail merge and the PDF button.** `HtmlBlocks.FromHtml` reads HTML (sanitized first with `RichText.Sanitize`, so only the rich-text tags come through: paragraphs, headings 1–4, bold, italic, underline, strike, nested lists, tables, links as text, images dropped; line breaks become spaces), and `DocumentPdf` and `DocumentDocx` gain `WriteHtml`. `DocumentPdf.WriteMerged` writes several documents as one PDF, each on a new page. Numbered lists count per level (a nested bullet or item no longer restarts the parent's count; checked in `pdftotext`). The Fluid sample has `Templates/document-invoice.liquid` with `/documents/invoices/{id}.pdf` and `.docx` and `/documents/mail-merge.pdf`, and the invoice page links to them. The admin apps (MVC, Razor Pages, Blazor) have a **Download PDF** button beside Download docx (`PluginsContent.DocumentPdfUrl`), each with a `QuestPDF.Settings.License` line in its `Program.cs`. Tests: 12 new (Blazor 767, MVC 234, Fluid 13, all passing; Release solution 0 warnings). |
| 2026-10-09 | **PDF output (phase 21, first slice).** QuestPDF 2026.9.0 (published 2026-09-14) in `Modulus.Documents`, as `DocumentPdf`: the document model (`DocBlock`, or the editor's JSON) as an A4 PDF with the Word output's margins, page numbers in the footer, titles, five heading sizes, runs with bold, italic, underline, strike, size and colour, alignment, bullet and numbered lists (nested, and a numbered list counts on), and tables with borders, whose cells can hold lists. **Licence:** QuestPDF is not MIT. Its Community licence is free for individuals and businesses under USD 1,000,000 in annual gross revenue, and not for public-sector bodies or publicly traded companies; the host sets `QuestPDF.Settings.License`, and Modulus does not choose it. Recorded in `DEPENDENCIES.md` and `docs/DOCUMENTATION.md#upgrading-ant-design-blazor-htmx-alpinejs-and-the-plugin-libraries`. **Not in it:** the Liquid templates (QuestPDF draws from its own layout, not from HTML, so a template reaches PDF only through the document model), the PDF button on the editor's ribbon, and mail merge. Tests: 7 in `DocumentPdfTests`. Checked with `pdfinfo` (A4, one page) and `pdftotext -layout` (text, lists, footer) and a render of the page. |
| 2026-10-09 | **Fluid (phase 16, §7.2 order step 4).** New `src/Modulus.AspNetCore.Fluid` (Fluid.Core 2.40.0, MIT, published 2026-08-20, pinned in `Directory.Packages.props`). `ModulusLiquid` parses and loads `.liquid` templates (a file is parsed again when it changes), renders every value HTML-escaped, and renders a page in a layout: the body is given to the layout as `content` as a `LiquidHtml`, which is printed unescaped. Model types are closed to templates until `ModulusLiquidOptions.Register<T>()`. Tags: `{% modulus_head %}` (the Modulus stylesheets and scripts under the asset prefix), `{% icon "name" %}` (Ant Design, refused when unknown), `{% barcode "value" %}` (Code 128 through `BarcodeSvg`). Filters: `| status_badge: "tone"` (the shared `StatusBadgeHtml`, which is new in `Modulus.Ui.Abstractions/Workflow`; an unknown tone draws as default) and `| money` (invariant, `N2` by default, up to 6 places). Sample `samples/Modulus.Samples.Fluid` on port 5103 (`/` lists the seeded invoices, `/invoices/{id}` shows one with its lines, a status badge, an icon and a barcode), with the shared table style in its layout. **Fixed on the way:** the renderer's `TryParse` method made ASP.NET Core's minimal API fail to build its routes (it reads a `TryParse(string, out T)` method as a binder), so it is `TryParseTemplate`. The sample's first version looked up invoice ids from 1; they start at 4988, so it now lists through `QueryInvoices`. Tests: 13 in `tests/Modulus.AspNetCore.Fluid.Tests` (escaping, `LiquidHtml`, the layout, model registration, the head tag and its prefix, the icon and barcode tags and the unknown-icon refusal, the filter's escaping and tone check, the money formats, parse errors, the file cache, and the barcode's escaping). Browser-checked at 1280 and 390 px: the list and the detail page render, the badges, icon and barcode draw, no horizontal overflow, no console errors. Not in it: `{% render %}`-style includes, a template editor, and PDF or Word output (phase 21). Solution: 0 warnings, 0 errors (Release); tests: Blazor 748, MVC 234, Fluid 13, all passing. |
| 2026-10-09 | **Stabilise the 🟡 work** (§7 order, step 1 and 2). Release build of `Modulus.slnx`: **0 warnings** (it had 59, all xUnit analyzer findings in the test projects: `Assert.Empty(x.Where(...))` in the new dynamic-form tests, `Assert.Equal(1, Count)` in the header-groups tests, and 53 calls that take an optional `CancellationToken` without one, in the saved-views, dashboard and group-by tests). Those now pass `cancellationToken:` (`TestContext.Current.CancellationToken`, qualified as `Xunit.` where bUnit's `TestContext` is also imported). `tools/antd-drift`: no drift. Tests: Blazor 748, MVC 234, passing. **Browser checks:** phase 14 (SPA navigation) on MVC and Razor Pages: a menu click swaps the page in the same document, the title and URL follow, back and forward work, no console errors. Phase 15 (large data) on all three: MVC and Razor Pages infinite scroll (51 to 91 rows) and virtual table (about 32 rows in the DOM after a far scroll); Blazor's Virtual mode (35 rows in the DOM, data at the scrolled position). Blazor has no infinite-scroll mode, which is recorded as a gap, not built. The pivot's 390 px and dark-mode checks (§7.1, item 6) passed on all three with no overflow and no console errors. |
| 2026-10-09 | P1: **Dynamic form** (§7.3), the last P1 item. `FormSchema` (`Modulus.Ui.Abstractions/Forms/FormSchema.cs`) describes a form as data: `FormField` with a `FormFieldKind` (Text, Multiline, Number, Money, Date, Choice, Switch) and its rules (required, maximum length, a pattern for text, minimum and maximum and decimal places for numbers and money, the currency, the options of a choice, placeholder and help). `FormSchema.Parse` reads it from JSON (kinds and option keys not case-sensitive) and `ToJson` writes it back; the constructor refuses what could not be drawn or checked (duplicate or badly formed keys, a choice without options, a rule on the wrong kind, an invalid pattern). `Validate` returns one `FormProblem` per failing field, in field order (keys the schema does not name are ignored); `Normalize` gives what is stored (trimmed text, invariant numbers, yyyy-MM-dd dates, true or false for a switch). `FormSchemaHtml` writes the markup for MVC: `app-schema-form` (`Modulus.AspNetCore.Mvc`, `SchemaFormTagHelper`), with the same item classes and messages as the Blazor `AppSchemaForm` (`Components/Fields`), which writes its wrapper in Razor. **Fixed on the way:** the first Blazor version put the wrapper in a raw `MarkupString`, whose open `div`s the browser closed where the fragment ended, so each control sat outside its box and far below its label; the wrapper is now Razor (noted in DESIGN). The sample `/forms/dynamic` is an expense claim whose fields come from `DynamicFormDemo.SchemaJson`, in all three apps (menu *Forms → Dynamic form*): an empty submit shows five messages, a bad trip reference and a fractional night count are refused, and a valid claim shows the stored values (the MVC and Razor Pages pages render them on the same request). Tests: 31 Blazor (the schema refusals, JSON round trip, each rule, normalising, the shared markup, the component's bindings and messages) and 2 MVC. Browser-checked on MVC, Razor Pages and Blazor at 1280 px (the same messages and stored values in all three, no console errors, no horizontal overflow), at 390 px on all three (the label and control geometry matches; Razor Pages checked in light and dark mode too), and dark mode on MVC and Blazor. Not in it: a schema editor, server-side lookups and dates with times, and the upload or signature kinds. Full suites: Blazor 748, MVC 234, passing. |
| 2026-10-09 | P1: **Rich-text tables** (§7.3). `AppRichText` and `app-rich-text` take tables: the toolbar's **Insert table** (a header row and two body rows, with the caret after it), **Add row below**, **Add column right**, **Delete row** and **Delete column** (the last two need the caret in a cell, so they are disabled until it is), and Tab / Shift+Tab move between cells, with Tab in the last cell adding a row. `RichText.Sanitize` keeps `table`, `thead`, `tbody`, `tfoot`, `tr`, `th` and `td`, and `colspan` / `rowspan` on cells as whole numbers from 2 to 50 only; `ToPlainText` puts a space between cells and rows. The table buttons are rendered disabled in both flavours and enabled by `modulus-shell.js`. Shared CSS in `modulus.css` (cell borders, a shaded header row). Tests: 5 sanitizer cases (theory rows), a plain-text assertion, the toolbar icon check extended to the table buttons, and a bUnit and an MVC case for the disabled buttons. Browser-checked on MVC and Blazor at `/forms/business` (Terms and notes): insert, row and column add and delete, Tab and Shift+Tab, Tab in the last cell, the hidden value; no console errors. Dark mode checked on MVC: borders and the header row read clearly. Not in it: merging cells from the toolbar (the markup accepts spans), column widths and a table's own header/footer tools. Full suites: Blazor 748, MVC 234, passing. |
| 2026-10-09 | Polish pass on the shipped components. **Query builder** (`QueryBuilderHtml`, Blazor `QueryBuilder`): a rule connector, icon remove buttons, dashed add buttons, an empty state, and a checkbox dropdown for multi-value choices (`details.m-dropdown.m-qb-multi`) in place of the native listbox, styled for dark mode. A **multi-value bug** fixed: MVC and Razor Pages read a repeated query value as one comma-joined string, so the filter was dropped and 100,000 rows came back; the three callers now split each value (11,213 rows, verified). Tests: 4 bUnit markup cases. **Header flicker:** the full-screen button and the search box jumped on a hard refresh because a script added `m-can-fullscreen` late; `modulus-theme.js` now sets it synchronously, so no frame shifts (measured per frame). **PDF page turns** dimmed the canvas; the toolbar now shows a progress bar, and the prev/next and page box are rendered with their disabled states (1 bUnit case). **Grid band over pinned columns:** a band row covered pinned columns with one empty cell, so the bands scrolled under the sticky pinned headers. `HeaderGroups.Rows` takes `pinnedLeft`/`pinnedRight` and emits one pinned placeholder per pinned column (`data-m-pin`); `modulus-shell.js` copies each placeholder's offset from the header cell under it, and the bands span only the middle columns. Used by the MVC `EditableGrid` and Blazor `EditableGrid`; ProTable and the Advanced table have no pinned columns and are unchanged. Verified at a 1000 px horizontal scroll (the bands stop at the pinned edge). Not checked: a vertical scroll of the group band against the sticky header, and the Razor Pages editable page. Tests: 2 new HeaderGroups cases, and the two band assertions updated to the new markup. Full suites: Blazor 668, MVC 222, passing. |
| 2026-10-09 | Packages from the plan (the phase 23 leftovers in §3, and the drop area of §4 Plugins): **Uppy** (the drop area; `AppUploader` / `app-uploader`), **Cropper.js 2.2.0** (`AppImageCropper` / `app-image-cropper`, `/plugins/image-cropper`) and **CodeMirror 6** (`AppCodeEditor` / `app-code-editor`, `/plugins/code-editor`). Each is one classic-script bundle built with esbuild (`lib/uppy`, `lib/cropper`, `lib/codemirror`; each `LICENSE.md` gives the packages, versions and the build). Every version is pinned exactly and is at least two weeks old (Uppy core 6.0.2 was published 2026-09-25, Dashboard and XHR upload 6.0.0 on 2026-08-26; Cropper.js 2.2.0 on 2026-08-23). Shared renderers `UploaderHtml`, `CropperHtml` (with `CropBox`, `ParseBox`, `Format`) and `CodeEditorHtml` in `Modulus.Ui.Abstractions/Plugins`. The Blazor components render their root and hidden input themselves, so the change event binds (`CropChanged` takes `CropBox?`, `ValueChanged` the text); the MVC tag helpers are `app-uploader`, `app-image-cropper` and `app-code-editor` (a bound control, so `asp-for` works). `/plugins/dropzone` draws its drag-and-drop card with Uppy in all three apps; the button upload stays on `app-upload`. The cropper accepts image data URIs, because the gallery's pictures are data URIs (`<img>` runs no script). **Fixed on the way:** the MVC dropzone button passed `max-size` as a literal string (no `@`); Uppy's XHR had to send the page's `hx-headers` antiforgery token, which the MVC endpoint checks (a 400 with no body without it); an empty 2xx reply is a success for Uppy (`getResponseData`), where the Blazor endpoint answers 200 with no body; the cropper's readout was taken before Cropper.js applied the ratio (a microtask), so it is read after; pdf.js was missing from `DEPENDENCIES.md` and `UPGRADING.md`, and now is listed. Browser-checked on MVC, Blazor and Razor Pages: a PDF uploads ("1 file uploaded") and a `.exe` is refused by Uppy; 1:1 makes the box 75 × 75 px and the form value follows; typing in the JSON rule reaches the hidden input; the read-only editor takes no edits; Blazor's "Last crop" follows the crop; dark mode and 390 px checked with no horizontal overflow; no console errors. Not in it: the upload endpoint still discards files (the demo), the cropper has no rotate or zoom, and CodeMirror has no linting. Tests: 25 Blazor (the renderers, the refusals, the component change events), 3 MVC. Full suites: Blazor 711, MVC 231, passing. |
| 2026-10-09 | P1: **Multi-column, paged lookup** (§7.3). `AppLookup` / `app-lookup` with `Columns` now shows those columns in the suggestion list under a header (`m-lookup-head`, `m-lookup-cells`), as the dialog does; without `Columns` the list keeps its label and details. The "Search more" dialog gets a **Show N more** button that asks for a longer list (the search takes a count, not an offset, so "more" raises it by a page; the button shows while more may exist). Blazor and the MVC Alpine `modulusLookup` (`more()`, `limit`, `multi`, `titles`) match. The business sample's customer and product lookups use columns, so both show it. Tests: 4 bUnit, 2 MVC. Browser-checked on MVC and Blazor: four columns under a header, and the product dialog 50 then 100 rows with no console errors. **Also fixed in the picker popup (`modulusPicker`, shared by MVC and the editable grid):** a click anywhere in the popup was read as a month step, because `closest('[data-nav]')` matched the page's own `<html data-nav="light">`; so no date, week, month, year or quarter could be picked. The lookups are now limited to the popup. The week and date pickers, the date-time popup and the range presets were checked on MVC and Blazor. Week text decided 2026-10-09: keep AntDesign's default ("2026-41Week"), the same in both flavours. The week numbers are Sunday-first (en-US), so an ISO "2026-W41" label would name a different week for some dates; the pivot's ISO labels would then disagree. Switching to ISO numbering is a separate change to the grid, the picker and the posted values. Full suites: Blazor 686, MVC 228, passing. |
| 2026-10-09 | P1: **Workflow and audit kit** (§7.3). `Modulus.Ui.Abstractions/Workflow/`: `ApprovalTimelineModel` (`ApprovalStep`: name, state, who, when, note; `ApprovalState` Done, Current, Waiting, Rejected, Skipped; each step's tone and state text, time as "MMM d, yyyy · HH:mm") and `AuditTrailModel` (`AuditEntry`: id, time, actor, action, field with before and after, or a note; entries grouped by day, newest first, filtered by action ignoring case, the change as "Field: before → after"). Blazor `ApprovalTimeline`, `AuditTrail` (filter buttons, `@bind-Action`) and `StatusBadge` (tones success, processing, error, warning, default) in `Components/Pro`; MVC `app-approval-timeline`, `app-audit-trail` (filter links) and `app-status-badge`, with the same markup. The sample `/apps/workflow` (purchase order PO-2041: two steps done, one in review, one waiting, one skipped; six audit entries) is in the Productivity menu in all three apps. **Fixed on the way:** an MVC `action` query parameter bound to the route's action name (`Index`), so the filter never matched; the controller binds `?action=` explicitly. Also, Blazor rendered `aria-pressed` for a selected filter as an empty attribute, so it is now `"true"` / `"false"`. Tests: 10 model cases (5 with a theory row), 4 bUnit, 4 MVC. Browser-checked on MVC, Blazor and Razor Pages: five steps with their tones, the filter (Edited: 2 days, 2 rows) in all three, no console errors. Full suites: Blazor 682, MVC 226, passing. |
| 2026-10-09 | P1: **Barcode** (§7.3). `Modulus.Ui.Abstractions/Barcode/BarcodeEncoder.cs`: `BarcodeKind` (`Code128`, `Ean13`), `BarcodeEncoder` (Code 128 set B, or set C for an even run of digits, with the mod-103 check; EAN-13 from 12 digits, adding the check digit, or 13 checked; a 10-module quiet zone; `Problem` says why a value cannot be drawn, `Modules` gives the bars and spaces) and `BarcodeSvg.Render` (one rectangle per run of bars, the value printed under them, a white background by default so a dark theme does not stop a scanner). The Code 128 bar patterns were **generated from ZXing.Net's `Code128Writer`** (a throwaway test read each symbol's modules), not typed from memory: the first hand-typed table had three duplicate patterns and six wrong entries. Blazor `AppBarcode` (Components/Extras) and MVC `app-barcode`. The sample `/plugins/barcode` (a shipment, a product, digit pairs, and a refused wrong check digit) is in the Plugins menu. Tests: 21 encoder cases (ZXing decodes every case back, and our modules equal ZXing's module for module where both use the same set), 3 SVG cases, 2 bUnit, 3 MVC. Browser-checked on MVC, Blazor and Razor Pages: three symbols and the refusal on each. Full suites: Blazor 661, MVC 222, passing. |
| 2026-10-09 | P1: **Dashboard layout** (§7.3). `Modulus.Ui.Abstractions/Dashboard/DashboardModel.cs`: `DashboardTileSpec` (id, title, default columns and rows), `DashboardPlacement` (a tile's place and size; the list's order is the page's order), `DashboardModel.Resolve` (a saved layout for the tiles that still exist, sizes clamped to 1–12 columns and 1–6 rows, new tiles added at their defaults), `Apply` (earlier and later swap with the neighbour, wider and narrower by a column, taller and shorter by a row, each within the grid), JSON in and out (`dashboard:{id}` in the `IUiStateStore`; bad entries dropped, at most 50 tiles), and `LoadAsync`/`SaveAsync`/`ClearAsync` (an anonymous visitor keeps nothing). Blazor `DashboardGrid` (a `Tile` template per tile; the buttons; reset; a note to sign in) and MVC `app-dashboard` (a form per tile that posts the change and the layout the page drew to `MapModulusDashboard()` at `/_modulus/dashboard`, which checks the antiforgery token and redirects to a local path). The sample `/dashboards/tiles` (four number tiles and an activity feed) is in the Overview menu. **Deviation:** resizing and moving are buttons, not pointer drag with a corner handle; the buttons work with mouse, keyboard and touch, and a pointer-drag handle is still open. **Fixed on the way:** `TryGetInt32` threw on a string value, so a posted `"width":"3"` would have failed the request; the value kind is checked first. Tests: 18 model cases (5 of them theory rows), 6 bUnit, 3 MVC. Browser-checked on MVC, Blazor and Razor Pages: sign in, narrow a tile, move one earlier, reload (both kept), reset (default back). Full suites: Blazor 635, MVC 219, passing. |
| 2026-10-09 | P1: **File manager** (§7.3). `Modulus.Ui.Abstractions/Files/FileManagerModel.cs`: `FileEntry` (id, parent, name, folder or file, size, modified), `FileManagerModel.Build` (a folder's children, folders first then by name, the breadcrumb to it, a search within it; an unknown folder shows the top level), `NameError` (required, at most 120 characters, no `. ..`, no `\ / : * ? " < > |` or control characters, unique among siblings ignoring case), `SizeText` (bytes, KB, MB, GB), `ModifiedText` (time today, yesterday, month and day, or the full date) and `KindOf` (from the extension). Blazor `FileManager` (breadcrumb, search, new folder, inline rename, delete after a confirmation; names are checked before a callback) and MVC `app-file-manager` (boosted folder and search links, Alpine create and rename forms, htmx posts to `/folder`, `/rename` and `/delete/{id}`, `files:refresh`). The sample `FileStore` (a singleton, in memory) and `/apps/files` in all three apps, in the Productivity menu. **Fixed on the way:** the sample apps re-executed every error status to the not-found page, so a 422 from an htmx form (the Kanban add with a blank title too) showed a 404 page; htmx requests now keep their status in the MVC and Razor Pages apps. **Also fixed:** the Blazor page passed `Search="_search"`, a literal string, because Razor treats a string-typed parameter's quoted value as text; it is `Search="@_search"`. Tests: 19 model cases (7 facts and 2 theories with 12 cases), 7 bUnit, 2 store, 3 MVC. Browser-checked on MVC, Blazor and Razor Pages: open a folder, search, create, a duplicate name refused, rename, delete. Full suites: Blazor 611, MVC 216, passing. |
| 2026-10-09 | P1: **PDF viewer** (§7.3), a plugin like the map. pdf.js 6.3.289 (Apache-2.0, published 2026-08-29, two weeks old at the time) vendored in `lib/pdfjs` as its ESM builds `pdf.min.mjs` and `pdf.worker.min.mjs` with its LICENSE, loaded with `import()` on first use (`modulus-plugins.js`, `[data-m-pdf]`). `Modulus.Ui.Abstractions/Plugins/PdfHtml.cs` renders the viewer (previous and next, a page box, zoom from 50% to 300%, a download link); it accepts only a same-site path or an http(s) address and refuses `javascript:`, `data:` and protocol-relative URLs. Blazor `AppPdf` and MVC `app-pdf`. The sample `/plugins/pdf` shows a generated three-page `sample.pdf` (the MVC and Razor Pages apps serve it from Samples.Web's `_content` path, Blazor from its own copy). Package records added the same way as MapLibre: `docs/DOCUMENTATION.md#upgrading-ant-design-blazor-htmx-alpinejs-and-the-plugin-libraries` (table and upgrade steps), `DEPENDENCIES.md`, the README, and the AntDesign manifest's Plugins category. Tests: 11 Blazor (renderer and component), 1 MVC. Browser-checked on MVC, Blazor and Razor Pages: the canvas draws, next goes to page 2, zoom goes to 125%, and a typed 9 clamps to page 3. Full suites: Blazor 583, MVC 213, passing. |
| 2026-10-09 | P1: **Gantt as library components** (§7.3, read only). `Modulus.Ui.Abstractions/Gantt/GanttModel.cs`: `GanttTask` (id, name, start and end days, progress, group, tone, owner), `GanttView` (the range, weekly ticks on the Mondays, rows, the today position) and `GanttModel.Build` (tasks reaching into the range, grouped in first-seen order, bars as percentages of the range, clipped edges, progress clamped to 0–100, late when ended before today and not finished; refuses ranges outside 1–366 days and tasks that end before they start). Blazor `Gantt` (task names and owners, the bars filled to progress, group headings, the today line in each row) and MVC `app-gantt` (the same markup). The sample `GanttPage` lays out a website relaunch from this week's Monday, at `/apps/gantt` in all three apps and in the Apps menu. Tests: 10 library model cases, 5 bUnit, 2 MVC. Browser-checked on MVC, Blazor and Razor Pages: 9 rows, 3 groups, one late bar, the today line, no console errors. A one-day bar was too narrow for its label, so bars have a 24 px minimum. Full suites: Blazor 572, MVC 212, passing. |
| 2026-10-09 | P1: **Scheduler as library components** (§7.3). `Modulus.Ui.Abstractions/Scheduler/SchedulerModel.cs`: `SchedulerEvent`, `SchedulerCategory`, `SchedulerQuery` (month, grid or agenda, categories on), `SchedulerView` (the grid's days, the agenda, upcoming, the filter state; `Build` keeps the events in the six weeks whose category is on) and `SchedulerModel` (the query from the URL, the grid's first day, the links' query strings, time labels). Blazor `Scheduler` (side panel and month or agenda; a month, view or filter change calls `OnQuery` and the page reloads; a day calls `OnAdd`; an event calls `OnEdit`) and MVC `app-scheduler` (boosted GET links and filter form as before; the `scheduler:refresh` event reloads after a save). The sample's `CalendarPage` loads the store's events; the event form stays the page's. Tests: 10 library model cases, 7 bUnit, 3 MVC. Browser-checked on MVC, Blazor and Razor Pages (month and agenda switches, Today, the Holiday filter 18 to 16 events, an event opening its drawer, no console errors). Full suites: Blazor 557, MVC 210, passing. |
| 2026-10-09 | P1: **Kanban as library components** (§7.3). `Modulus.Ui.Abstractions/Kanban/KanbanModel.cs`: `KanbanColumn`, `KanbanCard`, `KanbanMove` / `KanbanNewCard` (the callbacks' arguments) and `KanbanModel.Build` (columns in order, cards by place, overdue unless done, label tones, up to three avatars with initials and tints, the title rule). Blazor `KanbanBoard` (drag between and within columns, the card menu, the add form, a delete confirmation; `EventCallback`s, so the parent re-renders after a change) and MVC `app-kanban` (Alpine drag and htmx posts, the same routes as before). The sample's `KanbanPage` maps the store's tasks to cards; the three apps' kanban pages use it. Tests: 10 library model cases (7 facts and a 3-case theory), 4 bUnit (the columns and cards, drag, add, a blank title refused) and 3 MVC. Browser-checked on MVC and Blazor: drag a card to another column, add a task, delete one (the counts move each time). Found while checking: `Func` callbacks did not re-render the parent, so the Blazor board did not show an add until `EventCallback` replaced them. Full suites: Blazor 540, MVC 207, passing. |
| 2026-10-09 | Phase 26: **printing components** (§7.3). `Modulus.Ui.Abstractions/Printing`: `PrintOptions` and `PrintCss.Page` write the `@page` rule for a print view's paper (A4 or Letter), orientation and margin; `PrintTableHtml` renders a table whose header sits in a `thead` (repeats on every page), whose rows do not split, and whose total sits in a `tfoot` (printed once: a browser repeats a `tfoot` by default, so the print CSS sets it to a row group); `LabelLayout` (columns, rows, label size, gaps, margin; `A4ThreeBySeven` is the common 21-up sheet) and `LabelHtml` place the labels on sheets, one sheet per page. Blazor `LabelSheet` and `PrintTable` components and the MVC `app-label-sheet` and `app-print-table` tag helpers draw that markup. Print CSS in `modulus.css` (*Printing*): the sheet and table styles, print-only rules, and `.m-print-hide`. The sample `/tables/print` links to two print views on the blank layout (`/tables/print/sheet?kind=table` and `kind=labels`), which print on load like the invoice. Checked with Playwright's print PDF on all three apps: the parts list is 4 pages with the header on each and the total once; the 30 bin labels are 2 pages of 21 and 9. Tests: 9 library, 2 bUnit and 2 MVC. Full suites: Blazor 526, MVC 204, passing (one Blazor run failed while the three sample servers were loading the machine; three later runs were clean). |
| 2026-10-09 | Phase 25: **import wizard** (§7.3). `Modulus.Ui.Abstractions/Import`: `CsvReader` (RFC 4180 quotes and line breaks, comma, semicolon or tab delimiter, BOM, blank lines skipped); `ImportField` (kind, required, choice options, header aliases); `ImportMapping.Suggest` matches headers to fields by title, key, then alias (ignoring case and punctuation, one column per field); `ImportValidator.Validate` checks each row (number, `yyyy-MM-dd` date, a choice's value or label, text up to 500 characters, required cells) and refuses a file over 2,000 rows; `ImportPreview.CanCommit` only when every row is valid (a bad file is fixed and uploaded again; nothing is imported half way). `ImportFlow` runs the steps (`Load`, `Check`) for both front ends, and `ImportWizardHtml` renders the steps as forms: each step posts the file (hidden `csv`), the mapping (`m.{key}`) and a `step` button, so MVC needs no session. Limits: 1 MB, 2,000 rows, 100 columns. Blazor `ImportWizard` (`InputFile`, the same steps and classes) and `app-import-wizard` (MVC) render it; the sample `/tables/import` (customers into `CustomerImportStore`) runs in all three apps, with an example file that has a bad row on purpose. Tests: 18 library, 6 bUnit (upload through import, the callback, refused files, start over) and 3 MVC. Browser-checked on all three: the bad file shows the problems and disables Import; the good file imports. Also in this pass: the query table scrolls inside its card at 390 px (the page had been 521 px wide), and the phase 24 mobile and dark-mode checks are done. Full suites: Blazor 515, MVC 202, all passing. |
| 2026-10-09 | Phase 24: **QueryBuilder**, a visual WHERE builder (§7.3). `Modulus.Ui.Abstractions/Query`: `FilterGroup` / `FilterRule` (JSON via `FilterJson`; limits in `FilterLimits`: 30 rules, three group levels, 200-character values); `QueryBuilder<T>` registers Text, Number, Date and Choice fields with typed member expressions, `Normalize`s a filter (unknown fields, operators a kind doesn't offer, values that don't parse or aren't options are dropped), `Compile`s it to an `Expression<Func<T,bool>>` for EF and in-memory sources, and `Describe`s it in words. `QueryBuilderForm` reads the form's names (`f.{path}.{prop}`) and applies the add, remove and clear buttons; `QueryBuilderHtml` renders the GET form. `ProTableRequest.Where` carries the filter; `TableQuery.Filters(builder)` applies it after the column searches (restricted fields drop their rules). Front ends: the Blazor `QueryBuilder` component (same classes and the same `Edit`; edits apply at once) and `app-query-builder` (MVC; a field or operator change submits so the operators redraw). Sample `/tables/query` in all three, with the sentence, count, total and the first 25 rows. **Deviation from the plan:** the filter plugs into `TableQuery` (so into any page through `Where`), not into `ColumnSearch` strings: a column search can't express groups joined by OR, so the two stay separate grammars. Tests: 21 Blazor (model, compile, normalize, describe, form, renderer, table, and the component) and 2 MVC; the component's markup is checked against the MVC renderer's classes. Browser-checked: MVC (add, field, operator, value, apply, group, join), Blazor (the same, plus clear and a multi-select) and Razor Pages over HTTP. Also fixed a flaky bUnit test (`EditableGridTests.RowRule_…`: its find and click now run inside `InvokeAsync`); eight clean Blazor runs after the fix. |
| 2026-10-09 | Phase 23: **pivot derived measures** (ratios). `PivotQuery.Ratio(key, title, numerator, denominator, decimals, percent)` divides one measure (or `count`) by another for every cell and total, from their sums, so a total is the ratio of the totals. The query stores only the sums a ratio needs (`Stored`, capped at eight); a ratio's cell with no denominator shows nothing; `Restrict` drops a ratio whose measure a user may not see. `PivotMeasureInfo.Percent` shows the fraction as a percentage in the table and the workbook (`0.00%`). The sample's "Amount per shipment" is amount ÷ count (checked on `/tables/pivot`: 254,093,087.51 ÷ 100,000 = 2,540.93). Tests: 26 pivot tests (incl. the workbook percent style); full suites 469 Blazor and 197 MVC pass. |
| 2026-10-09 | Phase 23: **TreeGrid mode on `NestedGrid`**, with a sample at `/tables/tree` (`BillOfMaterials` settings and `BillOfMaterialsStore` in `Modulus.Samples.Shared`; `TreeGridActions` in Samples.Web; Blazor, MVC and Razor Pages pages). Assemblies roll up their parts' cost, so the top level's total is one bill's cost. Browser-checked on all three: expand, totals (33.82 root, 26.50 under the jacket), save of an edited quantity (jacket total follows). Fixes found on the way: `NestedGridSettings.LevelCount` (the level clamp in `NestedGridForm.ReadRequest` stopped at the one-entry `Levels`), and MVC leaves no longer show an expander (`EditableGridTagHelper.RowExpandable`, as Blazor's). Menu icon `tb:list-details`. `NestedGridSettings.Tree` (`NestedGridTree`: `MaxDepth`, `ChildCountField`, `Rollup`) repeats the first level at every depth up to `MaxDepth`; a row expands only when its child count is above zero. `Rollup` columns are totalled over every row that matches a level (`NestedGridPage.Totals`, `NestedGridQuery.Totals`) and shown under the level's table in both flavours, with the row count. The MVC sample loaders pass the totals through. Tests: 465 Blazor (one run failed once, not reproduced in four reruns; the test name was not captured) and 196 MVC pass; sample build 0 warnings. |
| 2026-10-05 | Phase 23: **ERP batch toolbar** on the editable grid. `GridBatchAction` gains `Icon`, `Primary`, `Danger`, `Confirm`, `When` (which rows it applies to) and `Print`; `GridBatchAction.Printing()` prints the selected rows. The batch bar shows each action with the number of selected rows it applies to ("Approve 4"), disabled with a tooltip when none apply; locked rows never count (`GridSchema.BatchRows`, `Applies`, `BatchKeys`, `BatchConfirm`: "Approve 4 rows? 6 selected rows are skipped.", `BatchNone`). Blazor: `EditableGrid Batch` + `OnBatch(GridBatchRequest)`. MVC: `app-editable-grid batch="…"` posts `{name}.op=batch:{key}` with `{name}.selected`; `EditableGridForm.Batch(post, actions)` (after `GridSchema.Protect`) returns the request or null. Print stays in the browser: `modulusShell.printTable(title, columns, rows)` next to `modulusShell.print` (now in `modulus-shell.js`). Sample (`/tables/editable`, all three apps): a read-only Approval column (Pending / Approved / Rejected), Approve and Reject on the pending rows, saved straight away (`ProjectGridStore.ApplyBatch`; Save keeps the stored approval), and Print. Checked in headless Chromium on 5191–5193: counts, confirm text, Approve and Reject persist after a reload, the toast, no "Unsaved changes" after a batch post, 390 px with no sideways scroll. 9 new tests; 658 green. |
| 2026-10-05 | Phase 23: **row states and row errors** on the editable grid. `GridRecord.State` is `Editable`, `ReadOnly` (cells can't change; the row can still move and be deleted) or `Locked` (no edit, delete or move), with `StateReason` ("Closed: done projects can't change"). Both grids show the state: muted text, a lock or eye mark in the first cell with the reason as its tooltip and screen-reader text, a grey bar on locked rows, disabled editors, Switch and Rate; empty cells show "—" instead of "Add"; a locked row's drag handle and delete button are disabled and batch Delete / Set value skip it ("Delete 8 rows? Locked rows stay."). A row error (`GridError.ForRow`, `IsRow`) is a rule over several cells: tinted red with a red bar and its message under the first cell; `GridSchema.Validate(columns, rows, rowRule)` skips read-only and locked rows and runs the rule on rows whose cells are valid; Blazor `EditableGrid ValidateRow` and `ShowErrors` (the server refusing rows from `OnSave` keeps the edits dirty). The error banner counts cells and rows (`GridSchema.ErrorSummary`: "1 row needs attention before saving."). MVC: `GridSchema.Protect(columns, posted, stored)` puts the stored values back into read-only and locked rows and re-inserts a deleted locked row, so a hand-made post can't change them; `delete-row` refuses a locked row. Also `CanEdit`, `StateText`, `StateIcon`, `StateClass`, and `SortableHtml.RowHandle(…, disabled, disabledTitle)`. Sample (`/tables/editable`, all three apps): done projects are locked, Partner API is read-only, and Due before Start is a row error. Checked in headless Chromium on 5191–5193: the states and marks, no editor on a locked cell, batch delete keeps the locked rows, the row error after Save, 390 px dark; route sweep of 96 routes clean. 4 new tests; 649 green. |
| 2026-10-05 | Phase 23: **Excel-style checkbox filter menu.** A column's search menu can list its values with checkboxes (search box, Select all, "(Blanks)", counts), as Excel's AutoFilter does. The pick is stored in the existing column search as a value list: `=a\|b` keeps those exact values, `!=a\|b` drops them, and `""` is a blank cell; `ColumnSearch.ValueQuery(picked, values)` writes whichever form is shorter (nothing picked out → no search), `Picked` reads it back, `Describe` labels the trigger ("Not Olivia Bennett"), `ValuesOf` and `Narrow` build and filter the list (`ColumnValue(Text, Count)`, at most `MaxValues` = 200, `BlankText`), and a list may be `MaxListLength` = 2000 characters. `GridSchema.SearchValues` offers the options of Select, Tag and Switch columns. Blazor: `ColumnHeader Values` with `ColumnValueList`; MVC: `ColumnHeader.Content(values:)`; the editable grids (Blazor, `<app-editable-grid>`) list Select, Tag and Switch columns by themselves. Server side, `TableQuery.SearchText` and `DataGridAuto` turn a value list into `values.Contains(x)` over a parameter list, so EF Core writes `IN` / `NOT IN (@p…)` (enums by their labels, bools as Yes/No), and a list is never cut at `MaxSearchLength`. Samples: `/tables/advanced` (Department, Office, Status) and `/tables/editable` (owner, priority) in all three apps. Fixes: Alpine put the menu's undeclared fields (`_boxes`, `_current`, `_scroll`) on the outermost scope, so two menus shared them; every component now declares its private fields (`modulusGrid`, `modulusAnchor`, `modulusAffix`, `modulusBackTop`, `modulusCountdown`, the dropdown, tabs and upload in `modulus-mvc.js`). In filter-row mode the checkbox-list columns keep their icon (the row's select picks one value). The menu focuses its input with `preventScroll` and starts its close-on-scroll listener two frames later (at 390 px the scroll into view closed it at once). An MVC client-side filter updates the trigger's `aria-label`. Checked in headless Chromium on 5191–5193: Department 86 → 79 rows and Reset back, owner 12 → 10, the pick survives reopening, labels right, no page errors, also at 390 px dark; route sweep of 96 routes clean. 15 new tests; 645 green. |
| 2026-10-04 | **Browser checks** of the built-but-unseen work, on all three samples. Route sweep of all 96 menu routes at desktop, 390 px and dark mode: clean. Saved views on `/tables/pivot` and `/tables/advanced` (save as default, the name on the trigger, the bare URL opening the default, unstar, delete, back). Pivot (§7.1 item 6): header dropdowns on screen and on top at 1300 and 390 px, the busy bar clears, page and table scroll survive, back works. Spreadsheet and document (phase 20): a typed value survives Save, the xlsx and docx downloads are valid, the downloaded xlsx imports. Fixes: `/tables/large` threw "density is not defined" in MVC and Razor Pages (the virtual table has no density menu: `density="false"`); `/plugins/player` overflowed 4 px at 390 px (`.m-media` gets `min-width: 0`, and the back and forward buttons hide below 576 px). 630 tests green. |
| 2026-10-04 | Phase 23: **multi-level headers.** `HeaderGroup(title, keys) { Level }` and `HeaderGroups` (`Ui.Abstractions/Grids/HeaderGroups.cs`): `Bands` merges neighbouring shown columns under one group (keys ignore case; a hidden column narrows its band, an untitled cell covers the rest) and `Rows` writes one `<tr class="m-header-bands">` per level, outermost first, each band with `colspan` and `data-m-span`. `ProTable HeaderGroups` renders them through Ant Design's `HeaderTemplate` above its own header row; `GroupingTable.ShownColumns()` maps the shown columns to field names (through `HeaderColumns` by `ColIndex`, since the first-pass columns have none) and the bands render again after a render that changes them. `EditableGrid HeaderGroups` does the same from its `GridColumn`s (its column markup moved into a `RowCells` fragment), and so does `<app-editable-grid header-groups>`. MVC: `HeaderGroups.Rows(shown, groups)` at the top of the `<thead>`; `modulus-shell.js` narrows or hides bands when the column picker hides columns, and the picker, resizing and sticky headers read the leaf row. Styles in `modulus.css`. Sample: `/tables/advanced` has "Job" over Position, Department and Office and "Employment" over Start date, Salary and Status (checked in the browser on MVC and Blazor, also with `?hide=department,office`: Job narrows to one column and the bands always add up to the header's cells); `/tables/editable` has "Money" over Budget, Spent and Progress and "Schedule" over Start and Due (checked on MVC and Blazor). 6 new tests; 630 green. |
| 2026-10-04 | **Print for the spreadsheet and the document** (`modulus-plugins.js`; Univer's printing is a Pro plugin, so ours is OSS): the content is rendered as HTML in a hidden frame and printed. Sheets: every visible sheet on its own page, display values from `FRange.getDisplayValues()` (formula results and formats), cell styles, borders, merges, column widths, hidden rows and columns skipped, frozen rows as a `thead` repeated on each page, a wide sheet zoomed to the page width. Document: `@page` from its page size and margins, title, subtitle and headings 1–5, alignment and spacing, bulleted, numbered (1. a. i.) and check lists, tables with column widths, spans and cell colours, links, sub- and superscript, inline images (printed once loaded), page breaks. Triggers: a Print button on both ribbons (a printer icon registered with Univer's icon manager), Ctrl+P inside an editor, and `[data-m-print="{editor id}"]` on any button (read-only editors too, and before Univer has started, from the markup's JSON). Samples: a Print button on `/plugins/spreadsheet` and `/plugins/document` (editable and read-only) in all three apps; checked in headless Chromium on MVC (the printed HTML of the budget workbook and the contract, as screenshots and PDF). |
| 2026-10-04 | Phase 23: **group-by with group headers, footers and aggregates.** `ProTableRequest.GroupBy` (`?groupBy=`, a column key; `ProTableUrl` reads and writes it) names the field; the response carries `Groups` (`TableGroup`: key, label, row count, totals, and whether it continues from the page before or onto the next) and `Totals` over every matching row (`TableTotal`, `Ui.Abstractions/Grids/TableGroups.cs`). `TableQuery.Group(field, label)` (a registered sort) and `Total(field, title, value, aggregate, decimals)` (Sum, Average, Min, Max; at most eight): a grouped page sorts by the group field first (in its requested direction), then the other sorts and the id, pages by offset, reads one row either side to know whether its first and last groups continue, and fills the counts and totals with one `GROUP BY` over the page's group values (a parameter list) plus one over everything (`TablePage.ToResponse()` carries both). `ProTable GroupFields` adds a Group by dropdown to the toolbar (`GroupByAsync` from code); rows render through Ant Design's `RowTemplate` (`GroupingTable` exposes the column count for the colspan): a header row before each group's first row (toggle with `aria-expanded`, value, row count, totals), a "Subtotal" row after its last when it ends on the page (`GroupFooters`), and the grand totals in the table footer; collapsing a group hides its rows. Rows can't be dragged while grouped. MVC: `TableGroups.HeaderRow`, `FooterRow`, `RowAttribute` and `Totals`, and `modulus-mvc.js` toggles `[data-m-group-toggle]`. `ModulusDataGrid` forwards `GroupFields` and carries the groups. Sample: `/tables/advanced` groups by department, office or status with payroll and average salary (checked on MVC: `?groupBy=Department&pageSize=20` shows Design, Engineering and Finance with subtotals for the first two, Finance "continued" on page 2, total 86 rows). 8 new tests (in memory, continuation, descending, restrict, markup, the SQL through EF Core, bUnit rendering and collapse); 624 green. |
| 2026-10-04 | Phase 23: **multi-column sort.** `SortKey` and `SortKeys` (`Ui.Abstractions/Grids/ProTableModels.cs`): a plain header click sorts by that column alone (ascending, descending, unsorted); Shift (or Ctrl/Cmd) + click adds the column after the other sorts, then turns it, then drops it; at most four. `ProTableRequest.ThenBy` holds the sorts after the first as text (`thenBy=-salary,name`, a minus is descending), so MVC binds it and old `sortField`/`sortOrder` links and saved views still work; `ProTableRequest.Sorts` reads and writes the whole list; `ProTableUrl` reads and writes `thenBy`. `TableQuery` orders by every registered sort, then the id (one `ORDER BY` in SQL; `SortsOf` reports what it applied); a cursor over several sorts pages by offset, keyset paging stays for one sort. `SortBy(request, keys)` does the same in memory. Headers: `ColumnHeader` (Blazor and MVC) show each column's place among several sorts as a small number by the carets, say "sort 2" to screen readers and "Shift+click to add" in the tooltip; `IColumnHeaderHost` gained `SortAsync(key, add)`, `MultiSort` and `SortRank` with defaults (EditableGrid keeps one sort). `ProTable MultiSort` turns it on; MVC passes `sortAddHref`/`sortRank` to `ColumnHeader.Content` (the sample's `Grid.SortTh(…, multiSort: true)`), and `modulus-shell.js` follows `data-sort-add-href` on a modified click (through the list's boost area). Sample: `/tables/advanced` in all three apps (checked on MVC: `?sortField=Department&sortOrder=Ascending&thenBy=-Salary` lists Design then Engineering, salaries falling within each, ranks 1 and 2 on the headers). 11 new tests; 616 green. |
| 2026-10-04 | Phase 23 started: **edit states in the editable and nested grids** (Blazor and MVC). A changed row is tinted amber with an amber bar at its start; each changed cell is tinted twice as strong with a corner mark (Excel's changed-cell triangle) and reads "(changed)" in its label; an added row keeps its blue tint and gets a blue bar; the row whose cell is open in an editor has a primary bar and a faint primary tint. A cell error stays red over the changed tint, forced-colors mode draws a dashed outline, and pinned cells layer the tint over their solid surface. Tokens `--m-warning-bg` / `--m-warning-bg-strong` (light and dark). Blazor tracks changed cells in `EditableGrid` (`m-grid-cell-dirty`, `m-grid-row-editing`); MVC marks the `<td>` (`m-grid-td-dirty`) in `modulusGrid.touch`. Every mark clears on save or discard. 1 new bUnit test; 605 green. Not yet seen in a browser. |
| 2026-10-04 | Phase 22 continued. **ProTable `UrlState`** (Blazor): the page, page size, sort and column searches live in the URL with the MVC grids' keys (`current`, `pageSize`, `sortField`, `sortOrder`, `s.{key}`; `ProTableUrl` in `Ui.Abstractions/Grids` reads and writes them), beside the page's own keys (`UrlQuery`, read back in `OnUrlChanged`). Each change pushes a history entry; back, forward and a saved view's link reload the table; the table's own navigations are not read back. **Saved views on `/tables/advanced`** in all three samples, with the same query keys, so a view means the same layout everywhere; MVC and Razor Pages redirect a bare visit to the starred view. `SavedViews.Transient` now drops `current`, so a view never keeps the page number. **Database store sample:** `EfUiStateStore` (EF Core, one `UiState` row per user and key, the same limits as the memory store) on SQLite in `App_Data/ui-state.db` (git-ignored), registered by `AddSampleUiState` before `AddModulus*`. The editable grid gets no dropdown (no URL state). 8 new tests; 604 green; the three samples build. Not yet checked in a browser: the samples need a restart. **Next order** (from the 2026-10-03 gap analysis against the "Ant Design core + specialised OSS components" proposal): phase 23 grid engine → PDF viewer → Gantt → calendar → image cropper → code editor (CodeMirror) → Uppy uploads. Reporting stays out of this repo (scope rule of 2026-10-01). |
| 2026-10-03 | Pivot *Expand all* and loaders. Expand all looked dead: on the default layout every header is already open, and a collapse dropped the deeper group-bys, so nothing was left for it to open. The pivot now remembers the full group-by lists, as Odoo's `fullRowGroupBys` does (`PivotState.RowsFull` / `ColumnsFull`, `rf` / `cf` in the URL, normalized by `PivotQuery`), and Expand all brings them back. When there is nothing to open (`CanExpandAll`), the button renders disabled. Loaders for MVC and Razor Pages (`modulus-spa.js`, `modulus-shell.css`): a 3 px progress bar with a glowing tip, from 80 ms. A page that takes more than 300 ms dims under a centred spinner chip (`.m-main-loader`). An update within the page (a list's boost area, or `[data-m-loading]`) dims its target under a sliding bar and a spinner after 150 ms (`.m-area-loading`). The pivot busy state has the same spinner. The page swap no longer flashes: the old body stays opaque while the new one fades in, and the view-transition group no longer morphs size. 2 new pivot tests; 598 green. Browser check pending: the running samples need a restart for the pivot change. |
| 2026-10-03 | Phase 22, user state (§7.2). **`IUiStateStore`** plus `UiUser.Key` and `MemoryUiStateStore` (registered by `AddModulusMvc` and `AddModulusBlazor` with `TryAdd`). **Saved views:** `SavedViews` (canonical query strings, 20 per page, one default, `DefaultUrlAsync`); `SavedViewsHtml`; `AppSavedViews` (Blazor, opens the default view); `<app-saved-views>` (MVC). `MapModulusSavedViews()` is a minimal-API POST with manual antiforgery validation, local-path-only redirect, and errors returned in `views-error`. **Field access:** `FieldAccess.Require(field, roles…)` / `.For(user)`, `TableQuery.Restrict`, `PivotQuery.Restrict`, `GridSchema.Allowed`. Sample: `/tables/pivot` has the Views dropdown in all three apps, and the *Largest* measure needs Admin or Editor (page and download). UPGRADING has a section on mapping the endpoint. 17 new tests; 596 green. Browser check pending. |
| 2026-10-03 | Small items before the ERP core: `DEPENDENCIES.md` (every package, vendored script, font, icon set and the OSM tile service, with versions and licences), `lib/univer/LICENSE.md` lists every bundled preset (sheets and docs) and the CSS build, the Tiptap note points at the right UPGRADING section. Less Blazor JS interop: `ThemeToggle`, `AppRichText`, `AppSignaturePad`, `AppColorPicker`, `AppLookup` and `AppCreatableSelect` no longer call custom JS (see §6); the MVC signature pad starts from `[data-m-signature]` instead of Alpine `x-init`; rich text change events are debounced and flushed on blur and submit. 579 tests green. |
| 2026-10-03 | Icon fallbacks are Tabler (3.48.0, MIT), not Remix Icon: `tb:` names (`ShellRenderer.TablerPrefix`, was `RemixPrefix`/`ri:`), listed in `tools/icons/tabler.txt` (was `remix.txt`) and drawn as strokes at 1.5 on a `<g>`, so they match Ant Design's outline weight. Every `ri:` use was renamed (e.g. `ri:check-double` → `tb:checks`, `ri:expand-up-down` → `tb:selector`, `ri:group` → `tb:users-group`); an `ri:` name now draws an empty icon. The app switcher and launcher buttons use `tb:grid-dots` (the nine-dot launcher), the Plugins app `tb:puzzle`. |
| 2026-10-03 | Library migrations and the spreadsheet (§7.2). **Leaflet → MapLibre GL JS 5.24.0** and **Quill → Tiptap 3.31.4**: both ESM-only, so each ships as a prebuilt IIFE bundle built with esbuild (`lib/maplibre` is the official UMD-less build, `lib/tiptap/tiptap.js` is ours; `lib/leaflet` and `lib/quill` deleted; rebuild recipes in each `LICENSE.md`). **Widget registry** in `modulus-plugins.js`: `widget(selector, init)` + `keepAlive(root, destroy)` + `scan()`, so every `[data-m-*]` root gets one init and one teardown across htmx swaps, Blazor renders and SPA navigation. **Spreadsheet on Univer 1.0.3** (Apache-2.0): `SpreadsheetSheet(Name, Rows)` — rows of numbers, strings, booleans and `"=SUM(…)"` formula strings — rendered by `SpreadsheetHtml`, `AppSpreadsheet` (Blazor, `@bind-Sheets`) and `app-spreadsheet` (MVC), wired in `modulus-plugins.js` through the registry (lazy load, readonly strips the toolbar/formula bar/header/footer/menus, debounced `onCommandExecuted` → `change` with the sheets JSON, flush into `[data-m-sheet-value]` on submit, `setTheme` on `modulus:theme`, dispose both `univerAPI` and `univer`). Univer also ships ESM-only (no UMD since 0.5.x, presets in `@univerjs/preset-sheets-core`); `lib/univer/univer.js` is our IIFE bundle exposing `window.ModulusUniver` (10.8 MB raw, 2.78 MB gzip; lazy-loaded only on spreadsheet pages; formulas run on the main thread, no worker). Sample `/plugins/spreadsheet` in all three apps (quarterly budget with formulas + notes sheet, editable posting `Budget` and a read-only grid), `PluginsContent.Budget` / `PostedSheets`. Tests: `PluginMarkupTests` spreadsheet markup ×3. Docs: README, UPGRADING (Univer row + upgrade steps), DESIGN *Plugins*, manifest Plugins/Spreadsheet. All 548 tests green. Browser sweep of the new pages still pending; xlsx in/out (ClosedXML via Modulus.Documents) next. |
| 2026-10-02 | PivotGrid verified end to end (§7.1): Release build 0 warnings, the whole suite 545/545 (twice), antd-drift clean, `/tables/pivot` 200 on 5100/5101/5102 with the rendered pivot markup byte-identical across the three flavours (84,033 bytes), the download returning the same valid workbook from all three (merged headers, bold totals; opens and converts in LibreOffice), drill-down links to `/tables/large` with the filters applied, and the Blazor download anchor opted out of router/htmx interception (`download`, `hx-boost="false"`, `data-enhance-nav="false"`). README gains the pivot feature bullet and `app-pivot-grid` in the Pro row of the tag-helper table. Fixes along the way: `PivotQuery.Group` builds the grouping key as a `PivotKey` member-init (an `Expression.New(typeof(PivotKey))` for the empty grand-total key) so EF translates it — the SQL asserts in `PivotTests` now check the open-header slices filter by `@` parameters, never literals; `AppVectorMap`'s assert uses the real `0 0 1024 1024` viewBox; MVC `EditableGridTests` expects `Filter&#x2026;` (the harness encodes through `HtmlEncoder.Default`); two cref fixes (`BusinessFieldTagHelpers`, `AppPhoneInput`). The two flaky `EditableGridTests` (drag reorder, filter row) are fixed at the root: bunit 2.x's sync triggers (`Click`, `Change`, …) are fire-and-forget — `TriggerEvent` discards the dispatch task — so a test could assert before the handler ran; the affected tests (drag, filter row, toolbar search, selection, plus the same pattern in `PluginComponentTests` and `NestedGridTests`) now await `ClickAsync` / `ChangeAsync`. Remaining for §7.1: the browser-only sweep (390 px, dark mode, back/forward, dropdown clipping, busy bar) and a try in Excel itself. |
| 2026-10-01 | Data grid in MVC and Razor Pages. `PurchaseGridActions` (typed `Page` + nested `Get`/`Post`) added to `Modulus.Samples.Web`; `PurchaseTablePage` record added to `Modulus.Samples.Web.Models.ViewModels`; shared view `Views/Shared/Ui/Tables/DataGrid.cshtml` (orders table with `DataGridAuto` columns, sort/search headers, pager, nested level partial); `TablesController` wired with `GET /tables/data-grid`, `GET /tables/data-grid/level` and `POST /tables/data-grid/level`; Razor Pages `Tables/DataGrid.cshtml` + `DataGridModel` at the same URLs. Build: 0 warnings, 0 errors. Curl-verified: `GET /tables/data-grid`, sort (`?orderBy=number`), column search (`?s.number=PO`), `GET /tables/data-grid/level` all return 200 on both port 5101 (MVC) and 5102 (Razor Pages); `POST /tables/data-grid/level` returns 400 without a token (antiforgery, same as `/tables/nested/level`). Page inventory updated: `/tables/data-grid` is now ✅ in all three flavours. |
| 2026-10-01 | DataGrid — `ModulusDataGrid` (Components/Pro): one tag with two engines, picked by what it is given. Typed mode: hand it a `TItem` and it discovers the columns through `DataGridAuto` (shared in Ui.Abstractions/Grids) — flat and flattened properties (`customerName`) as boxed `PropertyColumn`s rendered through the column's own `Text` (AntDesign 1.6.2 columns take cells through `CellRender`, not `ChildContent`), the `Id` and `[ScaffoldColumn(false)]` skipped, dates and money formatted, numbers right-aligned — and it turns the ProTable request (search box, per-column search with the `ColumnSearch` grammar, header sort, numeric ranges, paging) into one `IQueryable` expression tree: an `IQueryable` (EF) runs it on the server with every value a closure parameter (the `Box` trick `TableQuery` uses) so `ToQueryString()` shows real `@p0` SQL, a client grid materialises once and pages in memory. Schema mode: `NestedGridSettings` + `Load`/`Save` render the nested engine in the same tag with the same toolbar title; a grid given neither is rejected with a clear error. Sample `/tables/data-grid` (Blazor): a typed grid over an `IQueryable`, the same data as a client grid, and a two-level purchase-order grid — all three from the one tag. Tests in `ModulusDataGridTests` (10): discovery, the schema feed, the engine guards, in-memory search/sort/range/paging, the search grammar (negation keeps null cells, as in the data table), typed rendering and cells, header-click sort, and the EF SQL translation with parameters. |
| 2026-10-01 | Structure tidy-ups from §6 done: the grid code (`ProTableModels`, `DataGrid`, `NestedGrid`, `TableColumns`, plus the shared `ColumnSearch` and `Paging`) moved into `src/Modulus.Ui.Abstractions/Grids/`, the MVC form binding (`EditableGridForm`, `NestedGridForm`, `ColumnSearchQuery`) into `src/Modulus.AspNetCore.Mvc/FormBinding/`, and the test files into matching folders (`Grids/` in `Modulus.Blazor.Tests`, `FormBinding/` in `Modulus.AspNetCore.Mvc.Tests`). Namespaces unchanged, no consumer break; all moves are git renames. `global.json` pins the SDK (10.0.400, `rollForward: latestFeature`) so local builds and CI agree. Build and tests verified: the same failures before and after (the five pre-existing PivotGrid-era failures, see §5 and §7.1). |
| 2026-10-01 | ERP UI roadmap adopted (§7.3); docs only. Radzen, Syncfusion and DevExpress were reviewed as feature inventories, and an ERP Report Studio design as the product side of the same ground. Scope rule recorded: Modulus is a UI framework and takes the UI capabilities only — the grid engine (phase 23), a QueryBuilder component (24), an import wizard UI (25) and printing components (26) join phase 22 (`IUiStateStore`, saved views); P1 lists the component tier (Kanban, Scheduler, Gantt, PDF viewer, file manager, dashboard layout, barcode, rich-text tables, workflow/audit kit, lookup upgrade, dynamic forms), P2 the rest. Report definitions, scheduling, document pipelines and any product data layer stay out of this repo; the Report Studio is a separate future solution that will consume Modulus packages. DESIGN §1, §2 and §4 and the README documentation list were brought in line; §6 recorded the structure tidy-ups and the SDK pin. |
| 2026-10-01 | Clear button fixed and added (written, not yet built or run). `<app-table-settings clear-href>` showed Clear on every list and reloaded the whole page: Razor passes `clear-href="@null"` to a string property as `""`, so the button always rendered, with an empty link htmx doesn't boost. Empty now means no button (also for `reload-href`). Pivot (MVC, Razor Pages, Blazor) and Blazor Large data now have the same Clear while a search or filter is set; on the pivot it keeps the layout. |
| 2026-10-01 | Full-screen button in the header of all three samples (`ShellRenderer` `[data-m-fullscreen]`, `modulus-shell.js`): the whole app goes full screen through the browser's Fullscreen API, the icon and label follow the state (also after Escape), and the button hides where the browser has no such API (iPhone Safari). Full screen survives navigation in MVC and Razor Pages: the sample pager's page size (`select[data-m-nav-select]`) and the lists' row links to other pages (`a[data-m-page]`, followed by `modulus-spa.js` as page navigations) no longer load a whole page, and after any whole page load (a sign-in, a tenant switch, a form post) the next click or key press goes back to full screen (browsers end it on every page load and only a user gesture may start it). Blazor Pivot and Large data use the same search box as the other lists (an input with a search icon, searching as you type) instead of AntD Search with its button; an AntD Search in a table toolbar now matches the toolbar's height. Written, not yet built or run. |
| 2026-10-01 | PivotGrid in the manner of Odoo's pivot view (written, not yet built or run; open items in §7.1). `PivotQuery<T>` (in `Modulus.Ui.Abstractions.Pivot`) registers the fields to group by (text, enum, and date by year, quarter, month, week or day) and the measures (sum, average, min, max; count is built in). As in Odoo it reads only what shows: one `GroupBy` per row depth × column depth that has open headers, filtered to the records under them, so a collapsed header costs one group. Every total comes from the database (averages as sum/count), capped by `MaxGroups`. `PivotState` keeps the whole layout in the query string, and every control is a link to the next state. A header opens on the next group-by or offers the unused ones (a date gets an interval submenu). A collapse drops unused group-bys, as in Odoo. The toolbar has Flip axis, Expand all and Measures, and a measure column sorts the rows. `PivotHtml` draws the table for both `AppPivotGrid` (Blazor) and `<app-pivot-grid>` (MVC), and `PivotXlsx` writes the download with no library. Sample `/tables/pivot` in all three apps over the 100,000 shipments, with drill-down to `/tables/large` (Blazor Large now reads its filters from the URL too). Header dropdowns now float out of scrolling boxes, and a pivot link shows a busy bar and keeps the table's scroll (`modulus-shell.js`). The Blazor filter boxes no longer flick back while their navigation runs. Tests in `PivotTests` and `PivotTagHelperTests`; EF Core SqlServer is a new test-only package that checks the SQL. The rest of the 2.0 proposal is planned in §7. |
| 2026-09-30 | Icons are Ant Design only (written, not yet built or run). The Tabler sprite (`tabler.svg`) and `tools/tabler-icons` are gone. `ShellRenderer.Icon`, `<Ti>` and `<app-ti>` keep their names and markup but take an Ant Design icon name and draw it inline; every page, menu item, app, link and sample record was renamed from its Tabler name (`users` → `team`, `settings` → `setting`, `x` → `close`, `ant:` prefixes dropped), and a Tabler name now draws an empty icon. Six Ant Design icons newer than AntDesign 1.6.2 (moon, sun, truck, signature, product, x, from `@ant-design/icons-svg` 4.4.2) and 82 Remix Icon 4.9.1 fallbacks (`ri:` names: brands such as Visa, PayPal, Figma; fingerprint, cookie, plug, weather, media) are bundled in `ExtraIconSvg.g.cs`, generated by `tools/icons/build.sh` from `tools/icons/remix.txt`. The `app-ti` tag helper class is now `NamedIconTagHelper` (`ShellTagHelpers.cs`). CSS: `.m-ti` is a filled glyph (the stroke rules are gone). The search palette copies an icon by its `data-m-icon` name. |
| 2026-09-30 | Docs brought up to date with the uncommitted work. README: large data, SPA navigation, plugins, localization, dark brand, row drag, `Paging`, nested menus; the theme options table corrected (Tabler defaults, `SiderWidth` 240) and completed; samples list, tag helper table, projects, NuGet packaging and known limitations updated. DESIGN: architecture, dark brand, localization, menu trees, *Plugins* section, sample sign-in, roadmap. UPGRADING: Leaflet, Quill and jsVectorMap, the `--write-dark-brand` step. Manifest: `CreatableSelect`, a *Grids* section (editable, nested, load more, virtual table) and a *Plugins* section (lightbox, editor, map, vector map, media player, sortable). |
| 2026-09-30 | Row drag and drop in the data table, editable grid and every nested grid level, in all three apps (written, not yet built or run). A handle column (AntD `holder`) on each row: drag it, or focus it and press Up, Down, Home or End. The Sortable engine in `modulus-plugins.js` now also moves table rows (`data-m-sort-rows` on the element around the table, `data-m-sort-row` on the handle): handle only, expanded and nested rows travel with their row, a one-row table as the ghost, hidden (other page, filtered) rows skipped. Blazor reports the move and the component reorders (`EditableGrid.RowDraggable`/`OnRowMove`, `ProTable.OnRowMove` + `RowLabel`); MVC moves the DOM, so the grid posts its rows in the new order, and the data table posts `move` to `/tables/advanced/move` (MVC action and Razor Pages `OnPostMove`). Only the page's rows swap slots (`GridSchema.Reorder`, `SortMove.Within`, new `RowMove`); nested Save sets `NestedGridChanges.Ordered` and `NestedGridQuery.StoreOrder` keeps the order, with added rows where they were dropped. Handles are locked while a column sort is on. `NestedGridLevel.RowDraggable`; the Materials level sorts by header again. Tests: MVC handle markup and lock, Blazor reorder and lock, ordering helpers. Also: filter boxes and the column menu sit above antd's sorter click layer (clicking them no longer sorts); subtitle column search fix; `EditableGrid.HasRowChanges`. |
| 2026-09-30 | Grid headers, toolbars and MVC cell editors (written, not yet built or run). Header cells keep the title, sorter, search icon and column menu on one line (the title's flex-basis is 0, so it ellipsises; before, the editable grid's search icon wrapped to a second line). Toolbar buttons, links included, centre the AntD icon and text with one 6px gap (Export CSV and Columns were misaligned on the data table page), and the data table page and `app-table-columns` use AntD icons instead of the Tabler sprite. Filter bars are slim (30px controls, equal columns); the editable grid drops its second "Clear filters" link (the toolbar Clear does it). MVC and Razor Pages editable and nested grids edit like Blazor: Select, Tag and Multi-select cells use the creatable select (`modulusCreatableSelect`, markup shared through `CreatableSelectTagHelper.Render`), with coloured options, a check on picked ones and "Create new …" posting `add-option`; Date cells use AntD's picker (`modulusPicker`); a pick or closing the list ends the edit (Escape on an open list closes it and keeps the value; on a closed one it cancels). Pinned columns no longer show scrolled content through them: in MVC and Razor Pages every Select-column cell matched the "lift an open editor" rule (the always-present Add option panel had no `hidden`), so it painted at z-index 6 over the pinned cells (z-index 2); the rule now lifts only a pinned cell with an open list (a scrolling cell's list already floats above the pinned columns, so an open editor no longer overlaps them either). In all apps, translucent tints on pinned cells (selected, new, sorted, hovered header, a child level's header) are layered over an opaque surface. |
| 2026-09-30 | Grid pagination and Clear (written, not yet built or run). `Paging` (Ui.Abstractions): page sizes 10/20/50/100 plus a level's own, the "1–10 of 42" range and AntD's page window with ••• jumps (`app-pagination` uses it too). Editable grid: pages like the data table (`Paged`, `PageSize` = 10, rows per page; Blazor through AntD `Pagination`, MVC in the browser in `modulusGrid`, over the rows left after search and filters, back to page 1 on search, filter, sort or a new row). Nested levels: the same pager on the server, rows per page carried as `size=` in MVC (`NestedGridRequest.CustomPageSize`). A toolbar Clear (`.m-table-clear`) while a search, filter or column search is set, on the editable grid, every nested level and the data tables (`ProTable` `FiltersActive`/`OnClearFilters`, `app-table-settings clear-href`); the Customers, Users, Products, Orders and Invoices lists use it, and their dead Export buttons are gone. Blazor nested level with unsaved edits: search, filters, sort and paging are disabled with "Save or discard your changes first" (it looked like the Buyer filter did nothing). |
| 2026-09-30 | Menus organised like Odoo (written, not yet built or run). In a navbar dropdown, a group directly under it is a section (`.m-menu-section`): a bold heading with its entries indented one step, as in Odoo's app menus; only groups inside a section fold open (`.m-tree-group`). Sample menu: plain entries first, then sections, in every menu; Settings starts with *General settings* and *Users & companies* holds Users, User profile and the *Access rights* section; Website's Errors gains a *Status pages* group; Extra gains *Subscription*, *Work*, *Monitoring* and *Content* groups. `SampleShellContentProvider` adds **Plugins** and **Extra** launcher apps (group *Templates*), listed after Developer and Calendar so the pages Plugins shares with them stay in those apps. Test renamed to `NestedMenus_DropdownSectionsThenFoldingGroups_SidebarTree_OpenAlongThePathToThePage`. |
| 2026-09-28 | Nested menus as a collapsible tree (written, not yet built or run). `ShellRenderer`: a navbar dropdown is a single list (`.m-tree`) whose groups are `<details class="m-tree-group">` that fold open in place, to any depth, indented by `--m-depth` with a guide line; the multi-column dropdown (`m-dropdown-columns`) is gone. In the sidebar, groups below the top level are folding branches (`.m-snav-branch`, `SidebarChildren`) instead of section headers, also to any depth. Branches on the path to the current page start open (`m-in-path`). With the sidebar folded, the flyout shows the same tree and its branches still toggle (`modulus-shell.js` only stops the top-level summaries). Sample menu: Settings › Users & companies › Access rights, Website › Authentication › Password / Verification, Plugins › Inputs › Pickers, Developer › Interface › Foundations. New test `NestedMenus_RenderAsCollapsibleTrees_OpenAlongThePathToThePage`. |
| 2026-09-28 | Columns and Export CSV on every table (written, not yet built or run). Data tables: `ProTable` (`ColumnSettings`, `Exportable`, `ExportFileName`, `Id`) and MVC `<app-table-settings>` (`columns`, `export`, `export-name`) show labelled **Columns** and **Export CSV** buttons, run by `modulus-shell.js` on the rendered table (`[data-m-table-columns]`, `[data-m-table-export]`): the picker lists the header columns with checkboxes and Show all, remembers hidden columns per page and table in localStorage and hides them with a style rule scoped to the card (so Blazor re-renders keep them hidden); the export writes the rows shown in the visible columns (UTF-8 with BOM, formula-safe). The Advanced tables page keeps its server-side picker and export (`columns="false" export="false"` / `ColumnSettings="false" Exportable="false"`). Editable grid: Columns and Export CSV are labelled toolbar buttons again (Export left the ⋮ menu, which keeps Add column and density). On phones the two buttons show their icons only. |
| 2026-09-28 | Slim, professional grids (written, not yet built or run). Density: every table in a `.m-table-card` (ProTable, EditableGrid, NestedGrid, the MVC tables) is slimmer: 11px headers, 13px cells, ~36px rows at Standard and ~30px at Compact, 44px toolbar and pager, smaller tags and avatars, editors 28px high. Filter row: option columns (Select, Tag, Switch, Multi-select) get a select ("Any status") instead of a text box (`GridSchema.SearchChoices`, `ColumnChoice`, `ColumnSearch.AnyText` / `WithCurrent`; Blazor `ColumnHeader Choices`, MVC `ColumnHeader.Content(choices:)`); a single value searches `=Label`. `EditableGrid FilterRowOpen` / `filter-row-open` starts with the filter row shown; nested levels use it, so their filters sit under the headers and the bar above keeps only "Clear filters". Nested hierarchy: only the top level is a card; child levels are indented under the parent row with a guide line, a hairline frame and a tinted header. The nested sample pins each level's key column. Yes/no columns are centred (`m-grid-td-center`); delete and expand buttons have tooltips. |
| 2026-09-28 | Grid fixes (written, not yet built or run). Cell editor dropdowns were trapped in the table: `container-type` on the grid scroller made it the containing block of the `position: fixed` lists, so they sat off their cell and were clipped. The scroller no longer is a container (`--m-grid-view`, set by `modulus-shell.js`, keeps detail rows in view), and the cell holding an open editor is lifted above pinned cells. Row actions are visible on every row again; pinned columns get a soft shadow only while content scrolls under them, and the scrollbar is thin and always visible. One focus style for all cell editors; the selected option uses a grey fill. Icons: 15 Tabler names missing from the sprite (report spam, order and invoice stats, roles, user view, cards, store products, activity) now use Ant Design icons (`ant:` names). Build fix: CS1073 in `EditableGridTagHelper.AddOptionRow` (a `{ … }` arrow body in a `$"""` string). |
| 2026-09-28 | Grids, ERP style (written, not yet built or run): one compact toolbar for ProTable, EditableGrid and NestedGrid (Blazor and MVC): title and count, "N unsaved changes" with Discard and Save (primary only while dirty), icon buttons with tooltips (search that folds into its icon, filter row, columns, add row with `AddRowText`, reload) and a More menu (Export CSV, Add column, density). Filter row: `ColumnHeader` shows each column search as a box under the title (`ColumnHeaderScope.FilterRow`, MVC `thead.m-filter-row-on`, `ProTable FilterRow`, `<app-table-settings filter-row>`). Changed rows get a warning bar; chevron expanders; delete on row hover; lighter nested levels. Keys (modulus-shell.js): Ctrl+S save, Insert add row, Ctrl+F search, Delete removes the selection, arrows move between cells. Creatable select: "Create new …" opens a form in the list's place (name, eight named colours `GridSchema.PickerColors` / `ColorName`, Cancel, Add); the grid's MVC option panel matches. Fix: CS1073 in `AppSortable.razor` (two `@(…)` in one attribute). |
| 2026-09-28 | Plugins batch 2, reference pages and the §6 follow-ups (written, not yet built or run). Plugins: `/plugins/sortable`, `/plugins/player`, `/plugins/editor`, `/plugins/map`, `/plugins/map-fullsize`, `/plugins/map-vector` in all three samples (`AppSortable`, `AppMediaPlayer`, `AppEditor`, `AppMap`, `AppVectorMap` and their tag helpers; scripts in `modulus-plugins.js`, which loads Quill, Leaflet and jsVectorMap on first use). Reference pages: `/ui/icons` (every AntD icon from `AntIconSvg.Names`, three themes, search, a click copies the Blazor or tag-helper markup via `[data-m-copy]`), `/ui/typography`, `/ui/colors` (theme tokens and the 12 AntD presets from `AntPalette`), `/pages/changelog` and `/apps/users/grid` (cards, role filter, search), with menu entries and a *Reference pages* CSS section; data in `UiContent`. Theming: the brand colour replaces AntD's blue in dark mode. Localization: `ModulusThemeOptions.Cultures`, `ModulusCulture`, a language picker in the theme settings panel and `app.UseModulusLocalization()`. Samples: real cookie sign-in (`SampleAuth`; Blazor posts to `/auth/sign-in` through `modulusShell.submit`), `/auth/logout`, optional `Samples:RequireSignIn`. Packaging: NuGet metadata for the four libraries and a CI pack step. Fixes: CS8998 in `EditorHtml.Toolbar`; Dropdown button, Affix and BackTop menus in MVC and Razor Pages. New tests: `SampleLogicTests`, `LocalizationTests` (both test projects), ProTable `TitleExtra` / `RowSelectable`, `AntIconSvg.Names`. |
| 2026-09-28 | Plugins batch 1 (written, not yet built or run): a *Plugins* menu group and six pages in all three samples: `/plugins/charts-advanced`, `/plugins/color-picker`, `/plugins/date-picker`, `/plugins/dropzone`, `/plugins/lightbox`, `/plugins/tour` (bodies in `Views/Shared/Ui/Plugins`, data in `PluginsContent`, MVC `PluginsController`). Library: `ChartOptions.LegendToggle` (legend buttons hide the `[data-s]` series marks, handled in `modulus-shell.js`) and `ChartOptions.Links` (drill-down: linked hover columns and x-axis labels), exposed as `LegendToggle` / `Links` on the Blazor charts and `legend-toggle` / `links` on the chart tag helpers; `DatePeriods` + `DateRangePreset` (range presets, `yyyy-Qn` quarters); `app-date-picker picker="quarter"` / `"year"` with `from-year` / `to-year`; `app-range-picker presets` (`modulusRangePreset`); `app-upload action / max-size / max-count / list-type="picture"` with per-file progress, previews and rejection messages (`modulusUpload` rewritten). `POST /plugins/dropzone/upload` checks and discards files in all three samples. CI: `EditableGridTests` pins `en-US`; workflow actions moved off Node 20. Pages use AntD icons only. New tests: `PluginTagHelperTests`, `ChartInteractionTests`, `DatePeriodsTests`. |
| 2026-09-28 | Tabler extra pages: a new *Extra* menu group with 22 pages in all three samples (see §4 *Extra*), covering the Tabler "Extra" list alongside the existing Chat, Email, FAQ, Invoice, Pricing, Settings, Users and Wizard pages. Query-driven pages (activity, jobs, logs, search, tasks, cookie consent, settings plan, checkout) build their model in `ExtraContent` and read the same query string in every flavour; sample times are anchored to the app's start time. `modulus-pages.css` gains an *Extra pages* section; 25 new Tabler icons (`cloud-sun` is not in Tabler, so the weather widget uses `sun-wind`). MVC `SampleController.BlankPage` renders a page in `_BlankLayout`. Parity fixes: inline badges and search highlights no longer depend on whitespace (Razor keeps it, Blazor drops it), and the marketing brand is no longer wrapped in a second link. Also committed: the layout settings panel (content width, header and sidebar styles, folded sidebar, RTL, fifteen presets). Commit `4d81bee`. |
| 2026-09-27 | Business fields sample at `/forms/business` in all three apps (menu *Forms → Business fields*): a sales-order form with a status bar, customer and product lookups (the product lookup searches 250 products on the server: MVC `GET /forms/business/products`, Razor Pages `?handler=products`, Blazor a `SearchAsync` delegate), money, quantity, duration, lead-time range, phone, tax id mask, approval code, notes (rich text) and a signature. `BusinessFormInput` / `BusinessFormsDemo` in `Samples.Shared` hold the model, the demo data and the validation attributes (`NumberRangeValue`, `PhoneNumberValue`, `MaskComplete`, `SignatureValue`). Fixes: a phone number with a shared calling code (+1) now keeps the field's default country instead of the first match (`PhoneNumber.Split` takes a fallback); the one-time-code boxes lost characters when typed fast (a delayed `select()` pulled the focus back; typed keys are now handled in `beforeinput`); a server lookup that returns more rows than it shows says "Showing the first N" instead of a wrong total; the form status bar scrolls sideways on phones instead of wrapping. Build fixes in `EditableGrid.razor` (RZ2008) and the gallery's Mentions placeholder (RZ9986); three grid tests updated for header sorting. Manifest: a *Business* section with the 11 fields, and the 15 business charts under *Pro*. 98 new tests (shared rules, bUnit components, tag helpers). |
| 2026-09-27 | Lightbox: one engine in modulus-shell.js drives `AppLightbox` (Blazor, new), `<app-lightbox>` (MVC, new), `<app-image>` and `<app-image-group>`; the Alpine `modulusImageGroup` is gone and Blazor needs no JS interop. A `[data-m-lightbox]` root carries its images as `data-items` JSON (`LightboxItem` in Ui.Abstractions: src, title, caption, thumb); clicking a `[data-lightbox-index]` clones the hidden overlay (Ant Design's image preview DOM, `LightboxOverlay` / `Lightbox.Overlay`) into `<body>`. The preview has the toolbar ("n / total", close, zoom in/out, rotate, download), side arrows, a caption bar that keeps its height from image to image, and a thumbnail strip. Zoom works with the wheel, `+`/`-`/`0` and double click. Drag pans a zoomed image, swipe pages at 100%, and Home/End jump to the first/last image. Also: loading spinner, preloading, scroll lock, Tab kept inside, focus returned on close. `AppLightbox`/`app-lightbox` render a responsive tile grid (`MinWidth`, `Aspect`, `Captions`). New Lightbox card on /ui/components. 2 new tests (one per flavour). |
| 2026-09-27 | One table design for the data table, editable grid and nested grid, the same in Blazor, MVC and Razor Pages. `EditableGrid` / `app-editable-grid` now have the data-table toolbar: a search box with an icon, Columns (show/hide, plus pin/move with column editing), Export CSV, reload and density. They also have a filter row built from `Filterable` Select/Tag/MultiSelect/Switch columns ("Any …", Clear filters), row checkboxes with a batch bar (Set value…, Export selected, Delete, plus `BatchActions` / `grid-batch`), and styled cells: an avatar with initials and a tint (`GridColumn.Avatar`), a second line (`SubtitleKey`), and option icons (`GridOption.Icon`). The rules live in `GridSchema` (`Matches`, `FilterColumns`, `Csv`, `Initials`, `Tint`) and are mirrored in `modulusGrid`. The editable grid searches and filters in the browser; MVC keeps search and filters in sessionStorage, and hidden rows still post. Nested levels search and filter on the server (`NestedGridRequest.Filters`, `f.{key}` in the URL), and the pager shows "a–b of n". All three tables have resizable columns: a drag grip on `.m-resizable` headers sets the `<col>` width, and the editable grid saves the width into its schema. They also have expandable rows: `ProTable.RowDetail` / `RowExpandable`, and MVC `data-toggle-detail` rows. The data table sample shows an employee detail panel. |
| 2026-09-27 | Component gallery category nav (`.m-demo-nav`): removed the second box-shadow that painted a layout-coloured band around the sticky pill, which showed as an extra background over the content while scrolling. The pill design is unchanged; once it sticks under the header it gets `m-demo-nav-stuck` (`modulus-shell.js`) and the raised popup shadow. |
| 2026-09-27 | Nested, expandable editable grid: `NestedGrid` (Blazor) and `app-nested-grid` (MVC), one component for any depth. `NestedGridSettings` / `NestedGridLevel` in `Modulus.Ui.Abstractions` give each level its columns (or `ColumnSets` picked by a parent field), page size, search, sort, editing, add/delete and expand rules; the number of levels is the nesting limit. Children load lazily (Blazor `Load(NestedGridRequest)`; MVC an `intersect once` htmx GET), and each level saves on its own (`NestedGridChanges` with deleted ids). `NestedGridQuery` searches, sorts (empty cells last) and pages in-memory rows; `NestedGridForm` reads and writes the URL state and posted changes. Sample `/tables/nested` in all three apps: garment orders → styles → materials → items, where each material's category (fabric, accessory, packing) picks its items' columns, with a 1–4 levels switch. 8 new tests. |
| 2026-09-27 | "Add new" with a create form for `AppCreatableSelect` / `app-creatable-select`: `OnAddNew` + `AddNewText` (Blazor, the page opens a `ModalForm` and passes back the reloaded options and value) and `add-modal` + `add-new-text` (MVC: opens the named `app-modal`; the save answers `Response.HxOptionAdded(list, option)`, every select of that list adds the option and the one that opened the modal selects it). Gallery card "Select with Add new (modal form)" creates a real customer in all three samples (`POST /ui/components/customer`), so it also shows on `/apps/customers`. 3 new tests. |
| 2026-09-27 | Select with an "Add item" row: `AppCreatableSelect` (Blazor, `Components/Extras`) and `app-creatable-select` (MVC, Alpine `modulusCreatableSelect`), single or multiple, with tag-colour swatches, keyboard support and a `position: fixed` dropdown (`modulusShell.placeSelect`). `GridSchema.AddOption` / `OptionColors` check new options (trimmed, unique label, 60 characters, 50 options). `OnAdd` / `add-href` (JSON reply) can save an option first. The editable grid's Select, Multi-select and Tag editors use it: Blazor in place; MVC through a new `add-option:{key}:{row}` operation in `EditableGridForm.Apply` (with `EditableGridPost.Error`). `/tables/advanced` gains colour-tagged employee labels (`EmployeeLabels`): a label filter, label tags on each row, and a batch-bar dropdown that labels the selected rows, in all three samples (`POST /tables/advanced/labels` and `/tag` in MVC and Razor Pages). Gallery card in Data Entry. The filter row now fits four filters (auto-fit columns). 14 new tests. |
| 2026-09-27 | Header app switcher and tenant switcher, rendered by `ShellRenderer` for Blazor, MVC and Razor Pages: `ShellContent.Apps` / `AppsHref` (a grid of tinted app tiles) and `Tenants` / `CurrentTenantId` / `TenantLinks` (current tenant next to the brand; a filter box above five tenants). The current tenant is the provider's id, then the `modulus-tenant` cookie (`ShellView.TenantId`, `AppShell TenantId`), then the first. `modulus-shell.js` switches cookie tenants (cancellable `modulus:tenant-change`), filters the list and focuses it on open. At phone width the header shows the brand logo only and narrower icon buttons, so it fits 360 px. The Blazor sample reads the cookie while prerendering and keeps it with `[PersistentState]`. 6 new tests (first `ShellRenderer` tests). |
| 2026-09-27 | Ant Design 5 components that AntD Blazor 1.6.2 lacks, in Blazor (`Components/Extras`) and MVC (`ModernTagHelpers.cs`): `AppQRCode` / `app-qr-code`, `AppColorPicker` / `app-color-picker`, `AppFloatButton` / `AppFloatButtonGroup` / `AppFloatBackTop` (`app-float-button`, `app-float-button-group`, `app-float-back-top`) and `AppTour` / `app-tour` + `tour-step`, with gallery cards in all three samples.<br>Shared logic in `Modulus.Ui.Abstractions`: a byte-mode QR encoder (`QrCode`, verified by decoding with ZXing up to version 40), `QrSvg` / `QRCodeMarkup`, `HsvColor` + `ColorPreset`, and `TourStep`. `modulus-shell.js` adds `modulusShell.tour` (spotlight and placement with flipping), back-to-top for `[data-m-back-top]` and pointer capture; `modulus-widgets.js` adds `modulusColorPicker` and `modulusTour`. The MVC expired QR code refreshes in the browser (raising `modulus-qr-refresh`) unless `refresh-href` is set. A transparent QR code gets a white backing in dark mode. 58 new tests. |
| 2026-09-26 | Gallery: the remaining Ant Design components, in Blazor and MVC. New cards: Flex, Splitter, Layout, Menu, Dropdown button with Affix and BackTop, AutoComplete and Mentions, Input group and Checkbox group, Image (preview group), Countdown and Card meta, Comment, Drawer and Watermark. New MVC tag helpers `app-menu` / `app-sub-menu` / `app-menu-item` / `app-menu-divider`, `app-layout*`, `app-splitter` / `app-splitter-panel`, `app-affix`, `app-back-top`, `app-dropdown-button`, `app-auto-complete`, `app-mentions`, `app-input-group`, `app-image-group`, `app-countdown`, `app-comment` and `app-drawer`, plus card cover/meta/action slots; `app-flex` now emits the Blazor classes. They run on new Alpine components in `modulus-widgets.js`; drawers reuse `modulusModal`. Dark-mode menus and mention dropdowns use the container colours. 19 new tests. |
| 2026-09-26 | Editable grid at `/tables/editable` in all three samples. Columns are `GridColumn` data; cells edit in place (input, native number, select, multi-select, date, switch, rating) with shared `GridSchema` formatting and validation; rows expand into a detail panel; columns pin left or right (sticky, with scroll shadows), hide, reorder, are removed or added from a dialog; Save validates, Discard and Reset reload. Blazor: `EditableGrid`. MVC and Razor Pages: `app-editable-grid`, which posts the whole grid (hidden columns included) through htmx, and `EditableGridForm.Read` / `Apply`. `modulusShell.openPopup` opens AntD popups on edit. Fix: the gallery's Dropdown used `MenuItem Danger`, which AntD Blazor 1.6.2 lacks and which crashed the circuit on open. 30 new tests. |
| 2026-09-26 | MVC widgets: `app-cascader`, `app-tree-select`, `app-transfer`, `app-tree`, `app-calendar`, `app-carousel` and `app-anchor` emit the Ant Design DOM, run on small Alpine components in the new `modulus-widgets.js` and post through hidden inputs. They take their options from `TreeOption` (Modulus.Ui.Abstractions). The gallery gains Anchor, Cascader and TreeSelect, Transfer, Tree, Calendar and Carousel cards in all three samples. The Transfer stacks on phones. The manifest lists the new `mvc` entries. |
| 2026-09-26 | Filters in the URL: filter, sort and page state survives reload and back on the data table, Customers, Users, Products, Orders and Invoices (MVC and Razor Pages). GET filter forms now leave empty fields out of the pushed URL. The Razor Pages sample gets its missing `favicon.ico`. |
| 2026-09-26 | MVC library: `app-table-columns` (column picker whose links carry the hidden set in the URL) and `app-table-settings` (reload + density with ProTable's Comfortable / Standard / Compact); `app-table` gains `hidden-columns` (drops `data-col` cells), `density`, `scroll-x`, `layout` and a `table-footer` slot. The data table and every list page use them; `_TableSettings.cshtml` is gone. `TableColumns` moves the hidden-set logic into `Modulus.Ui.Abstractions`. |
| 2026-09-25 | Fix: MVC and Razor Pages pages could not be scrolled. The layout's shared drawer always carried `ant-drawer-open`, which triggers AntD's `body { overflow: hidden }` scroll lock; the lock now applies only while the drawer is open. |
| 2026-09-25 | Library rebuilt on AntDesign 1.6.2 aliases; Tabler shell; three samples with all pages; docs, tests and CI; first push to GitHub (`83c8ca2`). |

<!-- source: docs/roadmaps/README.md -->
## Modulus UI Framework - Strategic Roadmaps

This folder contains comprehensive implementation roadmaps for advancing the Modulus UI framework from a robust template system to an enterprise-grade, AI-integrated platform.

---

### 📋 Roadmap Documents

#### 1. **CLI Implementation Roadmap** ⭐
**File**: `CLI-IMPLEMENTATION.md`

Complete step-by-step guide to implementing all UI CLI commands.

**Covers**:
- Architecture overview of command structure
- Step 1: UiAddCommand family (12+ commands)
- Step 2: Theme management commands (5 commands)
- Step 3: Generation commands (8+ commands)
- Step 4: Audit/compliance commands (3+ commands)
- Step 5: Program.cs registration
- Implementation timeline (6 weeks)
- Testing strategy & integration tests
- Code sharing patterns

**Key Outcome**: Enable developers to scaffold complete UIs with single commands like:
```bash
modulus ui add-chart --type line
modulus ui create-user-dashboard
modulus ui theme create corporate
modulus ui add-auth --flow both
```

**Status**: Ready for Phase 3b-3d completion

---

#### 2. **Phase 4.5: Auth/Identity UI Layer**
**File**: `PHASE-4.5-AUTH-IDENTITY.md`

30+ enterprise-grade authentication and identity management templates.

**Covers**:
- Integration points with existing Modulus.Identity
- Login/Registration flows (8 templates)
- User management dashboard (6 templates)
- Role & permission management (5 templates)
- Security & compliance (7 templates)
- Multi-tenancy support (4 templates)
- 12+ new CLI commands for auth scaffolding
- Business value & compliance benefits

**Key Templates**:
- Modern login with 2FA/social OAuth
- User admin dashboard with role assignment
- Permission matrix builder
- Audit trail viewer
- Security event monitoring
- Access review workflows

**Status**: Ready to implement after Phase 3b-3d

---

#### 3. **Phases 8-14: Professional Enterprise Features**
**File**: `PHASES-8-14-PROFESSIONAL.md`

112 new templates + 26 new CLI commands across advanced features.

**Phase Breakdown**:

| Phase | Name | Templates | CLI Cmds | Timeline | Focus |
|-------|------|-----------|----------|----------|-------|
| **8** | Charts & Visualization | 12 | 3 | 1-2 wks | Line, bar, pie, heatmap, real-time |
| **9** | AI & Code Editors | 14 | 4 | 2-3 wks | Chat interface, Monaco, streaming |
| **10** | Real-Time Collaboration | 10 | 3 | 3-4 wks | Multi-user editing, presence |
| **11** | Professional Forms | 16 | 4 | 2-3 wks | Schema-driven, conditional, wizard |
| **12** | Theme Customization | 18 | 6 | 2-3 wks | CSS variables, dark mode, export |
| **13** | Mobile & PWA | 12 | 2 | 1-2 wks | Responsive, service worker, offline |
| **14** | Accessibility & i18n | 20 | 4 | 2-3 wks | WCAG 2.1 AA, RTL, localization |

**Total**: 112 templates, 26 commands, 18-20 weeks

**Key Commands**:
```bash
modulus ui add-chart --type line
modulus ui add-chat --provider claude
modulus ui add-collaboration --type editor
modulus ui add-form-builder
modulus ui theme create <name>
modulus ui add-pwa
modulus ui audit --level AA
```

**Status**: Ready for execution after Phase 3b-3d

---

### 🎯 Current Status

#### ✅ Completed (Phase 7)
- 67 production-ready templates
- 4 UI engines: MVC, Razor Pages, Blazor, Fluid
- Full CRUD operations (list, create, edit, detail)
- Admin dashboards
- Analytics & reports
- System monitoring & audit logs
- All builds: 0 errors, 0 warnings

#### ⏳ In Progress (Phase 3b-3d)
- **Phase 3b**: Template package resolution
- **Phase 3c**: Template rendering from packages
- **Phase 3d**: Integration tests (all 4 engines)

#### 🚀 Ready to Start
- Phase 4.5: Auth/Identity UI (30+ templates)
- Phase 8: Charts & Visualization (12 templates)
- Phases 9-14: Advanced features (100+ templates)

---

### 📊 Implementation Roadmap

```
Oct 2026: Phase 3b-3d (template resolution) ⏳ BLOCKING
          └─ Must complete for all other phases to work

Nov 2026: Phase 4.5 (Auth/Identity) ✅ Ready
          Phase 8 (Charts) ✅ Ready

Dec 2026: Phase 9 (AI/Code) ✅ Ready

Jan 2027: Phase 11 (Forms) + Phase 12 (Themes) ✅ Ready

Feb 2027: Phase 13 (Mobile)

Mar 2027: Phase 14 (Accessibility)

Apr 2027: v1.0 launch
          - 192+ templates
          - 50+ CLI commands
          - Full enterprise feature set
```

---

### 🔑 Key Strategic Insights

#### Market Opportunity
- **Gap**: Current .NET scaffold tools offer basic CRUD
- **Modulus**: Complete, professional SaaS/enterprise UIs in minutes
- **Competitive Advantage**: No other framework has this breadth

#### Business Value
For users:
- ✅ 70% reduction in boilerplate code
- ✅ Enterprise quality from day 1
- ✅ WCAG AA compliance built-in
- ✅ AI-ready architectures
- ✅ Mobile support out-of-box

For the framework:
- ✅ Category-leading market position
- ✅ Enterprise customer appeal
- ✅ Developer satisfaction
- ✅ Extensibility through templates

#### Success Criteria
- [ ] 192+ production templates across all engines
- [ ] 50+ specialized CLI commands
- [ ] 0 build errors, 0 warnings (all templates)
- [ ] WCAG 2.1 AA compliance
- [ ] <2 seconds to generate complete admin app
- [ ] 95%+ user satisfaction
- [ ] Adoption by 10+ enterprise projects

---

### 🚀 Getting Started

#### Phase 3b-3d (Immediate Blocker)
```bash
# Must implement:
# 1. ResolveTemplatesPackage() - extract .nupkg to cache
# 2. Template rendering from resolved package
# 3. Integration tests for all 4 engines

# Once complete: All following phases can execute
```

#### Phase 4.5 (Auth/Identity) - Next
```bash
modulus ui add-auth --flow both
# Creates: Login/Register/2FA pages + user dashboard

modulus ui create-user-dashboard
# Creates: Complete user admin interface
```

#### Phase 8 (Charts) - High ROI
```bash
modulus ui add-chart --type line
# Creates: Interactive chart component + sample data
```

---

### 📚 Navigation

- **PLAN.md** - Original framework design
- **DESIGN.md** - Architecture & technical decisions
- **UPGRADING.md** - Migration guides

---

### 💬 Questions?

Refer to specific roadmap documents for:
- **Implementation details**: CLI-IMPLEMENTATION.md
- **Auth/Identity features**: PHASE-4.5-AUTH-IDENTITY.md
- **Advanced features timeline**: PHASES-8-14-PROFESSIONAL.md

Each document includes:
- Architecture overview
- Step-by-step implementation guide
- Code examples
- Testing strategy
- Success criteria
- Timeline estimates

---

**Created**: October 10, 2026  
**Status**: Strategic planning complete, ready for execution  
**Next Review**: After Phase 3b-3d completion

<!-- source: docs/roadmaps/CLI-IMPLEMENTATION.md -->
---
name: cli-implementation-roadmap
description: Comprehensive roadmap for implementing all UI CLI commands
metadata:
  node_type: memory
  type: project
  created: 2026-10-10
  originSessionId: 88b0729e-9258-455d-b28b-b0d8f1f35b1d
  modified: 2026-10-10T11:46:47.466Z
---

## Modulus UI CLI Implementation Roadmap

> **Status (2026-10-11).** Built in `modulus/cli`: `ui theme create|set|list|export`, `ui add-component`, `ui create-dashboard`,
> `ui create-form-from-entity`, `ui add-auth`, `ui add-2fa`, `ui add-session-manager`, `ui add-chart`, `ui add-i18n`, `ui audit`, `ui create-permission-matrix`, `ui create-crud`, `ui add-assistant`, the restyled templates for mvc, razor-pages and blazor, the template package
> (`Modulus.Ui.Templates`) with a cache/local-feed/nuget.org resolver, and UI package references in new apps.
> The commands use Spectre.Console.Cli (the code samples below are pseudocode). See [../CLI.md](#modulus-cli-ui-commands) for the reference
> and for what is still open (chat and others, plus a few known issues, see CLI.md).


### Part 1: Architecture Overview

#### Current CLI Structure

```
modulus/cli/
├── Commands/
│   ├── NewAppCommand.cs          [✅ Has --ui-engine]
│   ├── GenerateCrudCommand.cs     [✅ Reads .modulus.json]
│   ├── UiAddCommand.cs            [🟡 Needs enhancement]
│   ├── UiRemoveCommand.cs
│   └── ...
├── Services/
│   ├── TemplateEngine.cs          [✅ Renders templates]
│   ├── NuGetVersionService.cs     [✅ Resolves packages]
│   ├── Models.cs                  [✅ Has AppModel.UiEngine]
│   └── ...
├── Templates/                     [App scaffolding templates]
└── Program.cs                     [Main entry point]
```

#### What Needs to Happen

1. **Extend UiAddCommand** → `modulus ui add-*` subcommands
2. **Create UiThemeCommand** → `modulus ui theme *` subcommands
3. **Create UiGenerateCommand** → `modulus ui create-*` subcommands
4. **Create UiAuditCommand** → `modulus ui audit` and compliance commands
5. **Update Program.cs** → Register all new commands

---

### Part 2: Step-by-Step Implementation Plan

#### STEP 1: Refactor UiAddCommand into Command Family

**Current Structure** (single flat command)
```
modulus ui add <template-name>
```

**Target Structure** (nested subcommands)
```
modulus ui add-chart --type line
modulus ui add-chat --provider claude
modulus ui add-form-builder
modulus ui add-2fa --type totp
```

**Implementation**:

```csharp
// File: cli/Commands/Ui/UiCommand.cs (base)
public class UiCommand : Command
{
    public override int Execute(CommandContext context)
    {
        throw new NotImplementedException("Use subcommands: ui add-*, ui theme, ui create-*, ui audit");
    }
}

// File: cli/Commands/Ui/UiAddCommand.cs (renamed from old UiAddCommand)
public class UiAddCommand : Command
{
    private readonly string _featureType; // "chart", "chat", "form", etc.
    
    public override string Description => $"Add {_featureType} to your app";
    
    public UiAddCommand(string featureType)
    {
        _featureType = featureType;
        AddOption("--engine", "UI engine override");
        AddOption("--module", "Module to add to");
        AddOption("--template", "Template variant");
        AddOption("--with-example", "Include example");
    }
}

// File: cli/Commands/Ui/Chart/UiAddChartCommand.cs
public class UiAddChartCommand : UiAddCommand
{
    public UiAddChartCommand() : base("chart")
    {
        AddOption("--type", "Chart type: line, bar, pie, scatter, heatmap, timeline");
        AddOption("--data-source", "How to get data: endpoint, static, example");
    }
    
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var engine = context.GetOption("--engine");
        var type = context.GetOption("--type") ?? "line";
        var module = context.GetOption("--module") ?? "default";
        
        // 1. Resolve UI engine from .modulus.json if not provided
        engine = await ResolveEngine(engine);
        
        // 2. Resolve template package
        var templatePath = await ResolveTemplate("Modulus.Ui.Templates", "0.9.0", 
            $"charts/chart-{type}");
        
        // 3. Render template with context
        var context = new
        {
            ChartType = type,
            ModuleName = module,
            DataEndpoint = $"/api/{module}/chart-data",
            SampleData = GetSampleDataForChart(type)
        };
        
        await _templateEngine.RenderToFile(templatePath, context, outputPath);
        
        // 4. Update project files if needed
        await UpdateProjectFiles(engine, module);
        
        Ux.Success($"Chart template added to {module}");
        return 0;
    }
}

// File: cli/Commands/Ui/Chat/UiAddChatCommand.cs
public class UiAddChatCommand : UiAddCommand
{
    public UiAddChatCommand() : base("chat")
    {
        AddOption("--provider", "AI provider: claude, openai, custom");
        AddOption("--model", "Model to use");
    }
    
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var provider = context.GetOption("--provider") ?? "claude";
        var model = context.GetOption("--model") ?? "claude-3.5-sonnet";
        
        // Resolve and render chat templates
        // Scaffold API endpoint for chat
        // Create example integration
    }
}

// Repeat for: Form, 2FA, Collaboration, etc.
```

**Commands to Create**:
- `cli/Commands/Ui/Chart/UiAddChartCommand.cs`
- `cli/Commands/Ui/Chat/UiAddChatCommand.cs`
- `cli/Commands/Ui/Forms/UiAddFormBuilderCommand.cs`
- `cli/Commands/Ui/Auth/UiAddAuthCommand.cs`
- `cli/Commands/Ui/Auth/UiAdd2FaCommand.cs`
- `cli/Commands/Ui/Collaboration/UiAddCollaborationCommand.cs`
- `cli/Commands/Ui/Mobile/UiAddPwaCommand.cs`
- ... (12+ total)

---

#### STEP 2: Create Theme Management Command Family

**File Structure**:
```
cli/Commands/Ui/Theme/
├── UiThemeCommand.cs (base)
├── UiThemeCreateCommand.cs
├── UiThemeSetCommand.cs
├── UiThemeListCommand.cs
├── UiThemeExportCommand.cs
└── UiThemeBuilderCommand.cs
```

**Implementation**:

```csharp
// cli/Commands/Ui/Theme/UiThemeCommand.cs
public class UiThemeCommand : Command
{
    public UiThemeCommand()
    {
        AddCommand("create", new UiThemeCreateCommand());
        AddCommand("set", new UiThemeSetCommand());
        AddCommand("list", new UiThemeListCommand());
        AddCommand("export", new UiThemeExportCommand());
        AddCommand("builder", new UiThemeBuilderCommand());
    }
}

// cli/Commands/Ui/Theme/UiThemeCreateCommand.cs
public class UiThemeCreateCommand : Command
{
    public UiThemeCreateCommand()
    {
        AddArgument("name", "Theme name");
        AddOption("--base", "Base theme to extend: tabler, minimal, custom");
        AddOption("--colors", "Color palette: auto-generate from base color");
    }
    
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var name = context.GetArgument("name");
        var baseTheme = context.GetOption("--base") ?? "tabler";
        
        // 1. Create theme directory
        var themeDir = Directory.CreateDirectory($"./Themes/{name}");
        
        // 2. Generate CSS variables file
        var cssVars = GenerateCssVariables(baseTheme);
        File.WriteAllText($"{themeDir}/variables.css", cssVars);
        
        // 3. Create theme manifest
        var manifest = new
        {
            name = name,
            baseTheme = baseTheme,
            created = DateTime.UtcNow,
            colors = new { /* ... */ }
        };
        File.WriteAllText($"{themeDir}/theme.json", JsonSerializer.Serialize(manifest));
        
        // 4. Add to project
        await RegisterThemeInProject(name);
        
        Ux.Success($"Theme '{name}' created");
        return 0;
    }
}

// cli/Commands/Ui/Theme/UiThemeSetCommand.cs
public class UiThemeSetCommand : Command
{
    public UiThemeSetCommand()
    {
        AddArgument("name", "Theme name");
    }
    
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var name = context.GetArgument("name");
        
        // Update appsettings.json or Program.cs
        // Set active theme globally
        await UpdateAppSettings(name);
        
        Ux.Success($"Active theme set to '{name}'");
        return 0;
    }
}

// cli/Commands/Ui/Theme/UiThemeExportCommand.cs
public class UiThemeExportCommand : Command
{
    public UiThemeExportCommand()
    {
        AddArgument("name", "Theme name");
        AddOption("--format", "Export format: css, scss, tailwind, json");
    }
    
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var name = context.GetArgument("name");
        var format = context.GetOption("--format") ?? "css";
        
        // 1. Load theme manifest
        var theme = LoadTheme(name);
        
        // 2. Convert to requested format
        var output = format switch
        {
            "css" => ConvertToCss(theme),
            "scss" => ConvertToScss(theme),
            "tailwind" => ConvertToTailwind(theme),
            "json" => JsonSerializer.Serialize(theme),
            _ => throw new InvalidOperationException($"Unknown format: {format}")
        };
        
        // 3. Export to file
        File.WriteAllText($"./{name}.{format}", output);
        
        Ux.Success($"Theme exported to {name}.{format}");
        return 0;
    }
}
```

**Commands to Create**:
- `cli/Commands/Ui/Theme/UiThemeCreateCommand.cs`
- `cli/Commands/Ui/Theme/UiThemeSetCommand.cs`
- `cli/Commands/Ui/Theme/UiThemeListCommand.cs`
- `cli/Commands/Ui/Theme/UiThemeExportCommand.cs`
- `cli/Commands/Ui/Theme/UiThemeBuilderCommand.cs`

---

#### STEP 3: Create Generation Commands

**File Structure**:
```
cli/Commands/Ui/Generate/
├── UiGenerateCommand.cs (base)
├── UiCreateUserDashboardCommand.cs
├── UiCreatePermissionMatrixCommand.cs
├── UiCreateFormFromEntityCommand.cs
├── UiCreateDashboardCommand.cs
└── ...
```

**Implementation**:

```csharp
// cli/Commands/Ui/Generate/UiCreateUserDashboardCommand.cs
public class UiCreateUserDashboardCommand : Command
{
    public UiCreateUserDashboardCommand()
    {
        AddOption("--engine", "UI engine override");
        AddOption("--module", "Module name: default 'Identity'");
        AddOption("--with-audit", "Include audit trail UI");
        AddOption("--with-2fa", "Include 2FA management");
    }
    
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var engine = await ResolveEngine(context.GetOption("--engine"));
        var module = context.GetOption("--module") ?? "Identity";
        var withAudit = context.GetFlag("--with-audit");
        var with2fa = context.GetFlag("--with-2fa");
        
        // 1. Resolve all required templates
        var templates = new[]
        {
            "users/users-list",
            "users/user-detail",
            "users/user-permissions",
            withAudit ? "audit/user-activity-log" : null,
            with2fa ? "auth/2fa-setup" : null
        }.Where(t => t != null).ToArray();
        
        // 2. Render each template
        foreach (var template in templates)
        {
            var path = await ResolveTemplate("Modulus.Ui.Templates", "0.9.0", template);
            await _templateEngine.RenderToFile(path, 
                new { ModuleName = module }, 
                outputPath);
        }
        
        // 3. Create controller/endpoints if needed
        await GenerateUserApiEndpoints(module, engine);
        
        // 4. Update navigation
        await AddToNavigation(module, "User Management", "/Identity/Users");
        
        Ux.Success($"User dashboard created in {module}");
        return 0;
    }
}

// cli/Commands/Ui/Generate/UiCreateFormFromEntityCommand.cs
public class UiCreateFormFromEntityCommand : Command
{
    public UiCreateFormFromEntityCommand()
    {
        AddArgument("entity", "Entity name (e.g., 'User', 'Order')");
        AddOption("--module", "Module containing entity");
        AddOption("--engine", "UI engine override");
        AddOption("--form-type", "Form type: create, edit, both (default)");
    }
    
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var entity = context.GetArgument("entity");
        var module = context.GetOption("--module") ?? "default";
        var engine = await ResolveEngine(context.GetOption("--engine"));
        var formType = context.GetOption("--form-type") ?? "both";
        
        // 1. Read entity metadata from .modulus.json or reflection
        var entityMeta = await GetEntityMetadata(entity, module);
        
        // 2. Generate form schema from entity properties
        var formSchema = GenerateFormSchema(entityMeta);
        
        // 3. Render form template with schema embedded
        var context = new
        {
            Entity = entity,
            Properties = entityMeta.Properties,
            FormSchema = JsonSerializer.Serialize(formSchema)
        };
        
        if (formType == "create" || formType == "both")
        {
            await RenderTemplate("forms/entity-form-create", context);
        }
        
        if (formType == "edit" || formType == "both")
        {
            await RenderTemplate("forms/entity-form-edit", context);
        }
        
        Ux.Success($"Form for {entity} created");
        return 0;
    }
}

// cli/Commands/Ui/Generate/UiCreateDashboardCommand.cs
public class UiCreateDashboardCommand : Command
{
    public UiCreateDashboardCommand()
    {
        AddArgument("template", "Dashboard template: analytics, sales, operations, custom");
        AddOption("--engine", "UI engine override");
        AddOption("--module", "Module name");
    }
    
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var template = context.GetArgument("template");
        var engine = await ResolveEngine(context.GetOption("--engine"));
        var module = context.GetOption("--module") ?? "Analytics";
        
        // Resolve and render complete dashboard
        // Include all charts, metrics, real-time feeds
        
        await RenderTemplate($"dashboards/{template}-dashboard", 
            new { ModuleName = module });
        
        Ux.Success($"Dashboard '{template}' created");
        return 0;
    }
}
```

**Commands to Create**:
- `cli/Commands/Ui/Generate/UiCreateUserDashboardCommand.cs`
- `cli/Commands/Ui/Generate/UiCreatePermissionMatrixCommand.cs`
- `cli/Commands/Ui/Generate/UiCreateFormFromEntityCommand.cs`
- `cli/Commands/Ui/Generate/UiCreateDashboardCommand.cs`
- ... (8+ total)

---

#### STEP 4: Create Audit & Compliance Commands

**File Structure**:
```
cli/Commands/Ui/Audit/
├── UiAuditCommand.cs (base)
├── UiAuditTemplatesCommand.cs
├── UiAddAuditViewerCommand.cs
├── UiAddAccessReviewCommand.cs
└── ...
```

**Implementation**:

```csharp
// cli/Commands/Ui/Audit/UiAuditCommand.cs
public class UiAuditCommand : Command
{
    public UiAuditCommand()
    {
        AddOption("--module", "Audit specific module");
        AddOption("--level", "Compliance level: A, AA, AAA (WCAG)");
        AddOption("--output", "Report format: json, html, markdown");
    }
    
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var module = context.GetOption("--module");
        var level = context.GetOption("--level") ?? "AA";
        var output = context.GetOption("--output") ?? "html";
        
        // 1. Scan all templates for compliance issues
        var issues = new List<ComplianceIssue>();
        
        issues.AddRange(AuditColorContrast(level));
        issues.AddRange(AuditAriaLabels());
        issues.AddRange(AuditKeyboardNav());
        issues.AddRange(AuditSemanticHtml());
        
        // 2. Generate report
        var report = GenerateComplianceReport(issues, level);
        
        // 3. Export in requested format
        var reportFile = $"accessibility-audit.{GetExtension(output)}";
        File.WriteAllText(reportFile, report);
        
        Ux.Success($"Audit report: {reportFile} ({issues.Count} issues found)");
        return 0;
    }
}

// cli/Commands/Ui/Audit/UiAddAccessReviewCommand.cs
public class UiAddAccessReviewCommand : Command
{
    public UiAddAccessReviewCommand()
    {
        AddOption("--engine", "UI engine override");
        AddOption("--module", "Module for review");
    }
    
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var engine = await ResolveEngine(context.GetOption("--engine"));
        var module = context.GetOption("--module") ?? "Identity";
        
        // Scaffold access review workflow
        // Forms for reviewers to approve/reject access
    }
}
```

**Commands to Create**:
- `cli/Commands/Ui/Audit/UiAuditCommand.cs`
- `cli/Commands/Ui/Audit/UiAddAccessReviewCommand.cs`
- `cli/Commands/Ui/Audit/UiAddAuditViewerCommand.cs`

---

#### STEP 5: Register All Commands in Program.cs

```csharp
// cli/Program.cs

var app = new CommandApp();

// Existing commands
app.AddCommand<NewAppCommand>("app");
app.AddCommand<GenerateCrudCommand>("generate-crud");

// UI command family
var uiCommand = new Command("ui");

// Add UI subcommands
uiCommand.AddCommand("add-chart", new UiAddChartCommand());
uiCommand.AddCommand("add-chat", new UiAddChatCommand());
uiCommand.AddCommand("add-form-builder", new UiAddFormBuilderCommand());
uiCommand.AddCommand("add-auth", new UiAddAuthCommand());
uiCommand.AddCommand("add-2fa", new UiAdd2FaCommand());
uiCommand.AddCommand("add-collaboration", new UiAddCollaborationCommand());
uiCommand.AddCommand("add-pwa", new UiAddPwaCommand());
uiCommand.AddCommand("add-i18n", new UiAddI18nCommand());

// Theme subcommands
uiCommand.AddCommand("theme", new UiThemeCommand()
    .AddCommand("create", new UiThemeCreateCommand())
    .AddCommand("set", new UiThemeSetCommand())
    .AddCommand("list", new UiThemeListCommand())
    .AddCommand("export", new UiThemeExportCommand())
);

// Generate subcommands
uiCommand.AddCommand("create-user-dashboard", new UiCreateUserDashboardCommand());
uiCommand.AddCommand("create-permission-matrix", new UiCreatePermissionMatrixCommand());
uiCommand.AddCommand("create-form-from-entity", new UiCreateFormFromEntityCommand());
uiCommand.AddCommand("create-dashboard", new UiCreateDashboardCommand());

// Audit subcommands
uiCommand.AddCommand("audit", new UiAuditCommand());
uiCommand.AddCommand("add-access-review", new UiAddAccessReviewCommand());

app.AddCommand(uiCommand);

// Result: all commands accessible as:
// modulus ui add-chart
// modulus ui theme create
// modulus ui create-user-dashboard
// modulus ui audit
```

---

### Part 3: Implementation Timeline

#### Week 1-2: Foundation (Steps 1-2)
- [ ] Create `UiAddCommand` family structure
- [ ] Implement 5 chart commands (`UiAddChartCommand`, etc.)
- [ ] Implement theme command family
- [ ] Update Program.cs to wire everything
- [ ] Test end-to-end: `modulus ui add-chart --type line`
- [ ] **Deliverable**: Charts + themes working

#### Week 3-4: Generation (Steps 3-4)
- [ ] Create generation command family
- [ ] Implement `UiCreateUserDashboardCommand`
- [ ] Implement `UiCreateFormFromEntityCommand`
- [ ] Implement `UiCreateDashboardCommand`
- [ ] Add audit/compliance commands
- [ ] **Deliverable**: Dashboard + form generation working

#### Week 5: Auth Integration
- [ ] Implement `UiAddAuthCommand`, `UiAdd2FaCommand`
- [ ] Implement user management commands
- [ ] Scaffold identity endpoints
- [ ] **Deliverable**: Full auth UI generation

#### Week 6: Polish & Testing
- [ ] Add --json flag to all commands
- [ ] Add --dry-run flag to all commands
- [ ] Create integration tests
- [ ] Write documentation
- [ ] **Deliverable**: Production-ready CLI

---

### Part 4: Code Sharing Strategy

To avoid duplicating template resolution logic, create a base class:

```csharp
// cli/Services/UiCommandBase.cs
public abstract class UiCommandBase : Command
{
    protected ITemplateEngine _templateEngine;
    protected INuGetVersionService _nugetService;
    protected IModulusSettings _settings;
    
    protected async Task<string> ResolveEngine(string? engineOverride)
    {
        // Read from .modulus.json, or use override, or prompt
        return engine;
    }
    
    protected async Task<string> ResolveTemplate(
        string packageId, 
        string version, 
        string templateName)
    {
        // Resolve Modulus.Ui.Templates package
        // Extract to cache
        // Return path to template
    }
    
    protected async Task RenderTemplate(string templatePath, object context)
    {
        // Use TemplateEngine to render
    }
    
    protected async Task UpdateProjectFiles(string engine, string module)
    {
        // Update .csproj, Program.cs, navigation, etc.
    }
}

// All command classes inherit from UiCommandBase
public class UiAddChartCommand : UiCommandBase { ... }
public class UiThemeCreateCommand : UiCommandBase { ... }
// etc.
```

---

### Part 5: Testing Strategy

#### Unit Tests
```csharp
[Theory]
[InlineData("mvc")]
[InlineData("razor-pages")]
[InlineData("blazor")]
[InlineData("fluid")]
public async Task UiAddChart_GeneratesCorrectTemplate_ForAllEngines(string engine)
{
    var cmd = new UiAddChartCommand();
    var result = await cmd.ExecuteAsync(new CommandContext
    {
        Options = new Dictionary<string, object>
        {
            { "--engine", engine },
            { "--type", "line" }
        }
    });
    
    Assert.Equal(0, result);
    Assert.FileExists("./Charts/line-chart.cshtml"); // or .razor, .liquid
}
```

#### Integration Tests
```csharp
[Fact]
public async Task FullWorkflow_CreatesCompleteApp()
{
    // 1. Create app
    await RunCommand("modulus app DemoApp --ui-engine mvc");
    
    // 2. Add chart
    await RunCommand("modulus ui add-chart --type line");
    
    // 3. Build
    var buildResult = await DotnetBuild();
    Assert.True(buildResult.Success);
    
    // 4. Verify template was rendered correctly
    var chartFile = File.ReadAllText("./Charts/line-chart.cshtml");
    Assert.Contains("Chart.js", chartFile);
}
```

#### Acceptance Tests
```bash
# Test all commands
modulus ui add-chart --type bar
modulus ui add-chat --provider claude
modulus ui add-form-builder
modulus ui theme create my-theme
modulus ui theme set my-theme
modulus ui create-user-dashboard
modulus ui create-permission-matrix User
modulus ui audit --level AA

# Verify app builds
dotnet build

# Verify pages load
dotnet run
# Check http://localhost:5000/charts
# Check http://localhost:5000/themes
# etc.
```

---

### Part 6: CLI Command Reference (Final)

```bash
# CHART & DATA VISUALIZATION
modulus ui add-chart --type {line|bar|pie|scatter|heatmap|timeline} [--module]
modulus ui add-dashboard --template {analytics|sales|operations}

# AI & INTELLIGENT FEATURES
modulus ui add-chat [--provider {claude|openai}] [--module]
modulus ui add-code-editor --type {monaco|ace} [--module]
modulus ui add-assistant --template {customer-support|dev-tools}

# COLLABORATION & REAL-TIME
modulus ui add-collaboration --type {editor|comments|presence} [--module]
modulus ui add-notifications [--module]

# FORMS & INPUT
modulus ui add-form-builder [--module]
modulus ui create-form-from-entity <Entity> [--module] [--form-type {create|edit|both}]
modulus ui add-wizard [--module]

# AUTH & IDENTITY
modulus ui add-auth --flow {login|registration|both}
modulus ui add-2fa --type {totp|sms}
modulus ui create-user-dashboard [--with-audit] [--with-2fa]
modulus ui create-permission-matrix <Entity> [--module]
modulus ui add-role-builder [--module]
modulus ui add-session-manager [--module]

# THEME MANAGEMENT
modulus ui theme create <name> [--base {tabler|minimal}]
modulus ui theme set <name>
modulus ui theme list
modulus ui theme export <name> --format {css|scss|tailwind|json}

# MOBILE & RESPONSIVE
modulus ui add-pwa [--module]
modulus ui test-responsive

# LOCALIZATION
modulus ui add-i18n --languages {list}

# ACCESSIBILITY & COMPLIANCE
modulus ui audit [--level {A|AA|AAA}] [--output {json|html|markdown}]
modulus ui add-access-review [--module]

# PREVIEW & HELP
modulus ui preview <template>
modulus ui docs <template>
```

---

### Part 7: Completion Checklist

#### Phase 3 Must Complete First
- [ ] Phase 3b: ResolveTemplatesPackage() method
- [ ] Phase 3c: Template rendering from package
- [ ] Phase 3d: Integration tests passing

#### CLI Implementation
- [ ] UiAddCommand family (8+ commands)
- [ ] UiThemeCommand family (5 commands)
- [ ] UiGenerateCommand family (8+ commands)
- [ ] UiAuditCommand family (3+ commands)
- [ ] All commands registered in Program.cs
- [ ] All --json, --dry-run flags implemented

#### Testing
- [ ] Unit tests for each command
- [ ] Integration tests for workflows
- [ ] End-to-end app creation test
- [ ] Template conformance tests (all engines)

#### Documentation
- [ ] CLI help text for each command
- [ ] Example usage in docs
- [ ] Video walkthrough
- [ ] Troubleshooting guide

---

### Success = One Command, Complete App

**Final Vision**:
```bash
$ modulus app MyApp --ui-engine mvc
$ modulus ui add-auth --flow both
$ modulus ui add-chat --provider claude
$ modulus ui create-user-dashboard --with-audit
$ modulus ui theme create corporate --colors blue
$ modulus ui add-i18n --languages en,es,fr
$ dotnet build
$ dotnet run

# Opens browser → Complete, professional SaaS app
# With: Login, Chat, User Dashboard, Audit Logs, i18n, Theme
# Ready to customize, zero boilerplate
```

This is the roadmap to **CLI-driven enterprise UI generation**.

<!-- source: docs/roadmaps/PHASE-4.5-AUTH-IDENTITY.md -->
---
name: auth-identity-ui-templates
description: Auth/Identity UI templates leveraging Modulus.Identity features
metadata:
  node_type: memory
  type: project
  created: 2026-10-10
  originSessionId: 88b0729e-9258-455d-b28b-b0d8f1f35b1d
  modified: 2026-10-10T11:45:40.302Z
---

## Auth/Identity UI Enhancement Layer

### Integration Points with Modulus.Identity

Your framework provides:
- ✅ User management (Modulus.Users)
- ✅ Role-based access control (Modulus.Identity.Authorization)
- ✅ Multi-tenancy support
- ✅ Claims-based authorization
- ✅ Audit logging
- ✅ Permission management

**Opportunity**: Build **15+ professional auth UI templates** that showcase these capabilities.

---

### Phase 4.5: Authentication & Authorization UI

#### Template Set A: Login & Registration (8 templates)

**Modern Login Flows** (all 4 engines)
```
- auth/login-basic.{cshtml,razor,liquid}.sbn
- auth/login-social.{cshtml,razor,liquid}.sbn (OAuth: Google, Microsoft, GitHub)
- auth/login-2fa.{cshtml,razor,liquid}.sbn (Two-factor auth)
```

**Registration & Onboarding** (all 4 engines)
```
- auth/register-form.{cshtml,razor,liquid}.sbn
- auth/register-wizard.{cshtml,razor,liquid}.sbn (Multi-step)
- auth/email-verification.{cshtml,razor,liquid}.sbn
- auth/password-reset.{cshtml,razor,liquid}.sbn
```

**Implementation Details**:
```csharp
// Templates inject user claims into views
@inject IAuthorizationService AuthService
@inject CurrentUserContext CurrentUser

// Current user always available
<span>Welcome, @CurrentUser.DisplayName</span>

// Role-based content
@if (User.IsInRole("Admin"))
{
    <button>Admin Tools</button>
}

// Permission-based rendering
@if (await AuthService.AuthorizeAsync(User, "DeleteUsers"))
{
    <button>Delete User</button>
}
```

---

#### Template Set B: User Management Dashboard (6 templates)

**User Admin Pages** (MVC + Razor Pages)
```
- users/users-list.{cshtml}.sbn
  - Search, filter by role, sort by last login
  - Bulk actions (activate, deactivate, assign roles)
  - Audit trail for each user

- users/user-detail.{cshtml}.sbn
  - Profile edit
  - Claims management
  - Role assignment
  - Permission matrix
  - Activity history

- users/user-permissions.{cshtml}.sbn
  - Permission builder (tree view)
  - Role-based vs direct permissions
  - Permission conflicts warning
  - Inheritance visualization
```

**Blazor Components**
```
- users/user-manager.razor.sbn
  - Real-time user list (SignalR)
  - Inline editing with auto-save
  - Permission selector component
  - Activity graph

- users/permission-matrix.razor.sbn
  - Row: Users, Column: Permissions
  - Click to grant/revoke
  - Bulk updates
  - Diff view (what changed)
```

---

#### Template Set C: Role & Permission Management (5 templates)

**Role Management**
```
- roles/roles-list.{cshtml,razor}.sbn
- roles/role-editor.{cshtml,razor}.sbn
  - Permission tree selector
  - Permission combinations (AND/OR)
  - Sample users with this role
```

**Permission Management**
```
- permissions/permissions-matrix.{cshtml,razor}.sbn
- permissions/permission-builder.{cshtml,razor}.sbn
  - Claims-based rule editor
  - "Create resource if owner" patterns
  - Preview affected users
```

---

#### Template Set D: Security & Compliance (7 templates)

**Account Security**
```
- security/change-password.{cshtml,razor}.sbn
  - Password strength indicator
  - History (can't reuse)
  - Force change on expiry

- security/2fa-setup.{cshtml,razor}.sbn
  - QR code for authenticator apps
  - Backup codes
  - Recovery options

- security/active-sessions.{cshtml,razor}.sbn
  - List all active sessions
  - Browser/device info
  - Revoke individual sessions
  - Suspicious activity alerts
```

**Audit & Compliance**
```
- audit/user-activity-log.{cshtml,razor}.sbn
  - What user did, when, from where
  - IP address, browser, device
  - Filter by action type
  - Export to CSV/Excel

- audit/access-review.{cshtml,razor}.sbn
  - "Users with this permission"
  - Permission trend (added/removed over time)
  - Compliance report generator
  - Segregation of duties checker

- audit/security-events.{cshtml,razor}.sbn
  - Failed login attempts (with rate limiting)
  - Permission changes
  - Account lockouts
  - Admin actions on users
  - Anomaly detection (impossible travel, etc.)
```

---

#### Template Set E: Multi-Tenancy UI (4 templates)

**Tenant Management** (if multi-tenant)
```
- tenants/tenant-list.{cshtml,razor}.sbn
  - Active tenants
  - User count per tenant
  - Subscription status
  - Data usage

- tenants/tenant-settings.{cshtml,razor}.sbn
  - Custom branding
  - Feature toggles
  - User import/export
  - SSO configuration

- tenants/tenant-isolation-view.{cshtml,razor}.sbn
  - Data isolation verification
  - Cross-tenant permission audit
  - User assignment by tenant

- tenants/tenant-analytics.{cshtml,razor}.sbn
  - Growth over time
  - Feature adoption
  - User engagement by tenant
```

---

### New CLI Commands for Auth/Identity

```bash
# Auth scaffolding
modulus ui add-auth --flow {login|registration|both}
  # Scaffolds: login/register pages with password validation
  # Includes: email confirmation, password reset flows

modulus ui add-2fa --type {totp|sms}
  # Scaffolds: 2FA setup wizard
  # Includes: QR code generator, backup codes, verify flow

# User management
modulus ui create-user-dashboard
  # Scaffolds: complete user admin interface
  # Includes: search, filters, bulk operations, roles

modulus ui create-permission-matrix --entity <entity>
  # Auto-generates: permission matrix based on resource operations
  # Maps to: Create, Read, Update, Delete per role

# Role management
modulus ui add-role-builder
  # Scaffolds: role editor with permission tree
  # Includes: permission search, bulk assign

# Audit & compliance
modulus ui add-audit-viewer
  # Scaffolds: audit log UI with filters
  # Includes: CSV export, anomaly detection

modulus ui add-access-review
  # Scaffolds: access review form/workflow
  # Includes: reviewer assignments, approval flow

# Security
modulus ui add-session-manager
  # Scaffolds: active sessions UI
  # Includes: device detection, session revocation

modulus ui add-security-settings
  # Scaffolds: account security dashboard
  # Includes: password management, 2FA, sessions, activity
```

---

### Integration Patterns

#### Pattern 1: Role-Based Rendering

```csharp
// mvc/admin-dashboard.cshtml.sbn
@if (User.IsInRole("Admin"))
{
    <div class="admin-card">
        <h3>System Users</h3>
        <p>Total: @TotalUsers</p>
        <!-- Link to user management -->
        <a href="/Admin/Users">Manage Users</a>
    </div>
}

@if (User.IsInRole("SecurityAdmin"))
{
    <div class="security-card">
        <h3>Security Events</h3>
        <p>Failed Logins: @FailedLoginCount</p>
        <!-- Link to security audit -->
        <a href="/Admin/SecurityEvents">View Events</a>
    </div>
}
```

#### Pattern 2: Permission-Based Actions

```csharp
// users/user-list.cshtml.sbn
@foreach (var user in Users)
{
    <tr>
        <td>@user.DisplayName</td>
        <td>
            @if (await AuthService.AuthorizeAsync(User, "EditUser"))
            {
                <a href="/Users/@user.Id/Edit">Edit</a>
            }
        </td>
        <td>
            @if (await AuthService.AuthorizeAsync(User, "DeleteUser"))
            {
                <button @onclick="() => DeleteUser(user.Id)">Delete</button>
            }
        </td>
    </tr>
}
```

#### Pattern 3: Claims-Based Customization

```csharp
// Blazor: permission-matrix.razor.sbn
@code {
    private async Task LoadUserPermissions()
    {
        var userClaims = User.Claims;
        var userPermissions = userClaims
            .Where(c => c.Type == "permission")
            .Select(c => c.Value)
            .ToList();
        
        // Render permission matrix based on claims
    }
}
```

#### Pattern 4: Audit Trail Integration

```csharp
// Every action is logged
public async Task EditUser(User user)
{
    var original = await _users.GetAsync(user.Id);
    var changeLog = ComputeChanges(original, user);
    
    await _users.UpdateAsync(user);
    
    // Audit logged automatically by middleware
    // Template shows in user-activity-log.cshtml.sbn
}
```

---

### Business Value

#### For IT/Security Teams
- ✅ Complete user lifecycle management UI
- ✅ Permission matrix visibility
- ✅ Audit trail for compliance
- ✅ Security event monitoring
- ✅ Multi-tenancy isolation verification

#### For Enterprises
- ✅ Meet SOC 2, ISO 27001 requirements (audit logs, access control)
- ✅ RBAC implementation showcase
- ✅ User provisioning/deprovisioning workflows
- ✅ Compliance reporting built-in

#### For Developers
- ✅ Copy-paste identity UI implementations
- ✅ Best practices baked in
- ✅ Security patterns enforced
- ✅ User management solved in hours, not weeks

---

### Template Count & CLI Commands

**New Templates**: 30+ (login, 2FA, user management, roles, audit, security, compliance)
- Login & Registration: 8
- User Management: 6
- Role & Permission Management: 5
- Security & Compliance: 7
- Multi-Tenancy: 4

**New CLI Commands**: 12+
```
modulus ui add-auth
modulus ui add-2fa
modulus ui create-user-dashboard
modulus ui create-permission-matrix
modulus ui add-role-builder
modulus ui add-audit-viewer
modulus ui add-access-review
modulus ui add-session-manager
modulus ui add-security-settings
modulus ui add-tenant-management
```

---

### Phasing Strategy

**Option A: Add as Phase 4.5** (between Phase 4 and Phase 5)
- Weeks 1-3 (implement 30+ auth templates)
- Complete before CRUD generation
- Users can scaffold apps with full auth UI from day 1

**Option B: Weave Throughout**
- Phase 5: Add auth examples in CRUD templates
- Phase 6: Admin dashboard includes user management
- Phase 7: Audit logs tie into security audit
- Phase 8+: Permission matrix for advanced features

**Recommendation**: **Option A** — Auth should come early, it's foundational

---

### One-Liner CLI Examples

```bash
# Create complete SaaS app in 4 commands
modulus app SaasApp --ui-engine mvc
modulus ui add-auth --flow both
modulus ui create-user-dashboard
modulus ui create-permission-matrix

# Generates:
# - Login/Register pages
# - User admin dashboard
# - Role management UI
# - Permission matrix
# - Audit logs viewer
# All wired up, ready to customize
```

---

### Success Criteria

- [ ] 30+ auth/identity templates created
- [ ] 12+ CLI commands for auth scaffolding
- [ ] All templates integrate with Modulus.Identity APIs
- [ ] User management workflows complete (CRUD + roles + permissions)
- [ ] Audit trail visible in UI automatically
- [ ] One command creates entire auth UI
- [ ] Enterprise customers adopt for user onboarding

<!-- source: docs/roadmaps/PHASES-8-14-PROFESSIONAL.md -->
## Modulus UI Professional Enhancement Roadmap

### Executive Summary

**Current State**: 67 production templates across 4 engines (MVC, Razor Pages, Blazor, Fluid) with full CRUD, admin dashboards, analytics, and system monitoring.

**Gap Analysis**: Lacks professional-grade advanced features needed for enterprise SaaS:
- AI/ML UI components (chat, code editors, real-time streaming)
- Advanced data visualization
- Real-time collaboration features
- Professional form builders
- Theme customization engine
- Mobile-first progressive enhancement
- Accessibility (WCAG 2.1 AA)

**Vision**: Make `modulus ui create` a one-command scaffold for sophisticated, production-ready interfaces.

---

### Phase 8: Advanced Data Visualization (Weeks 1-2)

#### Templates to Add: 12 files

**Charts & Graphs (4 engines)**
```
- charts/line-chart.{cshtml,razor,liquid}.sbn
- charts/bar-chart.{cshtml,razor,liquid}.sbn  
- charts/pie-chart.{cshtml,razor,liquid}.sbn
- charts/scatter-plot.{cshtml,razor,liquid}.sbn
- charts/heatmap.{cshtml,razor,liquid}.sbn
- charts/timeline.{cshtml,razor,liquid}.sbn
```

**Real-Time Dashboards**
- blazor/realtime-dashboard.razor.sbn (WebSocket/SignalR example)
- mvc/live-metrics.cshtml.sbn (with HTMX polling)
- fluid/analytics-board.liquid.sbn

**Implementation Details**:
- Chart.js v4.4 integration (responsive, 20+ chart types)
- ApexCharts.js for advanced analytics
- D3.js templates for custom visualizations
- Blazor interop for real-time updates
- Server-Sent Events (SSE) support in MVC/Razor Pages

**CLI Enhancement**:
```bash
modulus ui add-chart --engine mvc --type line --module analytics
# Scaffolds: line chart component + sample data endpoint
# Includes: interactive legend, exports, responsive sizing
```

---

### Phase 9: AI & Intelligent UI (Weeks 2-3)

#### Templates to Add: 14 files

**AI Chat Interface**
```
- ai/chat-interface.{razor,cshtml}.sbn
- ai/chat-message.{razor,cshtml}.sbn
- ai/chat-sidebar.{razor,cshtml}.sbn
- ai/ai-assistant-drawer.{razor,cshtml}.sbn
```

**Code & Content Editors**
```
- editors/code-editor-monaco.razor.sbn (Blazor)
- editors/markdown-editor.{cshtml,razor}.sbn
- editors/json-editor.{cshtml,razor}.sbn
- editors/sql-query-builder.{cshtml,razor}.sbn
```

**AI Features**
```
- ai/prompt-builder.{cshtml,razor}.sbn
- ai/ai-settings-panel.{cshtml,razor}.sbn
- ai/token-counter.{cshtml,razor}.sbn
- ai/stream-response-viewer.{cshtml,razor}.sbn
```

**Implementation Details**:
- Monaco Editor (VS Code) for code editing
- Streaming response handlers (SSE, WebSocket)
- Claude API integration examples
- Token counting utilities
- Chat history management (LocalStorage + DB)

**CLI Enhancement**:
```bash
modulus ui add-ai-chat --module customer-service
# Scaffolds: full chat UI + backend endpoint
# Includes: streaming handler, message persistence, auth

modulus ui add-code-editor --type monaco --module dev-tools
# Scaffolds: code editor with syntax highlighting
# Includes: language detection, formatting buttons
```

---

### Phase 10: Real-Time Collaboration (Weeks 3-4)

#### Templates to Add: 10 files

**Collaborative Components**
```
- collaboration/live-editor.razor.sbn
- collaboration/presence-avatars.{cshtml,razor}.sbn
- collaboration/cursor-tracking.razor.sbn
- collaboration/notification-center.{cshtml,razor}.sbn
- collaboration/activity-stream.{cshtml,razor}.sbn
```

**Web Components**
```
- components/monaco-editor-wrapper.razor.sbn
- components/rich-text-editor.{cshtml,razor}.sbn
- components/kanban-board.razor.sbn
```

**Implementation Details**:
- SignalR groups for real-time sync
- Operational Transformation (OT) for conflict resolution
- WebSocket connections with reconnect logic
- Blazor WebAssembly for rich interactions
- IndexedDB for offline-first architecture

**CLI Enhancement**:
```bash
modulus ui add-collab-editor --module documents
# Scaffolds: live multi-user editor
# Includes: presence awareness, conflict resolution, persistence

modulus ui add-notification-center
# Scaffolds: toast + modal notification system
# Includes: priority levels, grouping, persistence
```

---

### Phase 11: Professional Form Builder (Weeks 4-5)

#### Templates to Add: 16 files

**Form Components**
```
- forms/smart-form-builder.{cshtml,razor}.sbn
- forms/conditional-fields.{cshtml,razor}.sbn
- forms/field-dependencies.{cshtml,razor}.sbn
- forms/form-validation-rules.{cshtml,razor}.sbn
- forms/multi-step-wizard.{cshtml,razor}.sbn
- forms/form-versioning.{cshtml,razor}.sbn
```

**Advanced Input Types**
```
- inputs/autocomplete-combo.{cshtml,razor}.sbn
- inputs/date-range-picker.{cshtml,razor}.sbn
- inputs/file-upload-dropzone.{cshtml,razor}.sbn
- inputs/signature-pad.{cshtml,razor}.sbn
- inputs/rating-component.{cshtml,razor}.sbn
- inputs/tag-input.{cshtml,razor}.sbn
- inputs/rich-text-input.{cshtml,razor}.sbn
- inputs/password-strength.{cshtml,razor}.sbn
```

**Implementation Details**:
- JSON-based form schema (define form structure as data)
- Client-side validation with Parsley.js
- Server-side validation attributes
- Conditional field display (show/hide based on values)
- Form state persistence (autosave)
- Template re-use across modules

**CLI Enhancement**:
```bash
modulus ui add-form-builder --module surveys
# Scaffolds: form builder UI + JSON schema handler

modulus ui create-form-from-entity User --module identity
# Auto-generates: form based on entity properties
# Includes: validation rules, required fields, type-appropriate inputs
```

---

### Phase 12: Theme Customization Engine (Weeks 5-6)

#### Templates & Infrastructure: 18 files

**Theme System**
```
- themes/theme-editor.{cshtml,razor}.sbn
- themes/theme-preview.{cshtml,razor}.sbn
- themes/theme-exporter.{cshtml,razor}.sbn
- themes/theme-gallery.{cshtml,razor}.sbn
```

**CSS Customization**
```
- themes/css-variables-builder.{cshtml,razor}.sbn
- themes/dark-mode-toggle.{cshtml,razor}.sbn
- themes/contrast-adjuster.{cshtml,razor}.sbn
```

**Implementation Details**:
- CSS custom properties (variables) per theme
- Tabler theme extension layer
- Color palette builder (complementary, analogous, triadic)
- Font selection system
- Spacing & radius customization
- Export as: CSS, SCSS, Tailwind config
- Runtime theme switching (no reload)

**New CLI Commands**:
```bash
modulus ui create-theme --name "corporate-blue"
# Creates: theme/ folder with CSS variables, preview

modulus ui theme list
# Shows: available themes, active theme

modulus ui theme set corporate-blue
# Applies theme globally

modulus ui export-theme --format tailwind
# Generates: Tailwind configuration
```

---

### Phase 13: Mobile-First & Responsive (Weeks 6-7)

#### Enhancements: 12 files

**Mobile Layouts**
```
- mobile/mobile-sidebar.{cshtml,razor}.sbn
- mobile/bottom-navigation.{cshtml,razor}.sbn
- mobile/modal-sheet.{cshtml,razor}.sbn
- mobile/mobile-form.{cshtml,razor}.sbn
- mobile/touch-actions.{cshtml,razor}.sbn
```

**Responsive Components**
```
- responsive/adaptive-grid.{cshtml,razor}.sbn
- responsive/collapse-sidebar.{cshtml,razor}.sbn
- responsive/stack-on-mobile.{cshtml,razor}.sbn
- responsive/swipeable-tabs.{cshtml,razor}.sbn
```

**PWA Features**
```
- pwa/offline-indicator.{cshtml,razor}.sbn
- pwa/install-prompt.{cshtml,razor}.sbn
```

**Implementation Details**:
- Touch-friendly interaction targets (48x48px minimum)
- Gesture support (swipe, pinch)
- Viewport optimization per device
- Device-specific loading strategies
- Service Worker caching templates
- Offline-first patterns

**CLI Enhancement**:
```bash
modulus ui add-pwa
# Scaffolds: Service Worker, manifest.json
# Includes: offline page, install prompt, notifications

modulus ui test-responsive
# Generates: responsive test page with device emulation
```

---

### Phase 14: Accessibility & Compliance (Weeks 7-8)

#### Audit & Enhancement: 20 files

**WCAG 2.1 AA Compliance**
```
- a11y/aria-live-region.{cshtml,razor}.sbn
- a11y/keyboard-navigation.{cshtml,razor}.sbn
- a11y/focus-indicator.{cshtml,razor}.sbn
- a11y/skiplinks.{cshtml,razor}.sbn
- a11y/contrast-checker.{cshtml,razor}.sbn
```

**Localization Support**
```
- i18n/language-selector.{cshtml,razor}.sbn
- i18n/rtl-support.{cshtml,razor}.sbn
- i18n/localized-form.{cshtml,razor}.sbn
- i18n/translation-helper.{cshtml,razor}.sbn
```

**Implementation Details**:
- Semantic HTML templates (no divitis)
- ARIA labels for dynamic content
- Color contrast validators (WCAG AA, AAA)
- Keyboard navigation guides
- Screen reader testing helpers
- i18n infrastructure (JSON files per locale)
- RTL (Right-to-Left) language support

**CLI Enhancement**:
```bash
modulus ui audit --module customer-service
# Generates: accessibility audit report
# Checks: color contrast, ARIA labels, keyboard nav

modulus ui add-i18n --languages en,es,fr,de
# Scaffolds: i18n infrastructure
# Includes: translation files, language selector
```

---

### Enhanced CLI Architecture

#### New Command Structure

```bash
# Theme Management
modulus ui theme create <name>
modulus ui theme list
modulus ui theme set <name>
modulus ui theme export --format {css|scss|tailwind}
modulus ui theme builder  # Opens web-based theme editor

# AI Features
modulus ui add-chat [--provider {claude|openai|custom}]
modulus ui add-code-editor --type {monaco|ace|simple}
modulus ui add-assistant [--template {customer-support|product-helper|dev-tools}]

# Data Visualization
modulus ui add-chart --type {line|bar|pie|scatter|heatmap}
modulus ui add-dashboard --template {analytics|sales|operations}

# Forms
modulus ui add-form-builder
modulus ui create-form-from-entity <entity> --module <name>
modulus ui add-wizard

# Real-Time
modulus ui add-collaboration --type {editor|comments|presence}
modulus ui add-notifications

# Mobile
modulus ui add-pwa
modulus ui test-responsive

# Accessibility
modulus ui audit [--level {A|AA|AAA}]
modulus ui add-i18n --languages {list}
modulus ui a11y-report

# Advanced
modulus ui preview <template>
modulus ui docs <template>
```

#### New CLI Flags

```bash
modulus ui add-* 
  --engine {mvc|razor|blazor|fluid}
  --template {predefined-options}
  --with-example {true|false}
  --with-tests {true|false}
  --module <name>
  --interactive
  --dry-run
```

---

### New File Structure

```
modulus-ui/src/Modulus.Ui.Templates/templates/

├── charts/
│   ├── line-chart.cshtml.sbn
│   ├── line-chart.razor.sbn
│   └── ...
├── ai/
│   ├── chat-interface.razor.sbn
│   ├── code-editor-monaco.razor.sbn
│   └── ...
├── collaboration/
│   ├── live-editor.razor.sbn
│   └── ...
├── forms/
│   ├── smart-form-builder.cshtml.sbn
│   └── ...
├── themes/
│   ├── theme-editor.cshtml.sbn
│   └── ...
├── mobile/
│   ├── bottom-navigation.cshtml.sbn
│   └── ...
├── a11y/
│   ├── aria-live-region.cshtml.sbn
│   └── ...
└── i18n/
    ├── language-selector.cshtml.sbn
    └── ...

# Updated manifest
ui-kit.json
{
  "features": {
    "charts": ["line", "bar", "pie", "scatter", "heatmap", "timeline"],
    "ai": ["chat", "code-editor", "assistant"],
    "collaboration": ["live-editor", "presence", "notifications"],
    "forms": ["builder", "wizard", "schema-driven"],
    "themes": ["customization", "dark-mode", "export"],
    "mobile": ["pwa", "responsive", "touch-optimized"],
    "a11y": ["wcag2.1-aa", "i18n", "rtl"]
  }
}
```

---

### Implementation Priority Matrix

| Phase | Templates | CLI Commands | Effort | Impact | Timeline |
|-------|-----------|--------------|--------|--------|----------|
| 8: Charts | 12 | 3 | 🟢 Low | 🔴 High | 1-2 weeks |
| 9: AI/Code | 14 | 4 | 🟡 Medium | 🔴 High | 2-3 weeks |
| 10: Collaboration | 10 | 3 | 🔴 High | 🟡 Medium | 3-4 weeks |
| 11: Forms | 16 | 4 | 🟡 Medium | 🔴 High | 2-3 weeks |
| 12: Themes | 18 | 6 | 🟡 Medium | 🟡 Medium | 2-3 weeks |
| 13: Mobile | 12 | 2 | 🟡 Medium | 🟡 Medium | 1-2 weeks |
| 14: A11y | 20 | 4 | 🟡 Medium | 🟡 Medium | 2-3 weeks |

**Total**: 112 new templates, 26 new CLI commands, ~18-20 weeks for full implementation

---

### Business Value

#### For Users
- **Productivity**: Generate complete, enterprise-grade UIs in minutes instead of weeks
- **Quality**: Professional designs + accessibility + mobile support built-in
- **Flexibility**: Theme customization without code changes
- **Future-Ready**: AI integration, real-time features, modern patterns

#### For the Framework
- **Market Position**: Most comprehensive .NET UI scaffolding tool
- **Enterprise Appeal**: WCAG compliance, theme customization, AI-ready
- **Developer Satisfaction**: Reduces boilerplate by 70%+
- **Extensibility**: Clear patterns for custom template sets

---

### Success Metrics

- [ ] 112+ production templates across all engines
- [ ] 26+ specialized CLI commands
- [ ] 0 build errors, 0 warnings (all templates)
- [ ] WCAG 2.1 AA compliance on all components
- [ ] <2 seconds to generate full CRUD with theme
- [ ] 95%+ user satisfaction with generated code quality
- [ ] Adoption in 5+ enterprise projects

---

### Next Immediate Actions

1. **This Week**: Complete Phase 3b-3d (template resolution in CLI)
2. **Week 2**: Start Phase 8 (Charts & Visualization)
3. **Week 3**: Begin Phase 9 (AI & Code Editors)
4. **Week 4+**: Parallel work on remaining phases

**Blocker**: Phase 3b-3d must complete before template generation works end-to-end.

---

### Sample Usage After Implementation

```bash
# Create complete analytics dashboard in one command
modulus ui create-dashboard --template analytics --theme corporate-blue

# Create AI assistant UI
modulus ui add-assistant --type customer-support --provider claude

# Generate responsive form with theme
modulus ui create-form-from-entity Order --module sales --with-theme

# All templates are then rendered with version-matched Modulus.Ui.Templates
# Users get production-ready, themed, accessible, mobile-optimized UI
```

This is the path to making **Modulus the most productive .NET UI framework** available.
