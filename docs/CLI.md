# Modulus CLI: UI commands

The `modulus ui` commands scaffold UI into a Modulus web app. They read the app's engine from `.modulus.json`
(`ui_engine`, written by `modulus app --ui-engine`) or take `--engine mvc|razor-pages|blazor`. Fluid is not scaffolded yet.
Every command accepts `--dry-run`, `--force`, `--json`, `-v`, `-q` and `-o <app root>`.

Templates come from the `Modulus.Ui.Templates` package, the UI libraries from `Modulus.Ui.*`, `Modulus.AspNetCore.*`,
`Modulus.Blazor` and `Modulus.Documents`.

## Which UI a new app gets

`modulus app --kind webapp+api --ui-engine mvc|razor-pages` builds the **Web project on the Modulus UI framework**:
`AddModulusMvc`, the `_ModulusLayout` shell, `MapStaticAssets`, an auth frame for the sign-in pages and a sign-in
convention (every page needs a signed-in user except `/Account`). The CLI no longer carries any other UI: the older
`Cobytelabs.Modulus.UI.*` modules, the Tabler theme, `ui add/remove/update/eject/diff`, `--ui-modules` and the single-project
`webapp` kind were removed. `blazor`, `fluid` and `none` have page templates but no app host yet, so `modulus app` refuses
them for a web app. `generate-crud` scaffolds the API side only; add pages with the `ui` commands below.

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
| `ui add-admin [--pages a,b] [--engine] [--folder Admin] [--no-menu]` | The admin dashboard starter: 14 working pages (dashboard, analytics, reports, users, roles, tenants with settings and members, settings, audit log, system health, profile, notifications and preferences), plus an `AdminController` for MVC and a `Views/_ViewStart.cshtml` that sets the shell layout when the app has none. It adds `AddModulusAdminUi()` (the matching menu: Overview, Settings, System, My account) to `Program.cs` and, for MVC, controller routing. `--pages` limits both the files and the menu. The pages are yours to edit; they read data through `Modulus.Ui.Abstractions.Admin` services and show sample data until you register your own. They need a signed-in user (`--auth openiddict`), and the command warns when the host has no sign-in. |
| `ui add-auth` | Forgot-password and reset-password pages (`/auth/forgot-password`, `/auth/reset-password`). |
| `ui add-2fa` | The two-factor page (`/auth/two-factor`): authenticator setup, recovery codes, turn off. |
| `ui add-session-manager` | Sessions and recent sign-ins (`/auth/sessions`). |
| `ui add-chart --type line\|column\|donut\|heatmap` | A chart page (`/charts/<type>`) built on the framework's chart components, with sample data to replace. MVC shares one `ChartsController`. |
| `ui add-i18n --languages en,es,fr` | Sets `Modulus:Theme:Cultures` (the first is the default) and adds `app.UseModulusLocalization()` before `UseRouting`. With two or more languages the theme settings show a language picker. |
| `ui audit [--level A\|AA\|AAA] [--format json\|markdown\|html] [--out-file f] [--fail-on-issues]` | A static accessibility scan of every `.cshtml`, `.razor` and `.html` file under `src`. Seven rules (image alt, form labels, button/link names, keyboard access, page language, table headers, positive tabindex), all WCAG level A. A markup lint: it does not check colour contrast, focus order or content in the running page. |
| `ui create-permission-matrix <Entity> [--module] [--roles Admin,Sales] [--permissions a:b:c,...]` | A role × permission matrix (`/permissions/<entity>`). It reads grants with `GET /authorization/grants/Role/{role}` and grants or revokes with `POST` / `DELETE`, so the host needs `MapModulusAuthorizationManagement` and the caller the management permission. The API cannot list all roles or permissions, so rows and columns come from the options (default: `Admin,Manager,User` and the entity's `<module>:<route>:manage`). A denied permission shows as a locked box. |
| `ui create-crud <Entity> [--module] [--engine razor-pages\|blazor]` | List, create/edit and delete pages (`/<entities>`) over the entity's API (`generate-crud`) through a typed `<Entity>ApiClient`, plus a menu entry. Needs a `webapp+api` app; set `Api:BaseUrl` in the Web project. |
| `ui add-assistant` | Hosts the AI platform's embedded assistant: `POST /ai/session` (mints the session token with the server-side `Ai:Host:ApiKey`), a `_AiAssistant` layout partial rendering `<ai-assistant>`, the `ai:use` permission and `Ai:Host` settings (disabled by default). Razor Pages and MVC; supply the SDK script under `wwwroot/lib/ai-sdk/`. |
| `ui theme create <name> [--base tabler\|minimal] [--colors]` | `Themes/<name>/theme.json` and `variables.css`. |
| `ui theme set <name>` | Writes the theme to `Modulus:Theme` in `appsettings.json`. |
| `ui theme list` | The app's themes; the active one is marked. |
| `ui theme export <name> --format css\|scss\|tailwind\|json` | The theme in another format. |

The auth pages call the `/account/*` endpoints of Modulus.Identity on the same host. MVC gets a controller beside each
view; Razor Pages and Blazor carry their route in the page.

## Not built yet

`add-chat`, `add-form-builder`, `add-collaboration`, `add-pwa`, `create-user-dashboard` (Identity has
no endpoint to list users), `add-access-review`. They need template content or
backend endpoints that do not exist yet. Self-service registration is not offered because Identity only supports invitations.

## Known issues

- The identity-backed pages (`add-auth`, `add-2fa`, `add-session-manager`, permission matrix) call same-origin `/account/*` and `/authorization/*`; in a `webapp+api` app the Web host forwards the recovery routes only.
- The `mvc` engine is partly wired (no `AddControllersWithViews` in a fresh Web host, no `_ViewStart`/`_ViewImports`); prefer `razor-pages` or `blazor`.
- `admin-users`, `admin-settings`, `module-home` and `system-health` templates exist but no command scaffolds them yet.
- The `_content` assets of the UI packages are served by `dotnet run` only in the Development environment; a published app carries them in `wwwroot`.
