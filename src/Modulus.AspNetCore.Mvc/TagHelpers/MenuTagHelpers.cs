using System.Text;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

// Menu: the same <ul class="ant-menu"> tree AntD Blazor renders. The server marks the selected item and the open
// submenus; modulusMenu (modulus-widgets.js) keeps them in step when the user clicks, and opens horizontal and
// vertical submenus as popups.

internal sealed class MenuContext
{
    public required string Mode { get; init; }
    public required string Theme { get; init; }
    public required string? Selected { get; init; }
    public required HashSet<string> Open { get; init; }
    public int Level { get; set; } = 1;

    /// <summary>Keys rendered inside the submenu being built, so it can mark itself selected.</summary>
    public List<string>? Keys { get; set; }

    public bool Inline => Mode == "inline";
}

/// <summary>
/// Navigation menu: <c>&lt;app-menu mode="horizontal|inline|vertical" selected="mail" open="mailbox"&gt;</c> with
/// <c>app-menu-item</c>, <c>app-sub-menu</c> and <c>app-menu-divider</c> children.
/// </summary>
[HtmlTargetElement("app-menu")]
public class MenuTagHelper : TagHelper
{
    /// <summary>horizontal | inline | vertical</summary>
    public string Mode { get; set; } = "vertical";

    /// <summary>light | dark</summary>
    public string Theme { get; set; } = "light";

    /// <summary>Key of the selected item.</summary>
    public string? Selected { get; set; }

    /// <summary>Comma-separated keys of the submenus that start open (inline mode).</summary>
    public string? Open { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var open = (Open ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
        var menu = new MenuContext { Mode = Mode, Theme = Theme, Selected = Selected, Open = open };
        context.Items[typeof(MenuContext)] = menu;
        var items = (await output.GetChildContentAsync()).GetContent();

        output.TagName = "ul";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-menu", "ant-menu-root", $"ant-menu-{Html.E(Theme)}", $"ant-menu-{Html.E(Mode)}");
        output.Attributes.SetAttribute("direction", "ltr");
        output.Attributes.SetAttribute("role", "menu");
        output.Attributes.SetAttribute("x-data", new Microsoft.AspNetCore.Html.HtmlString(
            $"modulusMenu({Widgets.Config(new { selected = Selected ?? "", open = open.ToArray(), inline = menu.Inline })})"));
        output.Content.SetHtmlContent(items);
    }
}

[HtmlTargetElement("app-menu-item")]
public class MenuItemTagHelper : TagHelper
{
    public string Key { get; set; } = "";
    public string? Icon { get; set; }
    public string? Href { get; set; }
    public bool Disabled { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var body = (await output.GetChildContentAsync()).GetContent();
        if (!context.Items.TryGetValue(typeof(MenuContext), out var m) || m is not MenuContext menu)
        {
            output.SuppressOutput();
            return;
        }
        menu.Keys?.Add(Key);
        var selected = Key == menu.Selected;
        var key = Js(Key);
        output.TagName = "li";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-menu-item", selected ? "ant-menu-item-selected" : null, Disabled ? "ant-menu-item-disabled" : null);
        output.Attributes.SetAttribute("role", "menuitem");
        if (menu.Inline) output.AddStyle($"padding-left:{menu.Level * 24}px");
        if (!Disabled)
        {
            output.Attributes.SetAttribute(":class", $"{{ 'ant-menu-item-selected': selected === {key} }}");
            output.Attributes.SetAttribute("@click", $"select({key})");
            if (Href is null)
            {
                output.Attributes.SetAttribute("tabindex", "0");
                output.Attributes.SetAttribute("@keydown.enter.prevent", $"select({key})");
            }
        }
        else output.Attributes.SetAttribute("aria-disabled", "true");
        var title = Href is null ? body : $"""<a href="{Html.E(Href)}">{body}</a>""";
        output.Content.SetHtmlContent((Icon is null ? "" : AntIcons.Span(Icon)) + $"""<span class="ant-menu-title-content">{title}</span>""");
    }

