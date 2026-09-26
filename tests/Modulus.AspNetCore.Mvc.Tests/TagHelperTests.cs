using AntDesign;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc.TagHelpers;
using Modulus.Ui.Abstractions;
using Xunit;

namespace Modulus.AspNetCore.Mvc.Tests;

public class AntIconsTests
{
    // Guards the reflection into AntDesign's internal icon store: an AntDesign upgrade
    // that moves IconService.GetIconImg fails here instead of rendering blank icons.
    [Theory]
    [InlineData("dashboard", IconThemeType.Outline)]
    [InlineData("check-circle", IconThemeType.Fill)]
    public void Svg_ResolvesKnownIcons(string name, IconThemeType theme)
    {
        var svg = AntIcons.Svg(name, theme);

        Assert.NotNull(svg);
        Assert.StartsWith("<svg ", svg);
        Assert.Contains("aria-hidden=\"true\"", svg);
        Assert.Contains("fill=\"currentColor\"", svg);
    }

    [Fact]
    public void Svg_ReturnsNullForUnknownIcon() => Assert.Null(AntIcons.Svg("no-such-icon"));

    [Fact]
    public void Span_EncodesName() =>
        Assert.Contains("anticon-&lt;x&gt;", AntIcons.Span("<x>"));
}

public class TagHelperTests
{
    private static async Task<TagHelperOutput> RunAsync(TagHelper helper, string tag, string childContent = "")
    {
        var context = new TagHelperContext(tag, new TagHelperAttributeList(), new Dictionary<object, object>(), "id");
        var output = new TagHelperOutput(tag, new TagHelperAttributeList(), (_, _) =>
            Task.FromResult<TagHelperContent>(new DefaultTagHelperContent().SetHtmlContent(childContent)));
        await helper.ProcessAsync(context, output);
        return output;
    }

    private static string Classes(TagHelperOutput output) => output.Attributes["class"].Value.ToString()!;

    [Fact]
    public async Task Button_RendersAntDesignButton()
    {
        var output = await RunAsync(new ButtonTagHelper { Type = "primary", Size = "small", Danger = true, Icon = "plus" }, "app-button", "Add");

        Assert.Equal("button", output.TagName);
        Assert.Equal("button", output.Attributes["type"].Value);
        Assert.Equal("ant-btn ant-btn-primary ant-btn-sm ant-btn-dangerous", Classes(output));
        Assert.Contains("anticon-plus", output.Content.GetContent());
        Assert.EndsWith("<span>Add</span>", output.Content.GetContent());
    }

    [Fact]
    public async Task Button_WithHrefRendersLink_AndIconOnlyClass()
    {
        var output = await RunAsync(new ButtonTagHelper { Href = "/customers", Icon = "reload" }, "app-button");

        Assert.Equal("a", output.TagName);
        Assert.Equal("/customers", output.Attributes["href"].Value);
        Assert.False(output.Attributes.ContainsName("type"));
        Assert.Contains("ant-btn-icon-only", Classes(output));
    }

    [Theory]
    [InlineData("blue", "ant-tag ant-tag-blue")]
    [InlineData("default", "ant-tag")]
    [InlineData("#722ed1", "ant-tag ant-tag-has-color")]
    public async Task Tag_MapsColor(string color, string expected)
    {
        var output = await RunAsync(new TagTagHelper { Color = color }, "app-tag");

        Assert.Equal(expected, Classes(output));
    }

    [Fact]
    public async Task Badge_EncodesText()
    {
        var output = await RunAsync(new BadgeTagHelper { Status = "success", Text = "<b>Active</b>" }, "app-badge");

        var html = output.Content.GetContent();
        Assert.Contains("ant-badge-status-success", html);
        Assert.Contains("&lt;b&gt;Active&lt;/b&gt;", html);
    }

    [Fact]
    public async Task Badge_WrapsChildContentWithCount()
    {
        var output = await RunAsync(new BadgeTagHelper { Count = 120, OverflowCount = 99 }, "app-badge", "<span>Inbox</span>");

        var html = output.Content.GetContent();
        Assert.Contains("ant-badge", Classes(output));
        Assert.StartsWith("<span>Inbox</span>", html);
        Assert.Contains("99+", html);
    }
}

