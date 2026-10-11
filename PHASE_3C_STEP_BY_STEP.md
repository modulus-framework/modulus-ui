# Phase 3c: Step-by-Step Implementation Guide

**Phase**: 3c (Template Rendering & Generation)  
**Time**: 3-4 hours  
**Complexity**: Medium-High  
**Prerequisite**: ✅ Phase 3b complete  
**Status**: READY TO START

---

## 📋 Overview: What You'll Do

Phase 3c adds the ability to:
1. ✅ Load templates from Phase 3b cache
2. ✅ Render with Scriban (template engine)
3. ✅ Map entity metadata to template context
4. ✅ Generate UI files per engine (MVC, Razor Pages, Blazor)
5. ✅ Handle errors gracefully with fallback

---

## 🎯 Step 1: Create TemplateContext Model Class

**Location**: `modulus/cli/Services/TemplateContext.cs`  
**Action**: Create new file

```csharp
namespace Modulus.Cli.Services;

/// <summary>
/// Context object passed to Scriban templates during rendering.
/// Contains entity metadata and generation settings.
/// </summary>
internal class TemplateContext
{
    /// <summary>Entity name (singular, e.g. "Product")</summary>
    public required string Entity { get; init; }

    /// <summary>Entity name plural (e.g. "Products")</summary>
    public required string EntityPlural { get; init; }

    /// <summary>Module model containing namespace, name, etc.</summary>
    public required ModuleModel Module { get; init; }

    /// <summary>UI Engine selected (mvc, razor-pages, blazor, fluid)</summary>
    public required string UiEngine { get; init; }

    /// <summary>Root namespace for generated files</summary>
    public required string Namespace { get; init; }

    /// <summary>Timestamp for generation</summary>
    public DateTime Now { get; } = DateTime.UtcNow;

    /// <summary>Entity name in camelCase (e.g. "product")</summary>
    public required string EntityLower { get; init; }

    /// <summary>Entity name in route format (e.g. "products")</summary>
    public required string RouteName { get; init; }
}

/// <summary>Module metadata for template context</summary>
internal class ModuleModel
{
    public required string RootNamespace { get; init; }
    public required string ModuleNamespace { get; init; }
    public required string ModuleName { get; init; }
    public required string EntityName { get; init; }
    public required string EntityNameLower { get; init; }
    public required string RouteName { get; init; }
    public string? RequiredPermission { get; init; }
    public bool MultiTenant { get; init; }
}
```

**Dependencies**: None (simple data class)

---

## 🎯 Step 2: Create Scriban Template Renderer Service

**Location**: `modulus/cli/Services/ScribanRenderer.cs`  
**Action**: Create new file

```csharp
using Scriban;
using Scriban.Runtime;

namespace Modulus.Cli.Services;

/// <summary>
/// Renders Scriban templates with context data.
/// Used for template rendering in Phase 3c.
/// </summary>
internal class ScribanRenderer
{
    /// <summary>Render a template with context</summary>
    public string Render(string templateContent, TemplateContext context)
    {
        try
        {
            // Parse template
            var template = Template.Parse(templateContent);
            
            // Prepare context object
            var scriptObject = new ScriptObject();
            scriptObject.SetValue("entity", context.Entity, true);
            scriptObject.SetValue("entityPlural", context.EntityPlural, true);
            scriptObject.SetValue("entityLower", context.EntityLower, true);
            scriptObject.SetValue("routeName", context.RouteName, true);
            scriptObject.SetValue("module", context.Module, true);
            scriptObject.SetValue("uiEngine", context.UiEngine, true);
            scriptObject.SetValue("namespace", context.Namespace, true);
            scriptObject.SetValue("now", context.Now, true);

            var scriptContext = new TemplateContext();
            scriptContext.PushGlobal(scriptObject);

            // Render
            var output = template.Render(scriptContext);
            return output;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to render template: {ex.Message}", ex);
        }
    }
}
```

**Dependencies**: Scriban NuGet package (should already be in modulus CLI)

---

## 🎯 Step 3: Create UI Generation Service

**Location**: `modulus/cli/Services/UiFileGenerator.cs`  
**Action**: Create new file

