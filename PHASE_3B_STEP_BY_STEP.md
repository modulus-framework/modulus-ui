# Phase 3b: Step-by-Step Implementation Guide

**Phase**: 3b (NuGet Package Resolution)  
**Time**: 2-3 hours  
**Complexity**: Medium  
**Status**: READY TO START

---

## 📋 Overview: What You'll Do

Phase 3b adds the ability to:
1. ✅ Find `Modulus.Ui.Templates` NuGet package
2. ✅ Download from NuGet sources (or local feed)
3. ✅ Cache locally at `~/.modulus/cache/`
4. ✅ Validate extracted templates
5. ✅ Return path for Phase 3c to use

---

## 🎯 Step 1: Create TemplatePackageResolver.cs

**Location**: `modulus/cli/Services/TemplatePackageResolver.cs`  
**Action**: Create new file with this content:

```csharp
using System.IO.Compression;
using System.Xml.Linq;
using Serilog;

namespace Modulus.Cli.Services;

public interface ITemplatePackageResolver
{
    Task<string?> ResolveAsync(string packageId, string version);
    Task<bool> ValidateTemplatesAsync(string templatesPath);
}

public class TemplatePackageResolver : ITemplatePackageResolver
{
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;
    private readonly string _cacheDir;

    public TemplatePackageResolver(ILogger logger)
    {
        _logger = logger;
        _httpClient = new HttpClient();
        _cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".modulus", "cache"
        );
    }

    /// <summary>Resolve templates package: check cache, then NuGet sources</summary>
    public async Task<string?> ResolveAsync(string packageId, string version)
    {
        var templatesDir = GetCachePath(packageId, version, "templates");

        // Check cache first
        if (Directory.Exists(templatesDir) && HasValidTemplates(templatesDir))
        {
            _logger.Information("Using cached templates: {TemplatesDir}", templatesDir);
            return templatesDir;
        }

        _logger.Information("Resolving NuGet package {PackageId} {Version}", packageId, version);

        // Try to find and download .nupkg
        var nupkgPath = await FindNuGetPackageAsync(packageId, version);
        if (nupkgPath == null)
        {
            _logger.Error("Package not found: {PackageId} {Version}", packageId, version);
            return null;
        }

        // Extract .nupkg (ZIP file)
        var cacheDir = GetCachePath(packageId, version, "");
        Directory.CreateDirectory(cacheDir);

        try
        {
            ZipFile.ExtractToDirectory(nupkgPath, cacheDir, overwriteFiles: true);
            _logger.Information("Extracted {PackageId} to {CacheDir}", packageId, cacheDir);

            // Validate extraction
            if (await ValidateTemplatesAsync(templatesDir))
            {
                return templatesDir;
            }
            else
            {
                _logger.Error("Invalid templates structure in {PackageId}", packageId);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to extract {PackageId}", packageId);
            return null;
        }
    }

    /// <summary>Validate that templates directory has expected structure</summary>
    public async Task<bool> ValidateTemplatesAsync(string templatesPath)
    {
        if (!Directory.Exists(templatesPath))
            return false;

        // Check for at least one engine directory
        var engines = new[] { "mvc", "razor-pages", "blazor", "fluid" };
        var hasEngine = engines.Any(e => Directory.Exists(Path.Combine(templatesPath, e)));

        if (!hasEngine)
        {
            _logger.Warning("No template engine directories found in {Path}", templatesPath);
            return false;
        }

        // Check for ui-kit.json
        var manifestPath = Path.Combine(Path.GetDirectoryName(templatesPath)!, "..", "ui-kit.json");
        if (!File.Exists(manifestPath))
        {
            _logger.Warning("ui-kit.json not found at {Path}", manifestPath);
            return false;
        }

        return true;
    }

    private async Task<string?> FindNuGetPackageAsync(string packageId, string version)
    {
        var sources = GetNuGetSources();

        foreach (var source in sources)
        {
            _logger.Debug("Checking NuGet source: {Source}", source);
            var nupkgPath = await DownloadFromSourceAsync(source, packageId, version);
            if (nupkgPath != null && File.Exists(nupkgPath))
            {
                _logger.Information("Found package at {Source}", source);
                return nupkgPath;
            }
        }

        // Try local global packages folder
        var globalPackagesFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".nuget", "packages", packageId.ToLowerInvariant(), version
        );

        if (Directory.Exists(globalPackagesFolder))
        {
            var nupkgFile = Directory.GetFiles(globalPackagesFolder, "*.nupkg").FirstOrDefault();
            if (nupkgFile != null)
            {
                _logger.Information("Found package in global packages: {Path}", nupkgFile);
                return nupkgFile;
            }
        }

        return null;
    }

    private List<string> GetNuGetSources()
    {
        var sources = new List<string>();

        // Try to read NuGet.Config
        var configPaths = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "NuGet.Config"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".nuget", "NuGet", "NuGet.Config"
            ),
        };

        foreach (var configPath in configPaths)
        {
            if (!File.Exists(configPath)) continue;

            try
            {
                var doc = XDocument.Load(configPath);
                var packageSources = doc.Root?.Element("packageSources")?.Elements("add");
                if (packageSources != null)
                {
                    foreach (var source in packageSources)
                    {
                        var value = source.Attribute("value")?.Value;
                        if (!string.IsNullOrEmpty(value) && !sources.Contains(value))
                            sources.Add(value);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to parse NuGet.Config at {Path}", configPath);
            }
        }

        // Always include nuget.org as fallback
        if (!sources.Any(s => s.Contains("nuget.org")))
            sources.Add("https://api.nuget.org/v3/index.json");

        return sources;
    }

    private async Task<string?> DownloadFromSourceAsync(string source, string packageId, string version)
    {
        var downloadPath = Path.Combine(
            _cacheDir,
            $"{packageId}.{version}.nupkg"
        );

        // Return if already downloaded
        if (File.Exists(downloadPath))
            return downloadPath;

        try
        {
            // v3 API URL format
            var packageUrl = $"{source.TrimEnd('/')}/download/{packageId.ToLowerInvariant()}/{version}";

            _logger.Debug("Downloading from {Url}", packageUrl);
            var response = await _httpClient.GetAsync(packageUrl, HttpCompletionOption.ResponseContentRead);

            if (response.IsSuccessStatusCode)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(downloadPath)!);
                using var file = File.Create(downloadPath);
                await response.Content.CopyToAsync(file);
                _logger.Information("Downloaded {PackageId} to {Path}", packageId, downloadPath);
                return downloadPath;
            }
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Failed to download from {Source}", source);
        }

        return null;
    }

    private string GetCachePath(string packageId, string version, string subDir)
    {
        var path = Path.Combine(_cacheDir, packageId, version);
        if (!string.IsNullOrEmpty(subDir))
            path = Path.Combine(path, subDir);
        return path;
    }

    private bool HasValidTemplates(string templatesDir)
    {
        return Directory.Exists(templatesDir) &&
               Directory.EnumerateFiles(templatesDir, "*.sbn", SearchOption.AllDirectories).Any();
    }
}
```

