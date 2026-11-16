using System.Text.RegularExpressions;
using Modulus.Ui.Abstractions.Plugins;
using Xunit;

namespace Modulus.Templates.Partials.Tests;

/// <summary>
/// A template's header comment describes the values its tag accepts. The comment is written by hand, so it can drift from the
/// framework (the code editor's comment once listed languages the framework refuses). These tests read the comment and compare
/// it with the framework's own list, which is the source of truth.
/// </summary>
public class TemplateDocumentationTests
{
    private static readonly Regex LanguageList = new(@"Language \(([^)]*)\)", RegexOptions.Compiled);

    [Theory]
    [InlineData("mvc")]
    [InlineData("razor-pages")]
    public void CodeEditorTemplate_ListsTheLanguagesTheFrameworkAccepts(string engine)
    {
        var file = Path.Combine(FindRepoRoot(), "src", "Modulus.Ui.Templates", "templates", engine, "code-editor.cshtml.sbn");
        var documented = LanguageList.Match(File.ReadLines(file).First()).Groups[1].Value
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Order(StringComparer.Ordinal)
            .ToList();

        var accepted = CodeEditorHtml.Languages.Where(l => l.Length > 0).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(accepted, documented);
    }

    [Fact]
    public void CodeEditorTemplate_Blazor_ListsTheLanguagesTheFrameworkAccepts()
    {
        var file = Path.Combine(FindRepoRoot(), "src", "Modulus.Ui.Templates", "templates", "blazor", "code-editor.razor.sbn");
        var documented = LanguageList.Match(File.ReadLines(file).First()).Groups[1].Value
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Order(StringComparer.Ordinal)
            .ToList();

        var accepted = CodeEditorHtml.Languages.Where(l => l.Length > 0).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(accepted, documented);
    }

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Modulus.slnx"))) return dir.FullName;
        throw new InvalidOperationException("Run the tests from inside the Modulus repository.");
    }
}
