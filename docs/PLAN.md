# Plan and progress

The plan for the Modulus UI admin template and where each piece stands. Update the status tables in the same commit
as the work they describe.

Last updated: 2026-09-26 · CI green on `main` at `bf883c9`

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
| Data table (search, filters, sort, paging, columns, selection, CSV) | `/tables/advanced` | ✅ | ✅ | ✅ |

## 5. Verification

| Check | How | Last result |
|---|---|---|
| Unit and component tests | `dotnet test --solution Modulus.slnx` | ✅ 81 passed |
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
| 2026-09-26 | MVC widgets: `app-cascader`, `app-tree-select`, `app-transfer`, `app-tree`, `app-calendar`, `app-carousel` and `app-anchor` emit the Ant Design DOM, run on small Alpine components in the new `modulus-widgets.js` and post through hidden inputs. They take their options from `TreeOption` (Modulus.Ui.Abstractions). The gallery gains Anchor, Cascader and TreeSelect, Transfer, Tree, Calendar and Carousel cards in all three samples. The Transfer stacks on phones. The manifest lists the new `mvc` entries. |
| 2026-09-26 | Filters in the URL: filter, sort and page state survives reload and back on the data table, Customers, Users, Products, Orders and Invoices (MVC and Razor Pages). GET filter forms now leave empty fields out of the pushed URL. The Razor Pages sample gets its missing `favicon.ico`. |
| 2026-09-26 | MVC library: `app-table-columns` (column picker whose links carry the hidden set in the URL) and `app-table-settings` (reload + density with ProTable's Comfortable / Standard / Compact); `app-table` gains `hidden-columns` (drops `data-col` cells), `density`, `scroll-x`, `layout` and a `table-footer` slot. The data table and every list page use them; `_TableSettings.cshtml` is gone. `TableColumns` moves the hidden-set logic into `Modulus.Ui.Abstractions`. |
| 2026-09-25 | Fix: MVC and Razor Pages pages could not be scrolled. The layout's shared drawer always carried `ant-drawer-open`, which triggers AntD's `body { overflow: hidden }` scroll lock; the lock now applies only while the drawer is open. |
| 2026-09-25 | Library rebuilt on AntDesign 1.6.2 aliases; Tabler shell; three samples with all pages; docs, tests and CI; first push to GitHub (`83c8ca2`). |
