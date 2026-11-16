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
/// Tree with expand/collapse, selection and checkboxes (half-checked parents), like Ant Design's Tree:
/// <code>&lt;app-tree options="Model.Files" checkable name="files" expanded="…" checked="…" /&gt;</code>
/// With <c>checkable</c> and a <c>name</c> (or <c>asp-for</c>), the checked leaves post as <c>name=value</c> pairs;
/// without <c>checkable</c>, the selected node's value posts.
/// </summary>
[HtmlTargetElement("app-tree", TagStructure = TagStructure.WithoutEndTag)]
public class TreeTagHelper : BoundControlTagHelper
{
    public IReadOnlyList<TreeOption> Options { get; set; } = [];
    public bool Checkable { get; set; }
    public bool Selectable { get; set; } = true;
    public bool BlockNode { get; set; }
    public bool DefaultExpandAll { get; set; }

    /// <summary>Values of the nodes expanded initially.</summary>
    public IEnumerable<string>? Expanded { get; set; }

    /// <summary>Checked values (a parent checks its leaves); defaults to the bound value.</summary>
    public IEnumerable<string>? Checked { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (name, _, _) = Bound();
        var values = Checked?.ToList() ?? Widgets.Values(For?.Model, Value);
        var tree = new TreeModel(Options, Expanded ?? [], Checkable ? values : [], DefaultExpandAll);
        if (!Checkable && values.FirstOrDefault() is { } v) tree.Selected = tree.Rows.FindIndex(r => r.Node.Value == v);
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-tree", BlockNode ? "ant-tree-block-node" : null);
        output.Attributes.SetAttribute("role", "tree");
        output.Attributes.SetAttribute("x-data", new HtmlString($"modulusTree({Widgets.Config(Config(tree))})"));
        var inputs = new StringBuilder();
        if (name.Length > 0 && Checkable)
            foreach (var row in tree.Rows.Where(r => r.Node.IsLeaf))
                inputs.Append($"""<input type="hidden" name="{Html.E(name)}" value="{Html.E(row.Node.Value)}"{(tree.Checked.Contains(row.Index) ? "" : " disabled")} :disabled="!isChecked({row.Index})" />""");
        else if (name.Length > 0)
            inputs.Append($"""<input type="hidden" name="{Html.E(name)}" value="{Html.E(tree.Selected >= 0 ? tree.Rows[tree.Selected].Node.Value : "")}" :value="selectedValue()" />""");
        output.Content.SetHtmlContent($"""<div class="ant-tree-list"><div class="ant-tree-list-holder-inner">{tree.Render(Checkable, Selectable)}</div></div>{inputs}""");
    }

    private object Config(TreeModel tree) => new
    {
        nodes = tree.ClientNodes(),
        expanded = tree.Expanded,
        @checked = tree.Checked,
        selected = tree.Selected,
        checkable = Checkable,
        selectable = Selectable && !Disabled,
    };
}
