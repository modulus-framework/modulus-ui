using System.Globalization;
using System.Text;
using AntDesign;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Qr;
using Modulus.Ui.Abstractions.Theming;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Select whose dropdown ends with a "Create new …" row, like the Blazor <c>AppCreatableSelect</c>: the row opens a
/// small form in place of the list (a name, a named tag colour, Cancel and Add), so picking and creating stay apart. Posts hidden inputs named <c>name</c> (one per value with <c>multiple</c>) and raises
/// <c>change</c>, so a filter form's <c>@change</c> submits it.
/// <code>&lt;app-creatable-select name="label" options="Labels" value="@q.Label" placeholder="Any label" add-href="/labels" /&gt;</code>
/// Without <c>add-href</c> a new option lives in the page; with it, the label and colour are POSTed there (form
/// fields <c>label</c> and <c>color</c>, antiforgery header included) and the JSON reply,
/// <c>{ value, label, color }</c> or <c>{ error }</c>, is used.
/// </summary>
[HtmlTargetElement("app-creatable-select", TagStructure = TagStructure.WithoutEndTag)]
public class CreatableSelectTagHelper : BoundControlTagHelper
{
    public IEnumerable<GridOption> Options { get; set; } = [];

    public bool Multiple { get; set; }

    /// <summary>Selected values with <c>multiple</c> (otherwise <c>value</c> or <c>asp-for</c>).</summary>
    public IEnumerable<string>? Values { get; set; }

    public string? Placeholder { get; set; }
    public bool AllowClear { get; set; }

    [HtmlAttributeName("allow-add")] public bool AllowAdd { get; set; } = true;
    [HtmlAttributeName("show-colors")] public bool ShowColors { get; set; } = true;
    [HtmlAttributeName("add-placeholder")] public string AddPlaceholder { get; set; } = "New item";
    [HtmlAttributeName("add-text")] public string AddText { get; set; } = "Add";

    /// <summary>The row that opens the create form, and the form's title ("Create new label").</summary>
    [HtmlAttributeName("create-text")] public string CreateText { get; set; } = "Create new item";
    [HtmlAttributeName("add-href")] public string? AddHref { get; set; }

    /// <summary>
    /// Name of an &lt;app-modal&gt; that creates a record: the footer shows one "Add new" button that opens it, instead of
    /// the inline row. When the modal's form saves, the server calls <c>Response.HxOptionAdded(list, option)</c> with this
    /// name as the list; the option is added and selected.
    /// </summary>
    [HtmlAttributeName("add-modal")] public string? AddModal { get; set; }

    [HtmlAttributeName("add-new-text")] public string AddNewText { get; set; } = "Add new";

    [HtmlAttributeName("aria-label")]
    public string? AriaLabel { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (name, id, value) = Bound();
        var values = Multiple ? (Values?.ToList() ?? Widgets.Values(For?.Model, value)) : [];
        var m = new CreatableSelectModel(name, $"m-cselect-{context.UniqueId}", Options.ToList())
        {
            Id = id.Length > 0 ? id : null,
            SizeClass = SizeClass("ant-select"),
            Multiple = Multiple,
            Value = Multiple ? null : value,
            Values = values,
            Placeholder = Placeholder,
            AllowClear = AllowClear,
            Disabled = Disabled,
            AllowAdd = AllowAdd,
            ShowColors = ShowColors,
            AddPlaceholder = AddPlaceholder,
            AddText = AddText,
            CreateText = CreateText,
            AddHref = AddHref,
            AddModal = AddModal,
            AddNewText = AddNewText,
            AriaLabel = AriaLabel,
        };

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses(Classes(m));
        output.Attributes.SetAttribute("x-data", new HtmlString(XData(m)));
        output.Attributes.SetAttribute(":class", "open && 'ant-select-open ant-select-focused'");
        if (AddModal is not null) output.Attributes.SetAttribute("@modulus:option-added.window", "added($event.detail)");
        output.Content.SetHtmlContent(Inner(m));
    }

    /// <summary>The whole select as one element (the editable grid's Select, Tag and Multi-select editors).</summary>
    /// <param name="attributes">Extra attributes of the root, with a leading space.</param>
    internal static string Render(CreatableSelectModel m, string? attributes = null) =>
        $"""<div class="{Classes(m)}" x-data="{XData(m)}" :class="open &amp;&amp; 'ant-select-open ant-select-focused'"{attributes}>{Inner(m)}</div>""";

