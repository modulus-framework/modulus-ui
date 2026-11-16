using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.Json;
using AntDesign;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Cascader: pick a leaf through columns of options, shown as "Parent / Child".
/// <code>&lt;app-cascader asp-for="Location" options="Model.Locations" placeholder="Select a city" /&gt;</code>
/// The chosen node's value posts (values must be unique in the tree); <c>change-on-select</c> allows parents.
/// </summary>
[HtmlTargetElement("app-cascader", TagStructure = TagStructure.WithoutEndTag)]
public class CascaderTagHelper : BoundControlTagHelper
{
    public IReadOnlyList<TreeOption> Options { get; set; } = [];
    public string? Placeholder { get; set; }
    public bool ChangeOnSelect { get; set; }
    public string Separator { get; set; } = " / ";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (name, id, value) = Bound();
        var path = TreeOption.PathTo(Options, value);
        var label = string.Join(Separator, path.Select(p => p.Title));
        var (cls, style) = TakeClassAndStyle(output);
        var passThrough = output.TakeAttributes();
        var selectClass = Html.Cx("ant-select", "ant-cascader", "ant-select-single", "ant-select-show-arrow", "ant-cascader-picker",
            SizeClass("ant-select"), StatusClass("ant-select"), Disabled ? "ant-select-disabled" : null);
        var right = AntIcons.Span("right");
        var html =
            $$"""<div class="{{selectClass}}" :class="{ 'ant-select-open': open, 'ant-select-focused': open }" @click="toggleOpen()" @keydown.enter.prevent="toggleOpen()" @keydown.space.prevent="toggleOpen()" @keydown.escape="open = false">""" +
            $"""<div class="ant-select-selector"><span class="ant-select-selection-search"><input type="search" readonly unselectable="on" class="ant-select-selection-search-input" autocomplete="off" role="combobox" aria-haspopup="listbox" :aria-expanded="open"{Html.Attr("id", id.Length > 0 && !passThrough.Contains(" id=\"") ? id : null)}{Html.Attr("disabled", Disabled)}{passThrough} /></span>""" +
            $"""<span class="ant-select-selection-item" title="{Html.E(label)}" :title="label()" x-text="label()" x-show="value !== ''"{(path.Count > 0 ? "" : " style=\"display: none;\"")}>{Html.E(label)}</span>""" +
            (Placeholder is null ? "" : $"""<span class="ant-select-selection-placeholder" x-show="value === ''"{(path.Count > 0 ? " style=\"display: none;\"" : "")}>{Html.E(Placeholder)}</span>""") +
            $"""</div>{Widgets.Arrow()}</div>""" +
            $$"""<div class="ant-select-dropdown ant-cascader-dropdown ant-select-dropdown-placement-bottomLeft m-popup" x-show="open" x-transition.opacity x-cloak><div class="ant-cascader-menus">""" +
            $$"""<template x-for="(column, c) in columns()" :key="c"><ul class="ant-cascader-menu" role="menu">""" +
            $$"""<template x-for="option in column" :key="option.v"><li class="ant-cascader-menu-item" :class="{ 'ant-cascader-menu-item-expand': option.c, 'ant-cascader-menu-item-active': active[c] === option.v, 'ant-cascader-menu-item-disabled': option.d }" role="menuitemcheckbox" :title="option.t" :aria-checked="active[c] === option.v" :aria-disabled="option.d" @click.stop="pick(c, option)">""" +
            $$"""<div class="ant-cascader-menu-item-content" x-text="option.t"></div><template x-if="option.c"><div class="ant-cascader-menu-item-expand-icon">{{right}}</div></template></li></template></ul></template></div></div>""" +
            (name.Length > 0 ? $"""<input type="hidden" name="{Html.E(name)}" value="{Html.E(path.Count > 0 ? value : "")}" :value="value" />""" : "");

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", Html.Cx("m-select-wrap", cls));
        if (style is not null) output.Attributes.SetAttribute("style", style);
        output.Attributes.SetAttribute("x-data", new HtmlString($"modulusCascader({Widgets.Config(new { options = Options.Select(Node), path = path.Select(p => p.Value), changeOnSelect = ChangeOnSelect, separator = Separator, disabled = Disabled })})"));
        output.Attributes.SetAttribute("@click.outside", "open = false");
        output.Content.SetHtmlContent(html);
    }

    private static object Node(TreeOption o) => new { v = o.Value, t = o.Title, d = o.Disabled, c = o.Children?.Select(Node) };
}
