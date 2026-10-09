using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Ant Design form item bound to a model property: label, required mark, control, and the
/// ModelState error (with the error border) — the same markup Ant Design Blazor's FormItem renders.
/// <code>&lt;app-form-item asp-for="Name" label="Contact name"&gt;&lt;input asp-for="Name" class="ant-input" /&gt;&lt;/app-form-item&gt;</code>
/// </summary>
[HtmlTargetElement("app-form-item")]
public class FormItemTagHelper : TagHelper
{
    /// <summary>Set in <see cref="TagHelperContext.Items"/> for controls rendered inside a form item.</summary>
    public const string ContextKey = "modulus:form-item";

    [HtmlAttributeName("asp-for")] public ModelExpression? For { get; set; }

    public string? Label { get; set; }

    /// <summary>Help text below the control (hidden while an error is shown).</summary>
    public string? Help { get; set; }

    /// <summary>vertical | horizontal</summary>
    public string Layout { get; set; } = "vertical";

    [ViewContext, HtmlAttributeNotBound] public ViewContext ViewContext { get; set; } = default!;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        context.Items[ContextKey] = true;
        var control = (await output.GetChildContentAsync()).GetContent();
        var name = For?.Name;
        var id = name is null ? null : TagBuilder.CreateSanitizedId(name, "_");
        // Only an explicit [Required] shows the mark; MVC treats every non-nullable value type as
        // implicitly required, which would put an asterisk on each number and date field.
        var required = For?.Metadata.ValidatorMetadata.OfType<System.ComponentModel.DataAnnotations.RequiredAttribute>().Any() == true;
        var errors = name is not null && ViewContext.ViewData.ModelState.TryGetValue(name, out var entry)
            ? entry.Errors.Select(e => e.ErrorMessage).Where(m => !string.IsNullOrEmpty(m)).ToList()
            : [];
        var label = Label ?? For?.Metadata.DisplayName ?? For?.Metadata.PropertyName;

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-form-item", errors.Count > 0 ? "ant-form-item-has-error" : null, errors.Count > 0 || Help is not null ? "ant-form-item-with-help" : null);

        var labelHtml = string.IsNullOrEmpty(label) ? "" :
            $"""<div class="ant-form-item-label"><label{(id is null ? "" : $" for=\"{id}\"")}{(required ? " class=\"ant-form-item-required\"" : "")}>{Html.E(label)}</label></div>""";
        var explain = errors.Count > 0
            ? $"""<div class="ant-form-item-explain ant-form-item-explain-connected" role="alert">{string.Concat(errors.Select(e => $"<div class=\"ant-form-item-explain-error\">{Html.E(e)}</div>"))}</div>"""
            : Help is null ? "" : $"""<div class="ant-form-item-explain"><div>{Html.E(Help)}</div></div>""";
        output.Content.SetHtmlContent(
            $"""<div class="ant-row ant-form-item-row{(Layout == "vertical" ? " ant-form-item-row-vertical" : "")}">{labelHtml}<div class="ant-col ant-form-item-control"><div class="ant-form-item-control-input"><div class="ant-form-item-control-input-content">{control}</div></div>{explain}</div></div>""");
    }
}

/// <summary>
/// Native &lt;select&gt; styled as an Ant Design select (keyboard- and form-friendly, no JS).
/// <code>&lt;app-select asp-for="Status" items="Html.GetEnumSelectList&lt;CustomerStatus&gt;()" placeholder="Any status" /&gt;</code>
/// </summary>
[HtmlTargetElement("app-select", TagStructure = TagStructure.WithoutEndTag)]
public class SelectTagHelper : TagHelper
{
    [HtmlAttributeName("asp-for")] public ModelExpression? For { get; set; }

    /// <summary>Name when not bound with asp-for.</summary>
    public string? Name { get; set; }

    public IEnumerable<SelectListItem> Items { get; set; } = [];

    /// <summary>Adds an empty first option with this text (an "any" choice for filters).</summary>
    public string? Placeholder { get; set; }

    /// <summary>Use the enum member name as the option value (model binding friendly).</summary>
    public bool EnumNames { get; set; }

    /// <summary>Several options can be chosen. A bound list (asp-for on a string collection) marks each chosen option.</summary>
    public bool Multiple { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var name = For?.Name ?? Name ?? "";
        var current = For?.Model?.ToString();
        var chosen = Multiple && For?.Model is System.Collections.IEnumerable list and not string
            ? list.Cast<object?>().Select(Html.Format).ToHashSet()
            : [];
        output.TagName = "select";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("name", name);
        output.Attributes.SetAttribute("id", TagBuilder.CreateSanitizedId(name, "_"));
        if (Multiple) output.Attributes.SetAttribute("multiple", "multiple");
        output.AddClasses("m-select");

        var html = new System.Text.StringBuilder();
        if (Placeholder is not null && !Multiple) html.Append($"""<option value="">{Html.E(Placeholder)}</option>""");
        foreach (var item in Items)
        {
            var value = EnumNames ? item.Text : item.Value;
            var selected = Multiple
                ? item.Selected || chosen.Contains(value) || chosen.Contains(item.Value)
                : item.Selected || (current is not null && (value == current || item.Value == current || item.Text == current));
            html.Append($"""<option value="{Html.E(value)}"{(selected ? " selected" : "")}>{Html.E(item.Text)}</option>""");
        }
        output.Content.SetHtmlContent(html.ToString());
    }
}

