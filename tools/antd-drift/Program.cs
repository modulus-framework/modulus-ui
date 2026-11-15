// antd-drift — keeps Modulus honest about the Ant Design Blazor version it pins.
//
//   dotnet run --project tools/antd-drift                     check; exit 1 on actionable drift
//   dotnet run --project tools/antd-drift -- --update-baseline accept the current AntDesign surface
//   dotnet run --project tools/antd-drift -- --write-dark-brand regenerate antd-dark-brand.css
//
// It compares the referenced AntDesign assembly with manifest/antd-coverage.json:
//   * components added or removed upstream since the recorded baseline;
//   * every "alias" entry has an App* type that really derives from that AntDesign component;
//   * every App* alias in Modulus.Blazor is listed in the manifest;
//   * every app-* tag named in an "mvc" entry is a real tag helper in Modulus.AspNetCore.Mvc;
//   * an MVC or Razor Pages template does not pass a bare variable name to a string tag helper property (that sends the text
//     of the name, not the variable: placeholder="placeholder" instead of placeholder="@placeholder");
//   * antd-dark-brand.css still matches the pinned dark stylesheet (the brand colour in dark mode).
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
var darkBrandPath = Path.Combine(root, "src", "Modulus.Ui.Assets", "wwwroot", "css", "antd-dark-brand.css");

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

var darkCss = DarkBrandCss.Source(antdVersion);
if (args.Contains("--write-dark-brand"))
{
    if (darkCss is null)
    {
        Console.Error.WriteLine($"AntDesign {antdVersion} is not in the NuGet cache; run dotnet restore first.");
        return 1;
    }
    File.WriteAllText(darkBrandPath, DarkBrandCss.Build(darkCss));
    Console.WriteLine($"Wrote {Path.GetRelativePath(root, darkBrandPath)}.");
    return 0;
}

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

// Razor gives an unquoted value to a string-typed tag helper property as the text of the value, so placeholder="placeholder"
// sends the word "placeholder". A template must write @name to pass its variable. A bare name that the template declares,
// bound to a string property, is flagged.
var stringProps = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
foreach (var helper in typeof(Modulus.AspNetCore.Mvc.TagHelpers.ButtonTagHelper).Assembly.GetExportedTypes())
    foreach (var target in helper.GetCustomAttributes<HtmlTargetElementAttribute>())
    {
        if (!stringProps.TryGetValue(target.Tag, out var names)) stringProps[target.Tag] = names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var prop in helper.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite && p.PropertyType == typeof(string)))
            names.Add(prop.GetCustomAttribute<HtmlAttributeNameAttribute>()?.Name
                ?? Regex.Replace(prop.Name, "(?<=[a-z0-9])([A-Z])", "-$1").ToLowerInvariant());
    }
var bareProblems = new List<string>();
var templateRoot = Path.Combine(root, "src", "Modulus.Ui.Templates", "templates");
foreach (var engine in new[] { "mvc", "razor-pages" })
    foreach (var file in Directory.EnumerateFiles(Path.Combine(templateRoot, engine), "*.sbn"))
    {
        var text = File.ReadAllText(file);
        var declared = Regex.Matches(text, @"\b[A-Za-z_][\w<>\[\]?, ]*?\s+([a-z][A-Za-z0-9]*)\s*=(?!=)")
            .Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        foreach (Match tag in Regex.Matches(text, @"<(app-[a-z0-9-]+)([^>]*)>"))
            foreach (Match attr in Regex.Matches(tag.Groups[2].Value, @"([a-z][a-z0-9-]*)=""([A-Za-z][A-Za-z0-9]*)"""))
                if (stringProps.TryGetValue(tag.Groups[1].Value, out var names) && names.Contains(attr.Groups[1].Value) && declared.Contains(attr.Groups[2].Value))
                    bareProblems.Add($"`{Path.GetRelativePath(root, file)}` passes `{attr.Groups[2].Value}` to `{tag.Groups[1].Value} {attr.Groups[1].Value}` as text; write `{attr.Groups[1].Value}=\"@{attr.Groups[2].Value}\"`.");
    }

var darkProblems = new List<string>();
if (darkCss is null)
    darkProblems.Add($"AntDesign {antdVersion} dark stylesheet not found in the NuGet cache; antd-dark-brand.css was not checked.");
else if (!File.Exists(darkBrandPath) || File.ReadAllText(darkBrandPath).ReplaceLineEndings("\n") != DarkBrandCss.Build(darkCss))
    darkProblems.Add("`antd-dark-brand.css` no longer matches the dark stylesheet; run `--write-dark-brand` and check any new blue shades against `AntDarkBrand.Sources`.");

// Blazor wrapper classes that share a name with an AntDesign or Modulus.Blazor type. The CLI writes those wrappers
// with a Modulus prefix, so the list in ui-kit.json must match the Blazor templates on disk, in both directions.
var typeNames = antd.GetExportedTypes().Concat(modulus.GetExportedTypes())
    .Select(t => BaseName(t.Name)).ToHashSet(StringComparer.Ordinal);
var kitPath = Path.Combine(root, "src", "Modulus.Ui.Templates", "ui-kit.json");
var prefixed = JsonNode.Parse(File.ReadAllText(kitPath))!["blazorPrefixed"]!.AsArray()
    .Select(n => n!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
var clashing = Directory.EnumerateFiles(Path.Combine(root, "src", "Modulus.Ui.Templates", "templates", "blazor"), "*.razor.sbn")
    .Select(f => Path.GetFileName(f)[..^".razor.sbn".Length])
    .Where(k => typeNames.Contains(string.Concat(k.Split('-').Select(w => char.ToUpperInvariant(w[0]) + w[1..]))))
    .ToHashSet(StringComparer.Ordinal);
var classProblems = clashing.Except(prefixed).Order()
    .Select(k => $"`{k}` shares a class name with an AntDesign or Modulus.Blazor type but is not in `blazorPrefixed` (ui-kit.json).")
    .Concat(prefixed.Except(clashing).Order()
        .Select(k => $"`{k}` is in `blazorPrefixed` (ui-kit.json) but its Blazor template no longer clashes or does not exist."))
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
Section("Dark brand stylesheet", darkProblems);
Section("Blazor class names clashing with a framework type", classProblems);
Section("Template attributes that pass a variable name as text", bareProblems);
if (added.Count + removed.Count + aliasProblems.Count + mvcProblems.Count + darkProblems.Count + classProblems.Count + bareProblems.Count == 0)
    md.AppendLine("No drift. Modulus matches the pinned Ant Design Blazor surface.");

Directory.CreateDirectory(reportsDir);
var reportPath = Path.Combine(reportsDir, $"drift-{DateTime.UtcNow:yyyyMMdd-HHmmss}.md");
File.WriteAllText(reportPath, md.ToString());
Console.Write(md);
Console.WriteLine($"Report: {Path.GetRelativePath(root, reportPath)}");

return added.Count + removed.Count + aliasProblems.Count + mvcProblems.Count + darkProblems.Count + classProblems.Count + bareProblems.Count == 0 ? 0 : 1;

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
