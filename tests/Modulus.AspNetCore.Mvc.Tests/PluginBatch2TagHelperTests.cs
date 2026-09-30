using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.AspNetCore.Mvc.TagHelpers;
using Modulus.Ui.Abstractions.Plugins;
using Xunit;

namespace Modulus.AspNetCore.Mvc.Tests;

/// <summary>The second Plugins batch in MVC: media player, editor, maps and sortable lists, the same markup as Blazor.</summary>
public class PluginBatch2TagHelperTests
{
    private sealed record Node(TagHelper Helper, string Tag, string Content = "", Node[]? Children = null, TagHelperAttributeList? Attributes = null);

    private static async Task<string> RenderAsync(Node node, IDictionary<object, object>? items = null)
    {
        items ??= new Dictionary<object, object>();
        var output = new TagHelperOutput(node.Tag, node.Attributes ?? [], async (_, _) =>
        {
            var html = node.Content;
            foreach (var child in node.Children ?? []) html += await RenderAsync(child, new Dictionary<object, object>(items));
            return new DefaultTagHelperContent().SetHtmlContent(html);
        });
        await node.Helper.ProcessAsync(new TagHelperContext(node.Tag, [], items, Guid.NewGuid().ToString("N")), output);
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    [Fact]
    public async Task MediaPlayer_IsTheSharedMarkup_WithTheAuthorsClass()
    {
        var html = await RenderAsync(new Node(new MediaPlayerTagHelper { Src = "/t.wav", Audio = true, Title = "Song" }, "app-media-player",
            Attributes: [new("class", "wide")]));

        Assert.Equal(MediaPlayerHtml.Render(new MediaPlayerOptions { Src = "/t.wav", Video = false, Title = "Song", CssClass = "wide" }), html);
    }

    [Fact]
    public async Task Editor_PostsUnderItsName_AndCleansTheStartingValue()
    {
        var html = await RenderAsync(new Node(new EditorTagHelper { Name = "Body", Value = "<p>Hi</p><script>x</script>", Label = "Notes", MinHeight = 120 }, "app-editor"));

        Assert.StartsWith("<div class=\"m-editor\" data-m-editor", html);
        Assert.Contains("style=\"--m-editor-min-height: 120px\"", html);
        Assert.Contains("<div class=\"m-editor-host\"><p>Hi</p></div>", html);
        Assert.Contains("<input type=\"hidden\" name=\"Body\" id=\"Body\" value=\"&lt;p&gt;Hi&lt;/p&gt;\" data-m-editor-value />", html);
    }

    [Fact]
    public async Task Editor_Disabled_IsReadOnly_AndErrorStatusMarksIt()
    {
        var html = await RenderAsync(new Node(new EditorTagHelper { Name = "Body", Disabled = true, Status = "error" }, "app-editor"));

        Assert.StartsWith("<div class=\"m-editor m-editor-status-error\"", html);
        Assert.Contains(" data-readonly", html);
    }

    [Fact]
    public async Task Map_UsesTheCentreOnlyWhenBothHalvesAreGiven()
    {
        var markers = new[] { new MapMarker(1, 2, "X") };
        var fitted = await RenderAsync(new Node(new MapTagHelper { Id = "m", Markers = markers, CenterLat = 10 }, "app-map"));
        var centred = await RenderAsync(new Node(new MapTagHelper { Id = "m", Markers = markers, CenterLat = 10, CenterLng = 20, Zoom = 6 }, "app-map"));

        Assert.Equal(MapHtml.Tile("m", markers), fitted);
        Assert.Contains("data-zoom=\"6\" data-center=\"10,20\"", centred);
    }

    [Fact]
    public async Task VectorMap_IsTheSharedMarkup()
    {
        var data = Choropleth.Build(new Dictionary<string, double> { ["DE"] = 1 }, ["#000"], v => v.ToString());
        var html = await RenderAsync(new Node(new VectorMapTagHelper { Id = "v", Data = data, Label = "Sales" }, "app-vector-map"));

        Assert.Equal(MapHtml.Vector("v", data, "Sales"), html);
    }

    [Fact]
    public async Task Sortable_PostsItemsUnderTheListsInput_AndCarriesTheGroup()
    {
        var html = await RenderAsync(new Node(new SortableTagHelper { Name = "todo", Input = "Todo", Group = "board", Title = "To do", Layout = SortableLayout.Grid }, "app-sortable", Children:
        [
            new(new SortableItemTagHelper { ItemId = "t1", Label = "Write" }, "app-sortable-item", "Write"),
            new(new SortableItemTagHelper { ItemId = "t2" }, "app-sortable-item", "Price"),
        ]));

        Assert.StartsWith("<div class=\"m-sortable-root\" data-m-sort-root=\"\">", html);
        Assert.Contains("<ul class=\"m-sortable m-sortable-grid\" data-m-sortable=\"todo\"  data-m-sort-input=\"Todo\" data-m-sort-group=\"board\" data-m-sort-layout=\"grid\" data-m-sort-title=\"To do\" aria-label=\"To do\" data-empty=\"Drop items here\">", html);
        Assert.Contains("<li class=\"m-sort-item\" data-m-sort-id=\"t1\" data-m-sort-label=\"Write\">", html);
        Assert.Contains("aria-label=\"Move Write\"", html);
        Assert.Contains("aria-label=\"Move t2\"", html);
        Assert.Contains("aria-describedby=\"m-sort-help-todo\"", html);
        Assert.Contains("<div class=\"m-sort-item-body\">Write</div><input type=\"hidden\" name=\"Todo\" value=\"t1\" data-m-sort-value />", html);
        Assert.Contains("<input type=\"hidden\" data-m-sort-result />", html);
        Assert.Contains("id=\"m-sort-help-todo\"", html);
    }

    [Fact]
    public async Task Sortable_WithoutHandles_OrInput_RendersBareItems()
    {
        var html = await RenderAsync(new Node(new SortableTagHelper { Name = "l", Handle = false }, "app-sortable", Children:
        [
            new(new SortableItemTagHelper { ItemId = "a" }, "app-sortable-item", "A"),
        ]));

        Assert.DoesNotContain("data-m-sort-handle", html);
        Assert.DoesNotContain("data-m-sort-value", html);
        Assert.Contains("<li class=\"m-sort-item\" data-m-sort-id=\"a\"><div class=\"m-sort-item-body\">A</div></li>", html);
    }
}
