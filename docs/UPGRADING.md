# Upgrading Ant Design Blazor, htmx and Alpine.js

Modulus pins every third-party UI dependency in one place, so upgrades are deliberate and checked.

| Dependency | Pinned in | Current |
|---|---|---|
| `AntDesign`, `AntDesign.ProLayout` | `Directory.Packages.props` (keep both on the same version) | 1.6.2 |
| htmx | `src/Modulus.Ui.Assets/wwwroot/lib/htmx/` | 2.0.11 |
| Alpine.js | `src/Modulus.Ui.Assets/wwwroot/lib/alpinejs/` | 3.17.4 |

## What protects you

- **Aliases, not wrappers.** `App*` types are empty subclasses, so upstream parameter changes need no Modulus changes.
  Only a removed or renamed component breaks them, and the compiler reports it.
- **`tools/antd-drift`** (runs in CI on every push) compares the pinned AntDesign assembly with
  `manifest/antd-coverage.json`:
  - component types added or removed since the recorded baseline;
  - every `alias` entry has an `App*` type that really derives from that component;
  - every `App*` type is listed in the manifest;
  - every `app-*` tag named in an entry's `mvc` field is a real tag helper in `Modulus.AspNetCore.Mvc`.
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
