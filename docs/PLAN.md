# Plan and progress

The plan for the Modulus UI admin template and where each piece stands. Update the status tables in the same commit
as the work they describe.

Last updated: 2026-09-27 · CI green on `main` at `bf883c9`

**Legend:** ✅ done and verified · 🟡 in progress · ⬜ not started · ➖ not applicable

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
  - Icons come from a Tabler SVG sprite (`app-ti` / `Ti`).

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

## 4. Page inventory

Every route below exists in all three samples. Status columns: **B** Blazor · **M** MVC · **R** Razor Pages.

### Dashboards

| Page | Route | B | M | R |
|---|---|---|---|---|
| Analytics | `/` | ✅ | ✅ | ✅ |
| CRM | `/dashboards/crm` | ✅ | ✅ | ✅ |
| eCommerce | `/dashboards/ecommerce` | ✅ | ✅ | ✅ |

### Apps

| Page | Route | B | M | R |
|---|---|---|---|---|
| Email (list, read) | `/apps/email`, `/apps/email/{id}` | ✅ | ✅ | ✅ |
| Chat | `/apps/chat`, `/apps/chat/{id}` | ✅ | ✅ | ✅ |
| Calendar | `/apps/calendar` | ✅ | ✅ | ✅ |
| Kanban (drag, move, add, delete) | `/apps/kanban` | ✅ | ✅ | ✅ |
| Invoices (list, preview, print, add, edit) | `/apps/invoice/*` | ✅ | ✅ | ✅ |
| Users (list, view) | `/apps/users/list`, `/apps/users/view/{id}` | ✅ | ✅ | ✅ |
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
| Sign in / register | `/auth/login`, `/auth/register` | ✅ | ✅ | ✅ |
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
| Form layouts | `/forms/layouts` | ✅ | ✅ | ✅ |
| Wizard (per-step validation) | `/forms/wizard` | ✅ | ✅ | ✅ |
| Validation showcase | `/forms/validation` | ✅ | ✅ | ✅ |
| Business fields (money, quantity, duration, range, phone, mask, one-time code, lookup, status bar, rich text, signature) | `/forms/business` | ✅ | ✅ | ✅ |
| Data table (search, filters, sort, paging, columns, selection, CSV) | `/tables/advanced` | ✅ | ✅ | ✅ |
| Editable grid (schema-driven columns, in-place editors, expandable rows, pinned columns, add/remove columns) | `/tables/editable` | ✅ | ✅ | ✅ |

### Plugins (planned)

A new **Plugins** menu group in `AdminMenuContributor`, with one page per plugin in all three samples. Items that exist only as a gallery card get a full page. Items that don't exist yet need a new Blazor component and a matching MVC tag helper.

| Page | Route | Today | B | M | R |
|---|---|---|---|---|---|
| Charts | `/ui/charts` | Done: 18 chart components | ✅ | ✅ | ✅ |
| Charts advanced (heatmap, treemap, pareto, waterfall, variance, bullet, gauge, funnel, combo, plus interaction: zoom, legend toggle, drill-down) | `/plugins/charts-advanced` | The advanced types are on `/ui/charts` | ⬜ | ⬜ | ⬜ |
| Color picker (presets, formats, alpha, sizes, inside a form) | `/plugins/color-picker` | `AppColorPicker` / `app-color-picker` gallery card | ⬜ | ⬜ | ⬜ |
| Date picker (NEW): date, time, range, presets, disabled dates, week/month/quarter/year | `/plugins/date-picker` | Only a DatePicker and RangePicker in a gallery card | ⬜ | ⬜ | ⬜ |
| Datatables | `/tables/advanced` | Done, plus the editable and nested grids | ✅ | ✅ | ✅ |
| Dropzone: multiple files, previews, progress, type and size limits, remove | `/plugins/dropzone` | AntD `Upload Drag` in a gallery card | ⬜ | ⬜ | ⬜ |
| Sortable (NEW): reorder a list, drag between two lists, handles, sortable grid of cards | `/plugins/sortable` | Dragging only in Kanban and grid column reorder | ⬜ | ⬜ | ⬜ |
| Fullcalendar | `/apps/calendar` | Done | ✅ | ✅ | ✅ |
| Inline player: video and audio with custom controls, poster, captions | `/plugins/player` | Missing | ⬜ | ⬜ | ⬜ |
| Lightbox: image grid with full-screen preview, captions, keyboard paging | `/plugins/lightbox` | AntD `ImagePreviewGroup` gallery card | ⬜ | ⬜ | ⬜ |
| Map: markers with popups, zoom | `/plugins/map` | Missing | ⬜ | ⬜ | ⬜ |
| Map fullsize: the map fills the content area, with a floating side panel | `/plugins/map-fullsize` | Missing | ⬜ | ⬜ | ⬜ |
| Map vector: world/region choropleth with hover values and a legend | `/plugins/map-vector` | Missing | ⬜ | ⬜ | ⬜ |
| Tour (NEW): multi-step tour over a real page, placements, restart | `/plugins/tour` | `AppTour` / `app-tour` gallery card | ⬜ | ⬜ | ⬜ |
| WYSIWYG editor: toolbar, headings, lists, links, images, HTML output bound to a form field | `/plugins/editor` | `AppRichText` / `app-rich-text` field on `/forms/business` (formatting, lists, links); no images | ⬜ | ⬜ | ⬜ |

