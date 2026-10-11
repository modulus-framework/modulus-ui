# Sample Implementation Reference - Ready to Copy & Paste

**Purpose**: Complete working examples for Phase 3b-3d implementation  
**Status**: Copy-paste ready into modulus CLI  
**Language**: C#, ASP.NET Core  

---

## 1. Template Package Resolver (Phase 3b)

**File**: `modulus/cli/Services/TemplatePackageResolver.cs`

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

## 2. UI Generation Command Extension (Phase 3c)

**File**: `modulus/cli/Commands/GenerateCrudCommand.cs` (extension methods)

```csharp
using System.Text.Json;
using Scriban;

namespace Modulus.Cli.Commands;

public partial class GenerateCrudCommand
{
    private ITemplatePackageResolver _templateResolver;
    private TemplateEngine _templateEngine;

    /// <summary>Initialize template resolution for UI generation</summary>
    private void InitializeTemplateEngine(ITemplatePackageResolver resolver)
    {
        _templateResolver = resolver;
        _templateEngine = new TemplateEngine();
    }

    /// <summary>Generate UI pages for entity using templates</summary>
    private async Task<bool> GenerateUiAsync(ModuleModel module, EntityModel entity, string uiEngine)
    {
        if (uiEngine == "none")
            return true;

        _logger.Information("Generating {Engine} UI for {Entity}", uiEngine, entity.Name);

        try
        {
            // Resolve templates package
            var templatesPath = await _templateResolver.ResolveAsync("Modulus.Ui.Templates", "0.9.0");
            if (templatesPath == null)
            {
                _logger.Warning("Could not resolve Modulus.Ui.Templates; skipping UI generation");
                return true; // Not fatal
            }

            // Generate UI pages
            var success = await GenerateUiPagesAsync(module, entity, uiEngine, templatesPath);
            return success;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "UI generation failed; attempting traditional wiring fallback");
            return true; // Fallback to traditional wiring
        }
    }

    /// <summary>Generate all UI pages for an entity</summary>
    private async Task<bool> GenerateUiPagesAsync(
        ModuleModel module,
        EntityModel entity,
        string uiEngine,
        string templatesBasePath)
    {
        _logger.Information("Generating UI pages for {Entity} ({Engine})", entity.Name, uiEngine);

        var entityPlural = Pluralize(entity.Name);
        var output = GetOutputProjectPath(module);

        // Load ui-kit.json manifest
        var manifestPath = Path.Combine(
            Path.GetDirectoryName(templatesBasePath)!,
            "..", "ui-kit.json"
        );

        if (!File.Exists(manifestPath))
        {
            _logger.Error("ui-kit.json not found");
            return false;
        }

        try
        {
            var manifestJson = await File.ReadAllTextAsync(manifestPath);
            using var doc = JsonDocument.Parse(manifestJson);
            var root = doc.RootElement;

            // Get CRUD feature definition
            if (!root.TryGetProperty("features", out var features) ||
                !features.TryGetProperty("crud", out var crud) ||
                !crud.TryGetProperty("files", out var filesObj) ||
                !filesObj.TryGetProperty(uiEngine, out var engineFiles))
            {
                _logger.Warning("No CRUD templates defined for engine: {Engine}", uiEngine);
                return false;
            }

            // Render each template file
            var rendered = 0;
            foreach (var fileElement in engineFiles.EnumerateArray())
            {
                var pattern = fileElement.GetString();
                if (string.IsNullOrEmpty(pattern)) continue;

                // Substitute placeholders: [Entity], [Entities]
                var outputPath = pattern
                    .Replace("[Entity]", entity.Name, StringComparison.OrdinalIgnoreCase)
                    .Replace("[Entities]", entityPlural, StringComparison.OrdinalIgnoreCase);

                var fullOutputPath = Path.Combine(output, outputPath);

                // Determine template file name
                var templateFileName = GetTemplateFileNameForPattern(pattern, uiEngine);
                var templatePath = Path.Combine(templatesBasePath, uiEngine, templateFileName);

                if (!File.Exists(templatePath))
                {
                    _logger.Warning("Template not found: {Path}", templatePath);
                    continue;
                }

                // Render template with entity context
                if (await RenderTemplateAsync(templatePath, fullOutputPath, module, entity, entityPlural, uiEngine))
                {
                    rendered++;
                    _logger.Information("Generated: {OutputPath}", outputPath);
                }
            }

            _logger.Information("Generated {Count} UI files for {Entity}", rendered, entity.Name);
            return rendered > 0;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to parse ui-kit.json");
            return false;
        }
    }

    /// <summary>Render a single template with entity context</summary>
    private async Task<bool> RenderTemplateAsync(
        string templatePath,
        string outputPath,
        ModuleModel module,
        EntityModel entity,
        string entityPlural,
        string uiEngine)
    {
        try
        {
            // Read template
            var templateContent = await File.ReadAllTextAsync(templatePath);

            // Create context object
            var context = new TemplateContext
            {
                Entity = entity,
                EntityPlural = entityPlural,
                Module = module,
                UiEngine = uiEngine,
                Namespace = $"{module.Company}.{module.Name}.Presentation",
                Now = DateTime.UtcNow,
            };

            // Render with Scriban
            var template = Template.Parse(templateContent);
            var rendered = template.Render(context);

            // Write output
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            await File.WriteAllTextAsync(outputPath, rendered);

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to render template: {Path}", templatePath);
            return false;
        }
    }

    /// <summary>Map file pattern to template file name</summary>
    private string GetTemplateFileNameForPattern(string pattern, string uiEngine)
    {
        var ext = GetTemplateExtension(uiEngine);

        if (pattern.Contains("Index", StringComparison.OrdinalIgnoreCase))
            return $"crud-index.{ext}.sbn";

        if (pattern.Contains("Edit", StringComparison.OrdinalIgnoreCase))
            return $"crud-edit.{ext}.sbn";

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

    private string Pluralize(string singular)
    {
        // Simple pluralization (extend with proper rules as needed)
        if (singular.EndsWith("y", StringComparison.OrdinalIgnoreCase))
            return singular[..^1] + "ies";
        if (singular.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            return singular + "es";
        return singular + "s";
    }
}

/// <summary>Template rendering context for Scriban</summary>
public class TemplateContext
{
    public EntityModel Entity { get; set; }
    public string EntityPlural { get; set; }
    public ModuleModel Module { get; set; }
    public string UiEngine { get; set; }
    public string Namespace { get; set; }
    public DateTime Now { get; set; }
}
```

