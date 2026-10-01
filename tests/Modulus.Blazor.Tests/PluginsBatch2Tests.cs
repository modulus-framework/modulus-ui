using Bunit;
using Microsoft.AspNetCore.Components;
using Modulus.Blazor.Components.Extras;
using Modulus.Ui.Abstractions.Forms;
using Modulus.Ui.Abstractions.Plugins;
using Modulus.Ui.Abstractions.Shell;
using Xunit;

namespace Modulus.Blazor.Tests;

public class PluginMarkupTests
{
    [Fact]
    public void AntIconSvg_ReturnsTheAntDesignIcon_AndNullForAnUnknownName()
    {
        var svg = AntIconSvg.Svg("drag"); // the drag handle's icon (AntDesign 1.6 has no "holder")

        Assert.NotNull(svg);
        Assert.StartsWith("<svg focusable=\"false\" width=\"1em\" height=\"1em\" fill=\"currentColor\" aria-hidden=\"true\"", svg);
        Assert.Null(AntIconSvg.Svg("no-such-icon-anywhere"));
    }

    [Fact]
    public void AntIconSvg_ListsEveryIconOfATheme_EachOneDrawable()
    {
        var outline = AntIconSvg.Names();
        var twoTone = AntIconSvg.Names(AntIconSvg.TwoTone);

        Assert.Contains("api", outline);
        Assert.True(outline.Count > 300);
        Assert.Equal(outline.Order(StringComparer.Ordinal), outline);
        Assert.NotEmpty(twoTone);
        Assert.True(twoTone.Count < outline.Count);
        Assert.All(outline.Take(20), n => Assert.NotNull(AntIconSvg.Svg(n)));
        Assert.All(twoTone.Take(5), n => Assert.NotNull(AntIconSvg.Svg(n, AntIconSvg.TwoTone)));
    }

    [Fact]
    public void ShellIcon_WithTheAntPrefix_DrawsTheAntDesignSvg()
    {
        var html = ShellRenderer.Icon("ant:api");

        Assert.StartsWith("<svg class=\"m-ti m-ai\" data-m-icon=\"ant:api\" focusable=\"false\"", html);
        Assert.DoesNotContain("<use", html);
    }

    [Fact]
    public void ShellIcon_EveryBundledIcon_DrawsAnInlineSvg()
    {
        // Ant Design icons newer than the AntDesign package, and the Remix Icon fallbacks.
        Assert.All(ExtraIconSvg.Svgs.Keys, name => Assert.NotNull(AntIconSvg.Svg(name)));

        var html = ShellRenderer.Icon("team", "m-nav-icon");
        Assert.StartsWith("<svg class=\"m-ti m-nav-icon m-ai\" data-m-icon=\"team\" focusable=\"false\"", html);
        Assert.Contains("viewBox=\"64 64 896 896\"", html); // Ant Design
        Assert.Contains("viewBox=\"0 0 24 24\"", ShellRenderer.Icon("ri:fingerprint")); // Remix Icon
        Assert.EndsWith("</svg>", ShellRenderer.Icon("moon")); // newer than the AntDesign package
        Assert.Equal(ShellRenderer.Icon("x").Replace("data-m-icon=\"x\"", ""), ShellRenderer.Icon("ant:x").Replace("data-m-icon=\"ant:x\"", ""));
        Assert.DoesNotContain("<path", ShellRenderer.Icon("users")); // Tabler names are gone: an empty icon
    }

