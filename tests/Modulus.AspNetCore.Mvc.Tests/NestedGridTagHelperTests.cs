using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Primitives;
using Modulus.AspNetCore.Mvc.TagHelpers;
using Modulus.Ui.Abstractions;
using Xunit;

namespace Modulus.AspNetCore.Mvc.Tests;

/// <summary>NestedGridForm (URL state, loaded ids, changes) and app-nested-grid.</summary>
public class NestedGridTagHelperTests
{
    private static NestedGridSettings Settings() => new()
    {
        Levels =
        [
            new() { Title = "Orders", PageSize = 2, Columns = [new() { Key = "order", Title = "Order" }, new() { Key = "qty", Title = "Qty", Type = GridCellType.Number }] },
            new() { Title = "Styles", Columns = [new() { Key = "style", Title = "Style" }] },
        ],
    };

    private static GridRecord Row(string id, string order) => new(id, new Dictionary<string, string?> { ["order"] = order, ["qty"] = "10" });

    private static async Task<string> RenderAsync(NestedGridView view)
    {
        var helper = new NestedGridTagHelper { View = view, Url = "/nested/level?depth=2" };
        var output = new TagHelperOutput("app-nested-grid", [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        await helper.ProcessAsync(new TagHelperContext("app-nested-grid", [], new Dictionary<object, object>(), "n1"), output);
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    [Fact]
    public void ReadRequest_AndUrl_RoundTripTheState()
    {
        var query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["level"] = "1", ["parent"] = "PO-1", ["q"] = "shirt", ["sort"] = "style:desc", ["page"] = "3",
        });
        var request = NestedGridForm.ReadRequest(query, Settings());
        Assert.Equal(new NestedGridRequest(1, "PO-1", "shirt", "style", true, 3, 5), request);
        Assert.Equal("/x?depth=2&level=1&parent=PO-1&q=shirt&sort=style%3Adesc&page=3", NestedGridForm.Url("/x?depth=2", request));
        Assert.Equal("/x?level=1&parent=PO-1&sort=style%3Adesc&page=3", NestedGridForm.Url("/x", request, "q"));

        // Level 0 has no parent; an out-of-range level is clamped.
        var top = NestedGridForm.ReadRequest(new QueryCollection(new Dictionary<string, StringValues> { ["level"] = "9", ["parent"] = "x" }), Settings());
        Assert.Equal((1, "x"), (top.Level, top.ParentId));
        Assert.Null(NestedGridForm.ReadRequest(new QueryCollection(new Dictionary<string, StringValues> { ["parent"] = "x" }), Settings()).ParentId);
        Assert.Equal("nested-1-PO_1_2", NestedGridForm.ContainerId(1, "PO/1.2"));
    }

    [Fact]
    public void Filters_RoundTripThroughTheUrl_AndBadKeysAreDropped()
    {
        var query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["level"] = "1", ["parent"] = "PO-1", ["f.style"] = "ST-1", ["f.bad key"] = "x", ["f.empty"] = "", ["page"] = "2",
        });
        var request = NestedGridForm.ReadRequest(query, Settings());