/// <summary>Tag helpers added for full Ant Design coverage in MVC / Razor Pages.</summary>
public class CoverageTagHelperTests
{
    private static TagHelperContext Context(string tag, IDictionary<object, object> items) =>
        new(tag, new TagHelperAttributeList(), items, Guid.NewGuid().ToString("N"));

    private static TagHelperOutput Output(string tag, Func<Task<string>> child, TagHelperAttributeList? attributes = null) =>
        new(tag, attributes ?? [], async (_, _) => new DefaultTagHelperContent().SetHtmlContent(await child()));

    private static async Task<TagHelperOutput> RunAsync(TagHelper helper, string tag, string childContent = "", TagHelperAttributeList? attributes = null)
    {
        var output = Output(tag, () => Task.FromResult(childContent), attributes);
        await helper.ProcessAsync(Context(tag, new Dictionary<object, object>()), output);
        return output;
    }

    /// <summary>Runs a parent whose child content is the given child helpers, sharing context.Items as Razor does.</summary>
    private static async Task<TagHelperOutput> RunParentAsync(TagHelper parent, string tag, params (TagHelper Helper, string Tag, string Content)[] children)
    {
        var items = new Dictionary<object, object>();
        var output = Output(tag, async () =>
        {
            var html = "";
            foreach (var (helper, childTag, content) in children)
            {
                var childOutput = Output(childTag, () => Task.FromResult(content));
                await helper.ProcessAsync(Context(childTag, new Dictionary<object, object>(items)), childOutput);
                html += Render(childOutput);
            }
            return html;
        });
        await parent.ProcessAsync(Context(tag, items), output);
        return output;
    }

    private static string Render(TagHelperOutput output)
    {
        using var writer = new StringWriter();
        output.WriteTo(writer, System.Text.Encodings.Web.HtmlEncoder.Default);
        return writer.ToString();
    }

    private static string Classes(TagHelperOutput output) => output.Attributes["class"].Value.ToString()!;

    [Fact]
    public async Task Alert_WithDescription_UsesOutlineIconAndAlertRole()
    {
        var output = await RunAsync(new AlertTagHelper { Type = "warning", Message = "Trial ends", Description = "In 3 days", ShowIcon = true }, "app-alert");

        Assert.Equal("ant-alert ant-alert-warning ant-alert-with-description", Classes(output));
        Assert.Equal("alert", output.Attributes["role"].Value);
        var html = output.Content.GetContent();
        Assert.Contains("<div class=\"ant-alert-message\">Trial ends</div>", html);
        Assert.Contains("<div class=\"ant-alert-description\">In 3 days</div>", html);
        Assert.Contains("anticon-exclamation-circle", html);
    }

    [Fact]
    public async Task Alert_InfoWithoutIcon_IsPoliteStatus()
    {
        var output = await RunAsync(new AlertTagHelper { Message = "Saved" }, "app-alert");

        Assert.Equal("status", output.Attributes["role"].Value);
        Assert.Contains("ant-alert-no-icon", Classes(output));
    }

    [Fact]
    public async Task Progress_Line_ClampsPercentAndExposesProgressbar()
    {
        var output = await RunAsync(new ProgressTagHelper { Percent = 140 }, "app-progress");

        Assert.Equal("progressbar", output.Attributes["role"].Value);
        Assert.Equal("100", output.Attributes["aria-valuenow"].Value);
        Assert.Contains("ant-progress-status-success", Classes(output));
        Assert.Contains("width:100%", output.Content.GetContent());
    }

    [Fact]
    public async Task Progress_Circle_UsesRcProgressGeometry()
    {
        // Stroke 6 → radius 47 → circumference 2π·47 ≈ 295.31; 75% of it ≈ 221.482.
        var output = await RunAsync(new ProgressTagHelper { Percent = 75, Type = "circle" }, "app-progress");

        var html = output.Content.GetContent();
        Assert.Contains("ant-progress-circle", Classes(output));
        Assert.Contains("stroke-dasharray:295.31px, 295.31px", html);
        Assert.Contains("stroke-dasharray:221.482px, 295.31px", html);
        Assert.Contains(">75%</span>", html);
    }

