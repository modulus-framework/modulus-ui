using Modulus.AspNetCore.Fluid;
using Xunit;

namespace Modulus.AspNetCore.Fluid.Tests;

/// <summary>Liquid templates: escaping, the layout, the model types a template may read, the tags and the filter, and the file cache.</summary>
public class LiquidTemplateTests
{
    private static readonly Dictionary<string, object?> None = [];

    private static string Run(string source, IReadOnlyDictionary<string, object?>? values = null, ModulusLiquidOptions? options = null) =>
        new ModulusLiquid(options ?? new ModulusLiquidOptions()).Render(new ModulusLiquid(options ?? new ModulusLiquidOptions()).Parse(source), values ?? None);

    [Fact]
    public void AValue_IsEscaped()
    {
        var html = Run("{{ name }}", new Dictionary<string, object?> { ["name"] = "<b>Ana</b> & \"Co\"" });

        Assert.Equal("&lt;b&gt;Ana&lt;/b&gt; &amp; &quot;Co&quot;", html);
    }

    [Fact]
    public void ALiquidHtml_IsPrintedAsItIs()
    {
        var html = Run("{{ markup }}", new Dictionary<string, object?> { ["markup"] = new LiquidHtml("<i>ok</i>") });

        Assert.Equal("<i>ok</i>", html);
    }

    [Fact]
    public void ALayout_GetsTheBodyUnescapedAndTheTitleEscaped()
    {
        var liquid = new ModulusLiquid();
        var page = liquid.Parse("<p>{{ title }}</p>");
        var layout = liquid.Parse("<main>{{ content }}</main>");

        var html = liquid.RenderPage(page, layout, new Dictionary<string, object?> { ["title"] = "A & B" });

        Assert.Equal("<main><p>A &amp; B</p></main>", html);
    }

    public class Shipment
    {
        public string Ref { get; set; } = "";
        public string Secret { get; set; } = "";
    }

    [Fact]
    public void AModelType_ReadsOnlyWhatItIsRegisteredFor()
    {
        var shipment = new Shipment { Ref = "SHP-1", Secret = "x" };
        var registered = new ModulusLiquidOptions().Register<Shipment>();
        var open = new ModulusLiquidOptions();

        Assert.Equal("SHP-1", Run("{{ s.Ref }}", new Dictionary<string, object?> { ["s"] = shipment }, registered));
        Assert.Equal("", Run("{{ s.Ref }}", new Dictionary<string, object?> { ["s"] = shipment }, open));
    }

    [Fact]
    public void ModulusHead_LinksTheStylesheetsAndScriptsUnderTheAssetPrefix()
    {
        var html = Run("{% modulus_head %}");

        Assert.Contains("<link rel=\"stylesheet\" href=\"/_content/Modulus.Ui.Assets/css/modulus.css\" />", html);
        Assert.Contains("<script src=\"/_content/Modulus.Ui.Assets/js/modulus-shell.js\" defer></script>", html);
        Assert.Contains("/_content/AntDesign/css/ant-design-blazor.variable.min.css", html);

        var moved = Run("{% modulus_head %}", options: new ModulusLiquidOptions { AssetPrefix = "/static/ui/" });
        Assert.Contains("href=\"/static/ui/css/modulus.css\"", moved);
    }

    [Fact]
    public void IconTag_DrawsTheAntDesignIcon()
    {
        var html = Run("{% icon \"team\" %}");

        Assert.StartsWith("<svg", html);
    }

    [Fact]
    public void IconTag_RefusesAnUnknownName()
    {
        Assert.ThrowsAny<ArgumentException>(() => Run("{% icon \"no-such-icon-xyz\" %}"));
    }

    [Fact]
    public void BarcodeTag_DrawsACode128Symbol()
    {
        var html = Run("{% barcode \"SHP-2041\" %}");

        Assert.Contains("<svg", html);
        Assert.Contains("m-barcode", html);
        Assert.Contains("SHP-2041", html);
    }

    [Fact]
    public void BarcodeTag_EscapesItsValueWhenItPrintsIt()
    {
        var html = Run("{% barcode value %}", new Dictionary<string, object?> { ["value"] = "<b>X</b>" });

        Assert.DoesNotContain("<b>X</b>", html);
    }

    [Fact]
    public void StatusBadgeFilter_EscapesItsTextAndChecksItsTone()
    {
        Assert.Equal("<span class=\"m-status-badge m-status-success\"><span class=\"m-status-dot\" aria-hidden=\"true\"></span>Paid &amp; sent</span>",
            Run("{{ text | status_badge: \"success\" }}", new Dictionary<string, object?> { ["text"] = "Paid & sent" }));
        Assert.Contains("m-status-default", Run("{{ 'x' | status_badge: 'drop us' }}"));
    }

    [Fact]
    public void MoneyFilter_FormatsAmountsInTheInvariantCulture()
    {
        var values = new Dictionary<string, object?> { ["amount"] = 1234.5m };

        Assert.Equal("1,234.50", Run("{{ amount | money }}", values));
        Assert.Equal("1,235", Run("{{ amount | money: 0 }}", values));
        Assert.Equal("1,234.500000", Run("{{ amount | money: 99 }}", values)); // clamped to 6 places
    }

    [Fact]
    public void AnInvalidTemplate_IsRefused()
    {
        var liquid = new ModulusLiquid();

        Assert.False(liquid.TryParseTemplate("{% if x %}", out _, out var error));
        Assert.False(string.IsNullOrEmpty(error));
        Assert.Throws<ArgumentException>(() => liquid.Parse("{% if x %}"));
    }

    [Fact]
    public void ALoadedFile_IsParsedAgainWhenItChanges()
    {
        var path = Path.Combine(Path.GetTempPath(), "modulus-liquid-" + Guid.NewGuid().ToString("N") + ".liquid");
        try
        {
            File.WriteAllText(path, "one");
            var liquid = new ModulusLiquid();
            Assert.Equal("one", liquid.Render(liquid.Load(path), None));

            File.WriteAllText(path, "two");
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));
            Assert.Equal("two", liquid.Render(liquid.Load(path), None));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
