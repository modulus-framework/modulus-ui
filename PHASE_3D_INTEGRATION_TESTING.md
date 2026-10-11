# Phase 3d: Integration Testing & Verification

**Objective**: Verify template resolution, rendering, and full system integration across all UI engines

---

## Test Environment Setup

### Prerequisites

```bash
# 1. Build modulus-ui in Release mode (already done ✅)
cd E:\Personal\framework\modulus-ui
dotnet build Modulus.slnx -c Release

# 2. Pack Modulus.Ui.Templates locally
cd src/Modulus.Ui.Templates
dotnet pack -c Release -o ../../bin/packages
# Creates: ../../bin/packages/Modulus.Ui.Templates.0.9.0.nupkg

# 3. Create local NuGet feed
mkdir -p ~/.modulus/nuget-feed
cp bin/packages/Modulus.Ui.Templates.0.9.0.nupkg ~/.modulus/nuget-feed/

# 4. Configure NuGet to use local feed
# Edit ~/.nuget/NuGet/NuGet.Config:
# <add key="local-modulus" value="~/.modulus/nuget-feed" />

# 5. Build modulus CLI
cd <modulus-repo>/cli
dotnet build Modulus.Cli.csproj -c Debug
```

---

## Test 1: Package Resolution

**Objective**: Verify `ResolveTemplatesPackage()` correctly finds and extracts the package

### Test Case 1a: Local Cache Hit

```bash
# First run: Should download and cache
modulus generate-crud Customer --ui-engine mvc

# Verify cache created
ls -la ~/.modulus/cache/Modulus.Ui.Templates/0.9.0/templates/mvc/
# Should show: crud-index.cshtml.sbn, crud-edit.cshtml.sbn, etc.

# Second run: Should use cache (faster)
modulus generate-crud Product --ui-engine mvc
# Log should show: "Using cached templates: /Users/.../.modulus/cache/..."
```

### Test Case 1b: NuGet Source Resolution

```bash
# Remove cache to force fresh download
rm -rf ~/.modulus/cache/Modulus.Ui.Templates/0.9.0

# Run again - should resolve from nuget.org or local feed
modulus generate-crud Customer --ui-engine razor-pages

# Verify it downloaded
ls -la ~/.modulus/cache/Modulus.Ui.Templates/0.9.0/
# Should have extracted contents
```

### Test Case 1c: Invalid Package Version

```bash
# Try non-existent version
modulus generate-crud Customer --ui-engine blazor --ui-kit-version 99.0.0

# Expected: Error "Package not found: Modulus.Ui.Templates 99.0.0"
# Should NOT crash, should fall back to UiCrudWiring
```

---

## Test 2: Template Rendering - MVC

**Objective**: Verify MVC CRUD pages are correctly rendered

### Setup

```bash
cd ~/test-projects
modulus app CrudTestMvc --kind web --ui-engine mvc
cd CrudTestMvc
```

### Test Case 2a: Generate CRUD for Customer Entity

```bash
modulus generate-crud Customer --module Catalog

# Verify output files exist:
ls -la Presentation/Views/Customer/
# Expected:
#   Index.cshtml (list with search, delete actions)
#   Edit.cshtml (form with save, cancel)

ls -la Presentation/ApiClients/
# Expected: CustomerApiClient.cs

ls -la Presentation/Menu/
# Expected: CustomersMenuContributor.cs
```

### Test Case 2b: Verify Generated Code Quality

```bash
# 1. Build the project
dotnet build

# Expected: 0 errors, 0 warnings
# Verify:
#   - CustomerApiClient compiles
#   - Views have correct @addTagHelper directives
#   - MenuContributor inherits IMenuContributor

# 2. Check generated form
grep -A 5 "input id=\"firstName\"" Presentation/Views/Customer/Edit.cshtml
# Should show proper form-item structure

# 3. Check menu registration
grep -A 3 "class CustomersMenuContributor" Presentation/Menu/CustomersMenuContributor.cs
# Should implement IMenuContributor.ConfigureMenuAsync

# 4. Verify API client
grep "public async Task<List<CustomerDto>" Presentation/ApiClients/CustomerApiClient.cs
# Should have GetListAsync, GetAsync, CreateAsync, UpdateAsync, DeleteAsync
```

### Test Case 2c: Multiple Entities

```bash
modulus generate-crud Product --module Catalog
modulus generate-crud Order --module Sales
modulus generate-crud Invoice --module Finance

# Verify all three sets of files created
ls -la Presentation/Views/Product/
ls -la Presentation/Views/Order/
ls -la Presentation/Views/Invoice/

# Build again
dotnet build
# Should have 0 errors (all 3 sets compiled)
```

---

## Test 3: Template Rendering - Razor Pages

**Objective**: Verify Razor Pages CRUD pages use @page directives and correct syntax

### Setup

```bash
modulus app CrudTestRazor --kind web --ui-engine razor-pages
cd CrudTestRazor
```

### Test Case 3a: Page Directives

