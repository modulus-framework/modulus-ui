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
