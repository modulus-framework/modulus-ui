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
/// Select whose options are a tree: <c>&lt;app-tree-select asp-for="Team" options="Model.Teams" placeholder="Select a team" /&gt;</c>.
/// <c>checkable</c> makes it a multi-select with checkboxes; the checked leaves post as repeated values.
/// </summary>
[HtmlTargetElement("app-tree-select", TagStructure = TagStructure.WithoutEndTag)]
public class TreeSelectTagHelper : BoundControlTagHelper
{
    public IReadOnlyList<TreeOption> Options { get; set; } = [];
    public bool Checkable { get; set; }
    public bool TreeDefaultExpandAll { get; set; }
    public string? Placeholder { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (name, id, _) = Bound();
        var values = Widgets.Values(For?.Model, Value);
        var tree = new TreeModel(Options, [], Checkable ? values : [], TreeDefaultExpandAll);
        if (!Checkable && values.FirstOrDefault() is { } v) tree.Selected = tree.Rows.FindIndex(r => r.Node.Value == v);
        if (!TreeDefaultExpandAll)
            foreach (var i in Checkable ? tree.Checked : tree.Selected >= 0 ? [tree.Selected] : [])
                for (var p = tree.Rows[i].Parent; p >= 0; p = tree.Rows[p].Parent) tree.Expanded.Add(p);
        var (cls, style) = TakeClassAndStyle(output);
        var passThrough = output.TakeAttributes();

        var select = new StringBuilder();
        var selectClass = Html.Cx("ant-select", "ant-tree-select", Checkable ? "ant-select-multiple" : "ant-select-single", "ant-select-show-arrow",
            SizeClass("ant-select"), StatusClass("ant-select"), Disabled ? "ant-select-disabled" : null);
        select.Append($$"""<div class="{{selectClass}}" :class="{ 'ant-select-open': open, 'ant-select-focused': open }" @click="toggleOpen()" @keydown.enter.prevent="toggleOpen()" @keydown.space.prevent="toggleOpen()" @keydown.escape="open = false">""");
        select.Append("""<div class="ant-select-selector">""");
        var comboAttrs = $"""role="combobox" aria-haspopup="tree" :aria-expanded="open"{Html.Attr("id", id.Length > 0 && !passThrough.Contains(" id=\"") ? id : null)}{Html.Attr("disabled", Disabled)}{passThrough}""";
        var hasValue = Checkable ? tree.Checked.Count > 0 : tree.Selected >= 0;
        if (Checkable)
        {
            select.Append("""<div class="ant-select-selection-overflow">""");
            foreach (var row in tree.Rows.Where(r => r.Node.IsLeaf))
                select.Append($"""<div class="ant-select-selection-overflow-item" x-show="isChecked({row.Index})"{(tree.Checked.Contains(row.Index) ? "" : " style=\"display: none;\"")}><span class="ant-select-selection-item" title="{Html.E(row.Node.Title)}"><span class="ant-select-selection-item-content">{Html.E(row.Node.Title)}</span><span class="ant-select-selection-item-remove" unselectable="on" aria-hidden="true" @click.stop="check({row.Index})">{AntIcons.Span("close")}</span></span></div>""");
            select.Append($"""<div class="ant-select-selection-overflow-item ant-select-selection-overflow-item-suffix"><div class="ant-select-selection-search"><input type="search" readonly class="ant-select-selection-search-input" autocomplete="off" {comboAttrs} /></div></div></div>""");
        }
        else
        {
            select.Append($"""<span class="ant-select-selection-search"><input type="search" readonly unselectable="on" class="ant-select-selection-search-input" autocomplete="off" style="opacity: 0;" {comboAttrs} /></span>""");
            var label = tree.Selected >= 0 ? tree.Rows[tree.Selected].Node.Title : "";
            select.Append($"""<span class="ant-select-selection-item" title="{Html.E(label)}" x-show="selectedValue() !== ''" x-text="selectedTitle()" :title="selectedTitle()"{(hasValue ? "" : " style=\"display: none;\"")}>{Html.E(label)}</span>""");
        }
        if (Placeholder is not null)
            select.Append($"""<span class="ant-select-selection-placeholder" x-show="!hasValue()"{(hasValue ? " style=\"display: none;\"" : "")}>{Html.E(Placeholder)}</span>""");
        select.Append("</div>").Append(Widgets.Arrow()).Append("</div>");

        var dropdown = $"""<div class="ant-select-dropdown ant-select-dropdown-placement-bottomLeft ant-tree-select-dropdown m-popup" x-show="open" x-transition.opacity x-cloak><div class="m-popup-scroll"><div class="ant-select-tree ant-tree ant-tree-block-node" role="tree"><div class="ant-tree-list"><div class="ant-tree-list-holder-inner">{tree.Render(Checkable, true)}</div></div></div></div></div>""";

        var inputs = new StringBuilder();
        if (name.Length > 0 && Checkable)
            foreach (var row in tree.Rows.Where(r => r.Node.IsLeaf))
                inputs.Append($"""<input type="hidden" name="{Html.E(name)}" value="{Html.E(row.Node.Value)}"{(tree.Checked.Contains(row.Index) ? "" : " disabled")} :disabled="!isChecked({row.Index})" />""");
        else if (name.Length > 0)
            inputs.Append($"""<input type="hidden" name="{Html.E(name)}" value="{Html.E(tree.Selected >= 0 ? tree.Rows[tree.Selected].Node.Value : "")}" :value="selectedValue()" />""");

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", Html.Cx("m-select-wrap", cls));
        if (style is not null) output.Attributes.SetAttribute("style", style);
        output.Attributes.SetAttribute("x-data", new HtmlString($"modulusTreeSelect({Widgets.Config(new { nodes = tree.ClientNodes(), expanded = tree.Expanded, @checked = tree.Checked, selected = tree.Selected, checkable = Checkable, selectable = true, disabled = Disabled })})"));
        output.Attributes.SetAttribute("@click.outside", "open = false");
        output.Content.SetHtmlContent(select + dropdown + inputs);
    }
}
