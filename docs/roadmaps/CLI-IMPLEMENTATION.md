---
name: cli-implementation-roadmap
description: Comprehensive roadmap for implementing all UI CLI commands
metadata:
  node_type: memory
  type: project
  created: 2026-10-10
  originSessionId: 88b0729e-9258-455d-b28b-b0d8f1f35b1d
  modified: 2026-10-10T11:46:47.466Z
---

# Modulus UI CLI Implementation Roadmap

> **Status (2026-10-10).** Built in `modulus/cli`: `ui theme create|set|list|export`, `ui add-component`, `ui create-dashboard`,
> `ui create-form-from-entity`, `ui add-auth`, `ui add-2fa`, `ui add-session-manager`, `ui add-chart`, the template package
> (`Modulus.Ui.Templates`) with a cache/local-feed/nuget.org resolver, and UI package references in new apps.
> The commands use Spectre.Console.Cli (the code samples below are pseudocode). See [../CLI.md](../CLI.md) for the reference
> and for what is still open (charts, chat, audit, permission matrix and others).


## Part 1: Architecture Overview

### Current CLI Structure

```
modulus/cli/
├── Commands/
│   ├── NewAppCommand.cs          [✅ Has --ui-engine]
│   ├── GenerateCrudCommand.cs     [✅ Reads .modulus.json]
│   ├── UiAddCommand.cs            [🟡 Needs enhancement]
│   ├── UiRemoveCommand.cs
│   └── ...
├── Services/
│   ├── TemplateEngine.cs          [✅ Renders templates]
│   ├── NuGetVersionService.cs     [✅ Resolves packages]
│   ├── Models.cs                  [✅ Has AppModel.UiEngine]
│   └── ...
├── Templates/                     [App scaffolding templates]
└── Program.cs                     [Main entry point]
```

### What Needs to Happen

1. **Extend UiAddCommand** → `modulus ui add-*` subcommands
2. **Create UiThemeCommand** → `modulus ui theme *` subcommands
3. **Create UiGenerateCommand** → `modulus ui create-*` subcommands
4. **Create UiAuditCommand** → `modulus ui audit` and compliance commands
5. **Update Program.cs** → Register all new commands

---

## Part 2: Step-by-Step Implementation Plan

### STEP 1: Refactor UiAddCommand into Command Family

**Current Structure** (single flat command)
```
modulus ui add <template-name>
```

**Target Structure** (nested subcommands)
```
modulus ui add-chart --type line
modulus ui add-chat --provider claude
modulus ui add-form-builder
modulus ui add-2fa --type totp
```

**Implementation**:

```csharp
// File: cli/Commands/Ui/UiCommand.cs (base)
public class UiCommand : Command
{
    public override int Execute(CommandContext context)
    {
        throw new NotImplementedException("Use subcommands: ui add-*, ui theme, ui create-*, ui audit");
    }
}

// File: cli/Commands/Ui/UiAddCommand.cs (renamed from old UiAddCommand)
public class UiAddCommand : Command
{
    private readonly string _featureType; // "chart", "chat", "form", etc.
    
    public override string Description => $"Add {_featureType} to your app";
    
    public UiAddCommand(string featureType)
    {
        _featureType = featureType;
        AddOption("--engine", "UI engine override");
        AddOption("--module", "Module to add to");
        AddOption("--template", "Template variant");
        AddOption("--with-example", "Include example");
    }
}

// File: cli/Commands/Ui/Chart/UiAddChartCommand.cs
public class UiAddChartCommand : UiAddCommand
{
    public UiAddChartCommand() : base("chart")
    {
        AddOption("--type", "Chart type: line, bar, pie, scatter, heatmap, timeline");
        AddOption("--data-source", "How to get data: endpoint, static, example");
    }
    
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var engine = context.GetOption("--engine");
        var type = context.GetOption("--type") ?? "line";
        var module = context.GetOption("--module") ?? "default";
        
        // 1. Resolve UI engine from .modulus.json if not provided
        engine = await ResolveEngine(engine);
        
        // 2. Resolve template package
        var templatePath = await ResolveTemplate("Modulus.Ui.Templates", "0.9.0", 
            $"charts/chart-{type}");
        
        // 3. Render template with context
        var context = new
        {
            ChartType = type,
            ModuleName = module,
            DataEndpoint = $"/api/{module}/chart-data",
            SampleData = GetSampleDataForChart(type)
        };
        
        await _templateEngine.RenderToFile(templatePath, context, outputPath);
        
        // 4. Update project files if needed
        await UpdateProjectFiles(engine, module);
        
        Ux.Success($"Chart template added to {module}");
        return 0;
    }
}

// File: cli/Commands/Ui/Chat/UiAddChatCommand.cs
public class UiAddChatCommand : UiAddCommand
{
    public UiAddChatCommand() : base("chat")
    {
        AddOption("--provider", "AI provider: claude, openai, custom");
        AddOption("--model", "Model to use");
    }
    
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var provider = context.GetOption("--provider") ?? "claude";
        var model = context.GetOption("--model") ?? "claude-3.5-sonnet";
        
        // Resolve and render chat templates
        // Scaffold API endpoint for chat
        // Create example integration
    }
}

// Repeat for: Form, 2FA, Collaboration, etc.
```

