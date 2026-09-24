# Upgrading Ant Design (and htmx / Alpine.js)

1. Bump `AntDesign` in `Directory.Packages.props` (the only place it is pinned). For htmx/Alpine, replace the vendored files in `src/Modulus.Ui.Assets/wwwroot/lib/` and note the version.
2. Run `dotnet run --project tools/antd-manifest` (added in Phase 7) and read the drift report.
3. For each new component: add it to `manifest/antd-coverage.json`, then add wrapper (Blazor), TagHelper + Alpine component (MVC/Razor Pages).
4. For removed/renamed parameters used by wrappers: fix the wrapper; `Pro*` should not need changes (they use only `AppXxx`).
5. Update `modulus-theme.css` if antd tokens changed.
6. `dotnet build && dotnet test`; check the gallery in light/dark/compact.
7. Record the change in the changelog.

See `docs/PLAN.md` §8.