```csharp
using System.Text.Json;

namespace Modulus.Cli.Services;

/// <summary>
/// Generates UI files from templates using the template package resolver.
/// Handles all three engines: MVC, Razor Pages, and Blazor.
/// </summary>
internal class UiFileGenerator
{
    private readonly ITemplatePackageResolver _resolver;
    private readonly ScribanRenderer _renderer = new();

    public UiFileGenerator(ITemplatePackageResolver resolver)
    {
        _resolver = resolver;
    }

    /// <summary>
    /// Generate all UI files for an entity.
    /// </summary>
    /// <param name="module">Module information</param>
    /// <param name="entity">Entity name</param>
    /// <param name="uiEngine">UI engine (mvc, razor-pages, blazor, fluid)</param>
    /// <param name="outputPath">Base output directory</param>
    /// <returns>List of generated file paths</returns>
    public async Task<List<string>> GenerateAsync(
        ModuleModel module,
        string entity,
        string uiEngine,
        string outputPath)
    {
        var generated = new List<string>();

        // Resolve templates package
        var templatesPath = await _resolver.ResolveAsync("Modulus.Ui.Templates", "0.9.0");
        if (templatesPath == null)
        {
            Ux.Warning("Could not resolve templates package; skipping UI generation");
            return generated;
        }

        try
        {
            // Load ui-kit.json manifest
            var manifestPath = Path.Combine(
                Path.GetDirectoryName(templatesPath)!,
                "..", "..", "ui-kit.json"
            );

            if (!File.Exists(manifestPath))
            {
                Ux.Warning("ui-kit.json not found; skipping UI generation");
                return generated;
            }

            var manifestJson = await File.ReadAllTextAsync(manifestPath);
            using var doc = JsonDocument.Parse(manifestJson);
            var root = doc.RootElement;

            // Get CRUD feature definition
            if (!root.TryGetProperty("features", out var features) ||
                !features.TryGetProperty("crud", out var crud) ||
                !crud.TryGetProperty("files", out var filesObj) ||
                !filesObj.TryGetProperty(uiEngine, out var engineFiles))
            {
                Ux.Warning($"No CRUD templates defined for {uiEngine}");
                return generated;
            }

            // Prepare context
            var entityPlural = Pluralize(entity);
            var context = new TemplateContext
            {
                Entity = entity,
                EntityPlural = entityPlural,
                EntityLower = CodeGen.ToCamelCase(entity),
                RouteName = entityPlural.ToLowerInvariant(),
                Module = module,
                UiEngine = uiEngine,
                Namespace = $"{module.RootNamespace}.{module.ModuleName}.Presentation",
            };

            // Generate each template file
            foreach (var fileElement in engineFiles.EnumerateArray())
            {
                var pattern = fileElement.GetString();
                if (string.IsNullOrEmpty(pattern)) continue;

                // Substitute placeholders
                var outputFile = SubstitutePlaceholders(pattern, entity, entityPlural);
                var fullPath = Path.Combine(outputPath, outputFile);

                // Determine template file name
                var templateFileName = GetTemplateFileName(pattern, uiEngine);
                var templatePath = Path.Combine(templatesPath, uiEngine, templateFileName);

                if (!File.Exists(templatePath))
                {
                    Ux.Detail($"Template not found: {templatePath}");
                    continue;
                }

                // Render template
                var templateContent = await File.ReadAllTextAsync(templatePath);
                var rendered = _renderer.Render(templateContent, context);

                // Write file
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                await File.WriteAllTextAsync(fullPath, rendered);

                generated.Add(outputFile);
                Ux.Success($"Generated: {outputFile}");
            }

            return generated;
        }
        catch (Exception ex)
        {
            Ux.Error($"UI generation failed: {ex.Message}");
            return generated;
        }
    }

    private string SubstitutePlaceholders(string pattern, string entity, string plural)
    {
        return pattern
            .Replace("[Entity]", entity, StringComparison.OrdinalIgnoreCase)
            .Replace("[Entities]", plural, StringComparison.OrdinalIgnoreCase)
            .Replace("[entity]", CodeGen.ToCamelCase(entity), StringComparison.OrdinalIgnoreCase)
            .Replace("[entities]", CodeGen.ToCamelCase(plural), StringComparison.OrdinalIgnoreCase);
    }

    private string GetTemplateFileName(string pattern, string uiEngine)
    {
        var ext = GetTemplateExtension(uiEngine);

        if (pattern.Contains("Index", StringComparison.OrdinalIgnoreCase))
            return $"crud-index.{ext}.sbn";

        if (pattern.Contains("Edit", StringComparison.OrdinalIgnoreCase))
            return $"crud-edit.{ext}.sbn";

        if (pattern.Contains("ApiClient", StringComparison.OrdinalIgnoreCase))
            return "api-client.cs.sbn";

        if (pattern.Contains("Menu", StringComparison.OrdinalIgnoreCase))
            return "menu-contributor.cs.sbn";

        return "unknown.sbn";
    }

    private string GetTemplateExtension(string uiEngine) => uiEngine switch
    {
        "mvc" => "cshtml",
        "razor-pages" => "cshtml",
        "blazor" => "razor",
        "fluid" => "liquid",
        _ => "html"
    };

    private string Pluralize(string singular)
    {
        if (singular.EndsWith("y", StringComparison.OrdinalIgnoreCase))
            return singular[..^1] + "ies";
        if (singular.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            return singular + "es";
        return singular + "s";
    }
}
```

**Dependencies**: System.Text.Json (built-in)

---

## 🎯 Step 4: Add Scriban NuGet Package (if needed)

Check if Scriban is already installed:

```bash
cd E:\Personal\framework\modulus\cli
grep -i "scriban" Modulus.Cli.csproj

# If not found, add it:
dotnet add package Scriban
```

---

## 🎯 Step 5: Integrate into GenerateCrudCommand

Modify `GenerateCrudCommand.cs` to use the UI generator:

**Location**: Line ~35 (after TemplateEngine declaration)

