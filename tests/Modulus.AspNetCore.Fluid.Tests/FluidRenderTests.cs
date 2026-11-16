using Modulus.AspNetCore.Fluid;
using Xunit;

namespace Modulus.AspNetCore.Fluid.Tests;

/// <summary>{% render %} and {% include %} read component files from <see cref="ModulusLiquidOptions.TemplatesRoot"/>.</summary>
public class FluidRenderTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("fluid-render").FullName;

    public FluidRenderTests()
    {
        var components = Path.Combine(_root, "Shared", "Components");
        Directory.CreateDirectory(components);
        File.WriteAllText(Path.Combine(components, "greet.liquid"), "<p>Hello {{ name }}</p>");
        File.WriteAllText(Path.Combine(_root, "secret.liquid"), "TOP SECRET");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Page(string source, string root)
    {
        var liquid = new ModulusLiquid(new ModulusLiquidOptions { TemplatesRoot = root });
        return liquid.Render(liquid.Parse(source), new Dictionary<string, object?>());
    }

    [Fact]
    public void Render_IncludesAComponentByItsPathWithoutTheExtension()
    {
        var html = Page("<main>{% render 'Shared/Components/greet', name: 'Asha' %}</main>", _root);

        Assert.Equal("<main><p>Hello Asha</p></main>", html);
    }

    [Fact]
    public void Render_EscapesTheValuesItIsGiven()
    {
        var html = Page("{% render 'Shared/Components/greet', name: '<b>x</b>' %}", _root);

        Assert.Contains("Hello &lt;b&gt;x&lt;/b&gt;", html);
    }

    [Fact]
    public void Render_CannotReadAFileOutsideTheRoot()
    {
        var outside = Path.Combine(_root, "..", Path.GetFileName(_root) + "-outside");
        Directory.CreateDirectory(outside);
        try
        {
            File.WriteAllText(Path.Combine(outside, "leak.liquid"), "LEAKED");
            var liquid = new ModulusLiquid(new ModulusLiquidOptions { TemplatesRoot = Path.Combine(_root, "Shared") });

            var template = liquid.Parse("[{% render '../../" + Path.GetFileName(outside) + "/leak' %}]");

            // The name resolves to nothing, so the render fails as a missing file and the outside text never reaches the page.
            var error = Record.Exception(() => liquid.Render(template, new Dictionary<string, object?>()));
            Assert.IsType<FileNotFoundException>(error);
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public void Render_WithNoTemplatesRoot_FailsOnTheMissingFile()
    {
        var liquid = new ModulusLiquid();
        var template = liquid.Parse("{% render 'Shared/Components/greet' %}");

        Assert.ThrowsAny<Exception>(() => liquid.Render(template, new Dictionary<string, object?>()));
    }
}