        Assert.Equal(["style"], request.ActiveFilters.Select(f => f.Key));
        Assert.Equal("/x?level=1&parent=PO-1&f.style=ST-1&page=2", NestedGridForm.Url("/x", request));
        Assert.Equal("/x?level=1&parent=PO-1", NestedGridForm.Url("/x", request.WithFilter("style", null)));
    }

    [Fact]
    public async Task Renders_AServerFilterRow_ReloadAndThePagerRange()
    {
        var s = Settings();
        var columns = s.ColumnsFor(0, null);
        columns[1] = new() { Key = "qty", Title = "Qty", Type = GridCellType.Select, Filterable = true, Options = [new("10", "Ten")] };
        var request = new NestedGridRequest(0, null, PageSize: 2).WithFilter("qty", "10");
        var html = await RenderAsync(new NestedGridView(s, request, columns, [Row("PO-1", "PO-1")], 3, ["PO-1"], Title: "Orders"));

        Assert.Contains("""<select class="m-select" name="f.qty" aria-label="Qty" hx-get="/nested/level?depth=2&amp;level=0" hx-trigger="change" hx-include="this" hx-target="#nested-0-root" hx-swap="outerHTML" onchange="event.stopPropagation()"><option value="">Any qty</option><option value="10" selected>Ten</option></select>""", html);
        Assert.Contains("<span>Clear filters</span>", html);
        Assert.Contains("""aria-label="Reload" title="Reload" hx-get="/nested/level?depth=2&amp;level=0&amp;f.qty=10" hx-target="#nested-0-root" hx-swap="outerHTML">""", html);
        Assert.Contains("<li class=\"ant-pagination-total-text\">1–2 of 3</li>", html);
        // The level searches and filters on the server, so the grid's in-browser search is off.
        Assert.DoesNotContain("m-grid-search", html);
        Assert.Contains("placeholder=\"Search orders…\"", html);
    }

    [Fact]
    public void Changes_ListTheLoadedRowsNoLongerPosted()
    {
        var form = new FormCollection(new Dictionary<string, StringValues> { ["l1.loaded"] = new(["a", "b", "c", ""]) });
        var post = new EditableGridPost { Columns = [], Rows = [new GridRecord("a"), new GridRecord("n1")], Operation = "save" };
        var changes = NestedGridForm.Changes(post, form, new NestedGridRequest(1, "PO-1"));
        Assert.Equal(["b", "c"], changes.DeletedIds);
        Assert.Equal(["a", "n1"], changes.Rows.Select(r => r.Id));
        Assert.Equal((1, "PO-1"), (changes.Level, changes.ParentId));
    }

    [Fact]
    public async Task Renders_ALevelInItsOwnForm_WithLazyChildrenToolsAndPager()
    {
        var s = Settings();
        var html = await RenderAsync(new NestedGridView(s, new NestedGridRequest(0, null, PageSize: 2), s.ColumnsFor(0, null),
            [Row("PO-1", "PO-1"), Row("n1", "New")], 5, ["PO-1"], Title: "Orders"));

        Assert.StartsWith("<div id=\"nested-0-root\" class=\"m-nested m-nested-level-0\" @grid-change.stop=\"\">", html);
        Assert.Contains("<form method=\"post\" hx-post=\"/nested/level?depth=2&amp;level=0\" hx-target=\"#nested-0-root\" hx-swap=\"outerHTML\" onsubmit=\"event.stopPropagation()\" novalidate>", html);
        Assert.Contains("<input type=\"hidden\" name=\"l0.loaded\" value=\"PO-1\" />", html);
        Assert.Contains("name=\"l0.schema\"", html);
        Assert.Contains("id=\"nested-0-root-grid\"", html);
        // A saved row loads its styles when shown; a new row can't have any yet.
        Assert.Contains("hx-get=\"/nested/level?depth=2&amp;level=1&amp;parent=PO-1\" hx-trigger=\"intersect once\" hx-target=\"this\" hx-swap=\"outerHTML\"", html);
        Assert.Contains("Save this row to add its styles.", html);
        Assert.Contains("name=\"q\"", html);
        Assert.Contains("hx-get=\"/nested/level?depth=2&amp;level=0&amp;sort=qty%3Aasc\" hx-target=\"#nested-0-root\"", html); // the Qty header sorts the level
        Assert.Contains("ant-pagination-item-3", html); // 5 rows, 2 per page
        Assert.Contains("hx-get=\"/nested/level?depth=2&amp;level=0&amp;page=2\"", html);
        Assert.Contains("<span class=\"m-badge m-badge-gray-lt\">5</span>", html);
    }

    [Fact]
    public async Task LastLevelRowsDoNotExpand_ReadOnlyLevelsHideEditing()
    {
        var s = Settings();
        s.Levels[1].Editable = false;
        var html = await RenderAsync(new NestedGridView(s, new NestedGridRequest(1, "PO-1"), s.ColumnsFor(1, null), [new GridRecord("S1", new Dictionary<string, string?> { ["style"] = "ST-1" })], 1, ["S1"]));

        Assert.DoesNotContain("ant-table-row-expand-icon", html);
        Assert.DoesNotContain("Add row", html);
        Assert.DoesNotContain("value=\"save\"", html);
        Assert.Contains("<li class=\"ant-pagination-total-text\">1–1 of 1</li>", html); // one page: the range, as the data table shows it
        Assert.Contains("id=\"nested-1-PO-1\"", html);
    }
}