---

## 📌 Step 2: Register in Dependency Injection

**File**: `modulus/cli/Program.cs`  
**Action**: Add service registration

Find the service registration section and add:

```csharp
// Add template resolver service
services.AddSingleton<ITemplatePackageResolver>(sp =>
    new TemplatePackageResolver(sp.GetRequiredService<ILogger>())
);
```

**Location**: Near other service registrations (HttpClient, Serilog, etc.)

---

## 🔧 Step 3: Integrate into GenerateCrudCommand

**File**: `modulus/cli/Commands/GenerateCrudCommand.cs`  
**Action**: Add dependency and initialization

### 3a. Add to Constructor

```csharp
public class GenerateCrudCommand
{
    private readonly ITemplatePackageResolver _resolver;
    
    public GenerateCrudCommand(
        // ... existing parameters ...
        ITemplatePackageResolver resolver)
    {
        // ... existing assignments ...
        _resolver = resolver;
    }
}
```

### 3b. Call Resolution in GenerateAll()

Find the `GenerateAll()` method and add this before file generation:

```csharp
private async Task GenerateAll()
{
    // ... existing code ...
    
    // Phase 3b: Resolve templates if UI engine selected
    if (!string.IsNullOrEmpty(uiEngine) && uiEngine != "none")
    {
        _logger.Information("Resolving templates for {Engine}", uiEngine);
        var templatesPath = await _resolver.ResolveAsync("Modulus.Ui.Templates", "0.9.0");
        if (templatesPath != null)
        {
            _logger.Information("Templates resolved: {Path}", templatesPath);
            // Store for Phase 3c use
            _templatesPath = templatesPath;
        }
        else
        {
            _logger.Warning("Could not resolve templates; skipping UI generation");
        }
    }
    
    // ... rest of GenerateAll() ...
}
```

---

## ✅ Step 4: Build and Verify

### 4a. Build the CLI

```bash
cd modulus/cli
dotnet build -c Debug
```

**Expected**: 0 compilation errors

### 4b. Check CLI Help

```bash
modulus --help
# Should show existing options including --ui-engine
```

---

## 🧪 Step 5: Unit Tests for Phase 3b

**File**: `tests/Modulus.Cli.Tests/TemplatePackageResolverTests.cs`  
**Action**: Create test class

