using System.Collections;
using System.Globalization;
using System.Text;
using AntDesign;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions.Forms;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Lookup (Odoo many2one, SAP value help): type-ahead suggestions and a "Search more…" dialog (F4). Posts the value.
/// <code>
/// &lt;app-lookup asp-for="CustomerId" items="Model.Customers" aria-label="Customer" /&gt;
/// &lt;app-lookup asp-for="ProductId" search-href="/api/products/lookup" label="@Model.ProductName" /&gt;
/// </code>
/// The search endpoint takes <c>?q=text&amp;max=n</c> and returns <c>[{ value, label, description, fields }]</c>.
/// The field raises <c>m-lookup-change</c> with the picked item as its detail.
/// </summary>
[HtmlTargetElement("app-lookup", TagStructure = TagStructure.WithoutEndTag)]
public class LookupTagHelper : BoundControlTagHelper
{
    public IEnumerable<LookupItem>? Items { get; set; }

    /// <summary>JSON search endpoint, used instead of Items.</summary>
    public string? SearchHref { get; set; }

    /// <summary>The label of the value when it is not in Items.</summary>
    public string? Label { get; set; }

    public IEnumerable<LookupColumn>? Columns { get; set; }

    /// <summary>The dialog's title; null for "Search: {aria-label}".</summary>
    public string? Title { get; set; }

    public int MaxSuggestions { get; set; } = 8;
    public int DialogPageSize { get; set; } = 50;
    public int DialogWidth { get; set; } = 720;
    public string SearchMoreText { get; set; } = "Search more…";
    public bool AllowClear { get; set; } = true;