    [Fact]
    public void MediaPlayer_Video_HasTheStageCaptionsAndFullscreen()
    {
        var html = MediaPlayerHtml.Render(new MediaPlayerOptions
        {
            Src = "/clip.mp4",
            Poster = "/clip.jpg",
            Captions = [new MediaCaption(0, 2, "Hello <world>")],
        });

        Assert.Contains("class=\"m-media m-media-video\" data-m-player", html);
        Assert.Contains("<video class=\"m-media-media\" src=\"/clip.mp4\" preload=\"metadata\" playsinline poster=\"/clip.jpg\">", html);
        Assert.Contains("style=\"--m-media-ratio: 16 / 9\"", html);
        Assert.Contains("data-captions=\"[{&quot;start&quot;:0,&quot;end&quot;:2,&quot;text&quot;:&quot;Hello \\u003Cworld\\u003E&quot;}]\"", html);
        Assert.Contains("data-m-player-cmd=\"captions\"", html);
        Assert.Contains("aria-pressed=\"false\"", html);
        Assert.Contains("data-m-player-cmd=\"fullscreen\"", html);
        Assert.DoesNotContain("data-m-player-cmd=\"back\"", html);
    }

    [Fact]
    public void MediaPlayer_Audio_HasTheTrackAndSkipButtons_AndNoFullscreen()
    {
        var html = MediaPlayerHtml.Render(new MediaPlayerOptions { Src = "/t.wav", Video = false, Title = "Song", Subtitle = "Band", Loop = true });

        Assert.Contains("class=\"m-media m-media-audio\" data-m-player role=\"group\" aria-label=\"Song\"", html);
        Assert.Contains("<audio class=\"m-media-media\" src=\"/t.wav\" preload=\"metadata\" loop></audio>", html);
        Assert.Contains("<div class=\"m-media-title\">Song</div><div class=\"m-media-subtitle\">Band</div>", html);
        Assert.Contains("data-m-player-cmd=\"back\"", html);
        Assert.DoesNotContain("data-m-player-cmd=\"fullscreen\"", html);
        Assert.DoesNotContain("data-m-player-cmd=\"captions\"", html);
    }

    [Fact]
    public void Editor_RendersToolbarHostAndCleanValue()
    {
        var html = EditorHtml.Render("Body", "body", "<p>Hi<script>alert(1)</script> <img src=\"javascript:x\"><img src=\"/a.png\" alt=\"A\"></p>", placeholder: "Write", label: "Notes");

        Assert.Contains("data-m-editor data-m-debounce data-placeholder=\"Write\" data-label=\"Notes\"", html);
        Assert.Contains("data-m-ed=\"bold\"", html);
        Assert.Contains("data-m-ed=\"list\" data-arg=\"bullet\"", html);
        Assert.Contains("<div class=\"m-editor-host\"><p>Hi <img src=\"/a.png\" alt=\"A\"></p></div>", html);
        Assert.Contains("<input type=\"hidden\" name=\"Body\" id=\"body\" value=\"&lt;p&gt;Hi &lt;img src=&quot;/a.png&quot; alt=&quot;A&quot;&gt;&lt;/p&gt;\" data-m-editor-value />", html);
    }

    [Fact]
    public void Editor_ReadOnly_DisablesTheToolbar()
    {
        var html = EditorHtml.Render("Body", null, "", readOnly: true);

        Assert.Contains(" data-readonly", html);
        Assert.Contains("<select class=\"m-editor-heading\" data-m-ed-heading aria-label=\"Text style\" disabled>", html);
        Assert.DoesNotContain(" id=\"", html[html.IndexOf("<input type=\"hidden\"", StringComparison.Ordinal)..]);
    }

    [Theory]
    [InlineData("<img src=\"https://x.test/a.png\">", "<img src=\"https://x.test/a.png\" alt=\"\">")]
    [InlineData("<img src=\"//cdn.test/a.png\" alt='Logo'>", "<img src=\"https://cdn.test/a.png\" alt=\"Logo\">")]
    [InlineData("<img src=\"data:image/png;base64,AAAA\">", "")]
    [InlineData("<img src=\" java\tscript:alert(1)\">", "")]
    [InlineData("<img alt=\"no source\">", "")]
    public void RichText_KeepsSafeImages_OnlyWhenAsked(string html, string expected)
    {
        Assert.Equal(expected, RichText.Sanitize(html, images: true));
        Assert.Equal("", RichText.Sanitize(html));
    }

