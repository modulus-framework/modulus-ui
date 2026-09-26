using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.Json;
using AntDesign;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

// Hierarchical and composite widgets: Cascader, TreeSelect, Tree, Transfer, Calendar, Carousel and Anchor.
// Each renders Ant Design's DOM on the server (so the first paint matches the Blazor component) and an
// Alpine component in modulus-widgets.js takes over the interaction. Form widgets post plain inputs:
// hidden inputs that are disabled while unselected, so a form posts the current selection without script too.

internal static class Widgets
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>JSON for an Alpine <c>x-data</c> argument, encoded for an attribute value.</summary>
    public static string Config(object value) => Html.E(JsonSerializer.Serialize(value, Json));

    public static string Arrow() =>
        $"""<span class="ant-select-arrow" unselectable="on" aria-hidden="true" style="user-select: none;">{AntIcons.Span("down", IconThemeType.Outline, "ant-select-suffix")}</span>""";

    /// <summary>Values of a bound model: one string, or the items of a sequence.</summary>
    public static List<string> Values(object? model, string? value) => model switch
    {
        string s => [s],
        IEnumerable items => items.Cast<object?>().Where(i => i is not null).Select(i => Html.Format(i)).ToList(),
        null => value is null ? [] : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
        _ => [Html.Format(model)],
    };
}

/// <summary>Server-side state and markup for a list of tree nodes (Tree and TreeSelect share it).</summary>
internal sealed class TreeModel
{
    public sealed record Row(int Index, TreeOption Node, int Parent, int Depth, bool Last);

    public List<Row> Rows { get; } = [];
    public HashSet<int> Expanded { get; } = [];
    public HashSet<int> Checked { get; } = [];
    public int Selected { get; set; } = -1;

    public TreeModel(IReadOnlyList<TreeOption> roots, IEnumerable<string> expanded, IEnumerable<string> @checked, bool expandAll)
    {
        Add(roots, -1, 0);
        var byValue = Rows.GroupBy(r => r.Node.Value).ToDictionary(g => g.Key, g => g.First().Index);
        foreach (var v in expanded)
            if (byValue.TryGetValue(v, out var i)) Expanded.Add(i);
        if (expandAll) Expanded.UnionWith(Rows.Where(r => !r.Node.IsLeaf).Select(r => r.Index));
        foreach (var v in @checked)
        {
            if (!byValue.TryGetValue(v, out var i)) continue;
            if (Rows[i].Node.IsLeaf) Checked.Add(i);
            else Checked.UnionWith(EnabledLeaves(i));
        }
    }

    private void Add(IReadOnlyList<TreeOption> nodes, int parent, int depth)
    {
        for (var n = 0; n < nodes.Count; n++)
        {
            var index = Rows.Count;
            Rows.Add(new Row(index, nodes[n], parent, depth, n == nodes.Count - 1));
            if (nodes[n].Children is { } children) Add(children, index, depth + 1);
        }
    }

    public IEnumerable<int> Children(int index) => Rows.Where(r => r.Parent == index).Select(r => r.Index);

    public IEnumerable<int> EnabledLeaves(int index)
    {
        var row = Rows[index];
        if (row.Node.Disabled) return [];
        return row.Node.IsLeaf ? [index] : Children(index).SelectMany(EnabledLeaves);
    }

    /// <summary>"checked", "indeterminate" or "" (a parent is checked when all its enabled leaves are).</summary>
    public string State(int index)
    {
        if (Rows[index].Node.IsLeaf) return Checked.Contains(index) ? "checked" : "";
        var leaves = EnabledLeaves(index).ToList();
        var on = leaves.Count(Checked.Contains);
        return leaves.Count > 0 && on == leaves.Count ? "checked" : on > 0 ? "indeterminate" : "";
    }

    public bool Shown(int index)
    {
        for (var p = Rows[index].Parent; p >= 0; p = Rows[p].Parent)
            if (!Expanded.Contains(p)) return false;
        return true;
    }