**Commands to Create**:
- `cli/Commands/Ui/Chart/UiAddChartCommand.cs`
- `cli/Commands/Ui/Chat/UiAddChatCommand.cs`
- `cli/Commands/Ui/Forms/UiAddFormBuilderCommand.cs`
- `cli/Commands/Ui/Auth/UiAddAuthCommand.cs`
- `cli/Commands/Ui/Auth/UiAdd2FaCommand.cs`
- `cli/Commands/Ui/Collaboration/UiAddCollaborationCommand.cs`
- `cli/Commands/Ui/Mobile/UiAddPwaCommand.cs`
- ... (12+ total)

---

### STEP 2: Create Theme Management Command Family

**File Structure**:
```
cli/Commands/Ui/Theme/
├── UiThemeCommand.cs (base)
├── UiThemeCreateCommand.cs
├── UiThemeSetCommand.cs
├── UiThemeListCommand.cs
├── UiThemeExportCommand.cs
└── UiThemeBuilderCommand.cs
```

**Implementation**:

```csharp
// cli/Commands/Ui/Theme/UiThemeCommand.cs
public class UiThemeCommand : Command
{
    public UiThemeCommand()
    {
        AddCommand("create", new UiThemeCreateCommand());
        AddCommand("set", new UiThemeSetCommand());
        AddCommand("list", new UiThemeListCommand());
        AddCommand("export", new UiThemeExportCommand());
        AddCommand("builder", new UiThemeBuilderCommand());
    }
}

// cli/Commands/Ui/Theme/UiThemeCreateCommand.cs
public class UiThemeCreateCommand : Command
{
    public UiThemeCreateCommand()
    {
        AddArgument("name", "Theme name");
        AddOption("--base", "Base theme to extend: tabler, minimal, custom");
        AddOption("--colors", "Color palette: auto-generate from base color");
    }
    
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var name = context.GetArgument("name");
        var baseTheme = context.GetOption("--base") ?? "tabler";
        
        // 1. Create theme directory
        var themeDir = Directory.CreateDirectory($"./Themes/{name}");
        
        // 2. Generate CSS variables file
        var cssVars = GenerateCssVariables(baseTheme);
        File.WriteAllText($"{themeDir}/variables.css", cssVars);
        
        // 3. Create theme manifest
        var manifest = new
        {
            name = name,
            baseTheme = baseTheme,
            created = DateTime.UtcNow,
            colors = new { /* ... */ }
        };
        File.WriteAllText($"{themeDir}/theme.json", JsonSerializer.Serialize(manifest));
        
        // 4. Add to project
        await RegisterThemeInProject(name);
        
        Ux.Success($"Theme '{name}' created");
        return 0;
    }
}

// cli/Commands/Ui/Theme/UiThemeSetCommand.cs
public class UiThemeSetCommand : Command
{
    public UiThemeSetCommand()
    {
        AddArgument("name", "Theme name");
    }
    
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var name = context.GetArgument("name");
        
        // Update appsettings.json or Program.cs
        // Set active theme globally
        await UpdateAppSettings(name);
        
        Ux.Success($"Active theme set to '{name}'");
        return 0;
    }
}

// cli/Commands/Ui/Theme/UiThemeExportCommand.cs
public class UiThemeExportCommand : Command
{
    public UiThemeExportCommand()
    {
        AddArgument("name", "Theme name");
        AddOption("--format", "Export format: css, scss, tailwind, json");
    }
    
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var name = context.GetArgument("name");
        var format = context.GetOption("--format") ?? "css";
        
        // 1. Load theme manifest
        var theme = LoadTheme(name);
        
        // 2. Convert to requested format
        var output = format switch
        {
            "css" => ConvertToCss(theme),
            "scss" => ConvertToScss(theme),
            "tailwind" => ConvertToTailwind(theme),
            "json" => JsonSerializer.Serialize(theme),
            _ => throw new InvalidOperationException($"Unknown format: {format}")
        };
        
        // 3. Export to file
        File.WriteAllText($"./{name}.{format}", output);
        
        Ux.Success($"Theme exported to {name}.{format}");
        return 0;
    }
}
```