    [Fact]
    public void Choropleth_Build_MakesEqualSteps_AndClassifiesValues()
    {
        var map = Choropleth.Build(new Dictionary<string, double> { ["A"] = 0, ["B"] = 50, ["C"] = 100 }, ["#1", "#2", "#3", "#4"], v => v.ToString("0"));

        Assert.Equal(["0–25", "25–50", "50–75", "75+"], map.Classes.Select(c => c.Label));
        Assert.Equal("#1", map.ClassOf(0).Color);
        Assert.Equal("#3", map.ClassOf(50).Color);
        Assert.Equal("#4", map.ClassOf(100).Color);
        Assert.Equal("#1", map.ClassOf(-5).Color);
    }

    [Fact]
    public void MapTile_CarriesTheMarkersAndView()
    {
        var html = MapHtml.Tile("stores", [new MapMarker(51.5, -0.12, "London <HQ>", Tone: "success")], (51.5, -0.12), zoom: 12, scrollZoom: true, height: "300px");

        Assert.Contains("<div class=\"m-map\" id=\"stores\" data-m-map role=\"region\" aria-label=\"Map\" ", html);
        Assert.Contains("&quot;title&quot;:&quot;London \\u003CHQ\\u003E&quot;", html);
        Assert.Contains("data-zoom=\"12\" data-center=\"51.5,-0.12\" data-scroll-zoom style=\"height: 300px\">", html);
        Assert.Equal(" data-m-map-go=\"2\" data-m-map-target=\"stores\" data-zoom=\"12\" ", MapHtml.GoAttributes("stores", 2));
    }

    [Fact]
    public void MapVector_FillsCountriesByClass_AndDrawsTheLegend()
    {
        var data = Choropleth.Build(new Dictionary<string, double> { ["DE"] = 10, ["FR"] = 90 }, ["#aaa", "#000"], v => $"${v:0}k");
        var html = MapHtml.Vector("sales", data, "Sales");

        Assert.Contains("data-fills=\"{&quot;DE&quot;:&quot;#aaa&quot;,&quot;FR&quot;:&quot;#000&quot;}\"", html);
        Assert.Contains("data-labels=\"{&quot;DE&quot;:&quot;$10k&quot;,&quot;FR&quot;:&quot;$90k&quot;}\"", html);
        Assert.Contains("<span class=\"m-map-legend-swatch\" style=\"background: #000\"></span>$50k+", html);
        Assert.Contains("No data</span>", html);
        Assert.Contains("data-m-vector-selected", html);
    }

    [Theory]
    [InlineData("{\"id\":\"t1\",\"from\":\"todo\",\"to\":\"done\",\"index\":2}", true)]
    [InlineData("{\"id\":\"\",\"from\":\"a\",\"to\":\"a\",\"index\":0}", false)]
    [InlineData("{\"id\":\"t1\",\"from\":\"a\",\"to\":\"a\",\"index\":-1}", false)]
    [InlineData("on", false)]
    [InlineData("{broken", false)]
    [InlineData(null, false)]
    public void SortMove_TryParse_ReadsOnlyAWellFormedMove(string? json, bool ok)
    {
        Assert.Equal(ok, SortMove.TryParse(json, out var move));
        if (ok) Assert.Equal(new SortMove("t1", "todo", "done", 2), move);
    }

    [Fact]
    public void SortMove_Apply_MovesBetweenAndWithinLists()
    {
        var lists = new Dictionary<string, List<string>> { ["a"] = ["1", "2", "3"], ["b"] = ["4"] };

        Assert.True(new SortMove("2", "a", "b", 0).Apply(lists, x => x));
        Assert.Equal(["1", "3"], lists["a"]);
        Assert.Equal(["2", "4"], lists["b"]);

        Assert.True(new SortMove("1", "a", "a", 5).Apply(lists, x => x)); // clamped to the end
        Assert.Equal(["3", "1"], lists["a"]);

        Assert.False(new SortMove("9", "a", "b", 0).Apply(lists, x => x));
        Assert.False(new SortMove("3", "a", "zzz", 0).Apply(lists, x => x));
    }
}

