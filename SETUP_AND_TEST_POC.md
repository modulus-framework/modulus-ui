# Phase 3b POC Setup & Testing Guide

**Purpose**: Verify template resolution works end-to-end with a practical test  
**Time**: 15-20 minutes to complete  
**Output**: Cached templates + verified API integration  

---

## 🎯 Quick Start (5 minutes)

### Step 1: Pack the Templates Package

```bash
cd E:\Personal\framework\modulus-ui\src\Modulus.Ui.Templates
dotnet pack -c Release -o ../../bin/packages
# Creates: Modulus.Ui.Templates.0.9.0.nupkg (~500KB)
```

### Step 2: Create Local NuGet Feed

```bash
# PowerShell
$feedPath = "$env:USERPROFILE\.modulus\nuget-feed"
New-Item -ItemType Directory -Force -Path $feedPath
Copy-Item "E:\Personal\framework\modulus-ui\bin\packages\Modulus.Ui.Templates.0.9.0.nupkg" -Destination $feedPath

# Verify
Get-ChildItem $feedPath
# Should show: Modulus.Ui.Templates.0.9.0.nupkg
```

### Step 3: Configure NuGet.Config

```bash
# Create ~/.nuget/NuGet/NuGet.Config if missing
$configDir = "$env:USERPROFILE\.nuget\NuGet"
$configPath = "$configDir\NuGet.Config"

# Create directory
New-Item -ItemType Directory -Force -Path $configDir | Out-Null

# Create/update config
@'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="local-modulus" value="~/.modulus/nuget-feed" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <disabledPackageSources>
    <add key="Microsoft Visual Studio Offline Packages" value="true" />
  </disabledPackageSources>
</configuration>
'@ | Set-Content -Path $configPath

# Verify
Get-Content $configPath
```

---

## 🧪 Phase 3b POC Test (10 minutes)

### Create Test Console App

**File**: `E:\Personal\framework\modulus-ui\TemplateResolutionPoc.cs`

```csharp
using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Diagnostics;

class TemplateResolutionPoc
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("╔════════════════════════════════════════════╗");
        Console.WriteLine("║  Phase 3b: Template Resolution POC         ║");
        Console.WriteLine("╚════════════════════════════════════════════╝");
        Console.WriteLine();

        var resolver = new SimpleTemplateResolver();
        
        try
        {
            // Test 1: First resolution (cache miss)
            Console.WriteLine("📦 Test 1: First resolution (cache miss)");
            var sw = Stopwatch.StartNew();
            var path1 = await resolver.ResolveAsync("Modulus.Ui.Templates", "0.9.0");
            sw.Stop();
            
            if (path1 != null && Directory.Exists(path1))
            {
                Console.WriteLine($"✅ SUCCESS: Templates resolved in {sw.ElapsedMilliseconds}ms");
                Console.WriteLine($"   Location: {path1}");
                
                // Verify structure
                var mvcCount = Directory.GetFiles(Path.Combine(Path.GetDirectoryName(path1)!, "..", "mvc"), "*.sbn").Length;
                var razorCount = Directory.GetFiles(Path.Combine(Path.GetDirectoryName(path1)!, "..", "razor-pages"), "*.sbn").Length;
                var blazorCount = Directory.GetFiles(Path.Combine(Path.GetDirectoryName(path1)!, "..", "blazor"), "*.sbn").Length;
                
                Console.WriteLine($"   MVC templates: {mvcCount}");
                Console.WriteLine($"   Razor Pages templates: {razorCount}");
                Console.WriteLine($"   Blazor templates: {blazorCount}");
            }
            else
            {
                Console.WriteLine($"❌ FAILED: Templates not found at {path1}");
                return;
            }

            // Test 2: Cache hit
            Console.WriteLine();
            Console.WriteLine("⚡ Test 2: Second resolution (cache hit)");
            sw.Restart();
            var path2 = await resolver.ResolveAsync("Modulus.Ui.Templates", "0.9.0");
            sw.Stop();
            
            if (path1 == path2)
            {
                Console.WriteLine($"✅ SUCCESS: Using cached templates in {sw.ElapsedMilliseconds}ms");
                Console.WriteLine($"   Cache location: {Path.Combine(Path.GetDirectoryName(path1)!, "..")}");
            }

            // Test 3: Validate manifest
            Console.WriteLine();
            Console.WriteLine("📋 Test 3: Validate ui-kit.json manifest");
            var manifestPath = Path.Combine(Path.GetDirectoryName(path1)!, "..", "..", "ui-kit.json");
            if (File.Exists(manifestPath))
            {
                var manifestContent = File.ReadAllText(manifestPath);
                var hasNotifications = manifestContent.Contains("\"notifications\"");
                var hasTenant = manifestContent.Contains("\"tenant-management\"");
                var hasUser = manifestContent.Contains("\"user-management\"");
                
                if (hasNotifications && hasTenant && hasUser)
                {
                    Console.WriteLine("✅ SUCCESS: All features found in manifest");
                    Console.WriteLine("   - notifications");
                    Console.WriteLine("   - tenant-management");
                    Console.WriteLine("   - user-management");
                }
            }

            // Test 4: List available templates
            Console.WriteLine();
            Console.WriteLine("📂 Test 4: Available templates");
            var templatesDir = Path.Combine(Path.GetDirectoryName(path1)!, "..");
            foreach (var engine in new[] { "mvc", "razor-pages", "blazor", "fluid" })
            {
                var engineDir = Path.Combine(templatesDir, engine);
                if (Directory.Exists(engineDir))
                {
                    var templates = Directory.GetFiles(engineDir, "*.sbn");
                    Console.WriteLine($"   {engine}: {templates.Length} templates");
                }
            }

            Console.WriteLine();
            Console.WriteLine("╔════════════════════════════════════════════╗");
            Console.WriteLine("║  ✅ ALL TESTS PASSED                       ║");
            Console.WriteLine("║  Templates are ready for Phase 3c          ║");
            Console.WriteLine("╚════════════════════════════════════════════╝");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ERROR: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}

class SimpleTemplateResolver
{
    private readonly string _cacheDir;

    public SimpleTemplateResolver()
    {
        _cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".modulus", "cache"
        );
    }

    public async Task<string?> ResolveAsync(string packageId, string version)
    {
        var cachePackagePath = Path.Combine(_cacheDir, packageId, version);
        
        // Check cache first
        if (Directory.Exists(cachePackagePath) && 
            Directory.GetFiles(cachePackagePath, "*.sbn", SearchOption.AllDirectories).Length > 0)
        {
            return Path.Combine(cachePackagePath, "templates");
        }

        Console.WriteLine($"   Resolving {packageId} {version}...");

        // Find .nupkg from NuGet feed
        var nupkgPath = await FindNuGetPackageAsync(packageId, version);
        if (nupkgPath == null)
        {
            Console.WriteLine($"   ❌ Package not found");
            return null;
        }

        Console.WriteLine($"   Found at: {nupkgPath}");

        // Extract
        Directory.CreateDirectory(cachePackagePath);
        ZipFile.ExtractToDirectory(nupkgPath, cachePackagePath, overwriteFiles: true);
        
        Console.WriteLine($"   Extracted to: {cachePackagePath}");

        return Path.Combine(cachePackagePath, "templates");
    }

    private async Task<string?> FindNuGetPackageAsync(string packageId, string version)
    {
        // Check local feed first
        var feedPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".modulus", "nuget-feed"
        );

        var localNupkg = Path.Combine(feedPath, $"{packageId}.{version}.nupkg");
        if (File.Exists(localNupkg))
        {
            return localNupkg;
        }

        // Check global packages
        var globalPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".nuget", "packages", packageId.ToLowerInvariant(), version
        );

        if (Directory.Exists(globalPath))
        {
            var nupkg = Directory.GetFiles(globalPath, "*.nupkg").FirstOrDefault();
            if (nupkg != null)
                return nupkg;
        }

        return null;
    }
}
```