/// <summary>
/// Ant Design radio group over real radio inputs.
/// <code>&lt;app-radio-group asp-for="Tier" items="…" button-style="solid" /&gt;</code>
/// </summary>
[HtmlTargetElement("app-radio-group", TagStructure = TagStructure.WithoutEndTag)]
public class RadioGroupTagHelper : TagHelper
{
    [HtmlAttributeName("asp-for")] public ModelExpression For { get; set; } = default!;

    public IEnumerable<SelectListItem> Items { get; set; } = [];

    /// <summary>none (classic radios) | outline | solid (button style)</summary>
    public string ButtonStyle { get; set; } = "none";

    /// <summary>Stacks the choices one under another. The default lays them out in a row.</summary>
    public bool Vertical { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var name = For.Name;
        var current = For.Model?.ToString();
        var buttons = ButtonStyle != "none";
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-radio-group", "ant-radio-group-" + (ButtonStyle == "solid" ? "solid" : "outline"));
        if (Vertical) output.AddClasses("m-choice-vertical");
        output.Attributes.SetAttribute("role", "radiogroup");

        var html = new System.Text.StringBuilder();
        foreach (var item in Items)
        {
            var value = item.Text; // enum names bind back to enums
            var on = current == item.Text || current == item.Value;
            var check = on ? " checked" : "";
            if (buttons)
                html.Append($"""<label class="ant-radio-button-wrapper{(on ? " ant-radio-button-wrapper-checked" : "")}"><span class="ant-radio-button{(on ? " ant-radio-button-checked" : "")}"><input type="radio" class="ant-radio-button-input" name="{Html.E(name)}" value="{Html.E(value)}"{check} /><span class="ant-radio-button-inner"></span></span><span>{Html.E(item.Text)}</span></label>""");
            else
                html.Append($"""<label class="ant-radio-wrapper{(on ? " ant-radio-wrapper-checked" : "")}"><span class="ant-radio{(on ? " ant-radio-checked" : "")}"><input type="radio" class="ant-radio-input" name="{Html.E(name)}" value="{Html.E(value)}"{check} /><span class="ant-radio-inner"></span></span><span>{Html.E(item.Text)}</span></label>""");
        }
        output.Content.SetHtmlContent(html.ToString());
    }
}

/// <summary>
/// Ant Design checkbox over a real checkbox input. With <c>asp-for</c> it binds a bool
/// (posting "true" plus a hidden "false" like the built-in helper); otherwise set name/value.
/// </summary>
[HtmlTargetElement("app-checkbox")]
public class CheckboxTagHelper : TagHelper
{
    [HtmlAttributeName("asp-for")] public ModelExpression? For { get; set; }

    public string? Name { get; set; }
    public string? Value { get; set; }
    public bool Checked { get; set; }
    public bool Disabled { get; set; }

    /// <summary>Accessible label when there is no visible text.</summary>
    public string? AriaLabel { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var text = (await output.GetChildContentAsync()).GetContent();
        var name = For?.Name ?? Name ?? "";
        var on = For?.Model is true || Checked;
        var value = For is null ? Value ?? "true" : "true";
        var extra = string.Join(' ', output.Attributes.Where(a => a.Name.StartsWith("x-") || a.Name.StartsWith("@") || a.Name.StartsWith(":") || a.Name.StartsWith("data-"))
            .Select(a => $"{a.Name}=\"{Html.E(a.Value?.ToString())}\""));
        foreach (var a in output.Attributes.Where(a => a.Name.StartsWith("x-") || a.Name.StartsWith("@") || a.Name.StartsWith(":") || a.Name.StartsWith("data-")).ToList())
            output.Attributes.Remove(a);

        output.TagName = "label";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-checkbox-wrapper", on ? "ant-checkbox-wrapper-checked" : null, Disabled ? "ant-checkbox-wrapper-disabled" : null);
        var aria = AriaLabel is null ? "" : $""" aria-label="{Html.E(AriaLabel)}" """;
        var hidden = For is null ? "" : $"""<input type="hidden" name="{Html.E(name)}" value="false" />""";
        output.Content.SetHtmlContent(
            $"""<span class="ant-checkbox{(on ? " ant-checkbox-checked" : "")}{(Disabled ? " ant-checkbox-disabled" : "")}"><input type="checkbox" class="ant-checkbox-input" name="{Html.E(name)}" value="{Html.E(value)}"{(on ? " checked" : "")}{(Disabled ? " disabled" : "")}{aria} {extra} /><span class="ant-checkbox-inner"></span></span>""" +
            (string.IsNullOrWhiteSpace(text) ? "" : $"<span>{text}</span>") + hidden);
    }
}