    /// <summary>Config for the Alpine tree state: nodes by index (value, title, parent, leaf, disabled).</summary>
    public object ClientNodes() => Rows.Select(r => new { v = r.Node.Value, t = r.Node.Title, p = r.Parent, l = r.Node.IsLeaf, d = r.Node.Disabled });

    public string Render(bool checkable, bool selectable)
    {
        var sb = new StringBuilder();
        foreach (var row in Rows)
        {
            var i = row.Index;
            var leaf = row.Node.IsLeaf;
            var open = Expanded.Contains(i);
            var state = checkable ? State(i) : "";
            var selected = Selected == i;
            var nodeClass = Html.Cx("ant-tree-treenode",
                leaf ? null : open ? "ant-tree-treenode-switcher-open" : "ant-tree-treenode-switcher-close",
                state.Length > 0 ? $"ant-tree-treenode-checkbox-{state}" : null,
                selected ? "ant-tree-treenode-selected" : null,
                row.Node.Disabled ? "ant-tree-treenode-disabled" : null,
                row.Last ? "ant-tree-treenode-leaf-last" : null);
            sb.Append($"""<div class="{nodeClass}" :class="nodeClass({i})" x-show="shown({i})" role="treeitem" aria-level="{row.Depth + 1}"{(leaf ? "" : $" :aria-expanded=\"isOpen({i})\"")}{(Shown(i) ? "" : " style=\"display: none;\"")}>""");
            sb.Append("""<span class="ant-tree-indent" aria-hidden="true">""");
            for (var d = 0; d < row.Depth; d++) sb.Append("""<span class="ant-tree-indent-unit ant-tree-indent-unit-start"></span>""");
            sb.Append("</span>");
            sb.Append(leaf
                ? """<span class="ant-tree-switcher ant-tree-switcher-noop"></span>"""
                : $"""<span class="ant-tree-switcher ant-tree-switcher_{(open ? "open" : "close")}" :class="switcherClass({i})" @click.stop="toggle({i})">{AntIcons.Span("caret-down", IconThemeType.Fill, "ant-tree-switcher-icon")}</span>""");
            if (checkable)
                sb.Append($"""<span class="{Html.Cx("ant-tree-checkbox", state.Length > 0 ? $"ant-tree-checkbox-{state}" : null, row.Node.Disabled ? "ant-tree-checkbox-disabled" : null)}" :class="checkboxClass({i})" role="checkbox" :aria-checked="ariaChecked({i})"{(row.Node.Disabled ? " aria-disabled=\"true\"" : "")} @click.stop="check({i})"><span class="ant-tree-checkbox-inner"></span></span>""");
            var content = Html.Cx("ant-tree-node-content-wrapper", leaf ? null : open ? "ant-tree-node-content-wrapper-open" : "ant-tree-node-content-wrapper-close", selected ? "ant-tree-node-selected" : null);
            sb.Append($"""<span class="{content}" :class="contentClass({i})" title="{Html.E(row.Node.Title)}" @click="clickNode({i})"><span class="ant-tree-title">{Html.E(row.Node.Title)}</span></span>""");
            sb.Append("</div>");
        }
        return sb.ToString();
    }
}

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

/// <summary>
/// Month calendar (Ant Design's Calendar): <c>&lt;app-calendar value="Model.Day" /&gt;</c>, or
/// <c>fullscreen</c> for a page-sized one. Year and month selects and the Month / Year switch work in the
/// browser; with <c>name</c> (or <c>asp-for</c>) the selected day posts as yyyy-MM-dd.
/// Day and month names follow the request culture.
/// </summary>
[HtmlTargetElement("app-calendar", TagStructure = TagStructure.WithoutEndTag)]
public class CalendarTagHelper : BoundControlTagHelper
{
    /// <summary>The selected day (defaults to the bound value, then today).</summary>
    public DateTime? Date { get; set; }

