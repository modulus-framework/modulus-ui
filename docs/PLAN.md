# Plan and progress

The plan for the Modulus UI admin template and where each piece stands. Update the status tables in the same commit
as the work they describe.

Last updated: 2026-10-01 · `main` at `9605901` + uncommitted PivotGrid (written, not yet built or run) · the Modulus 2.0 roadmap is in §7

**Legend:** ✅ done and verified · 🟡 in progress · ⬜ not started · ❌ failing · ➖ not applicable

## 1. Goal

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

## 2. Architecture (summary)

Details are in [DESIGN.md](DESIGN.md).

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
  - Icons are Ant Design icons drawn inline (`app-ti` / `Ti` take an Ant Design icon name; `ri:` Remix Icon names for the few Ant Design lacks).

## 3. Phases

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
| 14 | Pro: M1 SPA navigation (MVC, Razor Pages) — `SpaNavigation`, fragment layout, `modulus-spa.js` (written, not yet built or run) | 🟡 |
| 15 | Pro: M2 large data — `TableQuery` pipeline (keyset cursors, optional count), `app-load-more`, `app-virtual-table`, `/tables/large` (written, not yet built or run) | 🟡 |
| 16 | Pro: M3 Fluid (Liquid) — `Modulus.AspNetCore.Fluid`, tag expander, Liquid layout, sample on 5103 | ⬜ |
| 17 | Pro: M4 hardening — error policy, CSP nonce, compression and caching, observability | ⬜ |
| 18 | Pro: M5 quality gates — parity snapshots, Playwright flows, performance budgets in CI | ⬜ |
| 19 | 2.0: PivotGrid in the manner of Odoo's pivot view — `PivotQuery`/`PivotState`/`PivotResult`/`PivotHtml`/`PivotXlsx`, `AppPivotGrid`, `app-pivot-grid`, `/tables/pivot` (written, not yet built or run; see §7.1) | 🟡 |
| 20 | 2.0: Spreadsheet (engine and licence decision first), with the widget init/destroy registry it needs (§7.2) | ⬜ |
| 21 | 2.0: Document templates on Fluid (after phase 16), then Document Processing: PDF, Word and Excel output (§7.2) | ⬜ |
| 22 | 2.0: User state — `IUiStateStore`, saved views (grid and pivot layouts), permission-aware columns (§7.2) | ⬜ |

## 4. Page inventory

Every route below exists in all three samples. Status columns: **B** Blazor · **M** MVC · **R** Razor Pages.

### Dashboards

| Page | Route | B | M | R |
|---|---|---|---|---|
| Analytics | `/` | ✅ | ✅ | ✅ |
| CRM | `/dashboards/crm` | ✅ | ✅ | ✅ |
| eCommerce | `/dashboards/ecommerce` | ✅ | ✅ | ✅ |
| ERP analytics | `/dashboards/erp` | ✅ | ✅ | ✅ |

### Apps

| Page | Route | B | M | R |
|---|---|---|---|---|
| Email (list, read) | `/apps/email`, `/apps/email/{id}` | ✅ | ✅ | ✅ |
| Chat | `/apps/chat`, `/apps/chat/{id}` | ✅ | ✅ | ✅ |
| Calendar | `/apps/calendar` | ✅ | ✅ | ✅ |
| Kanban (drag, move, add, delete) | `/apps/kanban` | ✅ | ✅ | ✅ |
| Invoices (list, preview, print, add, edit) | `/apps/invoice/*` | ✅ | ✅ | ✅ |
| Users (list, view) | `/apps/users/list`, `/apps/users/view/{id}` | ✅ | ✅ | ✅ |
| Users grid (cards, role filter, search) | `/apps/users/grid` | 🟡 | 🟡 | 🟡 |
| Roles | `/apps/roles` | ✅ | ✅ | ✅ |
| Permissions | `/apps/permissions` | ✅ | ✅ | ✅ |
| Customers (CRUD, detail) | `/apps/customers`, `/apps/customers/{id}` | ✅ | ✅ | ✅ |
| eCommerce products | `/apps/ecommerce/products` | ✅ | ✅ | ✅ |
| eCommerce orders (list, detail) | `/apps/ecommerce/orders`, `/apps/ecommerce/orders/{id}` | ✅ | ✅ | ✅ |

### Pages, auth and status

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

### UI, forms, tables and charts

