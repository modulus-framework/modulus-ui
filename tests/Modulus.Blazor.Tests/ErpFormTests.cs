using AntDesign;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Modulus.Blazor.Components.Pro;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>The modal and drawer forms the ERP templates wrap: the title, the fields bound to the model, and the footer slots.</summary>
public class ErpFormTests : AsyncBunitContext
{
    public ErpFormTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddAntDesign();
    }

    public sealed class Customer
    {
        public string Name { get; set; } = "";
    }

    [Fact]
    public void ModalForm_ShowsTheTitleAndTheFieldsWithTheModel()
    {
        var cut = Render<ModalForm<Customer>>(p => p
            .Add(x => x.Title, "New customer")
            .Add(x => x.Visible, true)
            .Add(x => x.Model, new Customer { Name = "Ada" })
            .Add(x => x.ChildContent, (RenderFragment<Customer>)(c => b => b.AddContent(0, c.Name))));

        Assert.Contains("New customer", cut.Markup);
        Assert.Contains("Ada", cut.Markup);
        Assert.Contains("Submit", cut.Markup);
    }

    [Fact]
    public void ModalForm_HasNoContentWhenClosed()
    {
        var cut = Render<ModalForm<Customer>>(p => p
            .Add(x => x.Title, "New customer")
            .Add(x => x.Visible, false)
            .Add(x => x.Model, new Customer { Name = "Ada" })
            .Add(x => x.ChildContent, (RenderFragment<Customer>)(c => b => b.AddContent(0, c.Name))));

        Assert.DoesNotContain("New customer", cut.Markup);
        Assert.DoesNotContain("Ada", cut.Markup);
    }

    [Fact]
    public void DrawerForm_ShowsTheTitleFieldsAndTheFooterSlots()
    {
        var cut = Render<DrawerForm<Customer>>(p => p
            .Add(x => x.Title, "Edit customer")
            .Add(x => x.Visible, true)
            .Add(x => x.Model, new Customer { Name = "Grace" })
            .Add(x => x.ChildContent, (RenderFragment<Customer>)(c => b => b.AddContent(0, c.Name)))
            .Add(x => x.FooterStart, (RenderFragment)(b => b.AddContent(0, "Delete")))
            .Add(x => x.FooterExtra, (RenderFragment)(b => b.AddContent(0, "History"))));

        Assert.Contains("Edit customer", cut.Markup);
        Assert.Contains("Grace", cut.Markup);
        Assert.Contains("Delete", cut.Markup);
        Assert.Contains("History", cut.Markup);
        Assert.Contains("Save", cut.Markup);
        Assert.Contains("Cancel", cut.Markup);
    }
}
