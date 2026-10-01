# Modulus UI

Admin-panel UI for **Blazor**, **ASP.NET Core MVC** and **Razor Pages**, built on
[Ant Design Blazor](https://antblazor.com) 1.6.2 and its official Pro layout, on .NET 10.

One theme, one menu definition and one set of design tokens produce the same [Tabler](https://tabler.io/admin-template)-style
admin template in every flavor. Blazor uses the real Ant Design Blazor components. MVC and Razor Pages use tag helpers that
emit the same Ant Design DOM, with htmx and Alpine.js for interactivity. The three sample apps render the same 90+ pages,
pixel-for-pixel where the controls allow it.

| Blazor — analytics dashboard | MVC — eCommerce dashboard (dark) |
|---|---|
| ![Blazor analytics dashboard](docs/screenshots/blazor-dashboard.png) | ![MVC eCommerce dashboard, dark theme](docs/screenshots/mvc-ecommerce-dark.png) |
| **Razor Pages — kanban** | **Blazor — data table (dark)** |
| ![Razor Pages kanban board](docs/screenshots/razorpages-kanban.png) | ![Blazor data table, dark theme](docs/screenshots/blazor-data-table-dark.png) |
| **MVC — form wizard** | **Razor Pages — sign-in** |
| ![MVC form wizard](docs/screenshots/mvc-wizard.png) | ![Razor Pages sign-in](docs/screenshots/razorpages-login.png) |

## What you get

- **Every Ant Design Blazor component**, as-is. Modulus adds thin `App*` aliases (`AppButton : AntDesign.Button`),
  so every upstream parameter works and new ones arrive automatically on upgrade. The coverage manifest lists 90 aliases;
  CI verifies them against the pinned AntDesign package.
- **Tabler-style shell**: header with page search (Ctrl K), an app switcher, a tenant switcher, notifications and account menu; a horizontal navbar,
  a condensed one-row header or a vertical sidebar, all switchable in a settings panel and remembered in cookies. Layout
  settings: content width (boxed, fluid, framed), header (fixed, sticky, static, floating; light or dark), sidebar
  (start or end side; default, transparent or floating; full, folded with flyouts, or folded until hovered), light or
  dark menu, and left-to-right or right-to-left. Fifteen one-click presets cover the usual demo layouts (Boxed,
  Condensed, Fluid, Fluid vertical, Horizontal, Navbar dark / floating / sticky, Right vertical, RTL, Vertical,
  Vertical transparent / floating, Folded sidebar, Folded hover); defaults come from `ModulusThemeOptions`; footer; light/dark mode with no flash on load. One renderer (`ShellRenderer`) produces the markup for
  Blazor's `AppShell` and MVC's `_ModulusLayout`. The Ant Design Pro `ModulusLayout` is still available for Blazor.
  Header content (user, notifications, `Apps`, `Tenants`, links) comes from your `IShellContentProvider`. A tenant
  without an `Href` is switched by writing the `modulus-tenant` cookie and reloading (cancel the `modulus:tenant-change`
  event to handle it yourself); set `ShellContent.CurrentTenantId` when your app resolves the tenant itself.
  Nested menus fold as a tree to any depth: in a navbar dropdown, a group directly under it is a bold section with its
  entries indented (as in Odoo's app menus) and deeper groups fold open in place; in the sidebar, groups are folding
  branches. The branches on the path to the current page start open. `MenuScope.App` shows only the current app's menu
  (Odoo style), with the other apps in the launcher.
  Nested menus fold as a tree to any depth: in a navbar dropdown, a group directly under it is a bold section with its
  entries indented (as in Odoo's app menus) and deeper groups fold open in place; in the sidebar, groups are folding
  branches. The branches on the path to the current page start open. `MenuScope.App` shows only the current app's menu
  (Odoo style), with the other apps in the launcher.
- **Pro components** following ProComponents conventions: `ProTable` (server paging, sorting, selection and batch actions,
  expandable row details via `RowDetail`, resizable columns, density, reload, a filter row, Columns and Export CSV,
  a Clear button while a search or filter is set), `QueryFilter` (collapsible search form),
  `ModalForm` / `DrawerForm` (validated, async submit), `StatisticCard` + `Trend`, `FooterToolbar`.
- **Row drag and drop** in the data table, the editable grid and every nested grid level: drag the handle, or focus it
  and press Up, Down, Home or End. Expanded and nested rows move with their row, and the handles lock while a column
  sort is on. Blazor reports the move (`ProTable.OnRowMove`, `EditableGrid.RowDraggable` / `OnRowMove`, `RowMove`);
  in MVC the grid posts its rows in the new order (`row-draggable`).
- **One pager everywhere** (`Paging`): page sizes 10/20/50/100, the "1–10 of 42" range and Ant Design's page window
  with ••• jumps, used by the data table, the editable grid, nested levels and `app-pagination`.
- **Editable grid** (`EditableGrid` / `app-editable-grid`): columns are data (`GridColumn`: text, number, currency,
  percent, select, multi-select, tag, date, switch, progress, rating). Cells edit in place with the matching control
  and are validated; rows expand into a detail template; columns pin left or right, hide, reorder, resize (drag the
  header edge) and are added or removed at run time. It looks and works like the data table: a search box, a filter
  row (`GridColumn.Filterable`), Columns and Export CSV buttons, row checkboxes with a batch bar (set a value, export
  or delete the selected rows), and cells with an initials avatar (`Avatar`), a second line (`SubtitleKey`) or option
  icons (`GridOption.Icon`). Blazor and MVC share one schema, formatter, search and validator (`GridSchema`).
- **Nested grid** (`NestedGrid` / `app-nested-grid`): one component for any depth. `NestedGridSettings` lists the
  levels, each with its own columns (or a column set picked by a field of the parent row), search, filters, sort,
  page size, editing and expand rules; the number of levels is the nesting limit. Every level has the editable
  grid's toolbar, selection and export, and searches and filters on the server (`NestedGridRequest.Filters`). A child table loads the first time its parent
  row expands (Blazor `Load(NestedGridRequest)`, MVC an htmx GET), and each level saves on its own
  (`NestedGridChanges`: the page as edited and the deleted ids).
- **Large data** (`TableQuery<T>` in `Modulus.Ui.Abstractions.Data`): turns a `ProTableRequest` into an `IQueryable`
  your database runs, so a table over millions of rows reads one page. Only registered sorts and column searches are
  applied; numbered pages use `Skip`/`Take` and the rows after a page use a keyset cursor. `CountMode.None` skips
  `COUNT(*)`. MVC adds `<app-load-more>` (infinite scroll) and `<app-virtual-table>` (only the rows in view are in the
  DOM); Blazor uses `Virtualize` over the same query. `/tables/large` shows all three over 100,000 rows.
  ```csharp
  static readonly TableQuery<Order> Orders = TableQuery<Order>.By(o => o.Id)
      .Sort("date", o => o.Date).Sort("amount", o => o.Amount)
      .SearchText("customer", o => o.Customer)
      .DefaultSort("date", descending: true);

  var page = await Orders.PageAsync(db.Orders, request, CountMode.Exact, (q, ct) => q.CountAsync(ct), ct);
  ```
- **SPA navigation for MVC and Razor Pages** (`ModulusThemeOptions.SpaNavigation`): links and forms in the shell load
  through htmx `hx-boost`. The server answers with only the page body, the sidebar and the header, so scripts and
  Alpine state stay loaded. It aborts stale navigations, shows a progress bar, prefetches on hover (`SpaPrefetch`),
  restores scroll positions and moves focus to the page. Page code runs through `Modulus.page(fn)` or
  `Modulus.onPage(fn)`. See [UPGRADING](docs/UPGRADING.md#spa-navigation-and-ishtmx) before turning it on.
- **Charts with no JS dependency**: `LineChart` (line/area, crosshair tooltip), `ColumnChart` (grouped/stacked),
  `BarChart`, `DonutChart`, `HeatmapChart`, `Sparkline`, `MiniBar`, plus business charts (treemap, pareto, waterfall,
  variance, bullet, gauge, funnel, combo and more). The geometry is shared by Blazor and MVC, hover tooltips are
  CSS-only, and each chart carries a screen-reader data table. `LegendToggle` / `legend-toggle` lets the legend hide
  series; `Links` / `links` makes columns and axis labels drill-down links.
- **Plugins**, each a Blazor component and a tag helper over one shared renderer in `Modulus.Ui.Abstractions.Plugins`:
  `AppEditor` / `app-editor` (WYSIWYG on Quill; the HTML is cleaned by `RichText.Sanitize(images: true)`),
  `AppMap` / `app-map` (Leaflet tile map with markers and popups), `AppVectorMap` / `app-vector-map` (jsVectorMap
  choropleth with hover values and a legend), `AppMediaPlayer` / `app-media-player` (video and audio with custom
  controls, poster and captions) and `AppSortable` / `app-sortable` (reorder a list, drag between lists, handles,
  keyboard moves; posts the order). `modulus-plugins.js` loads Quill, Leaflet and jsVectorMap only when a page uses
  them. `app-upload` also gains `action` (upload with per-file progress), `max-size`, `max-count` and
  `list-type="picture"`, and `app-date-picker` gains `picker="quarter"` / `"year"`; `app-range-picker presets` uses
  `DatePeriods`.
- **Ant Design 5 components that AntD Blazor 1.6.2 lacks**, in both flavours: `AppQRCode` / `app-qr-code` (a tested QR
  encoder, drawn as SVG, with icon and status overlays), `AppColorPicker` / `app-color-picker` (palette, hue and alpha
  sliders, hex/RGB input, presets, clear), `AppFloatButton` / `AppFloatButtonGroup` / `AppFloatBackTop` and
  `AppTour` / `app-tour` (spotlight mask, placement with flipping, keyboard).
- **Select with an "Add item" row**: `AppCreatableSelect` / `app-creatable-select`, single or multiple. The dropdown
  ends with a name, a tag colour and Add; the new option joins the list and is selected. Adding is checked by
  `GridSchema.AddOption` (unique, trimmed, 50 at most); `OnAdd` / `add-href` can save it first. The editable grid's
  Select, Multi-select and Tag editors use it, and the data table sample filters and bulk-labels employees with it.
  For real records, `OnAddNew` / `add-modal` swap the inline row for an "Add new" button that opens a create form
  (a `ModalForm`, or an `app-modal` whose save answers `Response.HxOptionAdded(list, option)`); the new record is
  added to the list and selected.
- **Business fields** for ERP-style forms, in both flavours (`Components/Fields` / `app-*`), all posting plain
  invariant values: `AppMoneyInput` (grouped amount, currency picker, the currency's decimals), `AppQuantityInput`
  (unit of measure with its precision), `AppDurationInput` (`1:30`, `1.5`, `90m` → minutes), `AppNumberRange`,
  `AppPhoneInput` (country picker + national number → `+1 202 555-0111`), `AppMaskedInput` (`99-9999999`),
  `AppOtpInput` (one box per character, paste and SMS autofill spread over the boxes), `AppLookup` (a many-to-one
  field with type-ahead and a "Search more…" dialog; local items or a server search), `AppStatusBar` (workflow
  stages with folded ones), `AppSignaturePad` (PNG data URL) and `AppRichText` (a small `contenteditable` editor
  whose HTML is cleaned by `RichText.Sanitize`). The parsing and checks live in `Modulus.Ui.Abstractions.Forms`
  (`NumberText`, `DurationText`, `Currency`, `UnitOfMeasure`, `InputMask`, `PhoneNumber`, `LookupSearch`,
  `RichText`, `SignatureImage`), so the server validates exactly what the browser accepts. A server lookup takes
  `GET {search-href}?q=text&max=n` and answers `[{ value, label, description, fields }]`.
- **Theming from one options object**: primary, success, warning, error and info colours plus border radius.
  The 10-step palette is generated with a C# port of `@ant-design/colors` (tested against the official palettes).
  Light/dark mode persists in a cookie, so the server renders the correct theme on the first byte. The brand colour
  also replaces Ant Design's blue in dark mode (`AntDarkBrand`, `antd-dark-brand.css`).
- **Localization**: list languages in `ModulusThemeOptions.Cultures` and the settings panel gets a language picker
  that sets the culture cookie. `app.UseModulusLocalization()` (MVC / Razor Pages) or `UseRequestLocalization` with
  the same cultures (Blazor) sets the request culture, which Ant Design's `LocaleProvider` and the tag helpers follow.
- **MVC / Razor Pages parity**: the same layout (`_ModulusLayout`), menu, header, theme toggle, drawer, confirm dialog
  and toast messages, driven by `Response.HxMessage()`, `HxCloseDrawer()` and `HxTrigger()`.
- **Full Ant Design markup for MVC**: 80+ tag helpers render the same DOM as Ant Design Blazor, so one stylesheet
  styles both flavours. Interactive pieces (tabs, dropdowns, collapse, modal, upload, rate) use small Alpine
  components with keyboard support. They need no build step.
- **Responsive shell**: below 992 px the navigation moves behind a menu button that slides the sidebar in as a drawer.
  Wide tables and segmented controls scroll inside their card instead of widening the page.
- **Three complete sample admin apps** (Blazor, MVC and Razor Pages) with the same pages and URLs:
  - **Dashboards**: analytics, CRM, eCommerce, ERP.
  - **Apps**: email, chat, calendar, kanban, invoices (list, preview, add, edit), users (list, grid, view), roles,
    permissions, customers CRUD, eCommerce products and orders.
  - **Pages**: profile, account settings, pricing, FAQ, help center, changelog; sign-in and sign-out (a real cookie
    sign-in against the sample users, password `modulus`), register, forgot/reset password, verify email, two-step
    verification; 404, 403, 500, maintenance and coming soon.
  - **Interface, forms and tables**: component gallery (every Ant Design category), cards, charts, icons (every Ant
    Design icon, search, copy the markup), typography, colours, form layouts, a validated multi-step wizard, a
    validation showcase, a business-fields form, a data table (search, filters, sorting, paging, column picker,
    density, selection and CSV export), the editable and nested grids, and a 100,000-row large data page.
  - **Extra**: 22 Tabler extra pages (gallery, search results, activity, tasks, logs, license, job listing, uptime,
    trial ended, cookie banner, widgets and more).
  - **Plugins**: advanced charts, colour picker, date picker, dropzone, sortable, media player, lightbox, map,
    full-size map, vector map, tour and WYSIWYG editor.

## Quick start — Blazor

```csharp
// Program.cs
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddModulusBlazor(theme =>
{
    theme.Title = "Contoso ERP";
    theme.PrimaryColor = "#1677ff";
});
builder.Services.AddTransient<IMenuContributor, AppMenuContributor>();
```

```razor
@* App.razor <head> *@
<ModulusHead />
@* before </body> *@
<script src="_content/AntDesign/js/ant-design-blazor.js"></script>

@* Routes.razor — once, after the Router *@
<AntContainer />

@* MainLayout.razor — the Tabler shell (or <ModulusLayout> for the Ant Design Pro BasicLayout) *@
<AppShell>@Body</AppShell>
```

```csharp
public sealed class AppMenuContributor : IMenuContributor
{
    public Task ConfigureMenuAsync(MenuConfigurationContext context)
    {
        context.Add(new MenuItemDefinition("dashboard", "Dashboard", "", "dashboard") { Order = 0, ExactMatch = true })
               .Add(new MenuItemDefinition("customers", "Customers", "customers", "team") { Order = 10, RequiredPolicy = "Customers.Read" });
        return Task.CompletedTask;
    }
}
```

A page:

```razor
<PageContainer Title="Customers">
    <ProTable TItem="Customer" HeaderTitle="Customer list" RowKey="c => c.Id" Request="LoadAsync">
        <Columns>
            <PropertyColumn Property="c => c.Name" Sortable />
            <PropertyColumn Property="c => c.Company" />
        </Columns>
    </ProTable>
</PageContainer>

@code {
    private Task<ProTableResponse<Customer>> LoadAsync(ProTableRequest request) =>
        Task.FromResult(_customers.SortBy(request, "Name", c => c.Name).ToPage(request));
}
```

See `samples/Modulus.AdminTemplate.Blazor/Components/Pages/Apps/Customers/CustomerList.razor` for the full pattern:
filter, table, batch delete and drawer form.

## Quick start — MVC / Razor Pages

```csharp
builder.Services.AddControllersWithViews();   // or AddRazorPages()
builder.Services.AddModulusMvc(theme => theme.Title = "Contoso ERP");
builder.Services.AddTransient<IMenuContributor, AppMenuContributor>();
```

```cshtml
@* _ViewImports.cshtml *@
@addTagHelper *, Modulus.AspNetCore.Mvc

@* _ViewStart.cshtml *@
@{ Layout = "_ModulusLayout"; }
```

```cshtml
<app-page-container title="Customers">
  <app-card title="Customer list">
    <app-card-extra>
      <app-button type="primary" icon="plus" hx-get="/customers/new" hx-target="#modulus-drawer-body"
                  x-on:click="$store.drawer.show('New customer')">New customer</app-button>
    </app-card-extra>
    <div id="table" hx-get="/customers/table" hx-trigger="load, customers:refresh from:body"></div>
  </app-card>
</app-page-container>
```

```csharp
// Controller, after a successful save:
Response.HxMessage(MessageType.Success, $"Created {customer.Name}");
Response.HxCloseDrawer();
Response.HxTrigger("customers:refresh");
return NoContent();
```

Tag helpers. `/ui/components` in the MVC and Razor Pages samples shows each one; the Blazor sample shows its twin at the same URL:

| Category | Tag helpers |
|---|---|
| General | `app-button` (`loading`), `app-icon`, `app-title`, `app-text`, `app-paragraph`, `app-link`, `app-float-button`, `app-float-button-group` (click/hover trigger), `app-float-back-top` |
| Layout | `app-divider`, `app-space`, `app-row` / `app-col` (24-column grid, gutters, xs–xxl), `app-flex`, `app-layout` / `app-layout-header` / `app-layout-sider` / `app-layout-content` / `app-layout-footer`, `app-splitter` / `app-splitter-panel` (drag and arrow keys) |
| Navigation | `app-breadcrumb`, `app-tabs` / `app-tab`, `app-steps` / `app-step`, `app-dropdown` / `app-dropdown-item`, `app-dropdown-button`, `app-menu` / `app-sub-menu` / `app-menu-item` / `app-menu-divider` (horizontal, vertical, inline), `app-pagination` (htmx or plain links), `app-anchor` / `app-anchor-link` (scroll-spy), `app-affix`, `app-back-top` |
| Data entry | `app-form-item` (`asp-for` + validation), `app-input` (affixes, addons, clear, password, datalist AutoComplete), `app-input-search`, `app-textarea` (count), `app-input-number`, `app-select`, `app-date-picker` (date/week/month/time), `app-range-picker`, `app-checkbox`, `app-checkbox-group`, `app-radio-group`, `app-switch`, `app-slider`, `app-rate`, `app-segmented`, `app-upload`, `app-cascader`, `app-tree-select`, `app-transfer`, `app-auto-complete`, `app-mentions`, `app-input-group` (compact), `app-color-picker` (hex/rgb, alpha, presets, clear), `app-creatable-select` ("Add item" row with tag colours; `add-href` saves new options) |
| Data display | `app-card` (`app-card-cover`, `app-card-meta`, `app-card-action`), `app-comment` / `comment-action`, `app-countdown`, `app-image-group` (shared preview), `app-avatar` / `app-avatar-group`, `app-badge` / `app-badge-ribbon`, `app-tag`, `app-statistic`, `app-descriptions`, `app-table` (`hidden-columns`, `density`, `scroll-x`, `table-footer`), `app-table-columns` (column picker), `app-table-settings` (reload, density), `app-list`, `app-timeline`, `app-collapse` / `app-panel`, `app-image`, `app-tooltip`, `app-popover`, `app-empty`, `app-tree` (checkable), `app-calendar`, `app-carousel` / `carousel-slide`, `app-qr-code` (icon, status), `app-tour` / `tour-step` |
| Feedback | `app-alert`, `app-progress` (line/circle/dashboard), `app-result`, `app-skeleton`, `app-spin`, `app-modal`, `app-drawer` (right/left/top/bottom), `app-watermark`; plus `Response.HxMessage`, `HxNotify`, `HxCloseModal` and `hx-confirm` |
| Pro | `app-page-container`, `app-page-header`, `app-statistic-card`, `app-trend`, `app-editable-grid` (`grid-toolbar`, `grid-title-extra`, `grid-filters`, `grid-batch`, `grid-summary`, `row-draggable`; read posts with `EditableGridForm.Read` / `Apply`), `app-nested-grid` (one level per request; state and saves through `NestedGridForm`) |
| Large data | `app-load-more` (infinite scroll), `app-virtual-table` (virtual scrolling, `modulus-virtual.js`) |
| Charts | `app-line-chart`, `app-column-chart`, `app-bar-chart`, `app-donut-chart`, `app-heatmap`, `app-sparkline`, `app-mini-bar` and the business charts; `legend-toggle`, `links` |
| Plugins | `app-editor` (Quill), `app-map` (Leaflet), `app-vector-map` (jsVectorMap), `app-media-player`, `app-sortable` / `app-sortable-item`, `app-lightbox` |
| Icons | `app-icon` (Ant Design), `app-ti` (an Ant Design outline icon by name, drawn inline like the shell's; `ri:` names for the few Remix Icon fallbacks) |

Controls bind with `asp-for` (name, id, value and validation state) or a plain `name`. Any other attributes you add,
such as `placeholder`, `hx-*` or `x-*`, go to the real `<input>`. Forms stay plain HTML and post normally.

## Theme options

Bind the options from configuration or set them in code:

```json
"Modulus": { "Theme": { "Title": "Modulus Admin", "PrimaryColor": "#066fd1", "BorderRadius": 6,
                        "DefaultMode": "Light", "Layout": "Horizontal", "MenuScope": "App",
                        "SpaNavigation": true, "Cultures": [ "en-US", "de-DE", "fr-FR" ] } }
```

| Option | Default | |
|---|---|---|
| `Title`, `LogoUrl` | "Modulus Admin", built-in mark | Header brand and document title suffix |
| `PrimaryColor`, `SuccessColor`, `WarningColor`, `ErrorColor`, `InfoColor` | Tabler palette (`#066fd1`, `#2fb344`, `#f59f00`, `#d63939`; info follows primary) | 10-step palettes generated server-side |
| `BorderRadius` | 6 | Controls; cards and overlays use radius + 2 |
| `DefaultMode` | `Light` | `Light`, `Dark` or `System`; the user's choice is stored in the `modulus-theme` cookie |
| `NavTheme`, `HeaderTheme` | `Light`, `Light` | Navigation and header row colour |
| `Layout` | `Horizontal` | `Horizontal`, `Vertical` or `Condensed` |
| `ContentWidth`, `Header` | `Boxed`, `Fixed` | Width `Boxed` / `Fluid` / `Framed`; header `Fixed` / `Sticky` / `Static` / `Floating` |
| `SidebarPosition`, `SidebarStyle`, `SidebarFold` | `Left`, `Default`, `Full` | Vertical layout only |
| `Direction` | `Ltr` | `Rtl` sets `dir="rtl"` on `<html>` |
| `MenuScope` | `All` | `App` shows only the current app's menu (Odoo) |
| `Density`, `Font`, `Base` | `Compact`, `Sans`, `Gray` | Spacing and type size, font family, neutral palette |
| `ColorPresets`, `ShowCustomizer` | eight colours, `true` | The theme settings panel |
| `Cultures` | none | Two or more add a language picker (see Localization above) |
| `SpaNavigation`, `SpaPrefetch` | `false`, `true` | MVC / Razor Pages SPA navigation and hover prefetch |
| `SiderWidth`, `HeaderHeight` | 240, 56 | Comfortable density; compact uses 224 and 48 |
| `Copyright`, `Version` | "{year} Modulus", none | Footer |

Users change most of these in the settings panel; their choices are kept in cookies, so the server renders them on
the first paint.

## Projects

| Project | Purpose |
|---|---|
| `src/Modulus.Ui.Abstractions` | Flavor-agnostic: theme options and palette generation, menu contributors, the shell renderer, `ProTableRequest/Response`, `TableQuery`, `Paging`, chart geometry, form rules, plugin renderers |
| `src/Modulus.Ui.Assets` | CSS (`modulus.css`, `modulus-shell.css`, `modulus-pages.css`, `antd-dark-brand.css`), scripts (`modulus-theme.js`, `modulus-shell.js`, `modulus-mvc.js`, `modulus-widgets.js`, `modulus-plugins.js`, `modulus-spa.js`, `modulus-virtual.js`), vendored htmx 2.0.11, Alpine.js 3.17.4, Leaflet 1.9.4, Quill 2.0.3 and jsVectorMap |
| `src/Modulus.Blazor` | `AddModulusBlazor`, `App*` aliases, ProLayout shell, Pro components, charts (no server framework dependency, so it is WASM-compatible) |
| `src/Modulus.AspNetCore.Mvc` | `AddModulusMvc`, tag helpers, `_ModulusLayout` and partials, htmx response helpers, `AntIcons` |
| `samples/Modulus.AdminTemplate.*` | The Blazor, MVC and Razor Pages admin templates |
| `samples/Modulus.Samples.Shared` | Models, in-memory stores, menu and page data shared by all three samples |
| `samples/Modulus.Samples.Web` | Page bodies (`Views/Shared/Ui/**`) shared by MVC and Razor Pages: controllers and page models only bind input and pick the view |
| `tests/*` | bUnit component tests, palette/menu/table/chart unit tests, tag helper and icon tests |
| `tools/antd-drift` | Checks the manifest and aliases against the pinned AntDesign package (see [UPGRADING](docs/UPGRADING.md)) |

## Build, test, run

```bash
dotnet build Modulus.slnx
dotnet test --solution Modulus.slnx
dotnet run --project tools/antd-drift

dotnet run --project samples/Modulus.AdminTemplate.Blazor       # http://localhost:5100
dotnet run --project samples/Modulus.AdminTemplate.Mvc          # http://localhost:5101
dotnet run --project samples/Modulus.AdminTemplate.RazorPages   # http://localhost:5102
```

Run the samples in the **Development** environment (the default launch profile does this) or publish them;
`dotnet run` in Production does not serve library static assets. Sign in with any sample user's email and the
password `modulus`; set `Samples:RequireSignIn` to `true` in `appsettings.json` to require it on every page.

### NuGet packages

The four libraries in `src/` share one version (`VersionPrefix` in `src/Directory.Build.props`) and ship with
SourceLink, symbols and this README:

```bash
dotnet pack Modulus.slnx -c Release -o artifacts/packages
```

CI packs `-ci.<run>` prereleases as the `packages` artifact; a local `dotnet pack` without a suffix packs the release version.

## Known limitations

- **Ant Design Blazor 1.6 ships antd v4 styling.** Modulus adds a v5-style refresh layer (6 px radius, softer shadows,
  v5 menu and table styling) over it. The result looks like modern Ant Design Pro, but it is not pixel-identical to antd v5.
- **Brand colour in dark mode is generated.** AntDesign's dark stylesheet hard-codes antd blue, so
  `antd-dark-brand.css` repeats the declarations that use it with the brand's dark palette. It is generated from the
  pinned AntDesign version; `tools/antd-drift` fails when it no longer matches (see [UPGRADING](docs/UPGRADING.md)).
- **Icons in MVC** reuse Ant Design Blazor's icon store through reflection, because the API is internal. A unit test fails
  if an AntDesign upgrade moves it.
- **Interactive MVC widgets need Alpine.** Cascader, TreeSelect, Transfer, Tree, Calendar, Carousel, Anchor, Mentions,
  Splitter, ColorPicker, Tour and the rest run on small Alpine components (`modulus-widgets.js`); form widgets post
  through hidden inputs, so forms still submit without script. The manifest lists the MVC equivalent for every component.
- **Business fields are Modulus components** with no AntD Blazor counterpart. The `AppRichText` field covers
  formatting, lists and links only (use `AppEditor` for images), and the HTML of both must still be sanitized on the
  server (`RichText.Sanitize`). Phone numbers are checked for shape (`+`, calling code, 8–15 digits), not against each
  country's numbering plan.
- **ColorPicker, FloatButton, QRCode and Tour are Modulus components**, not AntD Blazor ones (1.6.2 has none). They emit
  the antd v5 DOM, styled by `modulus.css`, and may differ in detail from antd v5.
- **Native pickers in MVC.** `app-date-picker` and `app-range-picker` use the browser's date/time inputs inside antd chrome,
  so the calendar popup follows the browser, not antd.
- **Plugin libraries are third-party.** The editor, maps and vector map wrap Quill, Leaflet and jsVectorMap, loaded from
  `Modulus.Ui.Assets` on first use. Tile maps load their tiles from `tile.openstreetmap.org`, whose usage policy does not
  cover heavy production traffic, so they need network access and, for real use, your own tile server.
- **Localization covers the framework**, not the sample texts: Ant Design's built-in strings, dates and numbers follow the
  request culture, but the sample pages stay in English.
- **Authorization** is wired for menus (`RequiredPolicy`). The samples sign in with a cookie against in-memory sample
  users; there is no user store, registration or password reset behind the auth pages.

## Documentation

- [docs/DESIGN.md](docs/DESIGN.md): design decisions and the analysis of other Ant Design-based libraries they come from
- [docs/UPGRADING.md](docs/UPGRADING.md): upgrading AntDesign, htmx, Alpine.js and the plugin libraries safely, and
  turning on SPA navigation
- [docs/PLAN.md](docs/PLAN.md): the plan, page inventory, verification results and change log
- `manifest/antd-coverage.json`: component-by-component coverage, verified in CI