    [Fact]
    public async Task Steps_DeriveStatusFromCurrent_AndHonourOverrides()
    {
        var output = await RunParentAsync(new StepsTagHelper { Current = 1 }, "app-steps",
            (new StepTagHelper { Title = "Account" }, "app-step", ""),
            (new StepTagHelper { Title = "Billing" }, "app-step", ""),
            (new StepTagHelper { Title = "Import", Status = "error" }, "app-step", ""));

        var html = output.Content.GetContent();
        Assert.Contains("ant-steps-item ant-steps-item-finish", html);
        Assert.Contains("ant-steps-item ant-steps-item-process ant-steps-item-active\" aria-current=\"step\"", html);
        Assert.Contains("ant-steps-item ant-steps-item-error", html);
        Assert.Contains("anticon-check", html);
        // The last step has no tail.
        Assert.Equal(2, html.Split("ant-steps-item-tail").Length - 1);
    }

    [Fact]
    public async Task Timeline_MarksOnlyTheLastItem()
    {
        var output = await RunParentAsync(new TimelineTagHelper(), "app-timeline",
            (new TimelineItemTagHelper { Color = "green" }, "app-timeline-item", "Paid"),
            (new TimelineItemTagHelper { Color = "#722ed1" }, "app-timeline-item", "Created"));

        var html = output.Content.GetContent();
        Assert.Equal("ul", output.TagName);
        Assert.Equal(1, html.Split("ant-timeline-item-last").Length - 1);
        Assert.Contains("<li class=\"ant-timeline-item-last ant-timeline-item\">", html);
        Assert.Contains("ant-timeline-item-head ant-timeline-item-head-green", html);
        Assert.Contains("border-color:#722ed1", html);
        Assert.DoesNotContain("ant-timeline-item-last-", html);
    }

    [Fact]
    public async Task Table_AddsAntClasses_AndEmptyPlaceholder()
    {
        var output = await RunAsync(new TableTagHelper { Size = "small", EmptyText = "No invoices" }, "app-table",
            "<thead><tr><th>Invoice</th><th>Amount</th></tr></thead><tbody></tbody>");

        var html = output.Content.GetContent();
        Assert.Contains("<div class=\"ant-table ant-table-small\">", html);
        Assert.Contains("<thead class=\"ant-table-thead\"", html);
        Assert.Contains("<th class=\"ant-table-cell\"", html);
        Assert.Contains("<tr class=\"ant-table-placeholder\"><td class=\"ant-table-cell\" colspan=\"2\">", html);
        Assert.Contains("No invoices", html);
    }

    [Fact]
    public async Task Table_ClassesBodyRows_ButKeepsAuthorClasses()
    {
        var output = await RunAsync(new TableTagHelper(), "app-table",
            "<tbody><tr><td>A</td></tr><tr class=\"m-selected\"><td class=\"num\">1</td></tr></tbody>");

        var html = output.Content.GetContent();
        Assert.Contains("<tr class=\"ant-table-row\"", html);
        Assert.Contains("<tr class=\"m-selected\">", html);
        Assert.Contains("<td class=\"num\">", html);
    }

    [Fact]
    public async Task Table_LeavesOutHiddenColumns()
    {
        var output = await RunAsync(new TableTagHelper { HiddenColumns = ["office"] }, "app-table",
            "<colgroup><col /><col data-col=\"office\" style=\"width:1px\" /></colgroup>" +
            "<thead><tr><th>Name</th><th data-col=\"office\" class=\"x\"><a>Office</a></th></tr></thead>" +
            "<tbody><tr><td>Ann</td><td data-col=\"office\"><span>Berlin</span></td></tr></tbody>");

        var html = output.Content.GetContent();
        Assert.DoesNotContain("office", html);
        Assert.DoesNotContain("Berlin", html);
        Assert.Contains("Ann", html);
        Assert.Contains("<colgroup><col /></colgroup>", html);
    }

    [Fact]
    public async Task Table_BindsDensity_AndScrollsHorizontally()
    {
        var output = await RunAsync(new TableTagHelper { Size = "small", Density = true, ScrollX = 1100, Layout = "fixed" }, "app-table",
            "<tbody><tr><td>A</td></tr></tbody>");

        var html = output.Content.GetContent();
        Assert.Contains("<div class=\"ant-table ant-table-scroll-horizontal\" :class=\"density\">", html);
        Assert.Contains("<div class=\"ant-table-content\" style=\"overflow: auto hidden;\">", html);
        Assert.Contains("<table style=\"width: 1100px; min-width: 100%; table-layout: fixed;\">", html);
    }