```bash
modulus generate-crud Customer --module Catalog

# Verify @page directives
head -5 Pages/Customer/Index.cshtml
# Should start with: @page "/customer"

head -5 Pages/Customer/Edit.cshtml
# Should start with: @page "/customer/{id}/edit"

# Verify page handlers exist
grep "public async Task OnGetAsync" Pages/Customer/Index.cshtml.cs
grep "public async Task OnPostAsync" Pages/Customer/Edit.cshtml.cs
```

### Test Case 3b: Form Binding

```bash
# Verify form uses asp-for and other Razor directives
grep "asp-for" Pages/Customer/Edit.cshtml | head -3
# Should show proper model binding

grep "BindProperty" Pages/Customer/Edit.cshtml.cs
# Should have [BindProperty] attributes

# Build
dotnet build
# Should compile with 0 errors
```

---

## Test 4: Template Rendering - Blazor

**Objective**: Verify Blazor components use proper @page, @code, and lifecycle methods

### Setup

```bash
modulus app CrudTestBlazor --kind web --ui-engine blazor
cd CrudTestBlazor
```

### Test Case 4a: Component Structure

```bash
modulus generate-crud Customer --module Catalog

# Verify @page directive
head -5 Components/Pages/Customer/Index.razor
# Should start with: @page "/customer"

# Verify @inject directives
grep "@inject HttpClient" Components/Pages/Customer/Index.razor
# Should have HttpClient injection

# Verify @code section
grep -A 30 "@code {" Components/Pages/Customer/Index.razor | head -35
# Should have:
#   - OnInitializedAsync() method
#   - API calls via Http.GetFromJsonAsync
#   - Proper data models
#   - Event handlers
```

### Test Case 4b: Async Data Loading

```bash
# Verify async/await patterns
grep "await Http.GetFromJsonAsync" Components/Pages/Customer/Index.razor
grep "OnInitializedAsync" Components/Pages/Customer/Index.razor

# Verify null checks and loading states
grep "Customers == null" Components/Pages/Customer/Index.razor
# Should handle loading state

# Build
dotnet build
# Should compile with 0 errors
```

---

## Test 5: Menu Integration

**Objective**: Verify MenuContributor classes auto-register menu items

### Test Cases

```bash
# After generating CRUD for Customer, Product, Order

# 1. Run the app
dotnet run

# 2. Navigate to http://localhost:5000
# 3. Check menu sidebar/navbar
# Should see:
#   - Catalog
#     - Customers
#     - Products
#   - Sales
#     - Orders

# 4. Click "Customers" - should navigate to /customer
# 5. Verify table displays (even if no data)
# 6. Try creating new customer - form should render
```

---

## Test 6: API Client Integration

**Objective**: Verify generated API clients communicate with backend

### Backend Setup

```csharp
// In your API project, add test endpoint
[ApiController]
[Route("api/[controller]")]
public class CustomerController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CustomerDto>>> GetList()
    {
        return Ok(new List<CustomerDto>
        {
            new() { Id = 1, FirstName = "John", LastName = "Doe", Email = "john@example.com" },
            new() { Id = 2, FirstName = "Jane", LastName = "Smith", Email = "jane@example.com" },
        });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CustomerDto>> Get(int id)
    {
        if (id == 1)
            return Ok(new CustomerDto { Id = 1, FirstName = "John", LastName = "Doe", Email = "john@example.com" });
        return NotFound();
    }

    [HttpPost]
    public async Task<ActionResult<int>> Create(CreateCustomerDto dto)
    {
        // Create and return ID
        return Ok(3);
    }
}
```

### Frontend Test

```bash
# 1. Run app with backend
dotnet run

# 2. Navigate to Customer list
# 3. Verify data loads from API
# 4. Check network tab - should see:
#     GET /api/customer
#     Response: [{ id: 1, firstName: "John", ... }, ...]

# 5. Click Edit on a customer
# 6. Verify single GET request:
#     GET /api/customer/1
#     Response: { id: 1, firstName: "John", ... }

# 7. Modify and save
# 8. Verify POST request:
#     POST /api/customer { ... }
#     Response: Success
```

---

## Test 7: Cross-Engine Compatibility

**Objective**: Verify same entity can generate for all engines

### Setup

```bash
# Create three projects, one per engine
modulus app TestMvc --kind web --ui-engine mvc
modulus app TestRazor --kind web --ui-engine razor-pages
modulus app TestBlazor --kind web --ui-engine blazor

# Generate same entity in all three
for app in TestMvc TestRazor TestBlazor; do
  cd $app
  modulus generate-crud Customer --module Catalog
  dotnet build
  cd ..
done

# All three should build with 0 errors
```

### Verification

```bash
# 1. Compare file structures
diff TestMvc/Presentation/Views/Customer/Index.cshtml \
     TestRazor/Pages/Customer/Index.cshtml

# Should differ in directives but have same form fields

# 2. Check line counts (should be similar)
wc -l TestMvc/Presentation/Views/Customer/Index.cshtml
wc -l TestRazor/Pages/Customer/Index.cshtml
wc -l TestBlazor/Components/Pages/Customer/Index.razor

# Should all be in similar range (150-250 lines)
```