**Commands to Create**:
- `cli/Commands/Ui/Theme/UiThemeCreateCommand.cs`
- `cli/Commands/Ui/Theme/UiThemeSetCommand.cs`
- `cli/Commands/Ui/Theme/UiThemeListCommand.cs`
- `cli/Commands/Ui/Theme/UiThemeExportCommand.cs`
- `cli/Commands/Ui/Theme/UiThemeBuilderCommand.cs`

---

### STEP 3: Create Generation Commands

**File Structure**:
```
cli/Commands/Ui/Generate/
├── UiGenerateCommand.cs (base)
├── UiCreateUserDashboardCommand.cs
├── UiCreatePermissionMatrixCommand.cs
├── UiCreateFormFromEntityCommand.cs
├── UiCreateDashboardCommand.cs
└── ...
```

**Implementation**:

```csharp
// cli/Commands/Ui/Generate/UiCreateUserDashboardCommand.cs
public class UiCreateUserDashboardCommand : Command
{
    public UiCreateUserDashboardCommand()
    {
        AddOption("--engine", "UI engine override");
        AddOption("--module", "Module name: default 'Identity'");
        AddOption("--with-audit", "Include audit trail UI");
        AddOption("--with-2fa", "Include 2FA management");
    }
    
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var engine = await ResolveEngine(context.GetOption("--engine"));
        var module = context.GetOption("--module") ?? "Identity";
        var withAudit = context.GetFlag("--with-audit");
        var with2fa = context.GetFlag("--with-2fa");
        
        // 1. Resolve all required templates
        var templates = new[]
        {
            "users/users-list",
            "users/user-detail",
            "users/user-permissions",
            withAudit ? "audit/user-activity-log" : null,
            with2fa ? "auth/2fa-setup" : null
        }.Where(t => t != null).ToArray();
        
        // 2. Render each template
        foreach (var template in templates)
        {
            var path = await ResolveTemplate("Modulus.Ui.Templates", "0.9.0", template);
            await _templateEngine.RenderToFile(path, 
                new { ModuleName = module }, 
                outputPath);
        }
        
        // 3. Create controller/endpoints if needed
        await GenerateUserApiEndpoints(module, engine);
        
        // 4. Update navigation
        await AddToNavigation(module, "User Management", "/Identity/Users");
        
        Ux.Success($"User dashboard created in {module}");
        return 0;
    }
}

// cli/Commands/Ui/Generate/UiCreateFormFromEntityCommand.cs
public class UiCreateFormFromEntityCommand : Command
{
    public UiCreateFormFromEntityCommand()
    {
        AddArgument("entity", "Entity name (e.g., 'User', 'Order')");
        AddOption("--module", "Module containing entity");
        AddOption("--engine", "UI engine override");
        AddOption("--form-type", "Form type: create, edit, both (default)");
    }
    
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var entity = context.GetArgument("entity");
        var module = context.GetOption("--module") ?? "default";
        var engine = await ResolveEngine(context.GetOption("--engine"));
        var formType = context.GetOption("--form-type") ?? "both";
        
        // 1. Read entity metadata from .modulus.json or reflection
        var entityMeta = await GetEntityMetadata(entity, module);
        
        // 2. Generate form schema from entity properties
        var formSchema = GenerateFormSchema(entityMeta);
        
        // 3. Render form template with schema embedded
        var context = new
        {
            Entity = entity,
            Properties = entityMeta.Properties,
            FormSchema = JsonSerializer.Serialize(formSchema)
        };
        
        if (formType == "create" || formType == "both")
        {
            await RenderTemplate("forms/entity-form-create", context);
        }
        
        if (formType == "edit" || formType == "both")
        {
            await RenderTemplate("forms/entity-form-edit", context);
        }
        
        Ux.Success($"Form for {entity} created");
        return 0;
    }
}

// cli/Commands/Ui/Generate/UiCreateDashboardCommand.cs
public class UiCreateDashboardCommand : Command
{
    public UiCreateDashboardCommand()
    {
        AddArgument("template", "Dashboard template: analytics, sales, operations, custom");
        AddOption("--engine", "UI engine override");
        AddOption("--module", "Module name");
    }
    
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var template = context.GetArgument("template");
        var engine = await ResolveEngine(context.GetOption("--engine"));
        var module = context.GetOption("--module") ?? "Analytics";
        
        // Resolve and render complete dashboard
        // Include all charts, metrics, real-time feeds
        
        await RenderTemplate($"dashboards/{template}-dashboard", 
            new { ModuleName = module });
        
        Ux.Success($"Dashboard '{template}' created");
        return 0;
    }
}
```