    public bool Fullscreen { get; set; }

    /// <summary>month | year</summary>
    public string Mode { get; set; } = "month";

    public string MonthLabel { get; set; } = "Month";
    public string YearLabel { get; set; } = "Year";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (name, _, _) = Bound();
        var culture = CultureInfo.CurrentCulture;
        var selected = (Date ?? For?.Model switch { DateTime d => (DateTime?)d, DateOnly d => d.ToDateTime(TimeOnly.MinValue), _ => null }
            ?? (DateTime.TryParse(Value, CultureInfo.InvariantCulture, out var parsed) ? parsed : DateTime.Today)).Date;
        var firstDay = (int)culture.DateTimeFormat.FirstDayOfWeek;
        var days = Enumerable.Range(0, 7).Select(i => ShortDay(culture, (i + firstDay) % 7)).ToArray();
        var months = culture.DateTimeFormat.AbbreviatedMonthNames.Take(12).ToArray();
        var year = Mode == "year";
        var group = $"m-cal-{context.UniqueId}";

        var years = string.Concat(Enumerable.Range(selected.Year - 10, 21).Select(y => $"""<option value="{y}"{(y == selected.Year ? " selected" : "")}>{y}</option>"""));
        var monthOptions = string.Concat(months.Select((m, i) => $"""<option value="{i}"{(i == selected.Month - 1 ? " selected" : "")}>{Html.E(m)}</option>"""));
        var header =
            """<div class="ant-picker-calendar-header">""" +
            $"""<select class="m-select m-select-sm ant-picker-calendar-year-select" aria-label="Year" x-model.number="year">{years}</select>""" +
            $"""<select class="m-select m-select-sm ant-picker-calendar-month-select" aria-label="Month" x-model.number="month" x-show="mode === 'month'"{(year ? " style=\"display: none;\"" : "")}>{monthOptions}</select>""" +
            """<div class="ant-picker-calendar-mode-switch ant-radio-group ant-radio-group-small">""" +
            ModeButton(group, "month", MonthLabel, !year) + ModeButton(group, "year", YearLabel, year) +
            "</div></div>";

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-picker-calendar", Fullscreen ? "ant-picker-calendar-full" : null);
        output.Attributes.SetAttribute("x-data", new HtmlString($"modulusCalendar({Widgets.Config(new { selected = selected.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), mode = year ? "year" : "month", firstDay, days, months, today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) })})"));
        output.Content.SetHtmlContent(
            header +
            $"""<div class="ant-picker-panel" x-html="panel()" @click="pick($event)">{(year ? YearPanel(selected, months) : MonthPanel(selected, firstDay, days))}</div>""" +
            (name.Length > 0 ? $"""<input type="hidden" name="{Html.E(name)}" value="{selected:yyyy-MM-dd}" :value="selected" />""" : ""));
    }

    private static string ModeButton(string group, string value, string label, bool on) =>
        $$"""<label class="{{Html.Cx("ant-radio-button-wrapper", on ? "ant-radio-button-wrapper-checked" : null)}}" :class="{ 'ant-radio-button-wrapper-checked': mode === '{{value}}' }"><span class="{{Html.Cx("ant-radio-button", on ? "ant-radio-button-checked" : null)}}" :class="{ 'ant-radio-button-checked': mode === '{{value}}' }"><input type="radio" class="ant-radio-button-input" name="{{group}}" value="{{value}}"{{(on ? " checked" : "")}} x-model="mode" /><span class="ant-radio-button-inner"></span></span><span>{{Html.E(label)}}</span></label>""";

    /// <summary>Two-letter weekday names, as AntD shows them ("Su", "Mo"; "So", "Mo"; "di", "lu").</summary>
    internal static string ShortDay(CultureInfo culture, int day)
    {
        var name = culture.DateTimeFormat.AbbreviatedDayNames[day].TrimEnd('.');
        return name.Length > 2 ? name[..2] : name;
    }

    // The same markup as modulusCalendar.panel() in modulus-widgets.js.
    internal static string MonthPanel(DateTime selected, int firstDay, string[] days)
    {
        var first = new DateTime(selected.Year, selected.Month, 1);
        var start = first.AddDays(-(((int)first.DayOfWeek - firstDay + 7) % 7));
        var sb = new StringBuilder("""<div class="ant-picker-date-panel"><div class="ant-picker-body"><table class="ant-picker-content"><thead><tr>""");
        foreach (var d in days) sb.Append($"<th>{Html.E(d)}</th>");
        sb.Append("</tr></thead><tbody>");
        for (var w = 0; w < 6; w++)
        {
            sb.Append("<tr>");
            for (var i = 0; i < 7; i++)
            {
                var day = start.AddDays(w * 7 + i);
                var cls = Html.Cx("ant-picker-cell", day.Month == selected.Month ? "ant-picker-cell-in-view" : null,
                    day == DateTime.Today ? "ant-picker-cell-today" : null, day == selected ? "ant-picker-cell-selected" : null);
                sb.Append($"""<td title="{day:yyyy-MM-dd}" class="{cls}"><div class="ant-picker-cell-inner ant-picker-calendar-date"><div class="ant-picker-calendar-date-value">{day.Day}</div><div class="ant-picker-calendar-date-content"></div></div></td>""");
            }
            sb.Append("</tr>");
        }
        return sb.Append("</tbody></table></div></div>").ToString();
    }

    internal static string YearPanel(DateTime selected, string[] months)
    {
        var sb = new StringBuilder("""<div class="ant-picker-month-panel"><div class="ant-picker-body"><table class="ant-picker-content"><tbody>""");
        for (var r = 0; r < 4; r++)
        {
            sb.Append("<tr>");
            for (var c = 0; c < 3; c++)
            {
                var m = r * 3 + c;
                var today = DateTime.Today.Year == selected.Year && DateTime.Today.Month == m + 1;
                var cls = Html.Cx("ant-picker-cell", "ant-picker-cell-in-view", today ? "ant-picker-cell-today" : null, m == selected.Month - 1 ? "ant-picker-cell-selected" : null);
                sb.Append($"""<td title="{selected.Year:D4}-{m + 1:D2}" class="{cls}"><div class="ant-picker-cell-inner ant-picker-calendar-date"><div class="ant-picker-calendar-date-value">{Html.E(months[m])}</div><div class="ant-picker-calendar-date-content"></div></div></td>""");
            }
            sb.Append("</tr>");
        }
        return sb.Append("</tbody></table></div></div>").ToString();
    }
}

