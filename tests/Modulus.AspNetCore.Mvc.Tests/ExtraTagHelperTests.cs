using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.AspNetCore.Mvc.TagHelpers;
using Modulus.Ui.Abstractions;
using Xunit;

namespace Modulus.AspNetCore.Mvc.Tests;

/// <summary>Menu, Layout, Splitter, Affix, BackTop, dropdown button, AutoComplete, Mentions, Card extras,
/// image groups, Countdown, Comment and Drawer.</summary>
public class ExtraTagHelperTests
{
    private sealed record Node(TagHelper Helper, string Tag, string Content = "", Node[]? Children = null, TagHelperAttributeList? Attributes = null);

    /// <summary>Renders a tree of helpers the way Razor does: children run inside the parent's GetChildContentAsync,
    /// each with a copy of the parent's context.Items.</summary>
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
    public async Task Menu_MarksSelectedItemAndOpenInlineSubmenu_WithIndent()
    {
        var html = await RenderAsync(new Node(new MenuTagHelper { Mode = "inline", Selected = "inbox", Open = "mailbox" }, "app-menu", Children:
        [
            new(new SubMenuTagHelper { Key = "mailbox", Title = "Mailbox" }, "app-sub-menu", Children:
            [
                new(new MenuItemTagHelper { Key = "inbox", Icon = "inbox" }, "app-menu-item", "Inbox"),
                new(new MenuItemTagHelper { Key = "sent" }, "app-menu-item", "Sent"),
            ]),
            new(new SubMenuTagHelper { Key = "projects", Title = "Projects" }, "app-sub-menu", Children: [new(new MenuItemTagHelper { Key = "active" }, "app-menu-item", "Active")]),
            new(new MenuItemTagHelper { Key = "team", Href = "/team" }, "app-menu-item", "Team"),
        ]));

        Assert.StartsWith("<ul class=\"ant-menu ant-menu-root ant-menu-light ant-menu-inline\"", html);
        Assert.Contains("class=\"ant-menu-submenu ant-menu-submenu-inline ant-menu-submenu-selected ant-menu-submenu-open\"", html);
        Assert.Contains("class=\"ant-menu-item ant-menu-item-selected\" role=\"menuitem\" style=\"padding-left:48px\"", html);
        Assert.Contains("class=\"ant-menu ant-menu-sub ant-menu-light ant-menu-inline ant-menu-hidden\"", html);
        Assert.Contains("style=\"padding-left:24px\"", html);
        Assert.Contains("<a href=\"/team\">Team</a>", html);
        Assert.Contains("[&#x27;inbox&#x27;,&#x27;sent&#x27;].includes(selected)", html);
    }

    [Fact]
    public async Task Menu_HorizontalSubmenuRendersAPopupBelow()
    {
        var html = await RenderAsync(new Node(new MenuTagHelper { Mode = "horizontal", Selected = "billing" }, "app-menu", Children:
        [
            new(new MenuItemTagHelper { Key = "mail" }, "app-menu-item", "Mail"),
            new(new SubMenuTagHelper { Key = "settings", Title = "Settings" }, "app-sub-menu", Children: [new(new MenuItemTagHelper { Key = "billing" }, "app-menu-item", "Billing")]),
            new(new MenuDividerTagHelper(), "app-menu-divider"),
        ]));

        Assert.Contains("ant-menu-submenu ant-menu-submenu-vertical ant-menu-submenu-selected", html);
        Assert.Contains("ant-menu-submenu-popup ant-menu-light ant-menu-submenu-placement-bottomLeft m-menu-popup-below", html);
        Assert.DoesNotContain("padding-left", html);
        Assert.Contains("<li class=\"ant-menu-item-divider\" role=\"separator\"></li>", html);
    }

    [Fact]
    public async Task Layout_DetectsSider_AndSiderSetsWidth()
    {
        var html = await RenderAsync(new Node(new LayoutTagHelper(), "app-layout", Children:
        [
            new(new LayoutSiderTagHelper { Width = 96, Theme = "light" }, "app-layout-sider", "Sider"),
            new(new LayoutContentTagHelper(), "app-layout-content", "Content"),
        ]));

        Assert.StartsWith("<section class=\"ant-layout ant-layout-has-sider\">", html);
        Assert.Contains("<aside class=\"ant-layout-sider ant-layout-sider-light\" style=\"flex: 0 0 96px;max-width: 96px;min-width: 96px;width: 96px;\"><div class=\"ant-layout-sider-children\">Sider</div></aside>", html);
        // Content is left to Razor (the helper only renames the element), so the harness renders it empty.
        Assert.Contains("<main class=\"ant-layout-content\">", html);
    }