| Page | Route | B | M | R |
|---|---|---|---|---|
| Component gallery (every AntD category) | `/ui/components` | ✅ | ✅ | ✅ |
| Cards | `/ui/cards` | ✅ | ✅ | ✅ |
| Charts | `/ui/charts` | ✅ | ✅ | ✅ |
| Icons (every Ant Design icon, three themes, search, copy markup) | `/ui/icons` | 🟡 | 🟡 | 🟡 |
| Typography | `/ui/typography` | 🟡 | 🟡 | 🟡 |
| Colours (theme tokens, Ant Design palette, copy value) | `/ui/colors` | 🟡 | 🟡 | 🟡 |
| Form layouts | `/forms/layouts` | ✅ | ✅ | ✅ |
| Wizard (per-step validation) | `/forms/wizard` | ✅ | ✅ | ✅ |
| Validation showcase | `/forms/validation` | ✅ | ✅ | ✅ |
| Business fields (money, quantity, duration, range, phone, mask, one-time code, lookup, status bar, rich text, signature) | `/forms/business` | ✅ | ✅ | ✅ |
| Data table (search, filters, sort, paging, columns, selection, CSV) | `/tables/advanced` | ✅ | ✅ | ✅ |
| Editable grid (schema-driven columns, in-place editors, expandable rows, pinned columns, add/remove columns) | `/tables/editable` | ✅ | ✅ | ✅ |
| Nested grid (lazy child levels, per-level columns, 1–4 levels) | `/tables/nested` | ✅ | ✅ | ✅ |
| Large data (100,000 rows: pages, infinite scroll, virtual table; `TableQuery`) — written, not yet built or run | `/tables/large` | 🟡 | 🟡 | 🟡 |
| Pivot (Odoo pivot view over the 100,000 shipments: header group-bys with date intervals, Measures, Flip axis, Expand all, Download xlsx, sort by a measure, drill-down to Large data) — written, not yet built or run | `/tables/pivot`, `/tables/pivot/download` | 🟡 | 🟡 | 🟡 |

### Extra (Tabler extra pages)

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

### Plugins

Menu group *Plugins* in `AdminMenuContributor` (Data, Inputs, Interface), with one page per plugin in all three samples. Page bodies are in `Views/Shared/Ui/Plugins`, data and page logic in `PluginsContent` (`Samples.Shared`); MVC routes in `PluginsController`. Charts, Datatables and Fullcalendar link to their existing pages. Pages use AntD icons only; the Tabler sprite is unchanged. 🟡 means written but not yet built, run or swept.

| Page | Route | Today | B | M | R |
|---|---|---|---|---|---|
| Charts | `/ui/charts` | Done: 18 chart components | ✅ | ✅ | ✅ |
| Charts advanced (heatmap, treemap, pareto, waterfall, variance, bullet, gauge, funnel, combo, plus interaction: zoom, legend toggle, drill-down) | `/plugins/charts-advanced` | Legend toggle (`ChartOptions.LegendToggle`), drill-down links (`ChartOptions.Links`), zoom by range links (`?days=`) | 🟡 | 🟡 | 🟡 |
| Color picker (presets, formats, alpha, sizes, inside a form) | `/plugins/color-picker` | Page with a GET form (`?primary=&accent=`) and a brand preview | 🟡 | 🟡 | 🟡 |
| Date picker: date, time, range, presets, disabled dates, week/month/quarter/year | `/plugins/date-picker` | MVC: `picker="quarter"` / `"year"`, `presets` on `app-range-picker`, `min`/`max`; `DatePeriods` shared | 🟡 | 🟡 | 🟡 |
| Datatables | `/tables/advanced` | Done, plus the editable and nested grids | ✅ | ✅ | ✅ |
| Dropzone: multiple files, previews, progress, type and size limits, remove | `/plugins/dropzone` | `app-upload` gains `action` (XHR with progress), `max-size`, `max-count`, `list-type="picture"`; `POST /plugins/dropzone/upload` in all three samples | 🟡 | 🟡 | 🟡 |
| Sortable (NEW): reorder a list, drag between two lists, handles, sortable grid of cards | `/plugins/sortable` | `AppSortable` / `app-sortable` + `app-sortable-item`, keyboard moves, posts the order | 🟡 | 🟡 | 🟡 |
| Fullcalendar | `/apps/calendar` | Done | ✅ | ✅ | ✅ |
| Inline player: video and audio with custom controls, poster, captions | `/plugins/player` | `AppMediaPlayer` / `app-media-player` (`MediaPlayerHtml`); audio track made on the server | 🟡 | 🟡 | 🟡 |
| Lightbox: image grid with full-screen preview, captions, keyboard paging | `/plugins/lightbox` | Captioned grid, square tiles, single image, keyboard list | 🟡 | 🟡 | 🟡 |
| Map: markers with popups, zoom | `/plugins/map` | `AppMap` / `app-map` on Leaflet (`MapHtml.Tile`) | 🟡 | 🟡 | 🟡 |
| Map fullsize: the map fills the content area, with a floating side panel | `/plugins/map-fullsize` | Full-height map with a searchable place list | 🟡 | 🟡 | 🟡 |
| Map vector: world/region choropleth with hover values and a legend | `/plugins/map-vector` | `AppVectorMap` / `app-vector-map` on jsVectorMap (`Choropleth`, `MapHtml.Vector`) | 🟡 | 🟡 | 🟡 |
| Tour: multi-step tour over a real page, placements, restart | `/plugins/tour` | Mock dashboard; steps for bottom / left / top / right plus centred intro and wrap-up; Start and Restart | 🟡 | 🟡 | 🟡 |
| WYSIWYG editor: toolbar, headings, lists, links, images, HTML output bound to a form field | `/plugins/editor` | `AppEditor` / `app-editor` on Quill (`EditorHtml`), cleaned with `RichText.Sanitize(images: true)` | 🟡 | 🟡 | 🟡 |

