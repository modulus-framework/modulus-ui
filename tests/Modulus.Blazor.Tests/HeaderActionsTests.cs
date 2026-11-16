using AntDesign;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Modulus.Blazor.Shell;
using Modulus.Ui.Abstractions.Shell;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>The header's account menu comes from the app's shell content, not from routes the framework guesses.</summary>
public class HeaderActionsTests : AsyncBunitContext
{
    public HeaderActionsTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<AntDesign.JsInterop.HtmlElement>(_ => true).SetResult(new AntDesign.JsInterop.HtmlElement());
        JSInterop.Setup<AntDesign.JsInterop.DomRect>(_ => true).SetResult(new AntDesign.JsInterop.DomRect());
        Services.AddAntDesign();
    }

    [Fact]
    public void WithoutLinks_TheHeaderShowsTheUserAndNoAccountMenu()
    {
        var cut = Render<HeaderActions>(p => p.Add(x => x.UserName, "Ada Park"));

        Assert.Contains("Ada Park", cut.Markup);
        Assert.DoesNotContain("Sign out", cut.Markup);
        Assert.DoesNotContain("aria-label=\"Account menu\"", cut.Markup);
    }

    [Fact]
    public void WithLinks_TheAccountMenuIsThere_AndOpens()
    {
        // The dropdown's items render in an overlay outside this component's markup, so only the trigger and the click are checked here.
        var cut = Render<HeaderActions>(p => p
            .Add(x => x.UserName, "Ada Park")
            .Add(x => x.UserLinks, new List<ShellLink> { new("Profile", "account/profile", "user") })
            .Add(x => x.SignOutHref, "account/logout"));

        var trigger = cut.Find("button[aria-label='Account menu']");
        trigger.Click();

        Assert.Contains("Ada Park", cut.Markup);
    }
}