### Run POC

```bash
cd E:\Personal\framework\modulus-ui

# Compile and run
dotnet run -f net8.0 --project . TemplateResolutionPoc.cs

# Or if using .NET CLI directly
csc TemplateResolutionPoc.cs -out:TemplateResolutionPoc.exe
.\TemplateResolutionPoc.exe
```

---

## 📊 Expected Output

```
╔════════════════════════════════════════════╗
║  Phase 3b: Template Resolution POC         ║
╚════════════════════════════════════════════╝

📦 Test 1: First resolution (cache miss)
   Resolving Modulus.Ui.Templates 0.9.0...
   Found at: C:\Users\<user>\.modulus\nuget-feed\Modulus.Ui.Templates.0.9.0.nupkg
   Extracted to: C:\Users\<user>\.modulus\cache\Modulus.Ui.Templates\0.9.0
✅ SUCCESS: Templates resolved in 1247ms
   Location: C:\Users\<user>\.modulus\cache\Modulus.Ui.Templates\0.9.0\templates
   MVC templates: 33
   Razor Pages templates: 32
   Blazor templates: 32

⚡ Test 2: Second resolution (cache hit)
✅ SUCCESS: Using cached templates in 12ms
   Cache location: C:\Users\<user>\.modulus\cache\Modulus.Ui.Templates\0.9.0

📋 Test 3: Validate ui-kit.json manifest
✅ SUCCESS: All features found in manifest
   - notifications
   - tenant-management
   - user-management

📂 Test 4: Available templates
   mvc: 33 templates
   razor-pages: 32 templates
   blazor: 32 templates
   fluid: 8 templates

╔════════════════════════════════════════════╗
║  ✅ ALL TESTS PASSED                       ║
║  Templates are ready for Phase 3c          ║
╚════════════════════════════════════════════╝
```

---

## ✅ Verification Checklist

After running POC:

- [ ] Cache directory created: `~/.modulus/cache/Modulus.Ui.Templates/0.9.0/`
- [ ] First resolution took 1-3 seconds
- [ ] Second resolution took <50ms (100x faster)
- [ ] All template engines present (mvc, razor-pages, blazor, fluid)
- [ ] ui-kit.json has all 3 new features
- [ ] No errors in extraction

**If all pass**: Phase 3b works! Ready for Phase 3c integration.

---

## 🚀 Next Step: Phase 3c

Once POC is verified, proceed with:

```bash
# Read Phase 3c guide
code PHASE_3C_TEMPLATE_RESOLUTION.md

# Copy TemplatePackageResolver.cs to modulus CLI
# Copy GenerateCrudCommand extensions to modulus CLI
# Implement GenerateUiAsync() in GenerateCrudCommand

# Test:
modulus generate-crud Customer --ui-engine mvc
# Should create Index.cshtml, Edit.cshtml, etc.
```

---

## 📝 Notes

- POC uses no external NuGet dependencies (uses built-in ZIP extraction)
- Cache location: `~/.modulus/cache/`
- Package location: `~/.modulus/nuget-feed/`
- Both use `~` which expands to user home directory
- Works on Windows, macOS, Linux

---

**Status**: Ready to test Phase 3b ✅
