# Modulus.Ui.Templates

Template content package for the Modulus UI Kit. Contains Scriban templates and manifests for scaffolding admin pages and layouts across all rendering engines (MVC, Razor Pages, Blazor, Fluid).

## Purpose

This package ships with the Modulus CLI and provides:
- **ui-kit.json**: manifest describing which templates are available, what packages they require, and which engines they support.
- **Scriban templates**: page and component templates that the CLI renders at scaffold time, one per engine.

The templates are embedded in the package so the CLI always has a version-matched set, preventing the UI drift that embedded views can have.

## Features

- **shell**: layout with header, navbar/sidebar, footer
- **crud**: list, form, and detail pages driven by entity metadata
- **auth**: login and register pages
- **errors**: 404, 403, 500 error pages
- **state-store**: UI state persistence for saved views

## Engines

- `mvc`: ASP.NET Core MVC with tag helpers
- `razor-pages`: ASP.NET Core Razor Pages
- `blazor`: Blazor Interactive Server with Ant Design components
- `fluid`: Liquid templates with Modulus Fluid engine
- `none`: API-only (no UI scaffolding)

## Usage

The Modulus CLI resolves this package by version and renders templates via `TemplateEngine` (Scriban).

```bash
modulus app MyApp --ui-engine mvc
modulus generate-crud Customer --ui-engine mvc
```

## Template Rendering

Templates use Scriban syntax:
- `{{ app_name }}`, `{{ module_name }}`, `{{ entity_name }}` (snake_case from the CLI)
- `{{ include 'shared/form' }}` for reusable snippets
- Conditional blocks: `{{ if enable_feature }}...{{ end }}`
