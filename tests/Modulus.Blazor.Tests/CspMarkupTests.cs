using System.Text.RegularExpressions;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>
/// The markup the framework writes must not use inline event handlers (onchange="...", onclick="..."): a strict
/// Content-Security-Policy blocks them, and the pages break in production. Blazor's @onclick directives are fine, and are not matched.
/// </summary>
public class CspMarkupTests
{
    private static readonly Regex InlineHandler = new(@"(?<![@\w-])on(click|change|input|submit|keydown|keyup|keypress|focus|blur|load|mouseover|mouseout|pointerdown|pointerup|dragover|drop)\s*=\s*\\?""",
        RegexOptions.Compiled);

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "Modulus.Ui.Assets"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("The repository root was not found.");
    }

    [Fact]
    public void NoFrameworkOrSampleMarkup_UsesAnInlineEventHandler()
    {
        var root = Root();
        var folders = new[] { "src", "samples" }.Select(f => Path.Combine(root, f));
        var files = folders.SelectMany(f => Directory.EnumerateFiles(f, "*.*", SearchOption.AllDirectories))
            .Where(f => f.EndsWith(".cs", StringComparison.Ordinal) || f.EndsWith(".cshtml", StringComparison.Ordinal) || f.EndsWith(".razor", StringComparison.Ordinal) || f.EndsWith(".sbn", StringComparison.Ordinal))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .ToList();

        var found = new List<string>();
        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
                if (InlineHandler.IsMatch(lines[i])) found.Add($"{Path.GetRelativePath(root, file)}:{i + 1}: {lines[i].Trim()[..Math.Min(120, lines[i].Trim().Length)]}");
        }
        Assert.True(found.Count == 0, "Inline event handlers found:\n" + string.Join("\n", found));
    }
}
