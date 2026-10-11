# Phase 3c: Template Package Resolution & Rendering

**Objective**: Implement `ResolveTemplatesPackage()` and template rendering in `GenerateCrudCommand`

---

## Overview

Phase 3c implements the pipeline to:
1. Resolve `Modulus.Ui.Templates` NuGet package from configured sources
2. Extract .nupkg (ZIP file) to local cache (~/.modulus/cache/)
3. Load template files for the selected UI engine
4. Render templates with entity metadata
5. Write generated files to project

---

## Step 1: ResolveTemplatesPackage() Method

Add this to `GenerateCrudCommand` in `modulus` CLI:

```csharp
using System.IO.Compression;
using System.Xml.Linq;

private async Task<string?> ResolveTemplatesPackage(string packageId, string version)
{
    // Cache directory: ~/.modulus/cache/{packageId}/{version}/
    var cacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".modulus", "cache", packageId, version
    );
    var templatesDir = Path.Combine(cacheDir, "templates");

    // Return cached templates if they exist
    if (Directory.Exists(templatesDir) && Directory.EnumerateFiles(templatesDir).Any())
    {
        _logger.Information("Using cached templates: {TemplatesDir}", templatesDir);
        return templatesDir;
    }

    _logger.Information("Resolving NuGet package {PackageId} {Version}", packageId, version);

    // Try to find and extract .nupkg
    var nupkgPath = await FindNuGetPackageAsync(packageId, version);
    if (nupkgPath == null)
    {
        _logger.Error("Package not found: {PackageId} {Version}", packageId, version);
        return null;
    }

    // Extract .nupkg (it's a ZIP file)
    Directory.CreateDirectory(cacheDir);
    try
    {
        ZipFile.ExtractToDirectory(nupkgPath, cacheDir, overwriteFiles: true);
        _logger.Information("Extracted {PackageId} to {CacheDir}", packageId, cacheDir);
    }
    catch (Exception ex)
    {
        _logger.Error(ex, "Failed to extract {PackageId}", packageId);
        return null;
    }

    // Validate templates directory exists
    if (!Directory.Exists(templatesDir))
    {
        _logger.Error("Package does not contain templates/ directory: {TemplatesDir}", templatesDir);
        return null;
    }

    return templatesDir;
}
```

---

## Step 2: FindNuGetPackageAsync() Helper

```csharp
private async Task<string?> FindNuGetPackageAsync(string packageId, string version)
{
    var sources = GetNuGetSources();
    
    foreach (var source in sources)
    {
        var nupkgPath = await DownloadFromSourceAsync(source, packageId, version);
        if (File.Exists(nupkgPath))
        {
            return nupkgPath;
        }
    }

    // Try local file cache
    var globalPackagesFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".nuget", "packages", packageId.ToLowerInvariant(), version
    );
    
    var nupkgFile = Directory.GetFiles(globalPackagesFolder, "*.nupkg").FirstOrDefault();
    if (nupkgFile != null) return nupkgFile;

    return null;
}

private List<string> GetNuGetSources()
{
    var sources = new List<string>();
    
    // Read NuGet.config sources (or use defaults)
    var configPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".nuget", "NuGet", "NuGet.Config"
    );
    
    if (File.Exists(configPath))
    {
        try
        {
            var doc = XDocument.Load(configPath);
            var packageSources = doc.Root?.Element("packageSources")?.Elements("add");
            if (packageSources != null)
            {
                foreach (var source in packageSources)
                {
                    var value = source.Attribute("value")?.Value;
                    if (!string.IsNullOrEmpty(value))
                        sources.Add(value);
                }
            }
        }
        catch { /* ignore config parse errors */ }
    }

    // Default to nuget.org if no sources configured
    if (sources.Count == 0)
    {
        sources.Add("https://api.nuget.org/v3/index.json");
    }

    return sources;
}

private async Task<string?> DownloadFromSourceAsync(string source, string packageId, string version)
{
    var downloadPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".modulus", "cache", $"{packageId}.{version}.nupkg"
    );

    if (File.Exists(downloadPath)) return downloadPath;

    try
    {
        // Construct download URL (v3 API)
        var packageUrl = $"{source.TrimEnd('/')}/download/{packageId.ToLowerInvariant()}/{version}";
        
        using var client = new HttpClient();
        var response = await client.GetAsync(packageUrl);
        
        if (response.IsSuccessStatusCode)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(downloadPath)!);
            using var file = File.Create(downloadPath);
            await response.Content.CopyToAsync(file);
            return downloadPath;
        }
    }
    catch (Exception ex)
    {
        _logger.Debug(ex, "Failed to download from {Source}", source);
    }

    return null;
}
```

---

## Step 3: UI Template Rendering Logic

In `GenerateCrudCommand.GenerateAll()`:

```csharp
// After reading .modulus.json and extracting uiEngine
if (withUi && uiEngine != "none")
{
    _logger.Information("Generating UI for engine: {Engine}", uiEngine);

    var templatesPath = await ResolveTemplatesPackage("Modulus.Ui.Templates", "0.9.0");
    if (templatesPath == null)
    {
        _logger.Warning("Could not resolve Modulus.Ui.Templates; skipping UI generation");
        return;
    }

    // Generate UI pages for the entity
    await GenerateUiPagesAsync(module, entity, uiEngine, templatesPath);
}
```

---

## Step 4: GenerateUiPagesAsync() Implementation

