using System.Reflection;

const string reportsDir = "../../drift-reports";
const string manifestPath = "../../manifest/antd-coverage.json";

Directory.CreateDirectory(reportsDir);

var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
var reportPath = Path.Combine(reportsDir, $"drift-report-{timestamp}.md");

var report = new DriftReport();

try
{
    // Load AntDesign assembly
    var assembly = Assembly.Load("AntDesign");

    Console.WriteLine($"🔍 Scanning AntDesign for components...");

    // Find all public classes in AntDesign namespace
    var componentTypes = assembly
        .GetTypes()
        .Where(t => t.IsPublic && t.IsClass && !t.IsAbstract)
        .Where(t => t.Namespace?.StartsWith("AntDesign") == true)
        .OrderBy(t => t.Name)
        .ToList();

    report.TotalTypes = componentTypes.Count;
    report.ScannedAt = DateTime.UtcNow;
    report.AntDesignVersion = assembly.GetName().Version?.ToString() ?? "unknown";

    Console.WriteLine($"   Found {componentTypes.Count} public types");

    // Common Ant Design components to check
    var expectedComponents = new[]
    {
        "Button", "Input", "Form", "Modal", "Drawer", "Menu", "Table", "Pagination",
        "Card", "Alert", "Tooltip", "Popover", "Dropdown", "Select", "DatePicker",
        "Checkbox", "Radio", "Switch", "Avatar", "Tag", "Badge", "Progress", "Spin",
        "Divider", "Empty", "Result", "Layout", "Breadcrumb", "Steps", "Tabs",
        "Icon", "Space", "Row", "Col", "Grid"
    };

    var implementedComponents = componentTypes
        .Where(t => expectedComponents.Contains(t.Name))
        .Select(t => t.Name)
        .OrderBy(n => n)
        .ToList();

    report.ImplementedCount = implementedComponents.Count;
    report.ImplementedComponents = implementedComponents;

    var missingComponents = expectedComponents
        .Except(implementedComponents)
        .OrderBy(n => n)
        .ToList();

    report.MissingCount = missingComponents.Count;
    report.MissingComponents = missingComponents;

    Console.WriteLine($"   {implementedComponents.Count} core components found");
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️  Drift check failed: {ex.Message}");
    report.Error = ex.Message;
}

// Write report
var markdown = GenerateMarkdown(report);
File.WriteAllText(reportPath, markdown);

Console.WriteLine($"\n✅ Drift report written to {reportPath}");
Console.WriteLine($"\n📊 Summary:");
Console.WriteLine($"   AntDesign Version: {report.AntDesignVersion}");
Console.WriteLine($"   Total Types: {report.TotalTypes}");
Console.WriteLine($"   Core Components: {report.ImplementedCount}/{report.ImplementedCount + report.MissingCount}");

if (report.MissingCount > 0)
{
    Console.WriteLine($"\n⚠️  Missing Components ({report.MissingCount}):");
    foreach (var comp in report.MissingComponents.Take(10))
    {
        Console.WriteLine($"   - {comp}");
    }
}

// Exit with error if gaps detected
return report.MissingCount > 0 ? 1 : 0;

static string GenerateMarkdown(DriftReport report)
{
    var md = new System.Text.StringBuilder();

    md.AppendLine("# Ant Design Drift Report");
    md.AppendLine();
    md.AppendLine($"**Generated:** {report.ScannedAt:yyyy-MM-dd HH:mm:ss UTC}");
    md.AppendLine();

    md.AppendLine("## Summary");
    md.AppendLine($"- **AntDesign NuGet Version:** {report.AntDesignVersion}");
    md.AppendLine($"- **Total Public Types:** {report.TotalTypes}");
    md.AppendLine($"- **Likely Components:** {report.SampleComponents.Count}+");
    md.AppendLine();

    md.AppendLine("## Component Coverage");
    md.AppendLine($"- **Implemented:** {report.ImplementedCount} / {report.ImplementedCount + report.MissingCount}");
    md.AppendLine();

    if (report.ImplementedComponents.Count > 0)
    {
        md.AppendLine("### ✅ Implemented Components");
        foreach (var comp in report.ImplementedComponents)
        {
            md.AppendLine($"- {comp}");
        }
        md.AppendLine();
    }

    if (report.MissingComponents.Count > 0)
    {
        md.AppendLine("### ❌ Missing Components");
        foreach (var comp in report.MissingComponents)
        {
            md.AppendLine($"- {comp} (needs wrapper or gap handling)");
        }
        md.AppendLine();
    }

    md.AppendLine("## Sample Component Analysis");
    if (report.SampleComponents.Count > 0)
    {
        md.AppendLine();
        md.AppendLine("| Component | Parameters | Status |");
        md.AppendLine("|-----------|-----------|--------|");
        foreach (var comp in report.SampleComponents)
        {
            md.AppendLine($"| {comp.Name} | {comp.ParameterCount} | Analyzed |");
        }
    }

    md.AppendLine();
    md.AppendLine("## Next Steps");
    md.AppendLine("1. Review missing components in manifest");
    md.AppendLine("2. Implement wrappers (AppXxx) for core components");
    md.AppendLine("3. Mark gaps as `gap` or `native` in coverage.json");
    md.AppendLine("4. Run this tool again after each dependency update");

    return md.ToString();
}

/// <summary>
/// Drift report data model
/// </summary>
class DriftReport
{
    public DateTime ScannedAt { get; set; }
    public string AntDesignVersion { get; set; } = "";
    public int TotalTypes { get; set; }
    public int ImplementedCount { get; set; }
    public int MissingCount { get; set; }
    public List<string> ImplementedComponents { get; set; } = new();
    public List<string> MissingComponents { get; set; } = new();
    public List<ComponentInfo> SampleComponents { get; set; } = new();
    public string? Error { get; set; }
}

class ComponentInfo
{
    public string Name { get; set; } = "";
    public string FullName { get; set; } = "";
    public int ParameterCount { get; set; }
}
