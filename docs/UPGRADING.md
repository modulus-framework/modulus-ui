# Upgrading Ant Design (and htmx / Alpine.js)

## Automated Checks
A GitHub Actions workflow (`antd-drift.yml`) runs weekly and on every dependency bump PR. It generates a drift report showing:
- New components in AntDesign that are not yet wrapped
- Parameter changes (additions, removals, renames) in wrapped components
- htmx and Alpine.js version updates

Check the drift report in the PR or in the issue opened by the action.

## Manual Upgrade Checklist

### 1. Bump Version (5 min)
```bash
# Edit Directory.Packages.props
# Change: <PackageVersion Include="AntDesign" Version="X.Y.Z" />
```
Commit: `chore: bump AntDesign to X.Y.Z`

### 2. Run Drift Detection (2 min)
```bash
cd tools/antd-drift
dotnet run --configuration Release
# Read drift-reports/drift-report-*.md
```
The report shows:
- **Missing Components**: Components in AntDesign not yet wrapped (add to manifest + create wrapper)
- **Parameter Changes**: Parameters added/removed in existing components (update wrapper)

### 3. Update Wrappers (Variable time)
For each parameter change:
1. Open `src/Modulus.Blazor/Components/{Category}/App{Component}.razor`
2. Check if the parameter is exposed (it probably shouldn't be if it's internal)
3. If the parameter is on the wrapper public API, update the `[Parameter]` declaration
4. Rebuild: `dotnet build -c Release`

For each new public component:
1. Add to `manifest/antd-coverage.json` with status `in-progress`
2. Create `src/Modulus.Blazor/Components/{Category}/App{Component}.razor` using the wrapper pattern
3. Add `[AntdComponent("{ComponentName}")]` attribute
4. Create MVC TagHelper in `src/Modulus.AspNetCore.Mvc/TagHelpers/App{Component}TagHelper.cs` (or Alpine component if interactive)
5. Update sample gallery page
6. Mark as `done` in manifest when complete

### 4. Check Themes (3 min)
If antd released new token names or deprecated old ones:
1. Review `antd` changelog for CSS variable changes (https://github.com/ant-design/ant-design/releases)
2. Update `src/Modulus.Blazor/wwwroot/css/modulus-theme.css` to reference only existing tokens
3. Run test: `dotnet test Modulus.Coverage.Tests` (if exists)

### 5. Build & Test (5 min)
```bash
dotnet clean
dotnet build -c Release          # All 6 projects must build
dotnet test                      # Blazor + coverage tests
```

### 6. Visual Check (10 min)
Open the sample gallery apps in **light, dark, and compact modes**:
- `samples/Modulus.AdminTemplate.Blazor` (dotnet run, http://localhost:5000)
- `samples/Modulus.AdminTemplate.Mvc` (dotnet run, http://localhost:5001)

Check:
- No visual regressions
- New components render correctly
- Theme colors apply correctly in both light/dark
- Responsive layout still works

### 7. Document & Commit (3 min)
```bash
git add -A
git commit -m "chore: upgrade AntDesign to X.Y.Z

- Updated AppXxx wrappers for parameter changes
- Added [component list] wrappers
- Updated modulus-theme.css tokens
- All tests pass, gallery verified in light/dark/compact"
```

Optionally, tag a release:
```bash
git tag -a vX.Y.Z-antd-X.Y.Z -m "Supports AntDesign X.Y.Z"
git push origin vX.Y.Z-antd-X.Y.Z
```

## Troubleshooting

**Q: Drift report shows 30 missing components — is this bad?**
A: No. The drift report is conservative and shows all public types in AntDesign that *might* be components. Most are utilities, hooks, or internal types. Ignore types that are not in the official Ant Design component catalog (https://ant.design/components/overview/). Update the drift tool's heuristics if needed.

**Q: A wrapper stopped compiling after the upgrade.**
A: The upstream component removed or renamed a parameter. Check the AntDesign changelog and update the wrapper's `[Parameter]` to match. If the parameter was public API of our wrapper, document the breaking change.

**Q: The theme looks broken in dark mode.**
A: Check `modulus-theme.css` for references to removed CSS variables. Run a diff against the latest antd CSS to see what changed. The antd team publishes their CSS variable mapping in each release.

**Q: A `Pro*` composite stopped working.**
A: `Pro*` components use only public `AppXxx` components. They should not break if you've updated all the `AppXxx` wrappers correctly. If a `Pro*` broke, it's likely using an `AppXxx` that was not updated fully. Re-run the drift check and ensure all wrappers match the new AntDesign API.

## Version Pinning Strategy

- **Modulus.Blazor**: Pins `AntDesign` via central package management (`Directory.Packages.props`).
- **Drift detection**: Weekly GitHub Actions workflow reports on new components and parameter changes.
- **Backwards compatibility**: We use semantic versioning. A new minor version (e.g., v2.2.0) may drop support for an older AntDesign version; this will be documented in the release notes.

See `docs/PLAN.md` §8 for the full update safety strategy.
