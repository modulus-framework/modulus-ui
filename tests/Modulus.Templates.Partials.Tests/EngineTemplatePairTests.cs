using Xunit;

namespace Modulus.Templates.Partials.Tests;

/// <summary>
/// The MVC and Razor Pages templates that share a name are either the same file or a reviewed difference (engine-divergence.txt).
/// A pair that starts to differ, or a listed pair that becomes identical, fails the test, so divergence is never accidental.
/// </summary>
public class EngineTemplatePairTests
{
    [Fact]
    public void DivergentPairs_AreExactlyTheReviewedList()
    {
        var root = FindRepoRoot();
        var mvc = Path.Combine(root, "src", "Modulus.Ui.Templates", "templates", "mvc");
        var razor = Path.Combine(root, "src", "Modulus.Ui.Templates", "templates", "razor-pages");

        var differing = Directory.EnumerateFiles(mvc, "*.cshtml.sbn")
            .Where(f => File.Exists(Path.Combine(razor, Path.GetFileName(f))))
            .Where(f => !File.ReadAllText(f).ReplaceLineEndings("\n").Equals(File.ReadAllText(Path.Combine(razor, Path.GetFileName(f))).ReplaceLineEndings("\n")))
            .Select(f => Path.GetFileName(f)[..^".cshtml.sbn".Length])
            .Order(StringComparer.Ordinal)
            .ToList();

        var reviewed = File.ReadAllLines(Path.Combine(root, "tests", "Modulus.Templates.Partials.Tests", "engine-divergence.txt"))
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .Order(StringComparer.Ordinal)
            .ToList();

        var newlyDiffering = differing.Except(reviewed).ToList();
        var nowIdentical = reviewed.Except(differing).ToList();
        Assert.True(newlyDiffering.Count == 0 && nowIdentical.Count == 0,
            $"Pairs that began to differ (review, then list in engine-divergence.txt): {string.Join(", ", newlyDiffering)}. " +
            $"Listed pairs that are now identical (remove them from the list): {string.Join(", ", nowIdentical)}.");
    }

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Modulus.slnx"))) return dir.FullName;
        throw new InvalidOperationException("Run the tests from inside the Modulus repository.");
    }
}
