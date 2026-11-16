using System.Runtime.CompilerServices;
using Modulus.AspNetCore.Fluid;
using Xunit;

namespace Modulus.AspNetCore.Fluid.Tests;

/// <summary>
/// The shipped Fluid templates (<c>src/Modulus.Ui.Templates/templates/fluid</c>) are plain Liquid files, not Scriban,
/// so each one must parse and render through <see cref="ModulusLiquid"/> with no data and leave no tag behind.
/// </summary>
public class FluidTemplateFilesTests
{
    private static readonly string TemplateDirectory = Path.GetFullPath(
        Path.Combine(Path.GetDirectoryName(ThisFile())!, "..", "..", "src", "Modulus.Ui.Templates", "templates", "fluid"));

    private static string ThisFile([CallerFilePath] string path = "") => path;

    public static IEnumerable<object[]> Templates() =>
        Directory.GetFiles(TemplateDirectory, "*.liquid").OrderBy(f => f).Select(f => new object[] { Path.GetFileName(f) });

    [Fact]
    public void TheFolderHasTemplates_AndNoScribanFiles()
    {
        Assert.NotEmpty(Directory.GetFiles(TemplateDirectory, "*.liquid"));
        Assert.Empty(Directory.GetFiles(TemplateDirectory, "*.sbn"));
    }

    [Theory]
    [MemberData(nameof(Templates))]
    public void EachTemplate_Parses(string file)
    {
        var liquid = new ModulusLiquid();
        var source = File.ReadAllText(Path.Combine(TemplateDirectory, file));

        Assert.True(liquid.TryParseTemplate(source, out _, out var error), error);
    }

    [Theory]
    [MemberData(nameof(Templates))]
    public void EachTemplate_RendersWithNoData_AndLeavesNoTag(string file)
    {
        var liquid = new ModulusLiquid();
        var template = liquid.Parse(File.ReadAllText(Path.Combine(TemplateDirectory, file)));

        var html = liquid.Render(template, new Dictionary<string, object?>());

        Assert.DoesNotContain("{#", html);
        Assert.DoesNotContain("{%", html);
        Assert.DoesNotContain("{{", html);
        Assert.DoesNotContain("}}", html);
    }
}
