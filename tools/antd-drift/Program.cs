// antd-drift — keeps Modulus honest about the Ant Design Blazor version it pins.
//
//   dotnet run --project tools/antd-drift                     check; exit 1 on actionable drift
//   dotnet run --project tools/antd-drift -- --update-baseline accept the current AntDesign surface
//
// It compares the referenced AntDesign assembly with manifest/antd-coverage.json:
//   * components added or removed upstream since the recorded baseline;
//   * every "alias" entry has an App* type that really derives from that AntDesign component;
//   * every App* alias in Modulus.Blazor is listed in the manifest;
//   * every app-* tag named in an "mvc" entry is a real tag helper in Modulus.AspNetCore.Mvc.
// Components from AntDesign.ProLayout live in another assembly and are not scanned.
// A markdown report is written to drift-reports/ for CI to attach to a PR or issue.

using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Razor.TagHelpers;

var root = FindRepoRoot();
var manifestPath = Path.Combine(root, "manifest", "antd-coverage.json");
var reportsDir = Path.Combine(root, "drift-reports");
var updateBaseline = args.Contains("--update-baseline");

var antd = typeof(AntDesign.Button).Assembly;
var modulus = typeof(Modulus.Blazor.ModulusBlazorModule).Assembly;
// The AntDesign assembly is always versioned 1.0.0, so read the pin itself.
var antdVersion = System.Xml.Linq.XDocument.Load(Path.Combine(root, "Directory.Packages.props"))
    .Descendants("PackageVersion").FirstOrDefault(e => (string?)e.Attribute("Include") == "AntDesign")
    ?.Attribute("Version")?.Value ?? "unknown";

var current = antd.GetExportedTypes()
    .Where(t => t is { IsClass: true, IsAbstract: false, IsNested: false } && typeof(IComponent).IsAssignableFrom(t))
    .Where(t => t.Namespace == "AntDesign")
    .Select(t => BaseName(t.Name))
    .ToHashSet(StringComparer.Ordinal);

var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();

if (updateBaseline)
{
    manifest["antDesignVersion"] = antdVersion;
    manifest["knownComponentTypes"] = new JsonArray(current.Order(StringComparer.Ordinal).Select(n => (JsonNode)n!).ToArray());
    File.WriteAllText(manifestPath, manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
    Console.WriteLine($"Baseline updated to AntDesign {antdVersion} ({current.Count} component types).");
    return 0;
}

var known = manifest["knownComponentTypes"]!.AsArray().Select(n => n!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
var entries = manifest["components"]!.AsObject()
    .SelectMany(category => category.Value!.AsObject().Select(c => (
        Category: category.Key,
        Name: c.Key,
        Type: c.Value!["type"]?.GetValue<string>() ?? c.Key,
        Status: c.Value!["status"]!.GetValue<string>(),
        Mvc: c.Value!["mvc"]?.GetValue<string>())))
    .ToList();

// App* aliases actually shipped: AppX : AntDesign.X (generic definitions compared by name).
var aliases = modulus.GetExportedTypes()
    .Where(t => t.Name.StartsWith("App", StringComparison.Ordinal) && t.BaseType?.Assembly == antd)
    .ToDictionary(t => BaseName(t.Name)[3..], t => BaseName(t.BaseType!.Name), StringComparer.Ordinal);

var added = current.Except(known).Order().ToList();
var removed = known.Except(current).Order().ToList();
var aliasProblems = new List<string>();
foreach (var (_, name, type, _, _) in entries.Where(e => e.Status == "alias"))
{
    if (!current.Contains(type))
        aliasProblems.Add($"`{name}` is marked alias but AntDesign {antdVersion} has no `{type}` component.");
    else if (!aliases.TryGetValue(name, out var baseName))
        aliasProblems.Add($"`{name}` is marked alias but Modulus.Blazor has no `App{name}`.");
    else if (baseName != type)
        aliasProblems.Add($"`App{name}` derives from `{baseName}`, expected `{type}`.");
}
var listed = entries.Select(e => e.Name).ToHashSet(StringComparer.Ordinal);
var unlisted = aliases.Keys.Where(k => !listed.Contains(k)).Order().ToList();
aliasProblems.AddRange(unlisted.Select(n => $"`App{n}` exists but is not listed in the manifest."));

// Tag helpers shipped by Modulus.AspNetCore.Mvc, by target element name.
var tagNames = typeof(Modulus.AspNetCore.Mvc.TagHelpers.ButtonTagHelper).Assembly.GetExportedTypes()
    .SelectMany(t => t.GetCustomAttributes<HtmlTargetElementAttribute>())
    .Select(a => a.Tag)
    .ToHashSet(StringComparer.Ordinal);
var mvcProblems = entries
    .Where(e => e.Mvc is not null)
    .SelectMany(e => Regex.Matches(e.Mvc!, @"\bapp-[a-z-]+").Select(m => (e.Name, Tag: m.Value)))
    .Where(x => !tagNames.Contains(x.Tag))
    .Select(x => $"`{x.Name}` maps to `<{x.Tag}>` but Modulus.AspNetCore.Mvc has no such tag helper.")
    .ToList();

var md = new StringBuilder()
    .AppendLine("# Ant Design drift report").AppendLine()
    .AppendLine($"- AntDesign referenced: **{antdVersion}** (baseline {manifest["antDesignVersion"]})")
    .AppendLine($"- Component types: {current.Count} (baseline {known.Count})")
    .AppendLine($"- Manifest entries: {entries.Count} — " + string.Join(", ", entries.GroupBy(e => e.Status).Select(g => $"{g.Count()} {g.Key}")))
    .AppendLine($"- MVC: {entries.Count(e => e.Mvc is not null)} entries mapped, {tagNames.Count} tag helper elements shipped")
    .AppendLine();
Section("New upstream component types (add an alias or classify them, then `--update-baseline`)", added);
Section("Component types removed upstream (aliases or samples may break)", removed);
Section("Alias problems", aliasProblems);
Section("MVC tag helper problems", mvcProblems);
if (added.Count + removed.Count + aliasProblems.Count + mvcProblems.Count == 0)
    md.AppendLine("No drift. Modulus matches the pinned Ant Design Blazor surface.");

Directory.CreateDirectory(reportsDir);
var reportPath = Path.Combine(reportsDir, $"drift-{DateTime.UtcNow:yyyyMMdd-HHmmss}.md");
File.WriteAllText(reportPath, md.ToString());
Console.Write(md);
Console.WriteLine($"Report: {Path.GetRelativePath(root, reportPath)}");

return added.Count + removed.Count + aliasProblems.Count + mvcProblems.Count == 0 ? 0 : 1;

void Section(string title, IReadOnlyList<string> items)
{
    if (items.Count == 0) return;
    md.AppendLine($"## {title}").AppendLine();
    foreach (var item in items) md.AppendLine($"- {item}");
    md.AppendLine();
}

static string BaseName(string name) => name.Split('`')[0];

static string FindRepoRoot()
{
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "Modulus.slnx"))) return dir.FullName;
    throw new InvalidOperationException("Run antd-drift from inside the Modulus repository.");
}
