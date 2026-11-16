using AntDesign;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Modulus.Blazor.Tests.ErpWrappers;
using Modulus.Ui.Abstractions.Dashboard;
using Modulus.Ui.Abstractions.State;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Workflow;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>
/// Renders the six ERP Blazor wrappers that the CLI scaffolds (<c>templates/blazor/*.razor.sbn</c>). The csproj copies those
/// templates into <c>ErpWrappers/</c> as <c>Modulus&lt;Name&gt;.razor</c>, so these tests compile and run the files the CLI writes.
/// Each test checks the value a template passes: a parameter sent as a bare name (<c>Title="Title"</c>) shows the name as
/// text, and the regression tests here catch that.
/// </summary>
public class ErpWrapperTests : AsyncBunitContext
{
    public ErpWrapperTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        // The table's dropdowns and measured elements ask the browser for these (as the grid tests do).
        JSInterop.Setup<AntDesign.JsInterop.HtmlElement>(_ => true).SetResult(new AntDesign.JsInterop.HtmlElement());
        JSInterop.Setup<AntDesign.JsInterop.DomRect>(_ => true).SetResult(new AntDesign.JsInterop.DomRect());
        Services.AddAntDesign();
        Services.AddSingleton<IUiStateStore>(new MemoryUiStateStore());
    }

    public sealed class Customer
    {
        public string Name { get; set; } = "";
    }

    public sealed class Order
    {
        public string Number { get; set; } = "";
    }

    [Fact]
    public void ApprovalTimeline_ShowsEachStepAndTheAriaLabelItWasGiven()
    {
        var steps = new List<ApprovalStep>
        {
            new("Submitted", ApprovalState.Done, "Ana", new DateTime(2026, 11, 10, 9, 0, 0)),
            new("Manager review", ApprovalState.Current, "Ben"),
        };

        var cut = Render<ModulusApprovalTimeline>(p => p
            .Add(x => x.Steps, steps)
            .Add(x => x.AriaLabel, "Approval steps"));

        Assert.Contains("Submitted", cut.Markup);
        Assert.Contains("Manager review", cut.Markup);
        Assert.Contains("Approval steps", cut.Markup);
        Assert.DoesNotContain("AriaLabel", cut.Markup);
    }

    [Fact]
    public void AuditTrail_ShowsTheEntries_AndTheActionFilterIsNotTheWordAction()
    {
        var entries = new List<AuditEntry>
        {
            new(1, new DateTime(2026, 11, 14, 10, 30, 0), "Ana", "Edited", "Quantity", "2", "5"),
            new(2, new DateTime(2026, 11, 13, 16, 5, 0), "Ben", "Approved", Note: "Within budget"),
        };

        // Without a filter every row shows. (Action="Action" once hid every row, because no action has that name.)
        var all = Render<ModulusAuditTrail>(p => p.Add(x => x.Entries, entries));
        Assert.Contains("Within budget", all.Markup);
        Assert.Contains("Quantity", all.Markup);

        var filtered = Render<ModulusAuditTrail>(p => p
            .Add(x => x.Entries, entries)
            .Add(x => x.Action, "Approved"));
        Assert.Contains("Within budget", filtered.Markup);
        Assert.DoesNotContain("Quantity", filtered.Markup);
    }

    [Fact]
    public void DashboardGrid_ShowsEachTileWithItsBody()
    {
        var tiles = new List<DashboardTileSpec> { new("sales", "Sales"), new("stock", "Stock") };

        var cut = Render<ModulusDashboardGrid>(p => p
            .Add(x => x.Id, "home")
            .Add(x => x.Tiles, tiles)
            .Add(x => x.Tile, (RenderFragment<DashboardTileSpec>)(t => b => b.AddContent(0, "Body of " + t.Title))));

        Assert.Contains("Body of Sales", cut.Markup);
        Assert.Contains("Body of Stock", cut.Markup);
        Assert.DoesNotContain(">Id<", cut.Markup);
    }

    [Fact]
    public void ProTable_ShowsItsHeaderTitle_NotTheParameterName()
    {
        var cut = Render<ModulusProTable<Order>>(p => p
            .Add(x => x.HeaderTitle, "Open orders")
            .Add(x => x.Request, _ => Task.FromResult(new ProTableResponse<Order>
            {
                Data = [new Order { Number = "PO-2041" }],
                Total = 1,
            })));

        Assert.Contains("Open orders", cut.Markup);
        Assert.DoesNotContain(">HeaderTitle<", cut.Markup);
    }

    [Fact]
    public void ModalForm_ShowsTheTitleAndTheSubmitText()
    {
        var cut = Render<ModulusModalForm<Customer>>(p => p
            .Add(x => x.Title, "New customer")
            .Add(x => x.Visible, true)
            .Add(x => x.Model, new Customer { Name = "Ada" })
            .Add(x => x.SubmitText, "Create"));

        Assert.Contains("New customer", cut.Markup);
        Assert.Contains("Create", cut.Markup);
        Assert.DoesNotContain(">Title<", cut.Markup);
    }

    [Fact]
    public void DrawerForm_ShowsTheTitleAndTheFooterButtons()
    {
        var cut = Render<ModulusDrawerForm<Customer>>(p => p
            .Add(x => x.Title, "Edit customer")
            .Add(x => x.Visible, true)
            .Add(x => x.Model, new Customer { Name = "Ada" })
            .Add(x => x.SubmitText, "Save changes")
            .Add(x => x.CancelText, "Discard"));

        Assert.Contains("Edit customer", cut.Markup);
        Assert.Contains("Save changes", cut.Markup);
        Assert.Contains("Discard", cut.Markup);
        Assert.DoesNotContain(">Title<", cut.Markup);
    }
}
