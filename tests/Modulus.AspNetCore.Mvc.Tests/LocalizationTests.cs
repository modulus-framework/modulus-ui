using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Modulus.Ui.Abstractions.Theming;
using Xunit;

namespace Modulus.AspNetCore.Mvc.Tests;

/// <summary>Request localization from the theme settings cultures (<c>app.UseModulusLocalization()</c>).</summary>
public class LocalizationTests
{
    [Fact]
    public void NoCultures_MeansNoLocalization()
    {
        Assert.Null(ModulusMvcModule.LocalizationOptions(null));
        Assert.Null(ModulusMvcModule.LocalizationOptions(["@@ not a culture @@"]));
    }

    [Fact]
    public void FirstCultureIsTheDefault_AndTheCookieWinsOverTheQueryString()
    {
        var options = ModulusMvcModule.LocalizationOptions(["en-US", "de-DE"])!;

        Assert.Equal("en-US", options.DefaultRequestCulture.Culture.Name);
        Assert.Equal(["en-US", "de-DE"], options.SupportedUICultures!.Select(c => c.Name));
        Assert.IsType<CookieRequestCultureProvider>(options.RequestCultureProviders[0]);
        Assert.Contains(options.RequestCultureProviders, p => p is QueryStringRequestCultureProvider);
    }

    [Fact]
    public async Task TheCookieThePanelWrites_IsReadByTheCookieProvider()
    {
        var options = ModulusMvcModule.LocalizationOptions(["en-US", "de-DE"])!;
        var http = new DefaultHttpContext();
        http.Request.Headers.Cookie = $"{ModulusCulture.CookieName}={Uri.EscapeDataString(ModulusCulture.CookieValue("de-DE"))}";

        var result = await options.RequestCultureProviders[0].DetermineProviderCultureResult(http);

        Assert.Equal("de-DE", result!.UICultures[0].Value);
    }
}