**Commands to Create**:
- `cli/Commands/Ui/Generate/UiCreateUserDashboardCommand.cs`
- `cli/Commands/Ui/Generate/UiCreatePermissionMatrixCommand.cs`
- `cli/Commands/Ui/Generate/UiCreateFormFromEntityCommand.cs`
- `cli/Commands/Ui/Generate/UiCreateDashboardCommand.cs`
- ... (8+ total)

---

### STEP 4: Create Audit & Compliance Commands

**File Structure**:
```
cli/Commands/Ui/Audit/
├── UiAuditCommand.cs (base)
├── UiAuditTemplatesCommand.cs
├── UiAddAuditViewerCommand.cs
├── UiAddAccessReviewCommand.cs
└── ...
```

**Implementation**:

```csharp
// cli/Commands/Ui/Audit/UiAuditCommand.cs
public class UiAuditCommand : Command
{
    public UiAuditCommand()
    {
        AddOption("--module", "Audit specific module");
        AddOption("--level", "Compliance level: A, AA, AAA (WCAG)");
        AddOption("--output", "Report format: json, html, markdown");
    }
    
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var module = context.GetOption("--module");
        var level = context.GetOption("--level") ?? "AA";
        var output = context.GetOption("--output") ?? "html";
        
        // 1. Scan all templates for compliance issues
        var issues = new List<ComplianceIssue>();
        
        issues.AddRange(AuditColorContrast(level));
        issues.AddRange(AuditAriaLabels());
        issues.AddRange(AuditKeyboardNav());
        issues.AddRange(AuditSemanticHtml());
        
        // 2. Generate report
        var report = GenerateComplianceReport(issues, level);
        
        // 3. Export in requested format
        var reportFile = $"accessibility-audit.{GetExtension(output)}";
        File.WriteAllText(reportFile, report);
        
        Ux.Success($"Audit report: {reportFile} ({issues.Count} issues found)");
        return 0;
    }
}

// cli/Commands/Ui/Audit/UiAddAccessReviewCommand.cs
public class UiAddAccessReviewCommand : Command
{
    public UiAddAccessReviewCommand()
    {
        AddOption("--engine", "UI engine override");
        AddOption("--module", "Module for review");
    }
    
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var engine = await ResolveEngine(context.GetOption("--engine"));
        var module = context.GetOption("--module") ?? "Identity";
        
        // Scaffold access review workflow
        // Forms for reviewers to approve/reject access
    }
}
```

**Commands to Create**:
- `cli/Commands/Ui/Audit/UiAuditCommand.cs`
- `cli/Commands/Ui/Audit/UiAddAccessReviewCommand.cs`
- `cli/Commands/Ui/Audit/UiAddAuditViewerCommand.cs`

---

### STEP 5: Register All Commands in Program.cs