public class PluginComponentTests : BunitContext
{
    private sealed record Item(string Id, string Name);

    [Fact]
    public void AppSortable_RendersItemsWithHandles_AndReportsMoves()
    {
        SortMove? reported = null;
        var cut = Render<AppSortable<Item>>(p => p
            .Add(c => c.Name, "todo")
            .Add(c => c.Group, "board")
            .Add(c => c.Title, "To do")
            .Add(c => c.Items, [new Item("a", "Alpha"), new Item("b", "Beta")])
            .Add(c => c.IdOf, i => i.Id)
            .Add(c => c.LabelOf, i => i.Name)
            .Add(c => c.ItemTemplate, i => b => b.AddContent(0, i.Name))
            .Add(c => c.OnSort, m => reported = m));

        var list = cut.Find("ul[data-m-sortable=\"todo\"]");
        Assert.Equal("event", list.GetAttribute("data-m-sort-mode"));
        Assert.Equal("board", list.GetAttribute("data-m-sort-group"));
        Assert.False(list.HasAttribute("data-m-sort-layout"));
        Assert.Equal(2, cut.FindAll("li[data-m-sort-id]").Count);
        Assert.Equal("Move Beta", cut.FindAll("[data-m-sort-handle]")[1].GetAttribute("aria-label"));
        Assert.Equal("m-sort-help-todo", cut.Find("[data-m-sort-handle]").GetAttribute("aria-describedby"));

        cut.Find("[data-m-sort-result]").Change("{\"id\":\"a\",\"from\":\"todo\",\"to\":\"todo\",\"index\":1}");
        Assert.Equal(new SortMove("a", "todo", "todo", 1), reported);
    }

    [Fact]
    public void AppEditor_BindsWhatTheScriptReports_AndKeepsItsStartingHtml()
    {
        string? bound = "<p>Start</p>";
        var cut = Render<AppEditor>(p => p
            .Add(c => c.Value, bound)
            .Add(c => c.ValueChanged, v => bound = v)
            .Add(c => c.Label, "Notes"));

        Assert.Equal("<p>Start</p>", cut.Find(".m-editor-host").InnerHtml);
        cut.Find("[data-m-editor-value]").Change("<p>Typed<script>x</script></p>");
        Assert.Equal("<p>Typed</p>", bound);

        // The parent echoes the value back: the editor is not rebuilt, so Quill keeps its state.
        cut.Render(p => p.Add(c => c.Value, bound));
        Assert.Equal("<p>Start</p>", cut.Find(".m-editor-host").InnerHtml);

        // A value from elsewhere (a reset) starts it again.
        cut.Render(p => p.Add(c => c.Value, "<p>Reset</p>"));
        Assert.Equal("<p>Reset</p>", cut.Find(".m-editor-host").InnerHtml);
    }

    [Fact]
    public void AppEditor_Error_MarksTheWrapper()
    {
        var cut = Render<AppEditor>(p => p.Add(c => c.Error, true));

        Assert.Contains("m-editor-invalid", cut.Find(".m-editor-wrap").ClassName);
    }

    [Fact]
    public void AppMediaPlayer_AndMaps_RenderTheSharedMarkup()
    {
        var player = Render<AppMediaPlayer>(p => p.Add(c => c.Src, "/a.mp4"));
        Assert.Equal(MediaPlayerHtml.Render(new MediaPlayerOptions { Src = "/a.mp4" }), player.Markup.Trim());

        var markers = new[] { new MapMarker(1, 2, "X") };
        var map = Render<AppMap>(p => p.Add(c => c.Id, "m").Add(c => c.Markers, markers));
        Assert.Equal(MapHtml.Tile("m", markers), map.Markup.Trim());
    }
}