```csharp
private readonly UiFileGenerator _uiGenerator;

// In ExecuteCore method, after entity and module setup:
_uiGenerator = new UiFileGenerator(_resolver);

// Then later in ExecuteCore, after generating other files:
var uiEngine = ReadUiEngine();  // From .modulus.json
if (!string.IsNullOrEmpty(uiEngine) && uiEngine != "none")
{
    var uiFiles = await _uiGenerator.GenerateAsync(
        model, entity, uiEngine, presDir);
    
    if (uiFiles.Count > 0)
    {
        generated.AddRange(uiFiles);
    }
}
```

---

## 🎯 Step 6: Add Helper Method to Read UI Engine

Add this method to `GenerateCrudCommand`:

```csharp
private string? ReadUiEngine()
{
    var modulosJsonPath = Path.Combine(Directory.GetCurrentDirectory(), ".modulus.json");
    if (!File.Exists(modulosJsonPath))
        return null;

    try
    {
        var json = File.ReadAllText(modulosJsonPath);
        var doc = System.Text.Json.JsonDocument.Parse(json);
        
        if (doc.RootElement.TryGetProperty("uiEngine", out var engine))
        {
            return engine.GetString();
        }
    }
    catch
    {
        // Silently ignore
    }

    return null;
}
```

---

## ✅ Step 7: Build & Verify

```bash
cd E:\Personal\framework\modulus\cli
dotnet build -c Debug

# Expected: Build succeeded. 0 Warning(s) 0 Error(s)
```

---

## 🧪 Step 8: Manual Testing

Test Phase 3c end-to-end:

```bash
# Create test app
modulus app TestApp --ui-engine mvc

cd TestApp

# Generate CRUD (should now use templates!)
modulus generate-crud Customer --module Catalog

# Check output
ls -la Presentation/Views/Customer/
# Should show: Index.cshtml, Edit.cshtml, etc.

# Verify content
cat Presentation/Views/Customer/Index.cshtml
# Should have rendered template content, not raw Scriban
```

---

## 📊 Phase 3c Sign-Off Checklist

- [ ] TemplateContext.cs created
- [ ] ScribanRenderer.cs created
- [ ] UiFileGenerator.cs created
- [ ] Scriban NuGet package installed
- [ ] GenerateCrudCommand integrated
- [ ] Build succeeds: 0 errors
- [ ] Manual test creates UI files
- [ ] Generated files have rendered content (no {{ }} placeholders)
- [ ] Works for mvc engine
- [ ] Works for razor-pages engine
- [ ] Works for blazor engine

---

## 🚀 What Phase 3c Does

```
User: modulus generate-crud Customer --module Catalog
        ↓
Phase 3b: ResolveAsync("Modulus.Ui.Templates", "0.9.0")
        ↓ (returns cache path)
Phase 3c: UiFileGenerator.GenerateAsync()
        ├→ Load ui-kit.json manifest
        ├→ Get CRUD templates for engine
        ├→ Prepare TemplateContext (entity metadata)
        ├→ For each template:
        │  ├→ Read template file (.sbn)
        │  ├→ ScribanRenderer.Render() with context
        │  └→ Write rendered output
        └→ Return list of generated files
        ↓
Output: Presentation/Views/Customer/Index.cshtml (rendered!)
        Presentation/Views/Customer/Edit.cshtml (rendered!)
        Presentation/ApiClients/CustomerApiClient.cs
        Presentation/Menu/CustomersMenuContributor.cs
```

---

## ⚠️ Troubleshooting

### Issue: "No template found"
**Cause**: Template file name doesn't match  
**Fix**: Check GetTemplateFileName() logic matches actual .sbn files

### Issue: "{{ entity }}" in output (not rendered)
**Cause**: Scriban didn't render properly  
**Fix**: Check TemplateContext properties are set correctly

### Issue: "Could not resolve templates"
**Cause**: Phase 3b not working or cache invalid  
**Fix**: Run POC again, verify ~/.modulus/cache/ has files

---

## 📝 Files to Create

| File | Lines | Purpose |
|------|-------|---------|
| TemplateContext.cs | 45 | Template rendering context |
| ScribanRenderer.cs | 50 | Scriban template engine wrapper |
| UiFileGenerator.cs | 180 | UI file generation orchestration |

**Total**: ~275 lines of new code

---

## ⏱️ Timeline

```
Step 1 (TemplateContext):  10 min  █░░░░░░░░░
Step 2 (Scriban):          20 min  ██░░░░░░░░
Step 3 (Generator):        40 min  ████░░░░░░
Step 4 (NuGet):             5 min  █░░░░░░░░░
Step 5 (Integration):      30 min  ███░░░░░░░
Step 6 (Helper):           10 min  █░░░░░░░░░
Step 7 (Build):            10 min  █░░░░░░░░░
Step 8 (Test):             30 min  ███░░░░░░░
─────────────────────────────────────────────
Total:                    155 min  (~2.5 hours)
```

---

**Status**: 🟢 READY TO IMPLEMENT  
**Prerequisite**: ✅ Phase 3b complete  
**Next**: Phase 3d (Integration Testing)