    [Fact]
    public async Task Splitter_RendersTwoPanesAndABar()
    {
        var html = await RenderAsync(new Node(new SplitterTagHelper { Layout = "vertical" }, "app-splitter", Children:
        [
            new(new SplitterPanelTagHelper { Size = "40%", Min = "20%", Max = "70%" }, "app-splitter-panel", "A"),
            new(new SplitterPanelTagHelper(), "app-splitter-panel", "B"),
        ]));

        Assert.Contains("ant-splitter ant-splitter-vertical", html);
        Assert.Contains("style=\"min-height:20%;height:40%;\" :style=\"first\">A</div>", html);
        Assert.Contains("style=\"height:60%;\" :style=\"second\">B</div>", html);
        Assert.Contains("role=\"separator\"", html);
        Assert.Contains("aria-valuemin=\"20\" aria-valuemax=\"70\" aria-valuenow=\"40\"", html);
    }

    [Fact]
    public async Task DropdownButton_PutsUnboundAttributesOnTheMainButton()
    {
        var html = await RenderAsync(new Node(new DropdownButtonTagHelper { Text = "Save" }, "app-dropdown-button",
            "<li class=\"ant-dropdown-menu-item\">Draft</li>", Attributes: [new("hx-post", "/save"), new("name", "op"), new("value", "save")]));

        Assert.Contains("<div class=\"ant-dropdown-button ant-btn-group\"><button class=\"ant-btn ant-btn-default\" type=\"button\" hx-post=\"/save\" name=\"op\" value=\"save\"><span>Save</span></button>", html);
        Assert.Contains("ant-dropdown-trigger ant-btn ant-btn-default ant-btn-icon-only", html);
        Assert.Contains("<li class=\"ant-dropdown-menu-item\">Draft</li>", html);
    }

    [Fact]
    public async Task AffixAndBackTop_UseTheirAlpineComponents()
    {
        Assert.Contains("x-data=\"modulusAffix(120)\"", await RenderAsync(new Node(new AffixTagHelper { OffsetTop = 120 }, "app-affix", "x")));
        var back = await RenderAsync(new Node(new BackTopTagHelper(), "app-back-top"));
        Assert.Contains("class=\"ant-back-top\" x-data=\"modulusBackTop(400)\"", back);
        Assert.Contains("aria-label=\"Back to top\"", back);
    }

    [Fact]
    public async Task AutoCompleteAndMentions_PostTheirValues()
    {
        var ac = await RenderAsync(new Node(new AutoCompleteTagHelper { Name = "assignee", Value = "Liam", Options = ["Liam Okafor", "Amelia"] }, "app-auto-complete",
            Attributes: [new("placeholder", "Start typing")]));
        Assert.Contains("<input class=\"ant-input\" type=\"search\" autocomplete=\"off\" role=\"combobox\"", ac);
        Assert.Contains("name=\"assignee\"", ac);
        Assert.Contains("value=\"Liam\"", ac);
        Assert.Contains("placeholder=\"Start typing\"", ac);
        Assert.Contains("modulusAutoComplete([&quot;Liam Okafor&quot;,&quot;Amelia&quot;])", ac);

        var mentions = await RenderAsync(new Node(new MentionsTagHelper { Name = "comment", Value = "Hi <b>", Rows = 3, Options = [new SelectListItem("Sofia Reyes", "sofia")] }, "app-mentions"));
        Assert.Contains("<div class=\"ant-mentions\"><textarea class=\"rc-textarea\" rows=\"3\" name=\"comment\"", mentions);
        Assert.Contains(">Hi &lt;b&gt;</textarea>", mentions);
        Assert.Contains("&quot;value&quot;:&quot;sofia&quot;,&quot;label&quot;:&quot;Sofia Reyes&quot;", mentions);
    }

    [Fact]
    public async Task Card_RendersCoverMetaAndActions()
    {
        var html = await RenderAsync(new Node(new CardTagHelper { Bordered = true }, "app-card", Children:
        [
            new(new CardCoverTagHelper(), "app-card-cover", "<img src=\"a.svg\" alt=\"A\" />"),
            new(new CardMetaTagHelper { Title = "Alpine lake", Description = "Shared", Avatar = "SP", AvatarColor = "#1677ff" }, "app-card-meta"),
            new(new CardActionTagHelper(), "app-card-action", "Like"),
            new(new CardActionTagHelper(), "app-card-action", "Share"),
        ]));

        Assert.Contains("<div class=\"ant-card-cover\"><img src=\"a.svg\" alt=\"A\" /></div><div class=\"ant-card-body\">", html);
        Assert.Contains("<div class=\"ant-card-meta-title\">Alpine lake</div><div class=\"ant-card-meta-description\">Shared</div>", html);
        Assert.Contains("background: #1677ff;", html);
        Assert.EndsWith("<ul class=\"ant-card-actions\"><li style=\"width:50%\"><span>Like</span></li><li style=\"width:50%\"><span>Share</span></li></ul></div>", html);
    }

