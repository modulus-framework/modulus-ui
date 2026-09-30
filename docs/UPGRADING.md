# Upgrading Ant Design Blazor, htmx, Alpine.js and the plugin libraries

Modulus pins every third-party UI dependency in one place, so upgrades are deliberate and checked.

| Dependency | Pinned in | Current |
|---|---|---|
| `AntDesign`, `AntDesign.ProLayout` | `Directory.Packages.props` (keep both on the same version) | 1.6.2 |
| htmx | `src/Modulus.Ui.Assets/wwwroot/lib/htmx/` | 2.0.11 |
| Alpine.js | `src/Modulus.Ui.Assets/wwwroot/lib/alpinejs/` | 3.17.4 |
| Leaflet (tile maps) | `src/Modulus.Ui.Assets/wwwroot/lib/leaflet/` | 1.9.4 |
| Quill (WYSIWYG editor) | `src/Modulus.Ui.Assets/wwwroot/lib/quill/` | 2.0.3 |
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

## Upgrading Leaflet, Quill or jsVectorMap

1. Replace the files in `wwwroot/lib/<name>/` with the new builds from the official release, keeping the file names
   (`modulus-plugins.js` loads them by path on first use), and update the table above.
2. Read the release notes for the APIs Modulus calls:
   - Leaflet: `L.map`, `L.tileLayer`, `L.marker` / `bindPopup`, `flyTo`, `fitBounds`, `invalidateSize`
   - Quill: the `snow` theme, toolbar modules, `getSemanticHTML`, `text-change`
   - jsVectorMap: the `world` map in `world.js`, region series (`regions` / `values`), `onRegionTooltipShow`
3. Run a sample and check `/plugins/editor` (format, insert an image and a link, save, and confirm the cleaned HTML
   comes back), `/plugins/map` and `/plugins/map-fullsize` (markers, popups, the place list flying to a place, resizing
   the window) and `/plugins/map-vector` (fills, tooltips, legend), in light and dark mode, with SPA navigation on
   (leave the page and come back).

## SPA navigation and `IsHtmx()`

`Request.IsHtmx()` is now false for boosted requests (`hx-boost`), as it already was for history restores: a boosted
link or form is a page load, so `IsHtmx() ? NoContent() : Redirect(...)` keeps working when
`ModulusThemeOptions.SpaNavigation` is on. Use `Request.IsBoosted()` to detect SPA navigation. To opt in:

1. Set `Modulus:Theme:SpaNavigation` to `true` (and `SpaPrefetch` to `false` to turn off hover prefetch).
2. Move inline page code to `Modulus.page(fn)` (or `Modulus.onPage(fn)` for code every page needs). `DOMContentLoaded`
   fires once, on the first page only.
3. Links that must load the whole page (downloads, pages outside the shell that respond with a shell page) take
   `download` or `data-m-reload`; anything that isn't a shell fragment already falls back to a full load.
