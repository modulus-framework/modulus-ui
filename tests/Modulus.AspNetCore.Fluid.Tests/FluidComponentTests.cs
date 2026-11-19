using System.Runtime.CompilerServices;
using Modulus.AspNetCore.Fluid;
using Xunit;

namespace Modulus.AspNetCore.Fluid.Tests;

/// <summary>Renders the Fluid component templates with data, so their markup and escaping are checked, not only that they parse.</summary>
public class FluidComponentTests
{
    private static readonly string TemplateDirectory = Path.GetFullPath(
        Path.Combine(Path.GetDirectoryName(ThisFile())!, "..", "..", "src", "Modulus.Ui.Templates", "templates", "fluid"));

    private static string ThisFile([CallerFilePath] string path = "") => path;

    private static string Render(string name, object model)
    {
        var liquid = new ModulusLiquid();
        var template = liquid.Parse(File.ReadAllText(Path.Combine(TemplateDirectory, name + ".liquid")));
        var data = model.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(model));
        return liquid.Render(template, data);
    }

    // Liquid reads list items as dictionaries; properties of anonymous types are not visible to it.
    private static Dictionary<string, object?> Row(params (string Key, object? Value)[] fields) =>
        fields.ToDictionary(f => f.Key, f => f.Value);

    [Fact]
    public void Badge_MapsActiveToSuccess_AndEscapesTheLabel()
    {
        var html = Render("badge", new { status = "Active", label = "<b>Live</b>" });

        Assert.Contains("ant-badge-status-success", html);
        Assert.Contains("&lt;b&gt;Live&lt;/b&gt;", html);
        Assert.DoesNotContain("<b>", html);
    }

    [Fact]
    public void Tag_UsesNamedColour_OrHexStyle_AndCloseButton()
    {
        Assert.Contains("m-tag m-tag-green", Render("tag", new { text = "Shipped", color = "green", closable = false }));
        var hex = Render("tag", new { text = "Custom", color = "#1677ff", closable = true });
        Assert.Contains("background: #1677ff", hex);
        Assert.Contains("m-tag-close", hex);
    }

    [Fact]
    public void StatusBadge_UsesTheTone()
    {
        var html = Render("status-badge", new { text = "Failed", tone = "danger" });

        Assert.Contains("m-status-badge m-status-danger", html);
        Assert.Contains("Failed", html);
    }

    [Fact]
    public void EmptyState_ShowsTitleMessageAndAction()
    {
        var html = Render("empty-state", new { title = "No invoices", message = "Create one to start.", action_text = "New invoice", action_url = "/invoices/new" });

        Assert.Contains("No invoices", html);
        Assert.Contains("Create one to start.", html);
        Assert.Contains("href=\"/invoices/new\"", html);
    }

    [Fact]
    public void EmptyState_HidesTheMessageAndActionWhenBlank()
    {
        var html = Render("empty-state", new { message = "", action_text = "" });

        Assert.Contains("Nothing here yet", html);
        Assert.DoesNotContain("m-empty-subtitle", html);
        Assert.DoesNotContain("ant-btn", html);
    }

    [Fact]
    public void Steps_MarksFinishedCurrentAndWaitingItems()
    {
        var html = Render("steps", new
        {
            items = new[] { Row(("title", "Cut")), Row(("title", "Sew"), ("description", "Line 3")), Row(("title", "Pack")) },
            current = 1,
        });

        Assert.Contains("ant-steps-item-finish", html);
        Assert.Contains("ant-steps-item-process", html);
        Assert.Contains("ant-steps-item-wait", html);
        Assert.Contains("aria-current=\"step\"", html);
        Assert.Contains("Line 3", html);
    }

    [Fact]
    public void Timeline_RendersItemsAndPendingText_EscapingEachText()
    {
        var html = Render("timeline", new
        {
            items = new[] { Row(("text", "<i>Created</i>"), ("color", "green"), ("label", "Mon")) },
            pending = "Waiting for approval",
        });

        Assert.Contains("ant-timeline-item-head-green", html);
        Assert.Contains("&lt;i&gt;Created&lt;/i&gt;", html);
        Assert.Contains("Waiting for approval", html);
        Assert.Contains("ant-timeline-item-head-gray", html);
    }

    [Fact]
    public void Result_UsesTheStatusCodeAndOneActionLink()
    {
        var html = Render("result", new { status = "404", title = "Page not found", action_text = "Home", action_url = "/" });

        Assert.Contains("ant-result-404", html);
        Assert.Contains("ant-result-icon", html);
        Assert.Contains("Page not found", html);
        Assert.Contains("href=\"/\"", html);
    }

    [Fact]
    public void Descriptions_ShowsEachLabelAndValueRow()
    {
        var html = Render("descriptions", new
        {
            title = "Customer",
            items = new[] { Row(("label", "Name"), ("value", "Asha")), Row(("label", "City"), ("value", "Dhaka")) },
        });

        Assert.Contains("ant-descriptions-bordered", html);
        Assert.Contains("Customer", html);
        Assert.Contains("Asha", html);
        Assert.Contains("Dhaka", html);
    }

    [Fact]
    public void Breadcrumb_LinksEveryItemExceptTheLast()
    {
        var html = Render("breadcrumb", new { items = new[] { Row(("label", "Home"), ("url", "/")), Row(("label", "Users"), ("url", "/users")), Row(("label", "Edit")) } });

        Assert.Contains("href=\"/users\"", html);
        Assert.Contains("aria-current=\"page\"", html);
        Assert.Contains(">Edit<", html);
    }

    [Fact]
    public void Breadcrumb_WithNoItems_ShowsHome()
    {
        Assert.Contains(">Home<", Render("breadcrumb", new { }));
    }

    [Fact]
    public void Pagination_LinksPreviousAndNextWithinRange()
    {
        var middle = Render("pagination", new { current_page = 2, total_pages = 3, base_url = "/orders" });
        Assert.Contains("href=\"/orders?page=1\"", middle);
        Assert.Contains("href=\"/orders?page=3\"", middle);
        Assert.Contains("2 / 3", middle);

        var last = Render("pagination", new { current_page = 3, total_pages = 3, base_url = "/orders" });
        Assert.DoesNotContain("?page=4", last);
        Assert.Contains("ant-pagination-next ant-pagination-disabled", last);
    }

    [Fact]
    public void PageHeader_ShowsPretitleTitleAndSubtitle()
    {
        var html = Render("page-header", new { pretitle = "Sales", title = "Orders", subtitle = "Last 30 days" });

        Assert.Contains("m-pretitle", html);
        Assert.Contains("Orders", html);
        Assert.Contains("Last 30 days", html);
    }

    [Fact]
    public void List_RendersItemsWithAvatarAndLink()
    {
        var html = Render("list", new
        {
            header = "Team",
            items = new[] { Row(("title", "Asha"), ("description", "Admin"), ("href", "/people/1"), ("avatar_name", "asha")) },
        });

        Assert.Contains("Team", html);
        Assert.Contains("href=\"/people/1\"", html);
        Assert.Contains(">A<", html);
        Assert.Contains("Admin", html);
    }

    [Fact]
    public void List_WithNoItems_ShowsTheEmptyText()
    {
        Assert.Contains("Nothing here yet.", Render("list", new { items = Array.Empty<object>() }));
    }

    [Fact]
    public void Avatar_ShowsInitial_OrImage()
    {
        Assert.Contains(">R<", Render("avatar", new { name = "rafi" }));

        var withImage = Render("avatar", new { name = "rafi", src = "/img/rafi.png" });
        Assert.Contains("<img src=\"/img/rafi.png\"", withImage);
    }

    [Fact]
    public void Statistic_ShowsPrefixSuffixAndTheValue()
    {
        var html = Render("statistic", new { title = "Revenue", value = 1234, prefix = "$", suffix = "USD" });

        Assert.Contains("Revenue", html);
        Assert.Contains("1234", html);
        Assert.Contains("ant-statistic-content-prefix", html);
        Assert.Contains("ant-statistic-content-suffix", html);
    }

    [Fact]
    public void Divider_WithText_RendersTheTextInsideTheSeparator()
    {
        var html = Render("divider", new { text = "Billing", orientation = "left", dashed = true, plain = false, type = "horizontal" });

        Assert.Contains("class=\"ant-divider ant-divider-horizontal ant-divider-with-text ant-divider-with-text-left ant-divider-dashed\"", html);
        Assert.Contains("<span class=\"ant-divider-inner-text\">Billing</span>", html);
        Assert.Contains("role=\"separator\"", html);
    }

    [Fact]
    public void Divider_Vertical_IsASpanWithoutText()
    {
        var html = Render("divider", new { type = "vertical", text = "ignored" });

        Assert.Contains("<span class=\"ant-divider ant-divider-vertical\" role=\"separator\"></span>", html);
        Assert.DoesNotContain("ant-divider-inner-text", html);
    }

    [Fact]
    public void Divider_EscapesItsText()
    {
        var html = Render("divider", new { text = "<i>x</i>" });

        Assert.Contains("&lt;i&gt;x&lt;/i&gt;", html);
        Assert.DoesNotContain("<i>", html);
    }

    [Fact]
    public void Skeleton_DrawsTitleAndRows_WithTheLastRowShorter()
    {
        var html = Render("skeleton", new { rows = 3, avatar = false, active = true, round = false, title = true });

        Assert.Contains("class=\"ant-skeleton ant-skeleton-active\"", html);
        Assert.Contains("ant-skeleton-title", html);
        Assert.Equal(3, CountOf(html, "<li"));
        Assert.Contains("<li style=\"width:61%\"></li>", html);
        Assert.DoesNotContain("ant-skeleton-avatar", html);
    }

    [Fact]
    public void Skeleton_WithAvatar_AddsTheHeaderAndWidensTheTitle()
    {
        var html = Render("skeleton", new { rows = 2, avatar = true, active = false, title = true });

        Assert.Contains("ant-skeleton-with-avatar", html);
        Assert.Contains("ant-skeleton-avatar-circle", html);
        Assert.Contains("width:50%", html);
        Assert.DoesNotContain("ant-skeleton-active", html);
    }

    [Fact]
    public void Skeleton_WithoutRowsOrTitle_UsesDefaultsAndNoTitle()
    {
        var defaults = Render("skeleton", new { });
        Assert.Equal(3, CountOf(defaults, "<li"));

        var noTitle = Render("skeleton", new { rows = 1, title = false });
        Assert.DoesNotContain("ant-skeleton-title", noTitle);
        Assert.Equal(1, CountOf(noTitle, "<li"));
    }

    [Fact]
    public void Checkbox_IsCheckedAndEscapesTheLabel()
    {
        var html = Render("checkbox", new { name = "terms", value = "yes", label = "<i>Agree</i>", @checked = true });

        Assert.Contains("ant-checkbox-checked", html);
        Assert.Contains("name=\"terms\"", html);
        Assert.Contains(" checked", html);
        Assert.Contains("&lt;i&gt;Agree&lt;/i&gt;", html);
    }

    [Fact]
    public void Rate_FillsStarsUpToTheValue()
    {
        var html = Render("rate", new { name = "stars", value = 3, count = 5 });

        Assert.Equal(3, CountOf(html, "ant-rate-star-full"));
        Assert.Equal(2, CountOf(html, "ant-rate-star-zero"));
    }

    [Fact]
    public void Tabs_MarksTheActiveTabAndPanel()
    {
        var html = Render("tabs", new
        {
            items = new[] { Row(("id", "a"), ("label", "First"), ("content", "one")), Row(("id", "b"), ("label", "Second"), ("content", "two")) },
            active = "b",
        });

        Assert.Contains("href=\"#tab-b\"", html);
        Assert.Contains("ant-tabs-tabpane-active", html);
        Assert.Equal(1, CountOf(html, "ant-tabs-tab-active"));
    }

    [Fact]
    public void Modal_IsDrawnOnlyWhenOpen()
    {
        Assert.DoesNotContain("ant-modal-root", Render("modal", new { open = false, title = "Hi" }));
        Assert.Contains("ant-modal-root", Render("modal", new { open = true, title = "Hi", content = "body" }));
    }

    [Fact]
    public void TreeSelect_IndentsByLevel()
    {
        var html = Render("tree-select", new
        {
            items = new[] { Row(("value", "1"), ("label", "Root"), ("level", 0)), Row(("value", "2"), ("label", "Child"), ("level", 1)) },
            value = "2",
        });

        // The renderer escapes the dash as an entity; the browser shows the same character.
        Assert.Contains("&#x2014; Child", html);
        Assert.Contains("value=\"2\" selected", html);
    }

    private static int CountOf(string text, string needle)
    {
        var count = 0;
        for (var i = text.IndexOf(needle, StringComparison.Ordinal); i >= 0; i = text.IndexOf(needle, i + needle.Length, StringComparison.Ordinal)) count++;
        return count;
    }
}
