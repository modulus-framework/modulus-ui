# Phase 3b-3d Implementation Checklist

**Goal**: Complete and test template resolution, rendering, and integration  
**Timeline**: 7-10 hours  
**Status**: Ready to start  

---

## Phase 3b: Template Package Resolution (2-3 hours)

### Step 1: Add TemplatePackageResolver.cs
- [ ] Copy `SAMPLE_IMPLEMENTATION_REFERENCE.md` → `modulus/cli/Services/TemplatePackageResolver.cs`
- [ ] Add NUnit dependencies if not present
- [ ] Build and verify no compilation errors

**Files to modify**:
```
modulus/cli/Services/TemplatePackageResolver.cs (NEW)
```

### Step 2: Setup NuGet Package
- [ ] Build modulus-ui: `dotnet build Modulus.slnx -c Release`
- [ ] Pack templates: `cd src/Modulus.Ui.Templates && dotnet pack -c Release`
- [ ] Create local feed: `mkdir ~/.modulus/nuget-feed`
- [ ] Copy .nupkg to feed: `cp bin/packages/*.nupkg ~/.modulus/nuget-feed/`

**Commands**:
```bash
cd E:\Personal\framework\modulus-ui\src\Modulus.Ui.Templates
dotnet pack -c Release -o ../../bin/packages
mkdir -p ~/.modulus/nuget-feed
cp ../../bin/packages/Modulus.Ui.Templates.0.9.0.nupkg ~/.modulus/nuget-feed/
```

### Step 3: Configure NuGet.Config
- [ ] Edit `~/.nuget/NuGet/NuGet.Config`
- [ ] Add local feed entry:
```xml
<packageSources>
  <add key="local" value="~/.modulus/nuget-feed" />
  <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
</packageSources>
```

### Step 4: Unit Test TemplatePackageResolver
- [ ] Copy unit tests from `SAMPLE_IMPLEMENTATION_REFERENCE.md`
- [ ] Create: `tests/Modulus.Cli.Tests/TemplatePackageResolverTests.cs`
- [ ] Run: `dotnet test TemplatePackageResolverTests`
- [ ] Verify: All tests pass ✅

**Expected test results**:
```
ResolveAsync_WithValidPackage_ReturnsCachePath ✅
ResolveAsync_WithCachedPackage_UsesCache ✅
ValidateTemplatesAsync_WithValidStructure_ReturnsTrue ✅
ValidateTemplatesAsync_WithMissingTemplates_ReturnsFalse ✅
```

### Phase 3b Sign-off
- [ ] `TemplatePackageResolver.cs` compiles without errors
- [ ] Cache directory created: `~/.modulus/cache/Modulus.Ui.Templates/0.9.0/`
- [ ] All unit tests pass (4/4)
- [ ] Can resolve from local NuGet feed
- [ ] Validation works correctly

**Effort**: 2-3 hours  
**Complexity**: Medium  
**Status**: Ready to start

---

## Phase 3c: Template Rendering & Generation (3-4 hours)

### Step 1: Extend GenerateCrudCommand
- [ ] Copy `GenerateUiAsync()` from `SAMPLE_IMPLEMENTATION_REFERENCE.md`
- [ ] Add to: `modulus/cli/Commands/GenerateCrudCommand.cs`
- [ ] Copy helper methods:
  - `GenerateUiPagesAsync()`
  - `RenderTemplateAsync()`
  - `GetTemplateFileNameForPattern()`
  - `GetTemplateExtension()`
  - `Pluralize()`

**Files to modify**:
```
modulus/cli/Commands/GenerateCrudCommand.cs
```

### Step 2: Add TemplateContext Class
- [ ] Copy `TemplateContext` from `SAMPLE_IMPLEMENTATION_REFERENCE.md`
- [ ] Add model classes:
  - `EntityModel`
  - `ModuleModel`
  - `PropertyModel`

**Files to create**:
```
modulus/cli/Models/TemplateContext.cs
```

### Step 3: Integrate TemplatePackageResolver
- [ ] Modify `GenerateCrudCommand` constructor to accept `ITemplatePackageResolver`
- [ ] Add method: `InitializeTemplateEngine(ITemplatePackageResolver)`
- [ ] Call from `GenerateAll()` when `withUi && uiEngine != "none"`