**Decisions** (2026-09-28)
- **Maps:** Leaflet for the tile maps (Map, Map fullsize) and jsVectorMap for the vector map.
- **WYSIWYG editor:** wrap Quill; clean its HTML on the server with `RichText.Sanitize`.
- **Menu placement:** a new Plugins group; Charts, Datatables and Calendar keep their entries and are linked from Plugins.
- **Icons:** plugin pages use AntD icons (`app-icon` / `Icon`); no new Tabler sprite icons.

**Order of work**
1. 🟡 Plugins menu group and pages for the gallery-only items: Color picker, Date picker, Dropzone, Lightbox, Tour, Charts advanced (written; build, tests and route sweep pending).
2. 🟡 Sortable: `modulus-plugins.js`, a Blazor component and an `app-sortable` tag helper (written).
3. 🟡 Inline player and WYSIWYG editor (Quill) (written).
4. 🟡 Map, Map fullsize and Map vector (Leaflet, jsVectorMap) (written).
5. Route sweep (desktop, 390 px, dark mode) over the new routes, and tests for any shared logic in `Modulus.Ui.Abstractions`.

## 5. Verification

| Check | How | Last result |
|---|---|---|
| Unit and component tests | `dotnet test --solution Modulus.slnx` | ✅ 371 passed |
| Release build | `dotnet build Modulus.slnx -c Release` | ✅ 0 warnings, 0 errors |
| AntD drift | `dotnet run --project tools/antd-drift` | ✅ no drift (AntDesign 1.6.2) |
| CI | GitHub Actions `CI` on `main` | ❌ red since `9579950`: `EditableGridTests.RendersColumnsFromSchema_WithPinnedCellsAndRequiredMarks` expects `$1,200`, but the runner's invariant culture renders `¤1,200`. Fixed locally (the test class pins `en-US`); green once pushed. |
| Route sweep, desktop | Every sidebar route in each sample: HTTP 200, no console errors | ✅ 44 × 3 (2026-09-27); the 22 `/extra/*` routes × 3 on 2026-09-28 |
| Route sweep, 390 px | Same, plus no horizontal page scroll | ✅ 44 × 3 (2026-09-27); the 22 `/extra/*` routes × 3 on 2026-09-28 |
| Route sweep, dark mode | Same, with the dark theme | ✅ 44 × 3 (2026-09-27); the 22 `/extra/*` routes × 3 on 2026-09-28 |
| Page scrolling | Mouse-wheel scroll on long pages; the page locks only while the drawer is open | ✅ all three samples |
| Visual parity | Screenshot diff of Blazor vs MVC and Razor Pages at 1440 px | ✅ identical or accepted diffs only |
| Icons | Every bundled icon draws an SVG, Tabler names draw nothing (`ShellIcon_EveryBundledIcon_DrawsAnInlineSvg`) | 🟡 written, not yet run |
| CSV export | Same bytes and headers from all three samples | ✅ |
| Pivot | `PivotTests` (state round trip, expand/collapse/flip/expand all, totals = LINQ sums, averages and maxima, `MaxGroups`, only open headers read, date labels, sort, markup, bUnit parity, xlsx, SQL `GROUP BY`s with parameters via EF Core `ToQueryString`) and `PivotTagHelperTests` (MVC = Blazor markup, `PivotState.Link`) | 🟡 written, not yet run |
| Pivot download | The xlsx from all three samples opens in Excel and LibreOffice, with merged headers and bold totals | ⬜ |

