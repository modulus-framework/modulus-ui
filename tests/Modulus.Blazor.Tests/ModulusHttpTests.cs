using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Modulus.Blazor.Theming;
using Xunit;

namespace Modulus.Blazor.Tests;

public class ModulusHttpTests : AsyncBunitContext
{
    private const string Module = "./_content/Synvro.Modulus.Ui.Assets/js/modulus-http.js";

    private static InvocationMatcher Call(string method, string url) =>
        i => i.Identifier == "send" && i.Arguments[0] as string == method && i.Arguments[1] as string == url;

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public async Task SendAsync_ImportsTheModuleOnce_AndPassesMethodUrlAndBody()
    {
        var module = JSInterop.SetupModule(Module);
        module.Setup<ModulusHttpResult>("send", Call("POST", "/account/2fa/enable"))
            .SetResult(new ModulusHttpResult(200, true, Json("""{"recoveryCodes":["a","b"]}""")));
        var http = new ModulusHttp(JSInterop.JSRuntime);

        var first = await http.PostAsync("/account/2fa/enable", new { code = "123456" });
        var second = await http.PostAsync("/account/2fa/enable", new { code = "654321" });

        Assert.True(first.Ok);
        Assert.True(second.Ok);
        Assert.Single(JSInterop.Invocations, i => i.Identifier == "import");
        Assert.Equal(2, module.Invocations.Count);
    }

    [Fact]
    public async Task GetAndDelete_UseTheirVerb_WithNoBody()
    {
        var module = JSInterop.SetupModule(Module);
        module.Setup<ModulusHttpResult>("send", Call("GET", "/account/sessions")).SetResult(new ModulusHttpResult(200, true, null));
        module.Setup<ModulusHttpResult>("send", Call("DELETE", "/account/sessions/s1")).SetResult(new ModulusHttpResult(204, true, null));
        var http = new ModulusHttp(JSInterop.JSRuntime);

        Assert.Equal(200, (await http.GetAsync("/account/sessions")).Status);
        Assert.Equal(204, (await http.DeleteAsync("/account/sessions/s1")).Status);
        Assert.All(module.Invocations, i => Assert.Null(i.Arguments[2]));
    }

    [Fact]
    public void Read_DeserializesCaseInsensitively_AndIsDefaultWithoutABody()
    {
        var withBody = new ModulusHttpResult(200, true, Json("""[{"permission":"a:b","type":"Allow"}]"""));

        var grants = withBody.Read<List<Grant>>();

        Assert.Equal("a:b", Assert.Single(grants!).Permission);
        Assert.Null(new ModulusHttpResult(204, true, null).Read<List<Grant>>());
        Assert.Null(new ModulusHttpResult(200, true, Json("null")).Read<List<Grant>>());
    }

    [Theory]
    [InlineData("""{"error":"The code is not valid."}""", "The code is not valid.")]
    [InlineData("""{"error":5}""", null)]
    [InlineData("""{"message":"x"}""", null)]
    [InlineData("""["x"]""", null)]
    public void Error_ReadsTheErrorStringOnly(string body, string? expected) =>
        Assert.Equal(expected, new ModulusHttpResult(400, false, Json(body)).Error);

    [Fact]
    public void Error_IsNullWithoutABody() => Assert.Null(new ModulusHttpResult(0, false, null).Error);

    [Fact]
    public void Antiforgery_RendersTheRequestTokenAsMeta()
    {
        Services.AddSingleton<IAntiforgery>(new FakeAntiforgery("tok&en"));

        var cut = Render<ModulusAntiforgery>(p => p.AddCascadingValue(new DefaultHttpContext()));

        Assert.Equal("tok&en", cut.Find("meta[name=csrf-token]").GetAttribute("content"));
    }

    [Fact]
    public void Antiforgery_RendersNothing_WithoutHttpContext()
    {
        Services.AddSingleton<IAntiforgery>(new FakeAntiforgery("t"));

        Assert.Empty(Render<ModulusAntiforgery>().Markup);
    }

    [Fact]
    public void Antiforgery_RendersNothing_WithoutTheService() =>
        Assert.Empty(Render<ModulusAntiforgery>(p => p.AddCascadingValue(new DefaultHttpContext())).Markup);

    private sealed record Grant(string Permission, string Type);

    private sealed class FakeAntiforgery(string token) : IAntiforgery
    {
        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => new(token, null, "form", "header");
        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => GetAndStoreTokens(httpContext);
        public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(true);
        public Task ValidateRequestAsync(HttpContext httpContext) => Task.CompletedTask;
        public void SetCookieTokenAndHeader(HttpContext httpContext) { }
    }
}