    [HtmlAttributeName("aria-label")] public string? AriaLabel { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (name, id, raw) = Bound();
        var extra = output.TakeAttributes();
        var items = SearchHref is null ? (Items ?? []).ToList() : null;
        var value = raw ?? "";
        var label = value.Length == 0 ? "" : items?.FirstOrDefault(i => i.Value == value)?.Label ?? Label ?? value;
        var columns = (Columns ?? LookupSearch.DefaultColumns).ToList();
        var listId = $"m-lookup-{context.UniqueId}";
        var more = $"{SearchMoreText} (F4)";
        var cfg = new
        {
            value,
            label,
            items = items?.Select(i => new { v = i.Value, l = i.Label, d = i.Description, f = i.Fields }),
            href = SearchHref,
            max = MaxSuggestions,
            page = DialogPageSize,
            columns = columns.Select(c => c.Key),
            titles = columns.Select(c => c.Title),
            multi = Columns is not null,
            disabled = Disabled,
        };

        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("m-lookup", Disabled ? "m-lookup-disabled" : null);
        output.Attributes.SetAttribute("x-data", new HtmlString($"modulusLookup({Widgets.Config(cfg)})"));
        output.Attributes.SetAttribute(":class", "{ 'm-lookup-open': open }");

        var box = Html.Cx("ant-input-affix-wrapper", "m-lookup-box", SizeClass("ant-input-affix-wrapper"), StatusClass("ant-input-affix-wrapper"), Disabled ? "ant-input-affix-wrapper-disabled" : null);
        var sb = new StringBuilder();
        sb.Append(BusinessFields.Hidden(name, value, "value", "value"));
        sb.Append($$"""<span class="{{box}}"><input type="text" class="ant-input m-lookup-input"{{Html.Attr("id", id.Length > 0 ? id : null)}} autocomplete="off"{{Html.Attr("disabled", Disabled)}} role="combobox" aria-autocomplete="list" aria-expanded="false" :aria-expanded="open ? 'true' : 'false'" aria-controls="{{listId}}" :aria-activedescendant="open && active >= 0 ? '{{listId}}-' + active : null"{{Html.Attr("aria-label", AriaLabel)}}{{BusinessFields.Invalid(EffectiveStatus)}} value="{{Html.E(label)}}" x-ref="input" x-model="text" @input="typed()" @keydown="key($event)" @blur="blur()"{{extra}} />""");
        sb.Append("""<span class="ant-input-suffix">""");
        if (AllowClear && !Disabled)
            sb.Append($"""<span class="ant-input-clear-icon" role="button" aria-label="Clear" x-show="text"{(label.Length == 0 ? " style=\"display: none;\"" : "")} @mousedown.prevent @click="pick(null)">{AntIcons.Span("close-circle", IconThemeType.Fill)}</span>""");
        sb.Append($"""<button type="button" class="m-lookup-more" aria-label="{Html.E(more)}" title="{Html.E(more)}"{Html.Attr("disabled", Disabled)} tabindex="-1" @mousedown.prevent @click="openDialog()">{AntIcons.Span("search")}</button></span></span>""");

        // Suggestions.
        sb.Append($$"""<div class="ant-select-dropdown ant-select-dropdown-placement-bottomLeft m-cselect-dropdown m-lookup-dropdown" :class="{ 'm-lookup-multi': multi }" x-show="open" x-cloak style="display: none;" @mousedown.prevent>""");
        sb.Append($$"""<div class="m-cselect-list" id="{{listId}}" role="listbox" aria-label="{{Html.E(AriaLabel ?? "Suggestions")}}">""");
        sb.Append("""<div class="m-lookup-head" x-show="multi && list.length" x-cloak style="display: none;" :style="'--m-lookup-cols: ' + cols.length" aria-hidden="true"><template x-for="t in titles"><span class="m-lookup-cell" x-text="t"></span></template></div>""");
        sb.Append("""<div class="m-cselect-empty" x-show="!list.length" x-text="loading ? 'Searching…' : 'No matches'"></div>""");
        sb.Append($$"""<template x-for="(item, i) in list" :key="item.v"><div class="ant-select-item ant-select-item-option" :class="{ 'ant-select-item-option-selected': item.v === value, 'ant-select-item-option-active': i === active }" :id="'{{listId}}-' + i" role="option" :aria-selected="item.v === value ? 'true' : 'false'" @click="pick(item)" @mouseenter="active = i">""");
        sb.Append("""<div class="ant-select-item-option-content"><div class="m-lookup-cells" x-show="multi" x-cloak style="display: none;" :style="'--m-lookup-cols: ' + cols.length"><template x-for="c in cols"><span class="m-lookup-cell" x-text="cell(item, c)"></span></template></div><span class="m-lookup-option-label" x-show="!multi" x-text="item.l"></span><span class="m-lookup-option-desc" x-show="!multi && item.d" x-text="item.d"></span></div></div></template></div>""");
        sb.Append($"""<div class="m-cselect-footer"><button type="button" class="ant-btn ant-btn-text ant-btn-block m-cselect-add-new m-lookup-search-more" @click="openDialog()">{AntIcons.Span("search")}<span>{Html.E(SearchMoreText)}</span></button></div></div>""");

        // "Search more" dialog.
        var titleId = $"{listId}-title";
        var title = Title ?? $"Search: {AriaLabel ?? "records"}";
        sb.Append("""<div class="m-modal m-lookup-dialog" x-show="dialog" x-cloak style="display: none;" @keydown.escape="closeDialog()"><div class="ant-modal-mask"></div>""");
        sb.Append($$"""<div class="ant-modal-wrap" role="dialog" aria-modal="true" aria-labelledby="{{titleId}}" @click.self="closeDialog()"><div class="ant-modal" style="width:{{DialogWidth}}px"><div class="ant-modal-content">""");
        sb.Append($"""<button type="button" class="ant-modal-close" aria-label="Close" @click="closeDialog()"><span class="ant-modal-close-x">{AntIcons.Span("close", IconThemeType.Outline, "ant-modal-close-icon")}</span></button>""");
        sb.Append($"""<div class="ant-modal-header"><div class="ant-modal-title" id="{titleId}">{Html.E(title)}</div></div><div class="ant-modal-body">""");
        sb.Append($"""<span class="ant-input-affix-wrapper m-lookup-search"><span class="ant-input-prefix">{AntIcons.Span("search")}</span><input type="text" class="ant-input m-lookup-search-input" placeholder="Search" aria-label="Search" autocomplete="off" x-ref="search" x-model="query" @input="search(false)" @keydown="dialogKey($event)" /></span>""");
        sb.Append("""<div class="ant-table-wrapper"><div class="ant-table ant-table-small m-lookup-table"><div class="ant-table-container"><div class="ant-table-content"><table><thead class="ant-table-thead"><tr>""");
        foreach (var c in columns) sb.Append($"""<th class="ant-table-cell">{Html.E(c.Title)}</th>""");
        sb.Append($"""</tr></thead><tbody class="ant-table-tbody"><tr class="ant-table-placeholder" x-show="!rows.length"><td class="ant-table-cell" colspan="{columns.Count}" x-text="loading ? 'Searching…' : 'No matches'"></td></tr>""");
        sb.Append("""<template x-for="(item, i) in rows" :key="item.v"><tr class="ant-table-row m-lookup-row" :class="{ 'ant-table-row-selected': item.v === value, 'm-lookup-row-active': i === row }" :aria-selected="item.v === value ? 'true' : 'false'" @click="pick(item)" @mouseenter="row = i">""");
        sb.Append("""<template x-for="c in cols"><td class="ant-table-cell" x-text="cell(item, c)"></td></template></tr></template></tbody></table></div></div></div></div>""");
        sb.Append("""<div class="m-lookup-count" x-text="countText"></div><button type="button" class="ant-btn ant-btn-default ant-btn-block m-lookup-more-rows" x-show="total < 0 || total > rows.length" x-cloak style="display: none;" @click="more()" x-text="'Show ' + page + ' more'"></button></div>""");
        sb.Append("""<div class="ant-modal-footer"><button type="button" class="ant-btn ant-btn-default" @click="closeDialog()"><span>Cancel</span></button></div></div></div></div></div>""");
        output.Content.SetHtmlContent(sb.ToString());
    }
}
