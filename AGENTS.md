# AGENTS.md

Guidance for AI agents and people working on the Modulus UI framework (this repository). The framework itself, its
core packages and the `modulus` CLI live in the `modulus` repository, see its `AGENTS.md`.

## What is here

- `src/Modulus.Ui.Abstractions` — the flavour-neutral models: theme, menus, `ProTableRequest`, `TableQuery`, the palette, forms.
- `src/Modulus.Blazor` — the Blazor components (`App*` aliases of Ant Design Blazor, the Pro components, charts, plugins).
- `src/Modulus.AspNetCore.Mvc` — the MVC and Razor Pages tag helpers, `_ModulusLayout`, the htmx response helpers.
- `src/Modulus.Ui.Assets` — the stylesheets, scripts and vendored libraries (htmx, Alpine.js, plugins).
- `src/Modulus.AspNetCore.Hosting`, `src/Modulus.AspNetCore.Fluid`, `src/Modulus.Documents` — hosting defaults, Liquid, documents.
- `src/Modulus.Ui.Templates` — the `ui-kit.json` manifest and the templates the CLI scaffolds from.
- `samples/` — three admin apps (Blazor, MVC, Razor Pages) and a Liquid sample, on ports 5100 to 5103.
- `docs/DOCUMENTATION.md` — the single documentation file (CLI commands, upgrading, design, plan, roadmaps).
- `website/` — the Docusaurus documentation site. Its pages are in `website/docs/`.
- `docs/examples/` — the ContosoAdmin and ContosoMvc projects that the tutorials show. Keep them building.

## Common commands

| Task | Command |
|------|---------|
| Build | `dotnet build Modulus.slnx` |
| Test | `dotnet test --solution Modulus.slnx` |
| Antd drift check | `dotnet run --project tools/antd-drift` |
| Run a sample | `dotnet run --project samples/Modulus.AdminTemplate.Blazor` (or `.Mvc`, `.RazorPages`, `Modulus.Samples.Fluid`) |
| Pack the libraries | `dotnet pack Modulus.slnx -c Release -o artifacts/packages` |
| Build the docs site | `cd website && npm ci && npm run build` |

Run the samples in the Development environment. Sign in with a sample user's email and the password `modulus`.

## Rules

- **Keep the templates and the manifest in step.** A component or package change that the CLI scaffolds must also update
  `src/Modulus.Ui.Templates/ui-kit.json` and the matching `templates/` files.
- **Keep the tutorials true.** The code in `website/docs/tutorial-*.md` is taken from `docs/examples/`. Change the example
  project, then regenerate the page's code blocks; do not hand-edit a code block so it drifts from the project.
- **One documentation file.** Put new documentation in `docs/DOCUMENTATION.md`, under a heading that matches its topic. Do not
  add separate Markdown files for plans or reports.
- **Antiforgery and CSP.** The MVC layout adds the antiforgery token to htmx requests, and the Content-Security-Policy uses a
  nonce. Do not add inline `<script>` blocks without the nonce (`CspNonce.Of(Context)`).
- **Licences.** The vendored files under `src/Modulus.Ui.Assets/wwwroot/lib` are third-party copies. Keep their `LICENSE` files.
- **Versions.** The four libraries share one version (`VersionPrefix` in `src/Directory.Build.props`). A change to one library
  in the UI repo is a change to the whole set.

## Before you finish a change

- `dotnet build Modulus.slnx` succeeds, and `dotnet test --solution Modulus.slnx` passes.
- A UI change is checked in a sample: open the page in the browser, light and dark mode.
- A docs change builds: `cd website && npm run build` (it fails on a broken link or anchor).