---

## Test 8: Template Cache Performance

**Objective**: Verify caching significantly improves performance

### Setup

```bash
# Clear cache
rm -rf ~/.modulus/cache

# Time first generation (cache miss)
time modulus generate-crud Customer1 --ui-engine mvc
# Expected: 3-5 seconds

# Time second generation (cache hit)
time modulus generate-crud Customer2 --ui-engine mvc
# Expected: <1 second

# Performance improvement should be 3-5x
```

---

## Test 9: Error Handling & Recovery

### Test Case 9a: Corrupted Cache

```bash
# Corrupt the cache
echo "garbage" > ~/.modulus/cache/Modulus.Ui.Templates/0.9.0/templates/mvc/crud-index.cshtml.sbn

# Generate CRUD
modulus generate-crud Customer --ui-engine mvc

# Expected:
#   - Error in template rendering
#   - Falls back to UiCrudWiring
#   - Generation completes successfully
#   - Log: "UI generation failed; falling back to traditional wiring"
```

### Test Case 9b: Missing NuGet Sources

```bash
# Temporarily break NuGet config
# (rename NuGet.Config)

modulus generate-crud Customer --ui-engine mvc

# Expected:
#   - Attempts to download, fails
#   - Falls back to UiCrudWiring
#   - Log: "Could not resolve Modulus.Ui.Templates"
```

---

## Automated Test Suite

Create in `tests/Modulus.Cli.Tests/CliScaffoldBuildTests.cs`:

```csharp
[TestFixture]
public class CliScaffoldUiGenerationTests
{
    [TestCase("mvc")]
    [TestCase("razor-pages")]
    [TestCase("blazor")]
    public async Task GenerateCrud_WithUiEngine_CreatesValidPages(string engine)
    {
        // Arrange
        var testDir = Path.Combine(Path.GetTempPath(), $"test-{engine}-{Guid.NewGuid()}");
        var app = new CliApp(testDir);
        
        // Act
        await app.RunAsync($"app TestApp --ui-engine {engine}");
        await app.RunAsync("generate-crud Customer --module Catalog");
        
        // Assert
        var buildResult = await app.BuildAsync();
        Assert.That(buildResult.Errors, Is.Empty, "Build should have 0 errors");
        
        VerifyFilesExist(testDir, engine);
        VerifyCodeQuality(testDir, engine);
    }

    [Test]
    public async Task ResolveTemplatesPackage_WithValidVersion_ReturnsTemplatesPath()
    {
        // Arrange
        var resolver = new TemplatePackageResolver();
        
        // Act
        var path = await resolver.ResolveAsync("Modulus.Ui.Templates", "0.9.0");
        
        // Assert
        Assert.That(path, Is.Not.Null);
        Assert.That(Directory.Exists(Path.Combine(path, "mvc")), Is.True);
        Assert.That(Directory.Exists(Path.Combine(path, "razor-pages")), Is.True);
        Assert.That(Directory.Exists(Path.Combine(path, "blazor")), Is.True);
    }

    [Test]
    public async Task ResolveTemplatesPackage_WithCachedVersion_UsesCachedPath()
    {
        // Arrange
        var resolver = new TemplatePackageResolver();
        var stopwatch = Stopwatch.StartNew();
        
        // Act
        var path1 = await resolver.ResolveAsync("Modulus.Ui.Templates", "0.9.0");
        var time1 = stopwatch.ElapsedMilliseconds;
        
        stopwatch.Restart();
        var path2 = await resolver.ResolveAsync("Modulus.Ui.Templates", "0.9.0");
        var time2 = stopwatch.ElapsedMilliseconds;
        
        // Assert
        Assert.That(path1, Is.EqualTo(path2));
        Assert.That(time2, Is.LessThan(time1 / 3), "Cached resolution should be 3x faster");
    }
}
```

---

## Success Criteria

✅ **All Tests Pass**
- [ ] Package resolution test
- [ ] MVC generation test
- [ ] Razor Pages generation test
- [ ] Blazor generation test
- [ ] Menu integration test
- [ ] API client test
- [ ] Cross-engine compatibility test
- [ ] Cache performance test
- [ ] Error handling test

✅ **Code Quality**
- [ ] Generated code compiles with 0 errors
- [ ] Generated code follows framework patterns
- [ ] No security warnings
- [ ] Proper async/await patterns

✅ **Performance**
- [ ] First generation: < 5 seconds
- [ ] Cached generation: < 1 second
- [ ] Package download: < 30 seconds

✅ **Documentation**
- [ ] API contract documented
- [ ] CLI help updated
- [ ] Integration guide published

---

## Summary

Phase 3d verifies the complete pipeline across:
- ✅ All 3 UI engines (MVC, Razor Pages, Blazor)
- ✅ Package resolution and caching
- ✅ Template rendering and code generation
- ✅ Menu integration
- ✅ API client generation
- ✅ Error handling and fallback

All tests passing indicates **production readiness** for Phase 4: Public release and documentation.