    internal static string Js(string value) => "'" + Html.E(value.Replace("\\", "\\\\").Replace("'", "\\'")) + "'";
}

/// <summary>Submenu: opens in place (inline) or as a popup (horizontal and vertical menus).</summary>
[HtmlTargetElement("app-sub-menu")]
public class SubMenuTagHelper : TagHelper
{
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Icon { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (!context.Items.TryGetValue(typeof(MenuContext), out var m) || m is not MenuContext menu)
        {
            output.SuppressOutput();
            return;
        }
        var outerKeys = menu.Keys;
        var keys = new List<string>();
        menu.Keys = keys;
        menu.Level++;
        var items = (await output.GetChildContentAsync()).GetContent();
        menu.Level--;
        menu.Keys = outerKeys;
        outerKeys?.AddRange(keys);

        var key = MenuItemTagHelper.Js(Key);
        var keyList = "[" + string.Join(",", keys.Select(MenuItemTagHelper.Js)) + "]";
        var selected = menu.Selected is not null && keys.Contains(menu.Selected);
        var open = menu.Inline && menu.Open.Contains(Key);
        var icon = Icon is null ? "" : AntIcons.Span(Icon);
        var title = $"""{icon}<span class="ant-menu-title-content">{Html.E(Title)}</span>""";

        output.TagName = "li";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("role", "menuitem");
        output.AddStyle("position:relative");
        if (menu.Inline)
        {
            output.AddClasses("ant-menu-submenu", "ant-menu-submenu-inline", selected ? "ant-menu-submenu-selected" : null, open ? "ant-menu-submenu-open" : null);
            output.Attributes.SetAttribute(":class", $"{{ 'ant-menu-submenu-open': isOpen({key}), 'ant-menu-submenu-selected': {keyList}.includes(selected) }}");
            output.Content.SetHtmlContent(
                $"""<div class="ant-menu-submenu-title" style="padding-left:{(menu.Level) * 24}px;" role="button" tabindex="0" aria-haspopup="true" aria-expanded="{(open ? "true" : "false")}" :aria-expanded="isOpen({key})" @click="toggle({key})" @keydown.enter.prevent="toggle({key})">{title}<i class="ant-menu-submenu-arrow"></i></div>""" +
                $"""<ul direction="ltr" class="{Html.Cx("ant-menu", "ant-menu-sub", $"ant-menu-{Html.E(menu.Theme)}", "ant-menu-inline", open ? null : "ant-menu-hidden")}" :class="{"{"} 'ant-menu-hidden': !isOpen({key}) {"}"}" role="menu">{items}</ul>""");
            return;
        }

        // Horizontal and vertical: the children open in a popup beside (or below) the title.
        var below = menu.Mode == "horizontal" && menu.Level == 1;
        output.AddClasses("ant-menu-submenu", "ant-menu-submenu-vertical", selected ? "ant-menu-submenu-selected" : null);
        output.Attributes.SetAttribute(":class", $"{{ 'ant-menu-submenu-open ant-menu-submenu-active': hover === {key}, 'ant-menu-submenu-selected': {keyList}.includes(selected) }}");
        output.Attributes.SetAttribute("@mouseenter", $"hover = {key}");
        output.Attributes.SetAttribute("@mouseleave", "hover = ''");
        var placement = below ? "ant-menu-submenu-placement-bottomLeft m-menu-popup-below" : "ant-menu-submenu-placement-rightTop m-menu-popup-right";
        output.Content.SetHtmlContent(
            $"""<div class="ant-menu-submenu-title" role="button" tabindex="0" aria-haspopup="true" aria-expanded="false" :aria-expanded="hover === {key}" @click="hover = hover === {key} ? '' : {key}" @keydown.enter.prevent="hover = hover === {key} ? '' : {key}">{title}{(below ? "" : "<i class=\"ant-menu-submenu-arrow\"></i>")}</div>""" +
            $"""<div class="ant-menu-submenu ant-menu-submenu-popup ant-menu-{Html.E(menu.Theme)} {placement}" x-show="hover === {key}" x-transition.opacity x-cloak>""" +
            $"""<ul direction="ltr" class="ant-menu ant-menu-sub ant-menu-{Html.E(menu.Theme)} ant-menu-vertical" role="menu">{items}</ul></div>""");
    }
}

[HtmlTargetElement("app-menu-divider", TagStructure = TagStructure.WithoutEndTag)]
public class MenuDividerTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "li";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-menu-item-divider");
        output.Attributes.SetAttribute("role", "separator");
    }
}
