using System.Globalization;
using Modulus.Ui.Abstractions.Shell;
using Modulus.Ui.Abstractions.Theming;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>The language picker of the theme settings panel and the culture list behind it.</summary>
public sealed class LocalizationTests : IDisposable
{
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;
    }

    [Fact]
    public void Supported_KeepsKnownCulturesOnce_InTheirOrder()
    {
        var cultures = ModulusCulture.Supported(["de-DE", " en-US ", "de-DE", "", "@@ not a culture @@"]);

        Assert.Equal(["de-DE", "en-US"], cultures.Select(c => c.Name));
        Assert.Empty(ModulusCulture.Supported(null));
    }

    [Fact]
    public void Match_PrefersTheSameName_ThenTheLanguage_ThenTheDefault()
    {
        var supported = ModulusCulture.Supported(["en-US", "de-DE", "fr-FR"]);

        Assert.Equal("fr-FR", ModulusCulture.Match(supported, CultureInfo.GetCultureInfo("fr-FR"))!.Name);
        Assert.Equal("de-DE", ModulusCulture.Match(supported, CultureInfo.GetCultureInfo("de-AT"))!.Name);
        Assert.Equal("en-US", ModulusCulture.Match(supported, CultureInfo.GetCultureInfo("ja-JP"))!.Name);
        Assert.Null(ModulusCulture.Match([], CultureInfo.InvariantCulture));
    }

    [Fact]
    public void CookieValue_IsAspNetCoresFormat()
    {
        Assert.Equal("c=de-DE|uic=de-DE", ModulusCulture.CookieValue("de-DE"));
        Assert.Equal(".AspNetCore.Culture", ModulusCulture.CookieName);
    }

    [Fact]
    public void Customizer_ShowsTheLanguagePicker_OnlyWithTwoOrMoreCultures_AndSelectsTheRequestCulture()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
        static string Render(params string[] cultures) =>
            ShellRenderer.Customizer(new ShellView(new ModulusThemeOptions { Cultures = [.. cultures] }, [], "", new ShellContent()));

        Assert.DoesNotContain("data-m-culture", Render());
        Assert.DoesNotContain("data-m-culture", Render("en-US"));

        var html = Render("en-US", "de-DE");
        Assert.Contains("""<select class="ant-input m-setting-select" id="m-setting-culture" name="culture" data-m-culture data-cookie=".AspNetCore.Culture">""", html);
        Assert.Contains("""<option value="de-DE" lang="de-DE" selected>Deutsch (Deutschland)</option>""", html);
        Assert.Contains("""<option value="en-US" lang="en-US">English (United States)</option>""", html);
    }

    [Fact]
    public void Clone_CopiesTheCultureList()
    {
        var options = new ModulusThemeOptions { Cultures = ["en-US"] };
        var copy = options.Clone();
        copy.Cultures.Add("de-DE");

        Assert.Equal(["en-US"], options.Cultures);
    }
}