    [Fact]
    public async Task Table_RendersFooterSlotUnderTheTable()
    {
        var output = await RunParentAsync(new TableTagHelper(), "app-table",
            (new TableFooterTagHelper(), "table-footer", "<ul class=\"ant-pagination\"></ul>"));

        Assert.EndsWith("</table></div></div></div><ul class=\"ant-pagination\"></ul></div></div>", output.Content.GetContent());
    }

    [Fact]
    public async Task TableSettings_RendersReloadAndDensityMenu()
    {
        var output = await RunAsync(new TableSettingsTagHelper { ReloadHref = "/customers?page=2" }, "app-table-settings");

        var html = output.Content.GetContent();
        Assert.Equal("m-table-toolbar-settings", Classes(output));
        Assert.Contains("href=\"/customers?page=2\" aria-label=\"Reload\"", html);
        Assert.Contains("aria-label=\"Table density\"", html);
        Assert.Contains("@click=\"setDensity('')\"", html);
        Assert.Contains("@click=\"setDensity('ant-table-middle')\"", html);
        Assert.Contains("@click=\"setDensity('ant-table-small')\"", html);
        Assert.Contains(">Comfortable<", html);
        Assert.Contains(">Standard<", html);
        Assert.Contains(">Compact<", html);
    }

    [Fact]
    public async Task TableSettings_WithoutReloadOrDensity_IsEmpty()
    {
        var output = await RunAsync(new TableSettingsTagHelper { Density = false }, "app-table-settings");

        Assert.Equal("", output.Content.GetContent());
    }

    [Fact]
    public async Task TableColumns_LinksToggleEachColumn()
    {
        TableColumn[] columns = [new("office", "Office"), new("salary", "Salary")];
        var output = await RunAsync(new TableColumnsTagHelper
        {
            Columns = columns,
            Hidden = new HashSet<string> { "salary" },
            Href = hide => $"/t?hide={hide}",
        }, "app-table-columns");

        var html = output.Content.GetContent();
        Assert.Null(output.TagName);
        Assert.Contains("<span>Columns</span>", html);
        // Office is shown: its link hides it (with salary). Salary is hidden: its link shows everything.
        Assert.Contains("href=\"/t?hide=office,salary\"", html);
        Assert.Contains("href=\"/t?hide=\"", html);
        Assert.Contains("#ti-square-check\"></use></svg>Office", html);
        Assert.Contains("#ti-square\"></use></svg>Salary", html);
    }

    [Fact]
    public async Task Switch_RendersAccessibleSwitchWithHiddenValue()
    {
        var output = await RunAsync(new SwitchTagHelper { Name = "Alerts", Checked = true }, "app-switch");

        var html = Render(output);
        Assert.Contains("role=\"switch\"", html);
        Assert.Contains("ant-switch-checked", html);
        Assert.Contains("name=\"Alerts\"", html);
        Assert.Contains("type=\"hidden\"", html);
    }

    [Fact]
    public async Task Input_MovesAuthorAttributesOntoTheInnerInput()
    {
        var attributes = new TagHelperAttributeList { { "placeholder", "Search" }, { "hx-get", "/customers" }, { "class", "m-wide" } };
        var output = await RunAsync(new InputTagHelper { Name = "q", PrefixIcon = "search", AllowClear = true }, "app-input", attributes: attributes);

        var html = Render(output);
        Assert.Contains("ant-input-affix-wrapper", html);
        Assert.Null(output.TagName);
        Assert.Contains("class=\"ant-input-affix-wrapper m-wide\"", html);
        Assert.Matches("<input[^>]*name=\"q\"[^>]*placeholder=\"Search\"[^>]*hx-get=\"/customers\"", html);
        Assert.Contains("anticon-search", html);
        Assert.Contains("ant-input-clear-icon", html);
    }

    [Fact]
    public async Task Pagination_LinkModeRendersPlainHrefs()
    {
        var output = await RunAsync(new PaginationTagHelper { Total = 85, Current = 2, Url = "/customers", Table = false }, "app-pagination");

        var html = Render(output);
        Assert.Contains("href=\"/customers?current=3&amp;pageSize=10\"", html);
        Assert.Contains("<li class=\"ant-pagination-item ant-pagination-item-2 ant-pagination-item-active\" aria-current=\"page\">", html);
        Assert.DoesNotContain("hx-get", html);
        Assert.Contains("ant-pagination-item-active", html);
    }