```csharp
// cli/Program.cs

var app = new CommandApp();

// Existing commands
app.AddCommand<NewAppCommand>("app");
app.AddCommand<GenerateCrudCommand>("generate-crud");

// UI command family
var uiCommand = new Command("ui");

// Add UI subcommands
uiCommand.AddCommand("add-chart", new UiAddChartCommand());
uiCommand.AddCommand("add-chat", new UiAddChatCommand());
uiCommand.AddCommand("add-form-builder", new UiAddFormBuilderCommand());
uiCommand.AddCommand("add-auth", new UiAddAuthCommand());
uiCommand.AddCommand("add-2fa", new UiAdd2FaCommand());
uiCommand.AddCommand("add-collaboration", new UiAddCollaborationCommand());
uiCommand.AddCommand("add-pwa", new UiAddPwaCommand());
uiCommand.AddCommand("add-i18n", new UiAddI18nCommand());

// Theme subcommands
uiCommand.AddCommand("theme", new UiThemeCommand()
    .AddCommand("create", new UiThemeCreateCommand())
    .AddCommand("set", new UiThemeSetCommand())
    .AddCommand("list", new UiThemeListCommand())
    .AddCommand("export", new UiThemeExportCommand())
);

// Generate subcommands
uiCommand.AddCommand("create-user-dashboard", new UiCreateUserDashboardCommand());
uiCommand.AddCommand("create-permission-matrix", new UiCreatePermissionMatrixCommand());
uiCommand.AddCommand("create-form-from-entity", new UiCreateFormFromEntityCommand());
uiCommand.AddCommand("create-dashboard", new UiCreateDashboardCommand());

// Audit subcommands
uiCommand.AddCommand("audit", new UiAuditCommand());
uiCommand.AddCommand("add-access-review", new UiAddAccessReviewCommand());

app.AddCommand(uiCommand);

// Result: all commands accessible as:
// modulus ui add-chart
// modulus ui theme create
// modulus ui create-user-dashboard
// modulus ui audit
```

---

## Part 3: Implementation Timeline

### Week 1-2: Foundation (Steps 1-2)
- [ ] Create `UiAddCommand` family structure
- [ ] Implement 5 chart commands (`UiAddChartCommand`, etc.)
- [ ] Implement theme command family
- [ ] Update Program.cs to wire everything
- [ ] Test end-to-end: `modulus ui add-chart --type line`
- [ ] **Deliverable**: Charts + themes working

### Week 3-4: Generation (Steps 3-4)
- [ ] Create generation command family
- [ ] Implement `UiCreateUserDashboardCommand`
- [ ] Implement `UiCreateFormFromEntityCommand`
- [ ] Implement `UiCreateDashboardCommand`
- [ ] Add audit/compliance commands
- [ ] **Deliverable**: Dashboard + form generation working

### Week 5: Auth Integration
- [ ] Implement `UiAddAuthCommand`, `UiAdd2FaCommand`
- [ ] Implement user management commands
- [ ] Scaffold identity endpoints
- [ ] **Deliverable**: Full auth UI generation

### Week 6: Polish & Testing
- [ ] Add --json flag to all commands
- [ ] Add --dry-run flag to all commands
- [ ] Create integration tests
- [ ] Write documentation
- [ ] **Deliverable**: Production-ready CLI

---

## Part 4: Code Sharing Strategy

To avoid duplicating template resolution logic, create a base class:

```csharp
// cli/Services/UiCommandBase.cs
public abstract class UiCommandBase : Command
{
    protected ITemplateEngine _templateEngine;
    protected INuGetVersionService _nugetService;
    protected IModulusSettings _settings;
    
    protected async Task<string> ResolveEngine(string? engineOverride)
    {
        // Read from .modulus.json, or use override, or prompt
        return engine;
    }
    
    protected async Task<string> ResolveTemplate(
        string packageId, 
        string version, 
        string templateName)
    {
        // Resolve Modulus.Ui.Templates package
        // Extract to cache
        // Return path to template
    }
    
    protected async Task RenderTemplate(string templatePath, object context)
    {
        // Use TemplateEngine to render
    }
    
    protected async Task UpdateProjectFiles(string engine, string module)
    {
        // Update .csproj, Program.cs, navigation, etc.
    }
}

// All command classes inherit from UiCommandBase
public class UiAddChartCommand : UiCommandBase { ... }
public class UiThemeCreateCommand : UiCommandBase { ... }
// etc.
```

---

## Part 5: Testing Strategy

### Unit Tests
```csharp
[Theory]
[InlineData("mvc")]
[InlineData("razor-pages")]
[InlineData("blazor")]
[InlineData("fluid")]
public async Task UiAddChart_GeneratesCorrectTemplate_ForAllEngines(string engine)
{
    var cmd = new UiAddChartCommand();
    var result = await cmd.ExecuteAsync(new CommandContext
    {
        Options = new Dictionary<string, object>
        {
            { "--engine", engine },
            { "--type", "line" }
        }
    });
    
    Assert.Equal(0, result);
    Assert.FileExists("./Charts/line-chart.cshtml"); // or .razor, .liquid
}
```