/// <summary>
/// Carousel of <c>&lt;carousel-slide&gt;</c> children with dots, arrow keys and autoplay (paused on hover):
/// <code>&lt;app-carousel autoplay="5000"&gt;&lt;carousel-slide&gt;…&lt;/carousel-slide&gt;&lt;/app-carousel&gt;</code>
/// </summary>
[HtmlTargetElement("app-carousel")]
public class CarouselTagHelper : TagHelper
{
    /// <summary>Milliseconds between slides; 0 turns autoplay off.</summary>
    public int Autoplay { get; set; }

    public bool Dots { get; set; } = true;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var slides = new List<string>();
        context.Items[typeof(CarouselTagHelper)] = slides;
        await output.GetChildContentAsync();
        var n = Math.Max(1, slides.Count);
        var sb = new StringBuilder($"""<div class="slick-slider slick-initialized" dir="ltr"><div class="slick-list"><div class="slick-track" style="width: {n * 100}%; transform: translate3d(0%, 0px, 0px);" :style="trackStyle()">""");
        for (var i = 0; i < slides.Count; i++)
            sb.Append($$"""<div data-index="{{i}}" class="{{Html.Cx("slick-slide", i == 0 ? "slick-active slick-current" : null)}}" :class="{ 'slick-active slick-current': index === {{i}} }" tabindex="-1" aria-hidden="{{(i == 0 ? "false" : "true")}}" :aria-hidden="index !== {{i}}" style="outline: none; width: {{100.0 / n:0.####}}%;"><div><div tabindex="-1" style="width: 100%; display: inline-block;">{{slides[i]}}</div></div></div>""");
        sb.Append("</div></div>");
        if (Dots && slides.Count > 1)
        {
            sb.Append("""<ul class="slick-dots slick-dots-bottom" style="display: block;">""");
            for (var i = 0; i < slides.Count; i++)
                sb.Append($$"""<li class="{{(i == 0 ? "slick-active" : "")}}" :class="{ 'slick-active': index === {{i}} }"><button type="button" aria-label="Slide {{i + 1}}" @click="go({{i}})">{{i + 1}}</button></li>""");
            sb.Append("</ul>");
        }
        sb.Append("</div>");
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-carousel");
        output.Attributes.SetAttribute("role", "region");
        output.Attributes.SetAttribute("aria-roledescription", "carousel");
        output.Attributes.SetAttribute("x-data", new HtmlString($"modulusCarousel({slides.Count}, {Autoplay})"));
        output.Attributes.SetAttribute("@mouseenter", "pause = true");
        output.Attributes.SetAttribute("@mouseleave", "pause = false");
        output.Attributes.SetAttribute("@keydown.left", "go(index - 1)");
        output.Attributes.SetAttribute("@keydown.right", "go(index + 1)");
        output.Content.SetHtmlContent(sb.ToString());
    }
}

