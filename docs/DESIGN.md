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
Modulus.Ui.Abstractions   theme options + palette, menus, ProTable request/response, chart geometry  (no UI deps)
        │
Modulus.Ui.Assets         modulus.css (tokens, v5 refresh, Pro primitives), modulus-theme.js, modulus-mvc.js,
        │                 htmx 2.0.11, Alpine 3.17.4; brings AntDesign + ProLayout static CSS to every flavor
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
- The v5 refresh (in `modulus.css`) touches only visual properties: radius, shadow, menu item shape, table header weight
  and control heights. Layout and behaviour stay Ant Design's.

### Blazor

- `AddModulusBlazor` registers AntDesign, maps options into `ProSettings` and registers the menu pipeline.
- `AppShell` is the default shell: the Tabler layout (header, horizontal navbar or vertical sidebar, footer, settings
  panel). Its markup comes from `ShellRenderer` in `Modulus.Ui.Abstractions`, the same code MVC's `_ModulusLayout` calls,
  so the two flavors cannot drift apart; `modulus-shell.js` adds the behaviour in both.
- `ModulusLayout` is the alternative Ant Design Pro shell. It wraps `BasicLayout`, converts `MenuItemDefinition` →
  `MenuDataItem`, hides items the user isn't authorized for and renders the footer via `FooterView`.
- Pro components are plain Razor over Ant Design components. They never touch AntDesign internals.

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

### Samples

- One page, three hosts. MVC and Razor Pages render the same page bodies from `Modulus.Samples.Web/Views/Shared/Ui/**`:
  a controller action or a page model binds the input, validates it and picks the view; the view never knows which
  host it runs in. URLs are the same in all three samples, so a page can be compared across them by changing the port.
- Data, validation rules and page logic that Blazor also needs (stores, `WizardInput.Validate`, `EmployeeDirectory`,
  chart series) live in `Modulus.Samples.Shared`, because the Blazor sample does not reference `Samples.Web`.
- Blazor pages reproduce the MVC markup where a component would differ (for example `FieldItem`, which emits the same
  form-item DOM as `app-form-item`), so screenshots of the same URL match.

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
| Icons | Tabler icons (`<Ti Name="users" />` / `<app-ti name="users" />`, from one SVG sprite) for page content and navigation; Ant Design icons inside Ant Design controls |
| Menus | Modules add items through `IMenuContributor`; `Order` 0–99 for app sections, 90+ for settings |

## 4. Roadmap (not built yet)

- MVC equivalents for the Blazor-only widgets (Cascader, TreeSelect, Transfer, Tree, Calendar, Carousel, Anchor),
  probably as Blazor components hosted in the MVC page rather than re-implemented in Alpine.
- Localization: bind `LocaleProvider` to the request culture.
- Brand colour in dark mode once AntDesign ships variable-based dark CSS, or a generated dark override.
- Replace `AntIcons` reflection if AntDesign makes its icon store public.