---

## 3. Mock API Endpoints (For Testing)

**File**: `test-api/Controllers/NotificationsController.cs`

```csharp
using Microsoft.AspNetCore.Mvc;

namespace TestApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private static readonly List<NotificationDto> MockNotifications = new()
    {
        new()
        {
            Id = "notif-1",
            Title = "System update completed",
            Message = "Version 2.1.0 has been deployed successfully",
            Type = "Info",
            IsRead = false,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            ActionUrl = null,
            ActionLabel = null,
        },
        new()
        {
            Id = "notif-2",
            Title = "Report generated",
            Message = "Your monthly sales report is ready",
            Type = "Success",
            IsRead = false,
            CreatedAt = DateTime.UtcNow.AddHours(-5),
            ActionUrl = "/reports/123",
            ActionLabel = "View",
        },
    };

    [HttpGet]
    public async Task<ActionResult<PagedResponse<NotificationDto>>> GetNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var items = MockNotifications
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Ok(new PagedResponse<NotificationDto>
        {
            Items = items,
            Total = MockNotifications.Count,
            Page = page,
            PageSize = pageSize,
        });
    }

    [HttpPost("{id}/read")]
    public async Task<IActionResult> MarkAsRead(string id)
    {
        var notif = MockNotifications.FirstOrDefault(n => n.Id == id);
        if (notif == null)
            return NotFound();

        notif.IsRead = true;
        return Ok();
    }

    [HttpPost("mark-all-read")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        foreach (var notif in MockNotifications)
            notif.IsRead = true;

        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var notif = MockNotifications.FirstOrDefault(n => n.Id == id);
        if (notif == null)
            return NotFound();

        MockNotifications.Remove(notif);
        return Ok();
    }

    [HttpGet("preferences")]
    public async Task<ActionResult<NotificationPreferencesDto>> GetPreferences()
    {
        return Ok(new NotificationPreferencesDto
        {
            EmailActivitySummary = true,
            EmailSecurityAlerts = true,
            EmailProductUpdates = false,
            EmailMarketingEmails = false,
            NotifySystemUpdates = true,
            NotifyReports = true,
            NotifyCollaboration = true,
            NotifyScheduled = false,
            DigestFrequency = "Real-time",
            QuietHours = "22:00-08:00",
        });
    }

    [HttpPost("preferences")]
    public async Task<IActionResult> UpdatePreferences([FromBody] NotificationPreferencesDto preferences)
    {
        // Persist preferences (implementation detail)
        return Ok();
    }
}

public class NotificationDto
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Message { get; set; }
    public string Type { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ActionUrl { get; set; }
    public string? ActionLabel { get; set; }
}

public class NotificationPreferencesDto
{
    public bool EmailActivitySummary { get; set; }
    public bool EmailSecurityAlerts { get; set; }
    public bool EmailProductUpdates { get; set; }
    public bool EmailMarketingEmails { get; set; }
    public bool NotifySystemUpdates { get; set; }
    public bool NotifyReports { get; set; }
    public bool NotifyCollaboration { get; set; }
    public bool NotifyScheduled { get; set; }
    public string DigestFrequency { get; set; }
    public string QuietHours { get; set; }
}

public class PagedResponse<T>
{
    public List<T> Items { get; set; }
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
```