    [Fact]
    public async Task ImageGroup_OwnsOnePreviewForItsImages()
    {
        var html = await RenderAsync(new Node(new ImageGroupTagHelper(), "app-image-group", Children:
        [
            new(new ImageTagHelper { Src = "a.png", Alt = "A" }, "app-image"),
            new(new ImageTagHelper { Src = "b.png", Alt = "B" }, "app-image"),
        ]));

        Assert.Contains("data-lightbox-index=\"0\"", html);
        Assert.Contains("data-lightbox-index=\"1\"", html);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "m-lightbox-source"));
        Assert.Contains("data-items=\"[{&quot;src&quot;:&quot;a.png&quot;,&quot;title&quot;:&quot;A&quot;,&quot;caption&quot;:null,&quot;thumb&quot;:&quot;a.png&quot;}", html);
        Assert.Contains("data-lb=\"next\"", html);
    }

    [Fact]
    public async Task Image_OnItsOwn_IsALightboxOfOne()
    {
        var html = await RenderAsync(new Node(new ImageTagHelper { Src = "a.png", Alt = "A" }, "app-image"));

        Assert.Contains("data-m-lightbox", html);
        Assert.Contains("data-lightbox-index=\"0\"", html);
        foreach (var action in new[] { "close", "zoom-in", "zoom-out", "rotate-right", "rotate-left", "download" })
            Assert.Contains($"data-lb=\"{action}\"", html);
    }

    [Fact]
    public async Task Lightbox_RendersCaptionedTiles()
    {
        var html = await RenderAsync(new Node(new LightboxTagHelper
        {
            Items = [new LightboxItem("a.png", "Lake", "Morning light"), new LightboxItem("b.png", "Dunes", Thumb: "b-small.png")],
        }, "app-lightbox"));

        Assert.Contains("<div class=\"m-lightbox-item-title\">Lake</div><div class=\"m-lightbox-item-caption\">Morning light</div>", html);
        Assert.Contains("<img src=\"b-small.png\" alt=\"Dunes\" loading=\"lazy\" />", html);
        Assert.Contains("data-lightbox-index=\"1\"", html);
    }

    [Theory]
    [InlineData(5_430_000L, "hh:mm:ss", "01:30:30")]
    [InlineData(93_784_000L, "hh:mm:ss", "26:03:04")]
    [InlineData(93_784_000L, "d:hh:mm:ss", "1:02:03:04")]
    [InlineData(0L, "hh:mm:ss", "00:00:00")]
    public void Countdown_FormatsTheTimeLeft(long ms, string format, string expected) =>
        Assert.Equal(expected, CountdownTagHelper.FormatSpan(ms, format));

    [Fact]
    public async Task Countdown_RendersAStatistic()
    {
        var html = await RenderAsync(new Node(new CountdownTagHelper { Title = "Offer ends in", Value = DateTime.Now.AddHours(2) }, "app-countdown"));
        Assert.Contains("<div class=\"ant-statistic-title\">Offer ends in</div>", html);
        Assert.Matches("role=\"timer\">(01:59:5\\d|02:00:00)</span>", html);
    }

    [Fact]
    public async Task Comment_RendersActionsAndNestedReplies()
    {
        var html = await RenderAsync(new Node(new CommentTagHelper { Author = "Liam", Datetime = "2 hours ago", Avatar = "LO", Content = "Ready <for> review" }, "app-comment", Children:
        [
            new(new CommentActionTagHelper(), "comment-action", "Reply"),
            new(new CommentTagHelper { Author = "Amelia", Content = "On it" }, "app-comment"),
        ]));

        Assert.Contains("<span class=\"ant-comment-content-author-name\"><a>Liam</a></span><span class=\"ant-comment-content-author-time\"><span>2 hours ago</span></span>", html);
        Assert.Contains("<p>Ready &lt;for&gt; review</p>", html);
        Assert.Contains("<ul class=\"ant-comment-actions\"><li><span>Reply</span></li></ul>", html);
        Assert.Contains("<div class=\"ant-comment-nested\"><div class=\"ant-comment\">", html);
        // The reply has no actions of its own.
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "ant-comment-actions"));
    }

    [Theory]
    [InlineData("left", "width: 360px")]
    [InlineData("bottom", "height: 240px")]
    [InlineData("nowhere", "width: 360px")]
    public async Task Drawer_PlacesItselfAndOpensLikeAModal(string placement, string size)
    {
        var html = await RenderAsync(new Node(new DrawerTagHelper { Name = "invoice", Title = "Invoice", Placement = placement, Width = 360, Height = 240 }, "app-drawer", "Body"));
        var side = placement == "nowhere" ? "right" : placement;

        Assert.Contains($"class=\"ant-drawer ant-drawer-{side} ant-drawer-open m-drawer\" x-data=\"modulusModal(&#x27;invoice&#x27;)\"", html);
        Assert.Contains($"style=\"{size}", html);
        Assert.Contains($"x-transition:enter-start=\"m-drawer-hidden-{side}\"", html);
        Assert.Contains("<div class=\"ant-drawer-body\">Body</div>", html);
    }

    [Fact]
    public async Task Flex_EmitsAntDesignClasses()
    {
        var html = await RenderAsync(new Node(new FlexTagHelper { Gap = "small", Justify = "space-between", Align = "center" }, "app-flex", "x"));
        Assert.Contains("class=\"ant-flex ant-flex-align-center ant-flex-justify-space-between ant-flex-gap-small ant-flex-wrap-nowrap\"", html);
        Assert.Contains("align-items:center;justify-content:space-between;", html);
    }
}
