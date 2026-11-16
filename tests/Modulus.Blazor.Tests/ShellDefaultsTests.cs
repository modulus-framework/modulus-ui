using Modulus.Ui.Abstractions.Shell;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>
/// The shell's defaults for a generated app. An app that has no sign-in or no settings page must not get a user-menu link to a
/// route it does not serve (the defaults used to point at profile, settings and logout, which 404 in every generated app).
/// </summary>
public class ShellDefaultsTests
{
    [Fact]
    public void DefaultUserMenu_HasNoLinksAndNoSignOut()
    {
        var content = new ShellContent();

        Assert.Empty(content.UserLinks);
        Assert.Null(content.SignOutHref);
    }
}