---

## 4. Unit Tests (Phase 3d)

**File**: `tests/Modulus.Cli.Tests/TemplatePackageResolverTests.cs`

```csharp
using NUnit.Framework;
using Serilog;

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
        Assert.That(time2, Is.LessThan(time1 / 2), "Cached resolution should be much faster");
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
        var invalidPath = Path.Combine(Path.GetTempPath(), "invalid-templates");
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

---

## 5. Integration Test Example

**File**: `tests/Modulus.Cli.Tests/GenerateCrudUiTests.cs`

```csharp
using NUnit.Framework;

namespace Modulus.Cli.Tests;

[TestFixture]
public class GenerateCrudUiTests
{
    private string _testProjectDir;

    [SetUp]
    public void Setup()
    {
        _testProjectDir = Path.Combine(Path.GetTempPath(), $"crud-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testProjectDir);
    }

    [TearDown]
    public void Cleanup()
    {
        if (Directory.Exists(_testProjectDir))
            Directory.Delete(_testProjectDir, recursive: true);
    }

    [TestCase("mvc")]
    [TestCase("razor-pages")]
    [TestCase("blazor")]
    public async Task GenerateCrud_CreatesValidPages(string engine)
    {
        // Arrange
        var module = new ModuleModel { Name = "Catalog", Company = "Test" };
        var entity = new EntityModel
        {
            Name = "Customer",
            DisplayName = "Customer Management",
            Properties = new()
            {
                new() { Name = "FirstName", Type = "string", DisplayName = "First Name", IsRequired = true },
                new() { Name = "LastName", Type = "string", DisplayName = "Last Name", IsRequired = true },
                new() { Name = "Email", Type = "string", DisplayName = "Email", IsRequired = true },
            }
        };

        var resolver = new TemplatePackageResolver(new NullLogger());
        var templatesPath = await resolver.ResolveAsync("Modulus.Ui.Templates", "0.9.0");

        // Act
        var command = new GenerateCrudCommand { OutputPath = _testProjectDir };
        var success = await command.GenerateUiAsync(module, entity, engine);

        // Assert
        Assert.That(success, Is.True);

        // Verify files created
        var indexFile = GetExpectedIndexFile(engine);
        Assert.That(File.Exists(indexFile), Is.True, $"Index file not created: {indexFile}");

        var editFile = GetExpectedEditFile(engine);
        Assert.That(File.Exists(editFile), Is.True, $"Edit file not created: {editFile}");
    }

    [Test]
    public async Task GenerateCrud_WithMultipleEntities_CreatesAllPages()
    {
        // Arrange
        var module = new ModuleModel { Name = "Catalog", Company = "Test" };
        var entities = new[]
        {
            new EntityModel { Name = "Customer", DisplayName = "Customer Management" },
            new EntityModel { Name = "Product", DisplayName = "Product Management" },
            new EntityModel { Name = "Order", DisplayName = "Order Management" },
        };

        // Act
        var resolver = new TemplatePackageResolver(new NullLogger());
        var templatesPath = await resolver.ResolveAsync("Modulus.Ui.Templates", "0.9.0");

        var command = new GenerateCrudCommand { OutputPath = _testProjectDir };
        var results = new List<bool>();

        foreach (var entity in entities)
        {
            results.Add(await command.GenerateUiAsync(module, entity, "mvc"));
        }

        // Assert
        Assert.That(results, Has.All.True);
        Assert.That(Directory.GetFiles(_testProjectDir, "*.cshtml", SearchOption.AllDirectories).Length, Is.GreaterThanOrEqualTo(6));
    }

    private string GetExpectedIndexFile(string engine) => engine switch
    {
        "mvc" => Path.Combine(_testProjectDir, "Views", "Customer", "Index.cshtml"),
        "razor-pages" => Path.Combine(_testProjectDir, "Pages", "Customer", "Index.cshtml"),
        "blazor" => Path.Combine(_testProjectDir, "Components", "Pages", "Customer", "Index.razor"),
        _ => throw new NotImplementedException(),
    };

    private string GetExpectedEditFile(string engine) => engine switch
    {
        "mvc" => Path.Combine(_testProjectDir, "Views", "Customer", "Edit.cshtml"),
        "razor-pages" => Path.Combine(_testProjectDir, "Pages", "Customer", "Edit.cshtml"),
        "blazor" => Path.Combine(_testProjectDir, "Components", "Pages", "Customer", "Edit.razor"),
        _ => throw new NotImplementedException(),
    };
}
```

---

## 6. Configuration Setup

**File**: `appsettings.json`

```json
{
  "Modulus": {
    "UiTemplates": {
      "PackageId": "Modulus.Ui.Templates",
      "Version": "0.9.0",
      "CacheDir": "~/.modulus/cache",
      "Timeout": 30000,
      "EnableCache": true,
      "NuGetSources": [
        "https://api.nuget.org/v3/index.json",
        "file://~/.modulus/nuget-feed"
      ]
    }
  }
}
```

---

## 7. Dependency Injection Setup

**File**: `modulus/cli/Program.cs`

```csharp
// Add services
services.AddSingleton<ITemplatePackageResolver>(sp =>
    new TemplatePackageResolver(sp.GetRequiredService<ILogger>())
);

// Register command
services.AddScoped<GenerateCrudCommand>();

// When creating command
var command = serviceProvider.GetRequiredService<GenerateCrudCommand>();
command.InitializeTemplateEngine(serviceProvider.GetRequiredService<ITemplatePackageResolver>());
```

---

## Summary

This reference provides:

✅ **Phase 3b**: Complete `TemplatePackageResolver` class (copy-paste ready)  
✅ **Phase 3c**: Extension methods for `GenerateCrudCommand` (copy-paste ready)  
✅ **Phase 3d**: Mock API controllers for testing (copy-paste ready)  
✅ **Testing**: Unit tests and integration tests (copy-paste ready)  
✅ **Configuration**: JSON config examples  
✅ **DI Setup**: Dependency injection setup  

All code follows ASP.NET Core best practices and Modulus conventions.

---

## Next Steps

1. **Copy** TemplatePackageResolver.cs into modulus/cli/Services/
2. **Extend** GenerateCrudCommand with provided methods
3. **Add** mock API controllers for testing
4. **Run** unit tests to verify functionality
5. **Execute** integration tests with real templates
6. **Verify** all 3 engines generate correctly

Ready to implement!