### Integration Tests
```csharp
[Fact]
public async Task FullWorkflow_CreatesCompleteApp()
{
    // 1. Create app
    await RunCommand("modulus app DemoApp --ui-engine mvc");
    
    // 2. Add chart
    await RunCommand("modulus ui add-chart --type line");
    
    // 3. Build
    var buildResult = await DotnetBuild();
    Assert.True(buildResult.Success);
    
    // 4. Verify template was rendered correctly
    var chartFile = File.ReadAllText("./Charts/line-chart.cshtml");
    Assert.Contains("Chart.js", chartFile);
}
```

### Acceptance Tests
```bash
# Test all commands
modulus ui add-chart --type bar
modulus ui add-chat --provider claude
modulus ui add-form-builder
modulus ui theme create my-theme
modulus ui theme set my-theme
modulus ui create-user-dashboard
modulus ui create-permission-matrix User
modulus ui audit --level AA

# Verify app builds
dotnet build

# Verify pages load
dotnet run
# Check http://localhost:5000/charts
# Check http://localhost:5000/themes
# etc.
```

---

## Part 6: CLI Command Reference (Final)

```bash
# CHART & DATA VISUALIZATION
modulus ui add-chart --type {line|bar|pie|scatter|heatmap|timeline} [--module]
modulus ui add-dashboard --template {analytics|sales|operations}

# AI & INTELLIGENT FEATURES
modulus ui add-chat [--provider {claude|openai}] [--module]
modulus ui add-code-editor --type {monaco|ace} [--module]
modulus ui add-assistant --template {customer-support|dev-tools}

# COLLABORATION & REAL-TIME
modulus ui add-collaboration --type {editor|comments|presence} [--module]
modulus ui add-notifications [--module]

# FORMS & INPUT
modulus ui add-form-builder [--module]
modulus ui create-form-from-entity <Entity> [--module] [--form-type {create|edit|both}]
modulus ui add-wizard [--module]

# AUTH & IDENTITY
modulus ui add-auth --flow {login|registration|both}
modulus ui add-2fa --type {totp|sms}
modulus ui create-user-dashboard [--with-audit] [--with-2fa]
modulus ui create-permission-matrix <Entity> [--module]
modulus ui add-role-builder [--module]
modulus ui add-session-manager [--module]

# THEME MANAGEMENT
modulus ui theme create <name> [--base {tabler|minimal}]
modulus ui theme set <name>
modulus ui theme list
modulus ui theme export <name> --format {css|scss|tailwind|json}

# MOBILE & RESPONSIVE
modulus ui add-pwa [--module]
modulus ui test-responsive

# LOCALIZATION
modulus ui add-i18n --languages {list}

# ACCESSIBILITY & COMPLIANCE
modulus ui audit [--level {A|AA|AAA}] [--output {json|html|markdown}]
modulus ui add-access-review [--module]

# PREVIEW & HELP
modulus ui preview <template>
modulus ui docs <template>
```

---

## Part 7: Completion Checklist

### Phase 3 Must Complete First
- [ ] Phase 3b: ResolveTemplatesPackage() method
- [ ] Phase 3c: Template rendering from package
- [ ] Phase 3d: Integration tests passing

### CLI Implementation
- [ ] UiAddCommand family (8+ commands)
- [ ] UiThemeCommand family (5 commands)
- [ ] UiGenerateCommand family (8+ commands)
- [ ] UiAuditCommand family (3+ commands)
- [ ] All commands registered in Program.cs
- [ ] All --json, --dry-run flags implemented

### Testing
- [ ] Unit tests for each command
- [ ] Integration tests for workflows
- [ ] End-to-end app creation test
- [ ] Template conformance tests (all engines)

### Documentation
- [ ] CLI help text for each command
- [ ] Example usage in docs
- [ ] Video walkthrough
- [ ] Troubleshooting guide

---

## Success = One Command, Complete App

**Final Vision**:
```bash
$ modulus app MyApp --ui-engine mvc
$ modulus ui add-auth --flow both
$ modulus ui add-chat --provider claude
$ modulus ui create-user-dashboard --with-audit
$ modulus ui theme create corporate --colors blue
$ modulus ui add-i18n --languages en,es,fr
$ dotnet build
$ dotnet run

# Opens browser → Complete, professional SaaS app
# With: Login, Chat, User Dashboard, Audit Logs, i18n, Theme
# Ready to customize, zero boilerplate
```

This is the roadmap to **CLI-driven enterprise UI generation**.
