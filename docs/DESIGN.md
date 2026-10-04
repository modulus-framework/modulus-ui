# Modulus UI — design notes

Why Modulus is built the way it is, and which conventions come from which Ant Design-based project.

## 1. What we studied

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

### Findings that shaped the build

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

## 2. Architecture

```
Modulus.Ui.Abstractions   theme options + palette, menus, ShellRenderer, ProTable request/response, TableQuery,
        │                 Paging, chart geometry, form rules, plugin renderers  (no UI deps)
        │
Modulus.Ui.Assets         modulus*.css (tokens, v5 refresh, shell, pages), antd-dark-brand.css, modulus-*.js
        │                 (theme, shell, mvc, widgets, plugins, spa, virtual), htmx 2.0.11, Alpine 3.17.4,
        │                 MapLibre, Tiptap, jsVectorMap; brings AntDesign + ProLayout static CSS to every flavor
   ┌────┴─────────────┐
Modulus.Blazor         Modulus.AspNetCore.Mvc
App* aliases, ProLayout    tag helpers, _ModulusLayout, htmx response helpers, AntIcons
shell, Pro components,
charts
```

### Theming

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

### Blazor

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

### Nested grid

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

### One table design

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

### Editable grid in MVC

- `app-editable-grid` posts the whole grid on every action: the schema as JSON in `{name}.schema`, row ids in
  `{name}.index`, cells as `{name}[{id}].{key}` (hidden columns included) and the action in `{name}.op`.
  `EditableGridForm.Read` validates ids and the schema (falling back to the server's columns), and `Apply` performs
  row and column operations; save, discard and reset stay with the app. htmx swaps just the grid card.
- Cell editing is client-side (`modulusGrid` in `modulus-widgets.js`) with the same formatting and validation rules
  as `GridSchema`, so edits cost no round trip until a structural action or Save.

### Business fields

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
- **Rich text** is `contenteditable` with `document.execCommand` for the toolbar. The posted HTML is never trusted:
  pages sanitize it before saving, and `ToPlainText` backs required and length checks.

### MVC / Razor Pages

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

### SPA navigation (MVC / Razor Pages)

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

### Large data

- `TableQuery<T>` (`Modulus.Ui.Abstractions.Data`) turns a `ProTableRequest` into an `IQueryable` the database runs:
  registered sorts and column searches only (anything else in the query string is ignored), order by the sort key then
  the id, `Skip`/`Take` for numbered pages, and a keyset cursor (`WHERE key > v OR (key = v AND id > i)`) for the rows
  after a page. `CountMode.None` skips `COUNT(*)` and reads one extra row to know whether more follow. Nullable keys and
  types SQL can't compare fall back to the cursor's offset. Values go into the query as parameters.
- MVC: `<app-load-more>` (infinite scroll: replaces itself with the next rows when revealed) and `<app-virtual-table>`
  with `modulus-virtual.js` (only the rows in view are in the DOM; server-rendered `<tr>` blocks by page number, cached,
  cancelled when scrolled past, the first block seeded with the page). Blazor uses `Virtualize` over the same query.
- A page's own boost area (`hx-sync="#area:replace"`) cancels a filter or sort request still running when another starts.

### Pivot

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

### User state, saved views and field access

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

### Group by (phase 23)

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

### Multi-column sort and edit states (phase 23)

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

### Multi-level headers (phase 23)

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

### The ERP UI map

The ERP suites in §1 were reviewed as feature inventories. This is the UI surface they imply and where Modulus
stands (2026-10-01; PLAN §7.3 has the plan):

| ERP UI surface | Modulus today | Planned |
|---|---|---|
| Data grid: grouping, aggregates, multi-level headers, saved layouts | `ProTable`, `EditableGrid`, `NestedGrid`, column picker, density; single-column sort | Phase 23 grid engine: multi-sort, group headers/footers/aggregates, multi-level headers, checkbox filter menu, row states |
| Pivot analysis | `PivotQuery` / `AppPivotGrid` (written, §7.1) | Pivot UI extensions (derived-measure display) |
| Tree / BOM grids | `NestedGrid` (parent → child levels, lazy load) | Phase 23: TreeGrid mode — self-referencing flat rows, per-level aggregates |
| Query builder / advanced filtering | `QueryFilter`, filter row, `ColumnSearch` | Phase 24 QueryBuilder component |
| Import | — | Phase 25 import wizard UI |
| Scheduler, gantt, kanban | Calendar and Kanban sample pages | P1: promoted to library components; Gantt |
| Lookups | `AppLookup` (type-ahead, "Search more…" dialog) | P1: multi-column, paged dialog |
| Workflow UI | `AppStatusBar`, `WorkflowStage` | P1: `ApprovalTimeline`, history/audit drawer, `StatusBadge` |
| Saved layouts per user | grid/pivot state in the URL; column picker in localStorage | Phase 22 `IUiStateStore` + saved views |
| Printing | invoice print page, `@media print` basics | Phase 26 printing components |

Out of scope for the framework, on purpose: report definitions and scheduling, document pipelines beyond phase 21,
and any product data store. The ERP Report Studio reviewed on 2026-10-01 is a separate future solution that will
consume Modulus packages for this UI surface.

### Component architecture rules

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

### Plugins

- **One renderer, two front ends.** Each plugin's markup is a static class in `Modulus.Ui.Abstractions.Plugins`
  (`EditorHtml`, `MapHtml`, `MediaPlayerHtml`, `SortableHtml`, `SpreadsheetHtml`); the Blazor component and the tag
  helper both call it, so they emit the same DOM. `modulus-plugins.js` finds the `[data-m-*]` roots (`data-m-editor`,
  `data-m-map`, `data-m-vector-map`, `data-m-player`, `data-m-sortable`, `data-m-sheet`) and wires them in both
  flavours, after htmx swaps and Blazor renders alike.
- **Libraries load on first use.** Tiptap, MapLibre, Univer and jsVectorMap are vendored in
  `Modulus.Ui.Assets/wwwroot/lib` and loaded (script and CSS) only when a page has a matching root, so pages without a
  plugin pay nothing. Maps, the editor and the spreadsheet wrap the library (Tiptap and Univer as prebuilt IIFE
  bundles, `lib/tiptap` and `lib/univer`, because both are ESM-only); the media player and sortable are Modulus code.
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

### Samples

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

## 3. Conventions

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

## 4. Roadmap (not built yet)

See [PLAN.md](PLAN.md) for status. Not started:

- A Fluid (Liquid) flavour: `Modulus.AspNetCore.Fluid` with a Liquid layout and tag expander.
- Hardening: error policy, CSP nonce, compression and caching, observability.
- Quality gates in CI: parity snapshots, Playwright flows, performance budgets.
- Replace `AntIcons` reflection if AntDesign makes its icon store public.
- Modulus 2.0: Spreadsheet (with a widget init/destroy registry), document templates and Document Processing,
  `IUiStateStore` with saved views, and permission-aware columns. See PLAN.md §7 for the decisions and order.
- ERP UI roadmap (adopted 2026-10-01): the grid engine, a QueryBuilder component, an import wizard UI and
  printing components (phases 22–26), then the P1 component tier. Scope rule: UI capabilities only — no product
  data layer, scheduling or document pipelines in this framework. See PLAN.md §7.3.
