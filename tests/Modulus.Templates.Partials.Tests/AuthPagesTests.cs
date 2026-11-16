using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Modulus.Templates.Partials.Tests;

/// <summary>
/// The two plain full pages the CLI scaffolds (forgot-password and reset-password, listed in plain-pages.txt) render in the
/// fixture with the sample's auth layout. The 390 px and dark-mode layout was checked in a browser on 2026-11-15.
/// </summary>
public class AuthPagesTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuthPagesTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Theory]
    [InlineData("/auth/forgot-password", "Forgot password")]
    [InlineData("/auth/reset-password", "Reset password")]
    public async Task AuthPage_RendersWithTheAuthLayout(string path, string heading)
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, $"{path}: HTTP {(int)response.StatusCode}");
        Assert.Contains(heading, html);
        Assert.Contains("Modulus Admin", html);
        Assert.DoesNotContain("Unhandled exception", html);
    }
}
