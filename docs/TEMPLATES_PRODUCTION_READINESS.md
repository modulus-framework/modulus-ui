# Modulus UI templates: production-readiness status and remaining work

Status: **in progress, paused.** Nothing in this document has been compiled, rendered or run in a browser yet.
Scope: `src/Modulus.Ui.Templates/templates/` for the four engines (`mvc`, `razor-pages`, `blazor`, `fluid`).

## 1. What changed so far

Security and correctness fixes, applied to both `mvc` and `razor-pages` unless noted:

- **Self-recursive `newRow()`** (`session-manager`): it called itself and overflowed the stack, so the page never loaded. Now uses `document.createElement('tr')`.
- **Un-nonced inline scripts** (`session-manager`, `two-factor`, `forgot-password`, `reset-password`, `permission-matrix`): these are blocked by the framework's enforced CSP. Each script now has `nonce="@Modulus.AspNetCore.Hosting.CspNonce.Of(Context)"` and is wrapped in an IIFE.
- **Inline event handlers** (`two-factor`: `onsubmit="return false"`): removed. Forms prevent submit in JavaScript.
- **Delete without confirmation** (`razor-pages/crud-index`): `onclick="return confirm(...)"` was blocked by the CSP, so Delete submitted with no prompt. Replaced with `data-confirm` on the form and a nonced handler.
- **Missing antiforgery header on `fetch` writes**: the POST and DELETE calls in `session-manager`, `two-factor`, `forgot-password`, `reset-password` and `permission-matrix` now send `RequestVerificationToken`, read from a hidden `#csrf` input rendered with `Antiforgery.GetAndStoreTokens(Context)`.
- **`Html.Raw` on caller data** (`card`, `tabs`): values are now rendered through Razor's normal encoding. Strings are encoded as text, and callers pass an `IHtmlContent` to insert markup.
- **`[Authorize]` added** after `@page` (razor-pages) and as `@attribute` (blazor) on every routable page except `forgot-password` and `reset-password`. The `mvc` views cannot carry the attribute, so the controller that renders each view must enforce authorization (see section 4).

Endpoint contracts were checked against `E:\Personal\framework\modulus\src` for the pages that have a backend. These match the real routes and JSON field names:

- `forgot-password`, `reset-password`
- `sessions` (list, revoke, revoke-all) and `login-history`
- `2fa` setup, enable, disable
- `permission-matrix` (`/authorization/grants`)

## 2. Findings that are not template fixes

- **Mock data without a backend.** Most admin and account pages (user admin, role CRUD, tenants, notifications, audit log, profile update, analytics, reports, admin settings) have hardcoded sample data, and some forms POST to pages with no handler (405). The framework has no HTTP endpoints for them. **Decision made:** show honest placeholders, not fake data. This is not yet implemented.
- **Blazor `HttpClient` calls to same-origin paths** (`/account/...`, `/api/...`). In Blazor Server these run on the server without the user's cookies, so they will likely return 401. This needs an in-process service call or a forwarded cookie handler. I have not confirmed how the sample app wires `HttpClient`.
- **`forgot-password` sends the email in the query string** (`POST /account/forgot-password?email=`). The framework's endpoint takes it from the query, so the template matches the server, but the address lands in access logs. Changing it means changing the endpoint to read a JSON body.
- **Blazor `two-factor`, `session-manager`, `permission-matrix`, `forgot-password`, `reset-password`** use `HttpClient`, so they inherit the cookie problem above. Their CSRF handling is also unverified because `HttpClient` does not send the antiforgery header.
- **CLI renderer contract.** `E:\Personal\framework\modulus\cli\Services\ScribanRenderer.cs` only replaces camelCase tokens (`{{ entityPlural }}`), while templates use snake_case (`{{ entity_name_plural }}`). The CRUD path uses real Scriban through `TemplateEngine`, which is correct. The legacy `UiFileGenerator` path does not. `UiFileGenerator.cs` also silently skips missing templates (`ApiClient`, `Menu`).
- **`ui-kit.json` maps output files, not templates.** Feature entries such as `shell`, `auth`, `errors` and `state-store` reference templates that do not exist here. The CLI matches templates by substring, which is fragile. It needs an explicit `template` field per file.
- **`CspNonce` requires `Modulus.AspNetCore.Hosting`.** The new auth templates use it, so apps generated from them must reference that package. `ui-kit.json` should list it for the auth feature.
- **Templates are not in a consistent Scriban/verbatim state.** Templates with `{{ }}` placeholders (CRUD, permission matrix, card/crud pages) are Scriban; the rest are copied verbatim. Keep this distinction when adding templates.

