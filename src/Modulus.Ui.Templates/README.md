# Modulus.Ui.Templates

Template content package for the Modulus UI Kit. Contains Scriban templates and manifests for scaffolding admin pages and layouts across all rendering engines (MVC, Razor Pages, Blazor, Fluid).

## Purpose

This package ships with the Modulus CLI and provides:
- **ui-kit.json**: manifest describing which templates are available, what packages they require, and which engines they support.
- **Scriban templates**: page and component templates that the CLI renders at scaffold time, one per engine.

The templates are embedded in the package so the CLI always has a version-matched set, preventing the UI drift that embedded views can have.

## Features

- **shell**: layout package set (header, navbar/sidebar, footer come from `Modulus.AspNetCore.Mvc` and `Modulus.Blazor`)
- **crud**: list, form, and detail pages driven by entity metadata (razor-pages, blazor)
- **admin**: working admin pages for mvc, razor-pages and blazor (see below)

## Engines

- `mvc`: ASP.NET Core MVC with tag helpers
- `razor-pages`: ASP.NET Core Razor Pages
- `blazor`: Blazor Interactive Server with Ant Design components
- `fluid`: Liquid templates with Modulus Fluid engine
- `none`: API-only (no UI scaffolding)

## Components

`modulus ui add-component` copies a component from `templates/<engine>/`. The catalog is `cli/Services/UiComponents.cs` in the `modulus` repo, and each entry lists the engines it has a template for:

- **mvc, razor-pages and blazor:** the 85 general components (inputs, data display, feedback, navigation, layout), plus `barcode`, `image-cropper`, `pdf-button` and `saved-views`, which have tag helpers.
- **blazor only:** 42 components with no MVC or Razor Pages tag helper: the chart components (`chart-bar`, `chart-waterfall` and the rest of the `chart-*` set), the form fields (`lookup`, `masked-input`, `phone-input`, `signature-pad`, `status-bar` and the others), and the grid, print and navigation components.

`add-component --engine mvc` refuses a Blazor-only component by name and skips it under `--all`. Fluid is not a scaffold target, so it has no component set in this README.

## Usage

The Modulus CLI resolves this package by version and renders templates via `TemplateEngine` (Scriban).

```bash
modulus app MyApp --ui-engine mvc
modulus ui create-crud Customer --engine razor-pages
```

`ui create-crud` writes the CRUD pages for an entity over its API. It supports `razor-pages` and `blazor` only; the CLI refuses `mvc`. `generate-crud` reads the engine from `.modulus.json` and has no `--ui-engine` flag.

## Admin dashboard starter (`admin` feature)

The `admin` feature in `ui-kit.json` lists working, editable pages for mvc, razor-pages and blazor: users, roles, tenants (list, settings,
members), notifications and preferences, audit log, settings, dashboard, analytics, reports, system health and the account pages. They are a
scaffold: the files become yours and you improve them.

- **Data seam.** Every page reads and writes through the interfaces in `Modulus.Ui.Abstractions.Admin` (`IUserDirectory`, `IRoleDirectory`,
  `ITenantDirectory`, `INotificationCenter`, `IAuditTrail`, `IAdminSettingsStore`, `IMetricsSource`). `AddModulusMvc` / `AddModulusBlazor` register an
  in-memory `SampleAdminData` with `TryAdd`, and pages show a "Sample data" banner until you register your own implementation (for example over
  the framework's `INotificationStore`, `IAuditLogStore` and `ISettingManager`).
- **Navigation.** `services.AddModulusAdminUi(o => o.Pages = ...)` adds the menu in the framework's own shape (apps Overview, Settings, System,
  My account, with sections inside). `AdminPages` limits it to the pages you scaffolded; `AdministrationPolicy` guards Settings and System.
- **MVC.** Render `admin-controller.cs.sbn` with `namespace`; the views go to `Views/Admin/<Name>.cshtml` as listed in the manifest.
- **Confirmations.** Destructive forms use `data-confirm`, handled globally by `modulus-shell.js` (CSP-safe, no inline script).

## Template Rendering

Templates use Scriban syntax:
- `{{ app_name }}`, `{{ module_name }}`, `{{ entity_name }}` (snake_case from the CLI)
- `{{ include 'shared/form' }}` for reusable snippets
- Conditional blocks: `{{ if enable_feature }}...{{ end }}`