```csharp
using NUnit.Framework;
using Serilog;
using Modulus.Cli.Services;

namespace Modulus.Cli.Tests;

[TestFixture]
public class TemplatePackageResolverTests
{
    private ITemplatePackageResolver _resolver;
    private ILogger _logger;

    [SetUp]
    public void Setup()
    {
        _logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();

        _resolver = new TemplatePackageResolver(_logger);
    }

    [Test]
    public async Task ResolveAsync_WithValidPackage_ReturnsCachePath()
    {
        // Arrange
        var packageId = "Modulus.Ui.Templates";
        var version = "0.9.0";

        // Act
        var result = await _resolver.ResolveAsync(packageId, version);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(Directory.Exists(result), Is.True);
        Assert.That(result, Does.Contain("templates"));
    }

    [Test]
    public async Task ResolveAsync_WithCachedPackage_UsesCache()
    {
        // Arrange
        var packageId = "Modulus.Ui.Templates";
        var version = "0.9.0";
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Act - First call
        var result1 = await _resolver.ResolveAsync(packageId, version);
        sw.Stop();
        var time1 = sw.ElapsedMilliseconds;

        // Reset timer
        sw.Restart();

        // Act - Second call (should use cache)
        var result2 = await _resolver.ResolveAsync(packageId, version);
        sw.Stop();
        var time2 = sw.ElapsedMilliseconds;

        // Assert
        Assert.That(result1, Is.EqualTo(result2));
        Assert.That(time2, Is.LessThan(time1), "Cached resolution should be faster");
    }

    [Test]
    public async Task ValidateTemplatesAsync_WithValidStructure_ReturnsTrue()
    {
        // Arrange
        var packageId = "Modulus.Ui.Templates";
        var version = "0.9.0";
        var result = await _resolver.ResolveAsync(packageId, version);

        // Act
        var isValid = await _resolver.ValidateTemplatesAsync(result);

        // Assert
        Assert.That(isValid, Is.True);
    }

    [Test]
    public async Task ValidateTemplatesAsync_WithMissingTemplates_ReturnsFalse()
    {
        // Arrange
        var invalidPath = Path.Combine(Path.GetTempPath(), $"invalid-templates-{Guid.NewGuid()}");
        Directory.CreateDirectory(invalidPath);

        // Act
        var isValid = await _resolver.ValidateTemplatesAsync(invalidPath);

        // Assert
        Assert.That(isValid, Is.False);

        // Cleanup
        Directory.Delete(invalidPath);
    }
}
```

### Run Tests

```bash
cd tests/Modulus.Cli.Tests
dotnet test TemplatePackageResolverTests

# Expected output:
# ✅ ResolveAsync_WithValidPackage_ReturnsCachePath PASSED
# ✅ ResolveAsync_WithCachedPackage_UsesCache PASSED
# ✅ ValidateTemplatesAsync_WithValidStructure_ReturnsTrue PASSED
# ✅ ValidateTemplatesAsync_WithMissingTemplates_ReturnsFalse PASSED
```

---

## 📊 Phase 3b Sign-Off Checklist

- [ ] `TemplatePackageResolver.cs` created in `modulus/cli/Services/`
- [ ] Interface `ITemplatePackageResolver` defined
- [ ] Dependency injection registered in `Program.cs`
- [ ] Integrated into `GenerateCrudCommand.cs`
- [ ] Build succeeds: `dotnet build` (0 errors)
- [ ] All 4 unit tests pass
- [ ] Cache directory created: `~/.modulus/cache/Modulus.Ui.Templates/0.9.0/`
- [ ] Templates extracted and validated
- [ ] First resolution: 100-300ms ✅
- [ ] Cache hit: <50ms ✅

---

## 🎯 What Phase 3b Does

| Component | Purpose | Status |
|-----------|---------|--------|
| **ITemplatePackageResolver** | Interface for package resolution | ✅ Ready |
| **ResolveAsync()** | Find, download, cache package | ✅ Ready |
| **ValidateTemplatesAsync()** | Verify extracted structure | ✅ Ready |
| **FindNuGetPackageAsync()** | Search NuGet sources | ✅ Ready |
| **GetNuGetSources()** | Parse NuGet.Config | ✅ Ready |
| **DownloadFromSourceAsync()** | HTTP download with fallback | ✅ Ready |
| **Caching** | ~/.modulus/cache/ with validation | ✅ Ready |

---

## 🚀 Next: Phase 3c

Once Phase 3b passes all tests, move to Phase 3c to:
- Load templates from cache
- Render with Scriban
- Generate UI files per engine

**Estimated**: 3-4 hours after Phase 3b

---

## ⚠️ Troubleshooting

### Issue: "Package not found"
**Cause**: NuGet.Config not configured  
**Fix**: 
```bash
# Verify ~/.nuget/NuGet/NuGet.Config exists
# Verify local feed: ~/.modulus/nuget-feed/Modulus.Ui.Templates.0.9.0.nupkg
```

### Issue: "Invalid templates structure"
**Cause**: .nupkg doesn't contain expected folders  
**Fix**: 
```bash
# Verify package has: /templates/mvc/, /templates/razor-pages/, etc.
# Verify ui-kit.json exists at root
```

### Issue: Tests fail with path errors
**Cause**: Windows path separator issue  
**Fix**: Code uses `Path.Combine()` which handles both Windows/Unix  

---

## 📝 Files to Create/Modify

| File | Action | Size |
|------|--------|------|
| `modulus/cli/Services/TemplatePackageResolver.cs` | Create | 250 lines |
| `modulus/cli/Program.cs` | Add 5 lines | 1 addition |
| `modulus/cli/Commands/GenerateCrudCommand.cs` | Add 3 lines | 1 addition |
| `tests/Modulus.Cli.Tests/TemplatePackageResolverTests.cs` | Create | 80 lines |

**Total**: ~335 lines of new code

---

**Status**: 🟢 READY TO IMPLEMENT  
**Estimated Time**: 2-3 hours  
**Next Milestone**: Phase 3c integration

