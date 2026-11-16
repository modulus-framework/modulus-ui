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
/// Two lists with search and move buttons, like Ant Design's Transfer:
/// <code>&lt;app-transfer name="permissions" options="Model.Permissions" target-keys="Model.Granted" titles="Available,Granted" show-search /&gt;</code>
/// Every key in the right-hand list posts as <c>name=key</c>.
/// </summary>
[HtmlTargetElement("app-transfer", TagStructure = TagStructure.WithoutEndTag)]
public class TransferTagHelper : BoundControlTagHelper
{
    public IReadOnlyList<TreeOption> Options { get; set; } = [];

    /// <summary>Keys in the right-hand list; defaults to the bound value.</summary>
    public IEnumerable<string>? TargetKeys { get; set; }

    /// <summary>"Left,Right" list titles.</summary>
    public string? Titles { get; set; }

    public bool ShowSearch { get; set; }
    public string SearchPlaceholder { get; set; } = "Search here";
    public string ItemUnit { get; set; } = "item";
    public string ItemsUnit { get; set; } = "items";
    public string NotFound { get; set; } = "No data";

    /// <summary>Style for each list (e.g. "flex: 1; height: 260px").</summary>
    public string? ListStyle { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (name, _, _) = Bound();
        var target = (TargetKeys ?? Widgets.Values(For?.Model, Value)).ToHashSet();
        var titles = (Titles ?? ",").Split(',', 2);
        var sb = new StringBuilder();
        sb.Append(List(0, titles[0].Trim(), target));
        sb.Append($"""<div class="ant-transfer-operation"><button type="button" class="ant-btn ant-btn-primary ant-btn-sm ant-btn-icon-only" aria-label="Move to {Html.E(titles[0].Trim())}" disabled :disabled="!canMove(1)" @click="move(1)">{AntIcons.Span("left")}</button>""" +
                  $"""<button type="button" class="ant-btn ant-btn-primary ant-btn-sm ant-btn-icon-only" aria-label="Move to {Html.E(titles[^1].Trim())}" disabled :disabled="!canMove(0)" @click="move(0)">{AntIcons.Span("right")}</button></div>""");
        sb.Append(List(1, titles[^1].Trim(), target));
        if (name.Length > 0)
            for (var i = 0; i < Options.Count; i++)
                sb.Append($"""<input type="hidden" name="{Html.E(name)}" value="{Html.E(Options[i].Value)}"{(target.Contains(Options[i].Value) ? "" : " disabled")} :disabled="!right[{i}]" />""");

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-transfer", Disabled ? "ant-transfer-disabled" : null);
        output.Attributes.SetAttribute("x-data", new HtmlString($"modulusTransfer({Widgets.Config(new { items = Options.Select(o => new { t = o.Title, d = o.Disabled || Disabled }), right = Options.Select(o => target.Contains(o.Value)), unit = ItemUnit, units = ItemsUnit })})"));
        output.Content.SetHtmlContent(sb.ToString());
    }

    private string List(int side, string title, HashSet<string> target)
    {
        var count = Options.Count(o => target.Contains(o.Value) == (side == 1));
        var sb = new StringBuilder();
        sb.Append($"""<div class="ant-transfer-list"{Html.Attr("style", ListStyle)}><div class="ant-transfer-list-header">""");
        sb.Append($$"""<label class="ant-checkbox-wrapper" :class="{ 'ant-checkbox-wrapper-checked': allChecked({{side}}) }"><span class="ant-checkbox" :class="{ 'ant-checkbox-checked': allChecked({{side}}), 'ant-checkbox-indeterminate': someChecked({{side}}) }"><input type="checkbox" class="ant-checkbox-input" aria-label="Select all" :checked="allChecked({{side}})" @change="checkAll({{side}}, $event.target.checked)" /><span class="ant-checkbox-inner"></span></span></label>""");
        sb.Append($"""<span class="ant-transfer-list-header-selected" x-text="header({side})">{count} {(count == 1 ? ItemUnit : ItemsUnit)}</span>""");
        sb.Append($"""<span class="ant-transfer-list-header-title">{Html.E(title)}</span></div>""");
        sb.Append($"""<div class="{Html.Cx("ant-transfer-list-body", ShowSearch ? "ant-transfer-list-body-with-search" : null)}">""");
        if (ShowSearch)
            sb.Append($"""<div class="ant-transfer-list-body-search-wrapper"><span class="ant-transfer-list-search ant-input-affix-wrapper"><span class="ant-input-prefix">{AntIcons.Span("search")}</span><input class="ant-input" type="text" placeholder="{Html.E(SearchPlaceholder)}" aria-label="{Html.E(SearchPlaceholder)}" x-model="search[{side}]" /></span></div>""");
        sb.Append($"""<ul class="ant-transfer-list-content" x-show="visibleCount({side}) > 0"{(count == 0 ? " style=\"display: none;\"" : "")}>""");
        for (var i = 0; i < Options.Count; i++)
        {
            var o = Options[i];
            var shown = target.Contains(o.Value) == (side == 1);
            var disabled = o.Disabled || Disabled;
            sb.Append($$"""<li class="{{Html.Cx("ant-transfer-list-content-item", disabled ? "ant-transfer-list-content-item-disabled" : null)}}" :class="{ 'ant-transfer-list-content-item-checked': checked[{{i}}] }" x-show="visible({{side}}, {{i}})"{{(shown ? "" : " style=\"display: none;\"")}}>""");
            sb.Append($$"""<label class="{{Html.Cx("ant-checkbox-wrapper", disabled ? "ant-checkbox-wrapper-disabled" : null)}}" :class="{ 'ant-checkbox-wrapper-checked': checked[{{i}}] }"><span class="{{Html.Cx("ant-checkbox", disabled ? "ant-checkbox-disabled" : null)}}" :class="{ 'ant-checkbox-checked': checked[{{i}}] }"><input type="checkbox" class="ant-checkbox-input"{{(disabled ? " disabled" : "")}} x-model="checked[{{i}}]" /><span class="ant-checkbox-inner"></span></span><span>{{Html.E(o.Title)}}</span></label></li>""");
        }
        sb.Append("</ul>");
        sb.Append($"""<div class="ant-transfer-list-body-not-found" x-show="visibleCount({side}) === 0"{(count == 0 ? "" : " style=\"display: none;\"")}>{EmptyTagHelper.Render(NotFound, "ant-empty-small")}</div>""");
        sb.Append("</div></div>");
        return sb.ToString();
    }
}
