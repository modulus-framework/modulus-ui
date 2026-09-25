using Modulus.Ui.Abstractions.Theming;
using Xunit;

namespace Modulus.Blazor.Tests;

public class ThemeTests
{
    [Theory]
    // antd v4 blue — must match the defaults baked into ant-design-blazor.variable.css.
    [InlineData("#1890ff", "#e6f7ff,#bae7ff,#91d5ff,#69c0ff,#40a9ff,#1890ff,#096dd9,#0050b3,#003a8c,#002766")]
    // antd v5 blue.
    [InlineData("#1677ff", "#e6f4ff,#bae0ff,#91caff,#69b1ff,#4096ff,#1677ff,#0958d9,#003eb3,#002c8c,#001d66")]
    // antd green / red — covers the hue-direction branch outside 60..240.
    [InlineData("#52c41a", "#f6ffed,#d9f7be,#b7eb8f,#95de64,#73d13d,#52c41a,#389e0d,#237804,#135200,#092b00")]
    [InlineData("#f5222d", "#fff1f0,#ffccc7,#ffa39e,#ff7875,#ff4d4f,#f5222d,#cf1322,#a8071a,#820014,#5c0011")]
    public void Palette_MatchesAntDesignColors(string seed, string expected)
    {
        var palette = AntPalette.Generate(AntColor.Parse(seed)).Select(c => c.ToHex());

        Assert.Equal(expected.Split(','), palette);
    }

    [Fact]
    public void ThemeCss_ReproducesVariableStylesheetDefaults()
    {
        // Ant Design's own seed colours (the Modulus defaults are Tabler's).
        var css = ThemeCss.Build(new ModulusThemeOptions
        {
            PrimaryColor = "#1890ff", InfoColor = "#1890ff", SuccessColor = "#52c41a", WarningColor = "#faad14", ErrorColor = "#ff4d4f",
        });

        // Values copied from AntDesign 1.6.2 ant-design-blazor.variable.css :root.
        Assert.Contains("--ant-primary-color:#1890ff;", css);
        Assert.Contains("--ant-primary-color-hover:#40a9ff;", css);
        Assert.Contains("--ant-primary-color-active:#096dd9;", css);
        Assert.Contains("--ant-primary-color-outline:rgba(24, 144, 255, 0.2);", css);
        Assert.Contains("--ant-primary-color-deprecated-l-35:#cbe6ff;", css);
        Assert.Contains("--ant-primary-color-deprecated-l-20:#7ec1ff;", css);
        Assert.Contains("--ant-primary-color-deprecated-t-20:#46a6ff;", css);
        Assert.Contains("--ant-primary-color-deprecated-t-50:#8cc8ff;", css);
        Assert.Contains("--ant-primary-color-deprecated-f-12:rgba(24, 144, 255, 0.12);", css);
        Assert.Contains("--ant-primary-color-active-deprecated-f-30:rgba(230, 247, 255, 0.3);", css);
        Assert.Contains("--ant-primary-color-active-deprecated-d-02:#dcf4ff;", css);
        Assert.Contains("--ant-error-color-active:#d9363e;", css);
        Assert.Contains("--ant-warning-color-hover:#ffc53d;", css);
        Assert.Contains("--ant-success-color-deprecated-border:#b7eb8f;", css);
    }

    [Fact]
    public void ThemeCss_EmitsRadiusAndLayoutTokens()
    {
        var css = ThemeCss.Build(new ModulusThemeOptions { BorderRadius = 4, SiderWidth = 240 });

        Assert.Contains("--m-radius:4px;", css);
        Assert.Contains("--m-radius-lg:6px;", css);
        Assert.Contains("--m-sider-width:240px;", css);
    }

    [Fact]
    public void ThemeCss_FallsBackOnInvalidColor()
    {
        var css = ThemeCss.Build(new ModulusThemeOptions { PrimaryColor = "not-a-colour" });

        Assert.Contains("--ant-primary-color:#1677ff;", css);
    }

    [Theory]
    [InlineData("dark", ThemeMode.Light, "dark")]
    [InlineData(null, ThemeMode.Dark, "dark")]
    [InlineData("garbage", ThemeMode.Light, "light")]
    [InlineData(null, ThemeMode.System, null)]
    public void ResolveMode_PrefersCookieThenDefault(string? cookie, ThemeMode mode, string? expected) =>
        Assert.Equal(expected, ThemeCss.ResolveMode(cookie, mode));
}
