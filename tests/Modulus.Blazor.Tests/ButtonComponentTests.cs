using Bunit;
using Modulus.Blazor.Components.General;
using Xunit;

namespace Modulus.Blazor.Tests;

public class ButtonComponentTests : TestContext
{
    [Fact]
    public void AppButton_Renders()
    {
        // Arrange & Act
        var cut = RenderComponent<AppButton>();

        // Assert
        Assert.NotNull(cut);
        Assert.Contains("button", cut.Markup);
    }

    [Fact]
    public void AppButton_RendersPrimaryType()
    {
        // Arrange & Act
        var cut = RenderComponent<AppButton>(parameters => parameters
            .Add(p => p.Type, AntDesign.ButtonType.Primary));

        // Assert
        Assert.NotNull(cut.Instance);
    }

    [Fact]
    public void AppButton_RendersDanger()
    {
        // Arrange & Act
        var cut = RenderComponent<AppButton>(parameters => parameters
            .Add(p => p.Danger, true));

        // Assert
        Assert.True(cut.Instance.Danger);
    }

    [Fact]
    public void AppButton_RendersDisabled()
    {
        // Arrange & Act
        var cut = RenderComponent<AppButton>(parameters => parameters
            .Add(p => p.Disabled, true));

        // Assert
        Assert.True(cut.Instance.Disabled);
    }

    [Fact]
    public void AppButton_RendersWithHref()
    {
        // Arrange & Act
        var cut = RenderComponent<AppButton>(parameters => parameters
            .Add(p => p.Href, "/dashboard"));

        // Assert
        Assert.Equal("/dashboard", cut.Instance.Href);
    }
}