```csharp
private async Task GenerateUiPagesAsync(
    ModuleModel module, 
    EntityModel entity, 
    string uiEngine, 
    string templatesBasePath)
{
    _logger.Information("Generating {Engine} UI pages for {Entity}", uiEngine, entity.Name);

    var entityPlural = Pluralize(entity.Name);
    var output = GetOutputProjectPath(module);

    // Load ui-kit.json manifest
    var manifestPath = Path.Combine(
        Path.GetDirectoryName(templatesBasePath)!,
        "..", "ui-kit.json"
    );
    
    if (!File.Exists(manifestPath))
    {
        _logger.Error("ui-kit.json not found at {Path}", manifestPath);
        return;
    }

    var manifestJson = await File.ReadAllTextAsync(manifestPath);
    var manifest = JsonDocument.Parse(manifestJson);

    // Get CRUD template definitions from manifest
    var crudFeature = manifest.RootElement.GetProperty("features").GetProperty("crud");
    var files = crudFeature.GetProperty("files").GetProperty(uiEngine);

    // Render each template
    var templateEngine = new ScribanTemplateEngine();
    
    foreach (var filePattern in files.EnumerateArray())
    {
        var pattern = filePattern.GetString();
        if (pattern == null) continue;

        // Substitute [Entity] and [Entities] placeholders
        var outputPath = pattern
            .Replace("[Entity]", entity.Name)
            .Replace("[Entities]", entityPlural);

        var fullOutputPath = Path.Combine(output, outputPath);
        
        // Find template file
        var templateName = GetTemplateNameForPattern(pattern, uiEngine);
        var templatePath = Path.Combine(templatesBasePath, uiEngine, templateName);

        if (!File.Exists(templatePath))
        {
            _logger.Warning("Template not found: {Path}", templatePath);
            continue;
        }

        // Prepare template context
        var context = new
        {
            Module = module,
            Entity = entity,
            EntityPlural = entityPlural,
            UiEngine = uiEngine,
            Namespace = $"{module.Company}.{module.Name}.Presentation",
            Now = DateTime.UtcNow,
        };

        // Render template
        try
        {
            var template = await File.ReadAllTextAsync(templatePath);
            var rendered = templateEngine.Render(template, context);

            // Write output file
            Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
            await File.WriteAllTextAsync(fullOutputPath, rendered);
            
            _logger.Information("Generated: {OutputPath}", outputPath);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to render template: {TemplatePath}", templatePath);
        }
    }

    _logger.Information("UI generation complete for {Entity}", entity.Name);
}

private string GetTemplateNameForPattern(string pattern, string uiEngine)
{
    // Pattern examples:
    // "Pages/[Entities]/Index.cshtml" → "crud-index.cshtml.sbn"
    // "Pages/[Entities]/Edit.cshtml" → "crud-edit.cshtml.sbn"
    // "Components/Pages/[Entities]/Index.razor" → "crud-index.razor.sbn"

    if (pattern.Contains("Index", StringComparison.OrdinalIgnoreCase))
        return $"crud-index.{GetTemplateExtension(uiEngine)}.sbn";
    
    if (pattern.Contains("Edit", StringComparison.OrdinalIgnoreCase))
        return $"crud-edit.{GetTemplateExtension(uiEngine)}.sbn";
    
    if (pattern.Contains("ApiClient", StringComparison.OrdinalIgnoreCase))
        return "api-client.cs.sbn";
    
    if (pattern.Contains("MenuContributor", StringComparison.OrdinalIgnoreCase))
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
```

---

## Step 5: Template Context Models

Ensure templates have access to:

```csharp
public record EntityModel
{
    public string Name { get; set; }           // "Customer"
    public string PluralName { get; set; }     // "Customers"
    public string DisplayName { get; set; }    // "Customer Management"
    public List<PropertyModel> Properties { get; set; } = [];
    public string Description { get; set; }
}

public record PropertyModel
{
    public string Name { get; set; }           // "FirstName"
    public string Type { get; set; }           // "string", "int", "datetime", etc.
    public string DisplayName { get; set; }    // "First Name"
    public bool IsRequired { get; set; }
    public bool IsSearchable { get; set; }
}

public record ModuleModel
{
    public string Name { get; set; }           // "Catalog"
    public string Company { get; set; }        // "Acme"
    public string Namespace { get; set; }      // "Acme.Catalog"
}
```

---

## Step 6: Error Handling & Fallback

```csharp
private async Task<bool> GenerateUiSafelyAsync(
    ModuleModel module,
    EntityModel entity,
    string uiEngine,
    string templatesPath)
{
    try
    {
        await GenerateUiPagesAsync(module, entity, uiEngine, templatesPath);
        return true;
    }
    catch (Exception ex)
    {
        _logger.Error(ex, "UI generation failed for {Entity}; falling back to traditional wiring", entity.Name);
        
        // Fallback: Use traditional UiCrudWiring
        await new UiCrudWiring(module, entity, uiEngine).GenerateAsync();
        return false;
    }
}
```

---

## Testing Checklist (Phase 3d)

- [ ] Unit test: `ResolveTemplatesPackage()` extracts correctly
- [ ] Unit test: `FindNuGetPackageAsync()` finds packages
- [ ] Unit test: Template context properly populated
- [ ] Integration test: MVC engine generates Index/Edit pages
- [ ] Integration test: Razor Pages engine generates with @page directives
- [ ] Integration test: Blazor engine generates with @code sections
- [ ] Integration test: Generated code compiles
- [ ] Integration test: Menu contributors auto-register
- [ ] End-to-end test: Full workflow from `modulus generate-crud` to running app

---

## Configuration

Add to `appsettings.json`:

```json
{
  "Modulus": {
    "UiTemplates": {
      "PackageId": "Modulus.Ui.Templates",
      "Version": "0.9.0",
      "CacheDir": "~/.modulus/cache",
      "Timeout": 30000
    }
  }
}
```

---

## Summary

Phase 3c implements the complete pipeline from NuGet package resolution through template rendering. The fallback to `UiCrudWiring` ensures backward compatibility if template resolution fails.

Ready for Phase 3d integration testing with all UI engines.