    private static string Classes(CreatableSelectModel m) =>
        Html.Cx("ant-select", m.Multiple ? "ant-select-multiple" : "ant-select-single", "ant-select-show-arrow", "m-cselect",
            m.SizeClass, m.AllowClear ? "ant-select-allow-clear" : null, m.Disabled ? "ant-select-disabled" : null);

    private static string XData(CreatableSelectModel m) => $"modulusCreatableSelect({Widgets.Config(new
    {
        name = m.Name,
        multiple = m.Multiple,
        value = m.Value ?? "",
        values = m.Values,
        options = m.Options.Select(o => new { value = o.Value, label = o.Label, color = o.Color }),
        placeholder = m.Placeholder,
        showColors = m.ShowColors,
        addHref = m.AddHref,
        addModal = m.AddModal,
        gridOp = m.GridOp,
        colors = GridSchema.PickerColors,
        colorNames = GridSchema.PickerColors.ToDictionary(c => c, GridSchema.ColorName),
        maxOptions = GridSchema.MaxOptions,
        maxLabel = GridSchema.MaxOptionLabel,
    })})";

    private static string Inner(CreatableSelectModel m)
    {
        var listId = m.ListId;
        var sb = new StringBuilder();
        sb.Append("""<span class="m-cselect-inputs" x-html="inputs()">""");
        if (m.Multiple)
            foreach (var v in m.Values) sb.Append($"""<input type="hidden" name="{Html.E(m.Name)}" value="{Html.E(v)}" />""");
        else
            sb.Append($"""<input type="hidden" name="{Html.E(m.Name)}" value="{Html.E(m.Value)}" />""");
        sb.Append("</span>");

        sb.Append($"""<div class="ant-select-selector"{Html.Attr("id", m.Id)} role="combobox" tabindex="{(m.Disabled ? -1 : 0)}" aria-haspopup="listbox" aria-expanded="false" aria-controls="{listId}"{Html.Attr("aria-label", m.AriaLabel)}""");
        sb.Append(m.Disabled
            ? """ aria-disabled="true">"""
            : $""" :aria-expanded="open" :aria-activedescendant="open && active >= 0 ? '{listId}-' + active : null" @click="onSelector($event)" @keydown="key($event)" x-html="selection()">""");
        sb.Append(Selection(m)).Append("</div>");
        sb.Append(Widgets.Arrow());
        if (m.AllowClear && !m.Disabled)
        {
            var empty = m.Multiple ? m.Values.Count == 0 : string.IsNullOrEmpty(m.Value);
            sb.Append($"""<span class="ant-select-clear" role="button" aria-label="Clear" x-show="has()"{(empty ? " style=\"display: none;\"" : "")} @click.stop="clear()">{AntIcons.Span("close-circle", IconThemeType.Fill)}</span>""");
        }

        if (!m.Disabled)
        {
            sb.Append("""<div class="m-cselect-backdrop" x-show="open" x-cloak @click="close()"></div>""");
            sb.Append("""<div class="ant-select-dropdown ant-select-dropdown-placement-bottomLeft m-cselect-dropdown" :class="creating && 'm-cselect-creating'" x-show="open" x-cloak>""");
            sb.Append($"""<div class="m-cselect-list" x-show="!creating" id="{listId}" role="listbox" aria-label="{Html.E(m.AriaLabel ?? "Options")}"{(m.Multiple ? " aria-multiselectable=\"true\"" : "")}>""");
            sb.Append("""<div class="m-cselect-empty" x-show="!options.length">No options yet</div>""");
            sb.Append("""<template x-for="(o, i) in options" :key="o.value">""");
            sb.Append($$"""<div class="ant-select-item ant-select-item-option" :class="{ 'ant-select-item-option-selected': sel(o.value), 'ant-select-item-option-active': i === active }" :id="'{{listId}}-' + i" role="option" :aria-selected="sel(o.value)" @click="pick(o.value)" @mouseenter="active = i">""");
            sb.Append("""<div class="ant-select-item-option-content"><span class="m-cselect-dot" :class="'ant-tag-' + o.color" x-show="showColors && o.color && o.color !== 'default'" aria-hidden="true"></span><span x-text="o.label"></span></div>""");
            sb.Append($"""<span class="ant-select-item-option-state" x-show="sel(o.value)" aria-hidden="true">{AntIcons.Span("check")}</span></div></template></div>""");
            if (m.AddModal is not null)
            {
                sb.Append($"""<div class="m-cselect-footer"><button type="button" class="ant-btn ant-btn-text ant-btn-block m-cselect-add-new" @click="addNew()">{AntIcons.Span("plus")}<span>{Html.E(m.AddNewText)}</span></button></div>""");
            }
            else if (m.AllowAdd)
            {
                var create = Html.E(m.CreateText);
                sb.Append($"""<div class="m-cselect-footer" x-show="!creating"><button type="button" class="ant-btn ant-btn-text ant-btn-block m-cselect-create" @click="startCreate()">{AntIcons.Span("plus")}<span>{create}</span></button></div>""");
                sb.Append($"""<div class="m-cselect-footer m-cselect-add" role="group" aria-label="{create}" x-show="creating" x-cloak @keydown.escape.stop.prevent="cancelCreate()">""");
                sb.Append($"""<div class="m-cselect-add-title">{create}</div><label class="m-cselect-field-label" for="{listId}-name">Name</label>""");
                // In a grid the name and colour post with the add-option operation (EditableGridForm.OptionField/ColorField).
                sb.Append($"""<input class="ant-input ant-input-sm" id="{listId}-name" type="text"{Html.Attr("name", m.OptionField)} x-ref="add" x-model="newLabel" placeholder="{Html.E(m.AddPlaceholder)}" autocomplete="off" maxlength="{GridSchema.MaxOptionLabel}" @input="error = ''" @change.stop @keydown.enter.stop.prevent="add()" />""");
                if (m.ColorField is not null) sb.Append($"""<input type="hidden" name="{Html.E(m.ColorField)}" :value="color()" />""");
                if (m.ShowColors)
                    sb.Append($"""<div class="m-cselect-field-label" id="{listId}-colour">Colour <span class="m-cselect-color-name" x-text="colorName(color())"></span></div>""")
                      .Append($"""<div class="m-cselect-colors" role="radiogroup" aria-labelledby="{listId}-colour"><template x-for="c in colors" :key="c"><button type="button" class="m-cselect-swatch" :class="'ant-tag-' + c" role="radio" :aria-checked="c === color()" :aria-label="colorName(c)" :title="colorName(c)" @click="picked = c"></button></template></div>""");
                sb.Append("""<div class="m-cselect-error" role="alert" x-show="error" x-text="error"></div>""");
                sb.Append($"""<div class="m-cselect-add-actions"><button type="button" class="ant-btn ant-btn-default ant-btn-sm m-cselect-cancel" @click="cancelCreate()"><span>Cancel</span></button>""")
                  .Append($"""<button type="button" class="ant-btn ant-btn-primary ant-btn-sm m-cselect-add-btn" :disabled="busy" @click="add()">{AntIcons.Span("plus")}<span>{Html.E(m.AddText)}</span></button></div></div>""");
            }
            sb.Append("</div>");
        }
        return sb.ToString();
    }

    // The selector's first paint; modulusCreatableSelect.selection() renders the same markup afterwards.
    private static string Selection(CreatableSelectModel m)
    {
        GridOption? Find(string? v) => m.Options.FirstOrDefault(o => o.Value == v);
        string Dot(GridOption? o) => m.ShowColors && o?.Color is { } c && c != "default" ? $"""<span class="m-cselect-dot ant-tag-{Html.E(c)}" aria-hidden="true"></span>""" : "";
        var placeholder = m.Placeholder is null ? "" : $"""<span class="ant-select-selection-placeholder">{Html.E(m.Placeholder)}</span>""";
        if (!m.Multiple)
        {
            if (string.IsNullOrEmpty(m.Value)) return placeholder;
            var o = Find(m.Value);
            return $"""<span class="ant-select-selection-item" title="{Html.E(o?.Label ?? m.Value)}">{Dot(o)}{Html.E(o?.Label ?? m.Value)}</span>""";
        }
        var sb = new StringBuilder("""<div class="ant-select-selection-overflow">""");
        foreach (var v in m.Values)
        {
            var o = Find(v);
            var tint = o?.Color is { } c && c != "default" ? $" ant-tag-{Html.E(c)}" : "";
            sb.Append($"""<span class="ant-select-selection-overflow-item"><span class="ant-select-selection-item m-cselect-tag{tint}" title="{Html.E(o?.Label ?? v)}"><span class="ant-select-selection-item-content">{Html.E(o?.Label ?? v)}</span>""");
            if (!m.Disabled) sb.Append($"""<span class="ant-select-selection-item-remove" role="button" aria-label="Remove {Html.E(o?.Label ?? v)}" data-remove="{Html.E(v)}">{AntIcons.Span("close")}</span>""");
            sb.Append("</span></span>");
        }
        if (m.Values.Count == 0) sb.Append(placeholder);
        return sb.Append("</div>").ToString();
    }
}