**Accepted visual differences:** these come from the controls, not the markup.
- Text rendering in a native `<select>` vs an AntD `Select`.
- The AntD password-reveal glyph.
- Subpixel antialiasing fringes.

## 6. Follow-up work

| Item | Area | Status |
|---|---|---|
| Column picker and density control built into the library: `app-table-columns`, `app-table-settings`, `app-table hidden-columns / density / scroll-x / table-footer` | MVC library | ✅ |
| Keep filters in the URL (`hx-push-url` with the query) on the MVC list pages: all ten already push the URL; empty filter fields are now left out of it | MVC samples | ✅ |
| MVC equivalents for Blazor-only widgets: `app-cascader`, `app-tree-select`, `app-transfer`, `app-tree`, `app-calendar`, `app-carousel`, `app-anchor` (Alpine, `modulus-widgets.js`), with gallery cards in all three samples | MVC library | ✅ |
| Editable data grid: schema-driven columns (text, number, currency, percent, select, multi-select, tag, date, switch, progress, rating) with in-place editors and validation, expandable rows, left/right pinned columns, add/delete rows, add/remove/pin/hide/reorder columns, save. `EditableGrid` (Blazor), `app-editable-grid` + `EditableGridForm` (MVC), `GridSchema` shared | Both | ✅ |
| Complete the AntD gallery: Link, Flex, Splitter, Affix, BackTop, DropdownButton, AutoComplete, InputGroup, CheckboxGroup, Mentions, Image, CountDown, Drawer, Watermark, Comment, plus Modulus ColorPicker, FloatButton and QRCode where AntD Blazor 1.6.2 has none | Both | ✅ AntD components in both flavours, plus Modulus QRCode, ColorPicker, FloatButton and Tour |
| Plugin pages: all twelve written (batch 1: Charts advanced, Color picker, Date picker, Dropzone, Lightbox, Tour; batch 2: Sortable, Inline player, Map, Map fullsize, Map vector, WYSIWYG editor); build, tests and route sweep pending (see §4 *Plugins*) | Both | 🟡 |
| Tabler-parity pages: gallery, search results, activity, tasks, logs, license, job listing, uptime, trial ended, cookie banner and widgets are done (§4 *Extra*); icons, typography, colours, changelog and users grid written, not yet run | Samples | 🟡 |
| Localization: `ModulusThemeOptions.Cultures` adds a language picker to the theme settings panel (culture cookie + reload); `app.UseModulusLocalization()` (MVC) and `UseRequestLocalization` (Blazor sample) set the request culture, which AntD's `LocaleProvider` and the tag helpers follow. Sample texts stay English | Both | 🟡 written |
| Brand colour in dark mode (AntD dark CSS hard-codes blue): `AntDarkBrand` + `antd-dark-brand.css` swap the blue shades for the brand's dark palette | Theming | 🟡 written |
| Replace `AntIcons` reflection if AntDesign makes its icon store public | MVC library | ⬜ |
| bUnit tests for `ProTable` (`TitleExtra`, `RowSelectable`) and tests for sample logic (`EmployeeDirectory`, `WizardInput`, `ModelValidation`, reference pages, `SampleAuth`); `Modulus.Blazor.Tests` now references `Samples.Shared` | Tests | 🟡 not yet run |
| Real authentication in the samples: cookie sign-in and sign-out against the sample users (password `modulus`), the signed-in user in the header; `Samples:RequireSignIn` (off by default) requires sign-in on every page but the auth and error pages | Samples | 🟡 written |
| Make `EditableGridTests` culture-independent: the class sets `en-US` in its constructor and restores it in `Dispose` | Tests | 🟡 awaiting CI |
| Update GitHub Actions off Node 20: `checkout@v7`, `setup-dotnet@v6`, `upload-artifact@v7`, `github-script@v9` in both workflows | CI | 🟡 awaiting CI |
| NuGet packaging and versioning: `src/Directory.Build.props` (one `VersionPrefix`, SourceLink, symbols, README), other projects not packable; CI packs `-ci.<run>` prereleases as the `packages` artifact | Release | 🟡 written |
| Nested menus organised like Odoo: navbar dropdowns are one narrow column with bold section headings (entries indented under them) and groups inside a section folding open in place; sidebar groups nest as folding branches, to any depth, instead of the multi-column dropdown. Every sample page belongs to a launcher app (Plugins and Extra added), so the current-app bar never falls back to the whole menu | Shell | 🟡 written |