/// <summary>One slide of an <c>app-carousel</c>.</summary>
[HtmlTargetElement("carousel-slide", ParentTag = "app-carousel")]
public class CarouselSlideTagHelper : TagHelper
{
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (context.Items.TryGetValue(typeof(CarouselTagHelper), out var slides))
            ((List<string>)slides).Add((await output.GetChildContentAsync()).GetContent());
        output.SuppressOutput();
    }
}

/// <summary>
/// In-page navigation that highlights the section in view (scroll-spy):
/// <code>&lt;app-anchor&gt;&lt;app-anchor-link href="#general" title="General" /&gt;…&lt;/app-anchor&gt;</code>
/// Links can nest. <c>offset-top</c> is the distance from the viewport top at which a section counts as current.
/// </summary>
[HtmlTargetElement("app-anchor")]
public class AnchorTagHelper : TagHelper
{
    public int OffsetTop { get; set; } = 128;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var links = (await output.GetChildContentAsync()).GetContent();
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-anchor-wrapper");
        output.AddStyle("max-height: 100vh;");
        output.Attributes.SetAttribute("x-data", new HtmlString($"modulusAnchor({OffsetTop})"));
        output.Content.SetHtmlContent($$"""<div class="ant-anchor"><div class="ant-anchor-ink"><span class="ant-anchor-ink-ball" :class="{ visible: active }" :style="ball"></span></div>{{links}}</div>""");
    }
}

/// <summary>A link of an <c>app-anchor</c>; child links nest under it.</summary>
[HtmlTargetElement("app-anchor-link")]
public class AnchorLinkTagHelper : TagHelper
{
    public string Href { get; set; } = "";
    public string Title { get; set; } = "";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var children = (await output.GetChildContentAsync()).GetContent();
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-anchor-link");
        output.Attributes.SetAttribute(":class", new HtmlString("{ 'ant-anchor-link-active': active === $el.dataset.href }"));
        output.Attributes.SetAttribute("data-href", Href);
        output.Content.SetHtmlContent($$"""<a class="ant-anchor-link-title" :class="{ 'ant-anchor-link-title-active': active === $el.parentElement.dataset.href }" href="{{Html.E(Href)}}" title="{{Html.E(Title)}}">{{Html.E(Title)}}</a>{{children}}""");
    }
}
