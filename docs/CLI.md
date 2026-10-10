# Modulus CLI: UI commands

The `modulus ui` commands scaffold UI into a Modulus web app. They read the app's engine from `.modulus.json`
(`ui_engine`, written by `modulus app --ui-engine`) or take `--engine mvc|razor-pages|blazor`. Fluid is not scaffolded yet.
Every command accepts `--dry-run`, `--force`, `--json`, `-v`, `-q` and `-o <app root>`.

Templates come from the `Modulus.Ui.Templates` package, the UI libraries from `Modulus.Ui.*`, `Modulus.AspNetCore.*`,
`Modulus.Blazor` and `Modulus.Documents`.

## Packages and feeds

| Where | What the CLI does |
| --- | --- |
| `~/.modulus/cache` | Templates unpacked from the package (re-unpacked when the local `.nupkg` is newer). |
| `~/.modulus/feed` (or `MODULUS_LOCAL_FEED`) | Local feed used until the packages are on nuget.org. |
| nuget.org | Used when the package is in neither of the above. |

Pack the libraries into the local feed: `dotnet pack Modulus.slnx -c Release -o ~/.modulus/feed`.
`modulus app --ui-engine <engine>` references the engine's packages (listed in the package's `ui-kit.json`) and, if the local
feed exists, adds it to `NuGet.config` with source mapping. Delete `~/.modulus/feed` once the packages are published.

## Commands

| Command | Adds |
| --- | --- |
| `ui add-component <names…> \| --all \| --list` | 17 components: alert, avatar, badge, breadcrumb, button, card, data-table, dropdown, empty-state, form-group, form-input, modal, pagination, progress, spinner, stat-card, tabs. |
| `ui create-dashboard <overview\|analytics\|reports\|audit> [--module]` | A dashboard page. |
| `ui create-form-from-entity <Entity> [--module]` | A create/edit form whose fields come from the entity class's public properties. |
| `ui add-auth` | Forgot-password and reset-password pages (`/auth/forgot-password`, `/auth/reset-password`). |
| `ui add-2fa` | The two-factor page (`/auth/two-factor`): authenticator setup, recovery codes, turn off. |
| `ui add-session-manager` | Sessions and recent sign-ins (`/auth/sessions`). |
| `ui add-chart --type line\|column\|donut\|heatmap` | A chart page (`/charts/<type>`) built on the framework's chart components, with sample data to replace. MVC shares one `ChartsController`. |
| `ui theme create <name> [--base tabler\|minimal] [--colors]` | `Themes/<name>/theme.json` and `variables.css`. |
| `ui theme set <name>` | Writes the theme to `Modulus:Theme` in `appsettings.json`. |
| `ui theme list` | The app's themes; the active one is marked. |
| `ui theme export <name> --format css\|scss\|tailwind\|json` | The theme in another format. |

The auth pages call the `/account/*` endpoints of Modulus.Identity on the same host. MVC gets a controller beside each
view; Razor Pages and Blazor carry their route in the page.

## Not built yet

`add-chat`, `add-form-builder`, `add-collaboration`, `add-pwa`, `add-i18n`, `create-user-dashboard` (Identity has
no endpoint to list users), `create-permission-matrix`, `audit` and `add-access-review`. They need template content or
backend endpoints that do not exist yet. Self-service registration is not offered because Identity only supports invitations.
