# Modulus UI — Full Plan

Status: approved 2026-09-24. Phase 0 done (solution + 4 projects build clean on net10.0).

## 1. Goal
Modulus is a modular application framework (ABP-style). This repo delivers its **UI layer**: a professional, ERP-grade **admin panel template** built on the **full Ant Design catalog**, in three flavors — **Blazor**, **MVC**, **Razor Pages** — that stays maintainable as Ant Design updates.

Decisions (fixed):
- Wrap `ant-design-blazor` (NuGet `AntDesign`, pinned 1.6.2, supports net10.0) for Blazor; we own `Pro*` and gap components.
- .NET 10, Blazor Server / WASM / Auto.
- MVC + Razor Pages interactivity = **htmx + Alpine.js** (no other JS framework).
- **Full parity**: every antd component in all three flavors; no "static-only" tiers.
- Reference upstream: `antd` on npm (latest at planning: 6.6.5). Note: `AntDesign` 1.6.x tracks an earlier antd major, so the gap/drift manifest is expected to show differences from day one; it is the tool for closing them.

## 2. Modulus (ABP-style) conventions
- Namespaces `Modulus.*`; packages `Modulus.Blazor`, `Modulus.AspNetCore.Mvc`, `Modulus.Ui.Abstractions`, `Modulus.Ui.Assets`; per-feature modules `Modulus.<Module>.Blazor | .Mvc | .RazorPages`.
- `ModulusBlazorModule : ModulusModule` with `[DependsOn]`; DI entry `services.AddModulusBlazor()` / `AddModulusMvcUi()`. The real `ModulusModule` base type lives in the core framework (not in this repo) — confirm namespace; until then a minimal `IModulusModule` stub is used.
- `Core/Navigation`: `MenuDefinition` + `IMenuContributor` (like ABP); `ModuleRegistry` discovers modules' menus, routable assemblies, permissions. Non-UI classes are `.cs`.
- `Core/Auth`: permission-based (`PermissionGate`, policy names from Modulus permission definitions).
- `Core/Localization`: `IStringLocalizer`; `LocaleProvider` also maps to antd locale.
- Theme is swappable (theme-module idea): modules depend on `ProLayout`/`PageContainer`, never layout internals.
- Generated module projects reference only the UI packages and use `Components/Pro/*`, `AppTable`, etc.; they never rebuild primitives.

## 3. Packages and repo layout
| Package | Serves | Content |
|---|---|---|
| `Modulus.Ui.Abstractions` | all | UI-agnostic models: menu, permissions, ProTable/ProForm schemas, theme settings |
| `Modulus.Ui.Assets` | all | `modulus-theme.css` (antd tokens via CSS variables), antd CSS, icons, vendored `htmx` + `alpinejs`, `modulus-alpine.js` |
| `Modulus.Blazor` | Blazor | `Core/` + `Components/{General,Layout,Navigation,DataEntry,DataDisplay,Feedback,Pro}` (`AppXxx` wrappers, `ProXxx` composites) |
| `Modulus.AspNetCore.Mvc` | MVC + Razor Pages | `_Layout` + partials, TagHelpers, ViewComponents (`.cs`), `IsHtmx()` helpers |

```
ui_template/
├── Modulus.slnx, Directory.Build.props, Directory.Packages.props  # AntDesign pinned ONLY here
├── src/{Modulus.Ui.Abstractions, Modulus.Ui.Assets, Modulus.Blazor, Modulus.AspNetCore.Mvc}
├── samples/Modulus.AdminTemplate.{Blazor,Mvc,RazorPages}   # mock ERP admin
├── tools/{antd-manifest, gen-wrappers}
├── manifest/{antd-coverage.json, api-snapshot.json}
├── tests/{Modulus.Blazor.Tests, Modulus.Mvc.Tests, Modulus.Coverage.Tests}
├── docs/{PLAN.md, UPGRADING.md}
└── .github/workflows/{ci.yml, antd-drift.yml}
```

## 4. Component inventory (100% of antd)
- **General**: Button, FloatButton, Icon, Typography
- **Layout**: Divider, Flex, Grid, Layout, Space (+Compact), Splitter
- **Navigation**: Anchor, Breadcrumb, Dropdown, Menu, Pagination, Steps, Tabs, Affix
- **Data Entry**: AutoComplete, Cascader, Checkbox, ColorPicker, DatePicker, Form, Input (+TextArea/Search/Password/OTP), InputNumber, Mentions, Radio, Rate, Select, Slider, Switch, TimePicker, Transfer, TreeSelect, Upload
- **Data Display**: Avatar, Badge, Calendar, Card, Carousel, Collapse, Descriptions, Empty, Image, List, Popover, QRCode, Segmented, Statistic, Table, Tag, Timeline, Tooltip, Tour, Tree, Skeleton (Comment only if still in target antd; else `n/a`)
- **Feedback**: Alert, Drawer, Message, Modal, Notification, Popconfirm, Progress, Result, Spin, Watermark
- **Other**: ConfigProvider, App context, breakpoints, theme algorithms (default/dark/compact)
- **Pro**: ProTable, ProForm (+ field set, StepsForm, ModalForm, DrawerForm, QueryFilter, LightFilter, LoginForm), ProCard, ProDescriptions, ProList, StatisticCard, ProLayout, PageContainer, ProSkeleton, ProField, EditableProTable, FooterToolbar, SettingDrawer

Seeded into `manifest/antd-coverage.json` with API doc URL per component.