**Code pattern**:
```csharp
public class GenerateCrudCommand
{
    private readonly ITemplatePackageResolver _resolver;
    
    public GenerateCrudCommand(ITemplatePackageResolver resolver)
    {
        _resolver = resolver;
    }
    
    // Call GenerateUiAsync in GenerateAll
    if (withUi && uiEngine != "none")
    {
        var success = await GenerateUiAsync(module, entity, uiEngine);
        if (!success)
            _logger.Warning("UI generation skipped");
    }
}
```

### Step 4: Build & Compile Test
- [ ] Build solution: `dotnet build cli/Modulus.Cli.csproj -c Debug`
- [ ] Verify: 0 compilation errors
- [ ] Run basic CLI: `modulus --help`
- [ ] Verify help shows UI options

**Expected output**:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### Step 5: Manual Generation Test
- [ ] Create test project: `modulus app TestProject --kind web --ui-engine mvc`
- [ ] Generate CRUD: `modulus generate-crud Customer --module Catalog`
- [ ] Verify output files exist
- [ ] Check file content for proper Scriban rendering

**Expected files**:
```
Presentation/Views/Customer/Index.cshtml
Presentation/Views/Customer/Edit.cshtml
Presentation/ApiClients/CustomerApiClient.cs
Presentation/Menu/CustomersMenuContributor.cs
```

### Phase 3c Sign-off
- [ ] `GenerateCrudCommand` compiles without errors
- [ ] UI pages generate successfully for mvc engine
- [ ] Generated files have correct content (no Scriban errors)
- [ ] File paths are correct for engine
- [ ] Pluralization works correctly
- [ ] No template rendering errors in logs

**Effort**: 3-4 hours  
**Complexity**: Medium-High  
**Status**: Depends on Phase 3b completion

---

## Phase 3d: Integration Testing (2-3 hours)

### Test 1: Package Resolution
- [ ] Run: `dotnet test TemplatePackageResolverTests`
- [ ] Verify: Cache hit is 3x+ faster than miss
- [ ] Verify: Invalid packages handled gracefully

**Expected results**:
```
✅ Package resolution test
✅ Cache management test
✅ Error handling test
```

### Test 2: MVC Generation
- [ ] Copy integration test from `SAMPLE_IMPLEMENTATION_REFERENCE.md`
- [ ] Create: `tests/Modulus.Cli.Tests/GenerateCrudUiTests.cs`
- [ ] Run: `dotnet test GenerateCrudUiTests.GenerateCrud_CreatesValidPages*mvc*`
- [ ] Verify: Index.cshtml and Edit.cshtml generated
- [ ] Build generated project: `dotnet build`
- [ ] Verify: 0 compilation errors

**Test commands**:
```bash
cd tests/Modulus.Cli.Tests
dotnet test GenerateCrudUiTests.GenerateCrud_CreatesValidPages -Filter "*mvc*"
```

### Test 3: Razor Pages Generation
- [ ] Run: `dotnet test GenerateCrudUiTests.GenerateCrud_CreatesValidPages -Filter "*razor-pages*"`
- [ ] Verify: @page directive present
- [ ] Verify: .cshtml.cs file generated
- [ ] Build generated project
- [ ] Verify: 0 compilation errors

**Expected output**:
```
Pages/Customer/Index.cshtml  (with @page "/customer")
Pages/Customer/Index.cshtml.cs
Pages/Customer/Edit.cshtml   (with @page "/customer/{id}/edit")
Pages/Customer/Edit.cshtml.cs
```

### Test 4: Blazor Generation
- [ ] Run: `dotnet test GenerateCrudUiTests.GenerateCrud_CreatesValidPages -Filter "*blazor*"`
- [ ] Verify: @page directive present
- [ ] Verify: @code section with OnInitializedAsync()
- [ ] Verify: @inject HttpClient declaration
- [ ] Build generated project
- [ ] Verify: 0 compilation errors

**Expected output**:
```
Components/Pages/Customer/Index.razor  (with @page, @code, @inject)
Components/Pages/Customer/Edit.razor   (with @page, @code, @inject)
```

### Test 5: Multi-Entity Generation
- [ ] Run: `dotnet test GenerateCrudUiTests.GenerateCrud_WithMultipleEntities_CreatesAllPages`
- [ ] Verify: 3+ entities generate successfully
- [ ] Verify: 6+ .cshtml files created
- [ ] Check menu contributors register correctly

**Expected files**:
```
Customer/, Product/, Order/ directories
Each with: Index.cshtml, Edit.cshtml, ApiClient.cs, MenuContributor.cs
```