## 7. Modulus 2.0 roadmap

From the "Modulus 2.0" proposal reviewed on 2026-09-30. The proposal fits the framework (one renderer for Blazor and
MVC, state in the URL, `[data-m-*]` roots started by `modulus-plugins.js`), but it was not adopted as written. This
section is the to-do list: what is done, what is left, and what was dropped and why.

### 7.1 PivotGrid (phase 19), written 2026-10-01, not yet built or run

| Item | Where | Status |
|---|---|---|
| Registered fields and measures; one grouped query per row depth × column depth that has open headers, filtered to them (Odoo's way: closed headers are not read); `MaxGroups` cap over what shows | `Ui.Abstractions/Pivot/PivotQuery.cs`, `PivotResult.cs` | 🟡 written |
| State in the query string (`rows`, `cols`, `measures`, `rx`/`ra`, `cx`/`ca`, `sort`, `sortcol`, `desc`); expand, expand by, collapse, flip, expand all, measures, sort; `PivotState.Link` keeps the page's own query values | `PivotState.cs` | 🟡 written |
| Shared markup: toolbar, nested column headers, indented row headers, group-by dropdowns with a date-interval submenu, sortable measure headers, drill-down links, empty and too-many-groups states | `PivotHtml.cs` | 🟡 written |
| xlsx download without a library (SpreadsheetML in a zip) | `PivotXlsx.cs` | 🟡 written |
| Blazor `AppPivotGrid`, MVC `<app-pivot-grid>` | `Modulus.Blazor/Components/Pro/AppPivotGrid.razor`, `TagHelpers/PivotTagHelpers.cs` | 🟡 written |
| Header dropdowns float out of the scrolling table: `modulus-shell.js` calls `placeDropdown` when a `details.m-dropdown` with a `.m-dropdown-panel` opens (and again when a date submenu opens) | `modulus-shell.js` | 🟡 written |
| Busy state and scroll: a pivot link marks the pivot `m-pivot-busy` (a bar runs along the table, values fade) until the new markup replaces it; the table's scroll position and the page's scroll survive the replacement; MVC swaps with `show:none` | `modulus-shell.js`, `Views/Shared/Ui/Tables/Pivot.cshtml` | 🟡 written |
| Pivot CSS (toolbar button group, table scrolling inside the card with sticky column and row headers, shaded row headers and totals, row hover, open menus above the sticky headers, indents) | `modulus-pages.css` *Pivot table* | 🟡 written |
| Blazor filters without flicker: the Status, City and keyword boxes show their value at once instead of reverting until the navigation lands; Blazor `/tables/large` reads `?status=&city=&keyword=` and gets the City select and search MVC has, so the Blazor pivot drills down too (`CellHref`) | Blazor `Pages/Tables/Pivot.razor`, `Large.razor` | 🟡 written |
| Sample `/tables/pivot` and `/tables/pivot/download` in all three apps; menu *Tables → Pivot* | `Samples.Shared/ShipmentPivot.cs`, `Views/Shared/Ui/Tables/Pivot.cshtml`, `TablesController`, `Pages/Tables/Pivot.cshtml`, Blazor `Pages/Tables/Pivot.razor` + `Program.cs` | 🟡 written |
| Tests | `tests/Modulus.Blazor.Tests/PivotTests.cs`, `tests/Modulus.AspNetCore.Mvc.Tests/PivotTagHelperTests.cs` | 🟡 written |

**Still to do or to check for the PivotGrid**
1. ⬜ Build with 0 warnings and run the tests: `dotnet build Modulus.slnx -c Release`, `dotnet test --solution Modulus.slnx`, `dotnet run --project tools/antd-drift`.
2. ⬜ Check that the `Microsoft.EntityFrameworkCore.SqlServer` test pin (`10.0.12` in `Directory.Packages.props`) exists on NuGet; change the version if not.
3. ⬜ EF Core translation (`Queries_TranslateToGroupBysInSql_WithTheOpenHeadersAsParameters`): confirm that the `PivotKey` member-init key (also the empty key of the grand-total query), the open-header filters, the enum `long` casts and `DateOnly.DayNumber` (the week interval) translate. If the empty key does not, group the grand total by a constant instead. If `DayNumber` does not, give `PivotQuery.Date` a provider-neutral week key or drop Week for databases.
4. 🟡 Blazor drill-down: written (Blazor Large reads its filters from the query; the pivot passes `CellHref`). Check that a value opens `/tables/large` filtered, in Pages and Virtual mode, and that back returns to the same pivot layout.
   - Also check the busy bar: it clears on every link (the 15 s timer is only a fallback for a render that changed nothing), and the table keeps its scroll after expanding a header far down or to the right.
5. ⬜ Blazor download link: confirm the interactive router does not intercept the `download` anchor (it carries `download` and `data-enhance-nav="false"`).
6. ⬜ Route sweep of `/tables/pivot` on 5100, 5101 and 5102. Check:
   - Header actions: expand, collapse, flip, expand all, measures, sort.
   - Back and forward.
   - The keyword and filters keep the layout.
   - At 390 px: the toolbar wraps, the table scrolls and the row headers stay sticky.
   - Dark mode.
   - The header dropdown is not clipped by the table.
7. ⬜ Screenshot parity, Blazor vs MVC at 1440 px (`/tables/pivot`).
8. ⬜ Open the downloaded xlsx in Excel and LibreOffice.
9. ⬜ README: add the pivot to the feature list and `app-pivot-grid` to the tag-helper table.
10. ⬜ Later, only if asked (Odoo has these; the first cut does not): a "Compare" period, the graph view switch, "Insert in spreadsheet", per-user saved layouts (needs §7.2 `IUiStateStore`).

### 7.2 The rest of the proposal

| Proposal item | Decision | Status |
|---|---|---|
| Spreadsheet (formulas, multi-sheet, xlsx in and out) | Do after PivotGrid. Decide the engine and licence first: Handsontable and HyperFormula are not free for commercial use. Look at permissive engines (e.g. Jspreadsheet CE, Fortune-sheet) or a server-side formula engine. Its init/destroy goes through the widget registry below. | ⬜ |
| Widget registry (`init`/`destroy` per `[data-m-*]` root) | Build it together with the Spreadsheet, not on its own. Today only Quill and jsVectorMap hold instances, and the `modulus-plugins.js` MutationObserver already starts and stops roots. | ⬜ |
| Document templates and Document Processing (PDF, Word, Excel output, mail merge) | After Fluid (phase 16): templates in Liquid, rendered to HTML, then to PDF or DOCX. Pick the PDF engine (and its licence) at that point. | ⬜ |
| `IUiStateStore` and saved views | A small store interface in `Ui.Abstractions` (per user, per page key), with a cookie default and a database sample. Saved views for the data table, editable grid and pivot. The pivot's state is already one query string, so a saved view is a named URL. | ⬜ |
| Permission-aware columns | Filter columns and pivot fields with a policy callback (`Func<string, bool>` or `IAuthorizationService`) before rendering and before querying; never trust the query string. | ⬜ |
| `DEPENDENCIES.md` | One page listing every vendored script (htmx, Alpine, Leaflet, Quill, jsVectorMap) and NuGet package, with its version, its licence and why it is used. | ⬜ |
| Architecture rules (proposal §48) | Adopted in DESIGN.md *Component architecture rules*, adapted: rule 6 uses the `modulus-plugins.js` observer, not htmx `afterSwap`. | ✅ written |
| htmx `afterSwap` lifecycle | Dropped: the MutationObserver in `modulus-plugins.js` already covers htmx swaps, Blazor renders and SPA navigation. | ➖ |
| Package renames and a JS folder restructure | Dropped: no gain for the cost of breaking every consumer. Revisit only at a major version. | ➖ |
| `IModulusPlugin` registry | Dropped for now: plugins are plain components and tag helpers, and a registry adds indirection without a use case. | ➖ |
| Choosing the aggregation per request | Dropped: each measure has one fixed aggregate (`PivotAggregate`), like Odoo's field `aggregator`. Anything else lets the URL pick work the server did not plan for. | ➖ |

**Order of work:**
1. Finish §7.1 (build, tests, sweep).
2. Stabilise the 🟡 work in §3–§6.
3. Spreadsheet, with the widget registry.
4. Fluid (phase 16).
5. Document templates.
6. Document Processing.

`IUiStateStore` and saved views can start any time after §7.1.

## 8. Change log

| Date | Change |
|---|---|
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
