using Microsoft.AspNetCore.Http;
using Modulus.AspNetCore.Mvc.Htmx;
using Xunit;

namespace Modulus.AspNetCore.Mvc.Tests;

/// <summary>Request detection and fragment responses for SPA navigation (hx-boost).</summary>
public class SpaNavigationTests
{
    private static HttpRequest Request(params (string Name, string Value)[] headers)
    {
        var http = new DefaultHttpContext();
        foreach (var (name, value) in headers) http.Request.Headers[name] = value;
        return http.Request;
    }

    [Fact]
    public void HtmxElementRequest_IsHtmx_NotBoosted()
    {
        var request = Request(("HX-Request", "true"));

        Assert.True(request.IsHtmx());
        Assert.False(request.IsBoosted());
    }

    [Fact]
    public void BoostedNavigation_IsBoosted_NotHtmx()
    {
        // A boosted link or form stands in for a page load: controllers answer it as they would the browser.
        var request = Request(("HX-Request", "true"), ("HX-Boosted", "true"));

        Assert.True(request.IsBoosted());
        Assert.False(request.IsHtmx());
    }

    [Fact]
    public void HistoryRestore_IsNeitherHtmxNorBoosted()
    {
        var request = Request(("HX-Request", "true"), ("HX-Boosted", "true"), ("HX-History-Restore-Request", "true"));

        Assert.False(request.IsHtmx());
        Assert.False(request.IsBoosted());
    }

    [Fact]
    public void PrepareFragment_MarksTheFragment_WithoutRetargeting()
    {
        // Targeting stays on the client, so a list's own boost area (hx-target="#customers-page") keeps working.
        var http = new DefaultHttpContext();

        SpaNavigation.PrepareFragment(http.Response);

        Assert.Equal("1", http.Response.Headers[SpaNavigation.FragmentHeader]);
        Assert.False(http.Response.Headers.ContainsKey("HX-Retarget"));
        Assert.False(http.Response.Headers.ContainsKey("HX-Reselect"));
    }

    [Fact]
    public void AddVary_KeysCachesOnTheHtmxHeaders()
    {
        var http = new DefaultHttpContext();
        http.Response.Headers.Vary = "Accept-Encoding";

        SpaNavigation.AddVary(http.Response);

        Assert.Equal(["Accept-Encoding", "HX-Request, HX-Boosted"], http.Response.Headers.Vary.Select(v => v ?? string.Empty).ToArray());
    }

    [Fact]
    public void Oob_MarksTheElementWithTheId()
    {
        var html = """<aside class="m-sidebar" id="m-sidebar"><nav></nav></aside><div class="m-sidebar-backdrop"></div>""";

        var oob = SpaNavigation.Oob(html, "m-sidebar");

        Assert.StartsWith("""<aside class="m-sidebar" id="m-sidebar" hx-swap-oob="true">""", oob);
        Assert.Equal(html, SpaNavigation.Oob(html, "m-header"));
    }

    [Theory]
    [InlineData(null, "crm", true)]
    [InlineData("crm", "crm", false)]
    [InlineData("crm", "sales", true)]
    [InlineData("", "", false)]
    public void AppChanged_ComparesTheAppOnScreen(string? current, string app, bool changed)
    {
        var request = current is null ? Request() : Request((SpaNavigation.AppHeader, current));

        Assert.Equal(changed, SpaNavigation.AppChanged(request, app));
    }
}