### Test 6: Performance Benchmarking
- [ ] First generation (cache miss): < 5 seconds
- [ ] Second generation (cache hit): < 1 second
- [ ] Package download: < 30 seconds

**Benchmark test**:
```bash
time modulus generate-crud Customer1 --ui-engine mvc  # ~3-5 seconds
time modulus generate-crud Customer2 --ui-engine mvc  # ~0.5 seconds
```

### Test 7: Error Handling
- [ ] Corrupt cache, verify fallback works
- [ ] Missing NuGet source, verify graceful failure
- [ ] Invalid entity model, verify error message

**Test scenarios**:
```bash
# Corrupt cache
echo "garbage" > ~/.modulus/cache/Modulus.Ui.Templates/0.9.0/templates/mvc/crud-index.cshtml.sbn

# Generate - should fallback
modulus generate-crud Customer --ui-engine mvc
# Log should show: "UI generation failed; falling back to traditional wiring"
```

### Phase 3d Sign-off Checklist
- [ ] All 4 unit tests pass
- [ ] All 4 integration tests pass
- [ ] Package resolution test passes
- [ ] MVC generation produces valid .cshtml files
- [ ] Razor Pages generation produces @page directives
- [ ] Blazor generation produces @code sections
- [ ] Multi-entity generation works
- [ ] Performance: cache hit < 1 second
- [ ] Error handling: graceful fallback
- [ ] Cross-engine: all 3 engines work
- [ ] All generated code compiles to 0 errors

**Effort**: 2-3 hours  
**Complexity**: Medium  
**Status**: Depends on Phase 3c completion

---

## Final Verification (30 minutes)

### Build Verification
- [ ] `dotnet build` produces 0 errors, 0 warnings
- [ ] `dotnet test` produces 12+ passing tests
- [ ] `dotnet build -c Release` for production build

### Documentation
- [ ] Update CLI help with UI command examples
- [ ] Add --ui-engine option to default help
- [ ] Document error codes and recovery

### Deployment Prep
- [ ] Package `Modulus.Ui.Templates` as 0.9.0
- [ ] Push to staging NuGet feed
- [ ] Update versioning in manifest
- [ ] Create release notes

---

## Sign-Off Sheet

### Phase 3b Completion
- Developer: ________________
- Date: ____/____/____
- Build Status: ☐ Pass ☐ Fail
- Tests Passed: 4/4
- Notes: _________________________________

### Phase 3c Completion
- Developer: ________________
- Date: ____/____/____
- Build Status: ☐ Pass ☐ Fail
- Generated Files Valid: ☐ Yes ☐ No
- Notes: _________________________________

### Phase 3d Completion
- QA Lead: ________________
- Date: ____/____/____
- All Tests Pass: ☐ Yes ☐ No
- All Engines Work: ☐ Yes ☐ No
- Performance OK: ☐ Yes ☐ No
- Ready for Release: ☐ Yes ☐ No
- Notes: _________________________________

---

## Resources

📖 **Documentation**:
- `MODULUS_FRAMEWORK_UI_INTEGRATION.md` - Framework architecture
- `PHASE_3C_TEMPLATE_RESOLUTION.md` - Implementation guide
- `PHASE_3D_INTEGRATION_TESTING.md` - Test specifications
- `SAMPLE_IMPLEMENTATION_REFERENCE.md` - Copy-paste code
- `IMPLEMENTATION_CHECKLIST.md` - This file

🔗 **Reference**:
- Modulus Framework: `Modulus.Ui.Abstractions`
- Template Engine: Scriban
- Test Framework: NUnit
- Build System: dotnet CLI

⏰ **Timeline**:
```
Phase 3b: ██████░░░░ 2-3 hours
Phase 3c: ██████████ 3-4 hours
Phase 3d: ██████░░░░ 2-3 hours
Total:    ████████░░ 7-10 hours
```

---

## Support

**Issues**?
1. Check `PHASE_3C_TEMPLATE_RESOLUTION.md` for implementation details
2. Review `SAMPLE_IMPLEMENTATION_REFERENCE.md` for code examples
3. Consult `PHASE_3D_INTEGRATION_TESTING.md` for test specifications

**Questions**?
1. Review framework docs: `MODULUS_FRAMEWORK_UI_INTEGRATION.md`
2. Check API specs for endpoint details
3. Review test examples for patterns

---

**Status: READY TO START PHASE 3B-3D** ✅