## 5. Blazor wrapper pattern
- `AppXxx` renders the matching `AntDesign` component; `[Parameter(CaptureUnmatchedValues = true)]` + forwarded parameters so new upstream params need no wrapper change.
- Defaults from one `ModulusDefaults` (size, shape, locale) via DI; theme via CSS variables only, never patching antd CSS.
- `[AntdComponent("Button", Since="…")]` attribute on every wrapper (read by the manifest tool).
- `ProXxx` use only public `AppXxx`, never `AntDesign` types → one upgrade seam.
- Gaps (likely Splitter, Tour, QRCode, Watermark, ColorPicker, FloatButton, Input.OTP, newer props): native Razor + token CSS, `Origin=Native` in manifest, swapped for upstream when it lands.

## 6. MVC / Razor Pages approach (htmx + Alpine.js)
- TagHelpers emit antd-class markup (shared CSS applies): `<app-button>`, `<app-card>`, `<app-table>`, `<pro-table>`…
- **Alpine.js**: client UI state (Modal, Drawer, Dropdown, Tooltip, Popover, Tabs, Collapse, Select, DatePicker/TimePicker/Calendar, Cascader, TreeSelect, Transfer, Tree, Slider, Rate, Tour, Carousel, Image preview, Message/Notification, menu collapse, theme). Reusable `Alpine.data('appModal', …)` in `modulus-alpine.js`; `x-transition` for antd-like motion.
- **htmx**: server round-trips — ProTable paging/sort/filter (partial swap), ProForm/ModalForm/DrawerForm submit with re-rendered validation, lazy Select/AutoComplete/Cascader, infinite List, Popconfirm delete, Upload progress.
- Bridge: `HX-Trigger` events (`modulus:message`, `modulus:modal-close`, `modulus:refresh-table`), `Alpine.initTree` after `htmx:afterSwap`, antiforgery in `htmx:configRequest`, `IsHtmx()` so one page/action returns full page or partial.
- Vendored pinned `htmx` and `alpinejs` (no CDN); decide `@alpinejs/csp` build for strict CSP in Phase 6.
- Shared schemas (`Modulus.Ui.Abstractions`) drive `ProTable`/`ProForm` identically in all flavors.

## 7. Admin panel template (ERP-grade)
- **Shell**: collapsible multi-level sider (permission-filtered, search, badges), header (global search, notifications, messages, language, dark mode, fullscreen, user menu, tenant/branch/fiscal-year switcher), breadcrumbs, multi-tab switcher, footer, `SettingDrawer` (theme color, side/top/mix layout, fixed header/sider, compact, content width, RTL), responsive mobile drawer.
- **Template pages**: Login/Register/Forgot/2FA; Dashboard (stats, charts, timeline, to-dos); List (ProTable: search, filters, column settings, density, export, bulk + row actions); Detail (ProDescriptions, tabs, audit history); Form (ProForm, wizard); Master–detail (editable lines, totals); Profile/Account; Users/Roles/Permissions; Audit log; Settings; Notification center; 403/404/500.
- **ERP blocks**: editable grid (inline edit, keyboard nav), tree tables (chart of accounts, org units), document status flow, approval workflow view, attachments, print/export (CSV/Excel/PDF), currency/date/number localization, command palette/shortcuts.
- Sample hosts show a mock ERP (Customers, Sales Orders, Inventory, Accounting) with fake data in all three flavors.

## 8. Keeping up with Ant Design (and htmx/Alpine) updates
1. Single version pin: `Directory.Packages.props` (Renovate/Dependabot PRs).
2. `manifest/antd-coverage.json`: antd component → AntDesign type → wrapper → status per flavor (`done | in-progress | gap | n/a`); release fails if any flavor has a `gap`.
3. `tools/antd-manifest`: reflects over the AntDesign assembly, diffs types/`[Parameter]`s against `api-snapshot.json`; queries npm for latest `antd`, `htmx.org`, `alpinejs` and diffs component list/versions; writes a markdown report; non-zero exit on unmapped new components or removed APIs in use.
4. CI: `ci.yml` (build, bUnit, coverage); `antd-drift.yml` weekly + on dependency PRs → issue "antd drift report".
5. Wrapper design (unmatched-values capture, one defaults object) absorbs most additions without changes.
6. Token test: `modulus-theme.css` only references existing antd CSS variables; TagHelper class names checked against shipped antd CSS.
7. `docs/UPGRADING.md` checklist.

## 9. Phases
0. Repo, plan, solution, project scaffolds ✅
1. Foundation: theme (`ThemeProvider`, tokens, dark mode), interop, DI/module entry, assets package
2. Wrapper pattern + manifest seeded + audit gap list; build order General → Layout → Navigation → Feedback → DataEntry → DataDisplay
3. Gap components (native)
4. Pro composites
5. Core shell + template pages (Blazor) — **first vertical slice**: shell + Login + Dashboard + one list/detail/form flow
6. MVC/Razor Pages: 6a static TagHelpers → 6b Alpine interactive components → 6c htmx-driven Pro*; sample hosts
7. Drift tool, CI, UPGRADING.md; broaden to full parity and close all gaps

## 10. Verification
- `dotnet build` / `dotnet test` clean on net10.0.
- bUnit test per `AppXxx` (renders, forwards params, defaults); MVC TagHelper output tests; Alpine/htmx behavior via Playwright on the sample hosts.
- Coverage test: manifest ↔ code agree; no `gap` at release.
- Visual check of gallery + admin template in light/dark/compact/RTL.
- Simulated upgrade on a branch (bump `AntDesign`) must produce a drift report.

## 11. Open items
- Real `ModulusModule` base type/namespace from the core framework.
- Charts library for Dashboard (Ant Design Charts equivalent for .NET: e.g. ApexCharts.Blazor / Chart.js via interop) — decide in Phase 5.
- CSP strategy (`@alpinejs/csp`).