    [Fact]
    public async Task Pagination_HtmxModeKeepsHxGet()
    {
        var output = await RunAsync(new PaginationTagHelper { Total = 85, Current = 9, Url = "/customers", HxTarget = "#table" }, "app-pagination");

        var html = Render(output);
        Assert.Contains("hx-get=\"/customers?current=8&amp;pageSize=10\"", html);
        Assert.Contains("<li class=\"ant-pagination-next ant-pagination-disabled\" aria-disabled=\"true\">", html);
        Assert.Contains("hx-target=\"#table\"", html);
    }
    [Theory]
    [InlineData(1, 90, "1 2 3 4 5 >> 9")]
    [InlineData(4, 90, "1 2 3 4 5 6 >> 9")]
    [InlineData(5, 90, "1 << 3 4 5 6 7 >> 9")]
    [InlineData(9, 90, "1 << 5 6 7 8 9")]
    [InlineData(3, 70, "1 2 3 4 5 6 7")]
    public async Task Pagination_ShowsTheSamePagesAsAntDesign(int current, int total, string expected)
    {
        var output = await RunAsync(new PaginationTagHelper { Total = total, Current = current, Url = "/customers" }, "app-pagination");

        var items = System.Text.RegularExpressions.Regex.Matches(Render(output), "ant-pagination-(?:item-(\\d+)|jump-(prev|next))[ \"]")
            .Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value == "prev" ? "<<" : ">>");
        Assert.Equal(expected, string.Join(' ', items));
    }

    [Fact]
    public async Task Pagination_JumpItemsMoveFivePages()
    {
        var output = await RunAsync(new PaginationTagHelper { Total = 200, Current = 7, Url = "/customers" }, "app-pagination");

        var html = Render(output);
        Assert.Contains("<li class=\"ant-pagination-jump-prev ant-pagination-jump-prev-custom-icon\"><a class=\"ant-pagination-item-link\" href=\"/customers?current=2&amp;pageSize=10\"", html);
        Assert.Contains("<li class=\"ant-pagination-jump-next ant-pagination-jump-next-custom-icon\"><a class=\"ant-pagination-item-link\" href=\"/customers?current=12&amp;pageSize=10\"", html);
        Assert.Contains("anticon-double-right ant-pagination-item-link-icon", html);
        Assert.Contains("<span class=\"ant-pagination-item-ellipsis\">•••</span>", html);
    }

    [Fact]
    public async Task TextArea_ShowCountRendersAntDesignStructure()
    {
        var attributes = new TagHelperAttributeList { { "maxlength", "300" } };
        var output = await RunAsync(new TextAreaTagHelper { Name = "Bio", Value = "Hello", Rows = 3, ShowCount = true }, "app-textarea", attributes: attributes);

        var html = Render(output);
        Assert.Equal("ant-input-textarea ant-input-textarea-show-count", Classes(output));
        Assert.Equal("5 / 300", output.Attributes["data-count"].Value);
        Assert.Contains("<span class=\"ant-input-affix-wrapper\"><textarea class=\"ant-input\" style=\"height:80.003px;overflow-y:auto;overflow-x:hidden\" rows=\"3\"", html);
        Assert.Contains("<span class=\"ant-input-textarea-suffix\"></span>", html);
    }

    [Fact]
    public async Task TextArea_InsideFormItemHangsTheCounterIntoTheHelpRow()
    {
        var output = await RunParentAsync(new FormItemTagHelper { Label = "Bio" }, "app-form-item",
            (new TextAreaTagHelper { Name = "Bio", ShowCount = true }, "app-textarea", ""));

        Assert.Contains("ant-input-textarea-show-count ant-input-textarea-in-form-item", Render(output));
    }

    [Fact]
    public async Task Checkbox_DisabledMatchesAntDesign()
    {
        var output = await RunAsync(new CheckboxTagHelper { Name = "ids", Value = "1", Disabled = true }, "app-checkbox");

        var html = Render(output);
        Assert.Equal("ant-checkbox-wrapper ant-checkbox-wrapper-disabled", Classes(output));
        Assert.Contains("<span class=\"ant-checkbox ant-checkbox-disabled\"><input type=\"checkbox\" class=\"ant-checkbox-input\" name=\"ids\" value=\"1\" disabled", html);
    }
}
