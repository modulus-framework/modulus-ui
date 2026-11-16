using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.Json;
using AntDesign;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

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