## 3. Remaining work

1. **Placeholders for pages with no backend** (`mvc`, `razor-pages`, `blazor`): add a visible "not connected" notice, remove no-op buttons (`@onclick="() => { }"`, `href="#"`), disable forms with `<fieldset disabled>`, and replace sample rows with empty states. Affected pages: `admin-settings`, `admin-users`, `analytics`, `audit-logs`, `notification-center`, `notification-preferences`, `reports-dashboard`, `role-management`, `system-health`, `tenant-list`, `tenant-members`, `tenant-settings`, `user-profile`, `dashboard`.
2. **Blazor stubs**: `role-management` and `tenant-members` have empty handlers, and `pagination`, `reports-dashboard` and `tenant-list` have `href="#"` links.
3. **Pages with a backend** (after section 2 is resolved): `session-manager`, `two-factor`, `forgot-password`, `reset-password`, `permission-matrix` are wired. Blazor versions still need the cookie fix.
4. **`razor-pages/entity-form`**: declares `@page` together with `@model {{entity_name}}Dto`, which is invalid because a page's model must be a `PageModel`. Make it a partial, or give it a page model.
5. **`mvc` CRUD**: `crud-index` and `crud-edit` are missing for parity. They need a controller template, and the CLI command must be wired to generate it.
6. **`blazor` parity**: `admin-settings` and `admin-users` are missing.
7. **`fluid` parity**: 8 templates exist; about 35 are missing (`admin-settings`, `admin-users`, `analytics`, `audit-logs`, `avatar`, `badge`, `breadcrumb`, `chart-*`, `crud-*`, `data-table`, `dropdown`, `empty-state`, `entity-form`, `forms`, `modal`, `notification-*`, `pagination`, `permission-matrix`, `reset-password`, `role-management`, `session-manager`, `stat-card`, `system-health`, `tabs`, `tenant-*`, `two-factor`, `user-profile`, `forgot-password`). Liquid templates must not use Scriban syntax, so verify how the CLI copies `.liquid.sbn` files before writing them.
8. **`ui-kit.json`**: explicit `template` per output file; remove or implement the `shell`, `auth`, `errors` and `state-store` entries; list `Modulus.AspNetCore.Hosting` for `auth`.
9. **Docs**: the template index is at `src/Modulus.Ui.Templates/README.md` and mentions a `templates/README.md` and a `modulus generate-crud` command that do not match the real files or commands.
10. **Verification**: none has been done. Needed: a Scriban parse check of every `.sbn` template; a Razor compile of generated `mvc`, `razor-pages` and `blazor` output; a Liquid parse of `fluid`; a Playwright run with the CSP enforced (the existing `tests/browser` gate) to confirm no CSP violations and that the session and 2FA pages load.

## 4. Known limits of the current fixes

- `mvc` views rely on their controller for `[Authorize]`, which the templates do not generate.
- The `#csrf` input is rendered per page. It works only if the page is served through `Antiforgery`-enabled MVC or Razor Pages.
- The `permission-matrix` role list and API prefix are substituted by the Scriban generator. A role name containing a quote would break the generated JavaScript; the generator should escape it.