**Open decisions**
- **Maps:** a JS library (Leaflet for tile maps, jsVectorMap for vector maps) or plain SVG with no dependency. Plain SVG works for the vector map; tile maps (Map, Map fullsize) realistically need Leaflet.
- **WYSIWYG editor:** build a small `contenteditable` editor on top of `RichText.cs`, or wrap an existing editor (Quill, TipTap).
- **Menu placement:** a new Plugins group, or add the pages to Interface. The existing Charts, Datatables and Calendar entries stay where they are and are linked from Plugins.

**Order of work**
1. Plugins menu group and pages for the gallery-only items: Color picker, Date picker, Dropzone, Lightbox, Tour, Charts advanced.
2. Sortable: shared Alpine/JS sortable in `modulus-widgets.js`, a Blazor wrapper, and an `app-sortable` tag helper.
3. Inline player and WYSIWYG editor.
4. Map, Map fullsize and Map vector, once the library decision is made.
5. Route sweep (desktop, 390 px, dark mode) over the new routes, and tests for any shared logic in `Modulus.Ui.Abstractions`.

## 5. Verification

| Check | How | Last result |
|---|---|---|
| Unit and component tests | `dotnet test --solution Modulus.slnx` | ✅ 364 passed |
| Release build | `dotnet build Modulus.slnx -c Release` | ✅ 0 warnings, 0 errors |
| AntD drift | `dotnet run --project tools/antd-drift` | ✅ no drift (AntDesign 1.6.2) |
| CI | GitHub Actions `CI` on `main` | ✅ green |
| Route sweep, desktop | Every sidebar route in each sample: HTTP 200, no console errors | ✅ 44 × 3 |
| Route sweep, 390 px | Same, plus no horizontal page scroll | ✅ 44 × 3 |
| Route sweep, dark mode | Same, with the dark theme | ✅ 44 × 3 |
| Page scrolling | Mouse-wheel scroll on long pages; the page locks only while the drawer is open | ✅ all three samples |
| Visual parity | Screenshot diff of Blazor vs MVC and Razor Pages at 1440 px | ✅ identical or accepted diffs only |
| Icons | Every rendered `<use>` resolves in `tabler.svg` | ✅ 146 of 146 |
| CSV export | Same bytes and headers from all three samples | ✅ |

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
| Plugin pages: Charts advanced, Color picker, Date picker, Dropzone, Sortable, Inline player, Lightbox, Map, Map fullsize, Map vector, Tour, WYSIWYG editor (see §4 *Plugins (planned)*) | Both | ⬜ |
| Tabler-parity pages: icons, typography, colours, gallery, search results, activity, tasks, logs, changelog, license, job listing, uptime, trial ended, cookie banner, users grid, widgets | Samples | ⬜ |
| Localization: bind `LocaleProvider` to the request culture | Both | ⬜ |
| Brand colour in dark mode (AntD dark CSS hard-codes blue) | Theming | ⬜ |
| Replace `AntIcons` reflection if AntDesign makes its icon store public | MVC library | ⬜ |
| bUnit tests for `ProTable` (`TitleExtra`, `RowSelectable`) and tests for sample logic (`EmployeeDirectory`, `WizardInput`, `ModelValidation`) | Tests | ⬜ |
| Real authentication in the samples (sign-in is a demo) | Samples | ⬜ |
| Update GitHub Actions off Node 20 (`checkout`, `setup-dotnet`, `upload-artifact` v4 warnings) | CI | ⬜ |
| NuGet packaging and versioning | Release | ⬜ |

## 7. Change log

| Date | Change |
|---|---|
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
