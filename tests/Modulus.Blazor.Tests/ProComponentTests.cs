using Bunit;
using Modulus.Blazor.Components.Pro;
using Modulus.Ui.Abstractions;
using Xunit;

namespace Modulus.Blazor.Tests;

public class ProComponentTests : TestContext
{
    [Fact]
    public void ProCard_Renders()
    {
        // Arrange & Act
        var cut = RenderComponent<ProCard>();

        // Assert
        Assert.NotNull(cut);
    }

    [Fact]
    public void ProForm_RendersWithFields()
    {
        // Arrange
        var fields = new List<ProFormField>
        {
            new() { Name = "Name", Label = "Full Name", FieldType = "text", Required = true },
            new() { Name = "Email", Label = "Email Address", FieldType = "email", Required = true }
        };

        // Act
        var cut = RenderComponent<ProForm>(parameters => parameters
            .Add(p => p.Fields, fields));

        // Assert
        Assert.Equal(2, cut.Instance.Fields.Count);
    }

    [Fact]
    public void ModalForm_Renders()
    {
        // Arrange
        var fields = new List<ProFormField>();

        // Act
        var cut = RenderComponent<ModalForm>(parameters => parameters
            .Add(p => p.Title, "Create Item")
            .Add(p => p.Fields, fields));

        // Assert
        Assert.NotNull(cut.Instance);
    }

    [Fact]
    public void DrawerForm_Renders()
    {
        // Arrange
        var fields = new List<ProFormField>();

        // Act
        var cut = RenderComponent<DrawerForm>(parameters => parameters
            .Add(p => p.Title, "Edit Form")
            .Add(p => p.Width, 500)
            .Add(p => p.Fields, fields));

        // Assert
        Assert.NotNull(cut.Instance);
    }
}
