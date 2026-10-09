using System.Globalization;
using System.Net;
using System.Text;
using Modulus.Ui.Abstractions.Shell;

namespace Modulus.Ui.Abstractions.Query;

/// <summary>
/// The builder's markup: a GET form (so the filter lives in the URL, like the tables' state) with one row per rule, a
/// group per nested join, and the buttons that add, remove and clear. A rule after the first shows the join ("and" or "or")
/// in its own column, so the match reads down the rows. The MVC tag helper emits it; the Blazor component draws the same
/// classes and names and runs the same <see cref="QueryBuilderForm.Edit"/>.
/// </summary>
public static class QueryBuilderHtml
{
    /// <summary>Renders the filter as a form. <paramref name="action"/> is the page the form submits to (empty: this page).</summary>
    public static string Render(IReadOnlyList<QueryField> schema, FilterGroup model, string name = QueryBuilderForm.DefaultName, string? action = null)
    {
        var sb = new StringBuilder();
        sb.Append($"""<form class="m-qb" method="get"{(string.IsNullOrEmpty(action) ? "" : $" action=\"{E(action)}\"")} data-m-query-builder>""");
        // The first submit button is the one Enter presses in a field; it must apply the filter, not a row button.
        sb.Append("""<button type="submit" class="m-qb-default" hidden tabindex="-1" aria-hidden="true"></button>""");
        Group(sb, schema, model, "", 0, name, root: true);
        sb.Append($"""<div class="m-qb-footer"><button type="submit" class="ant-btn ant-btn-primary">Apply filters</button><button type="submit" class="ant-btn ant-btn-text" name="{E(name)}.op" value="clear">Clear</button></div>""");
        return sb.Append("</form>").ToString();
    }

    private static void Group(StringBuilder sb, IReadOnlyList<QueryField> schema, FilterGroup group, string path, int depth, string name, bool root)
    {
        var joinName = root ? $"{name}.join" : $"{name}.{path}.join";
        sb.Append($"""<div class="m-qb-group" data-depth="{depth.ToString(CultureInfo.InvariantCulture)}">""");
        sb.Append("""<div class="m-qb-head">""");
        if (!root)
            sb.Append($"""<input type="hidden" name="{E(name)}.{E(path)}.kind" value="group" />""");
        sb.Append("""<span class="m-qb-label">Match</span>""");
        sb.Append($"""<select class="m-select m-select-sm m-qb-join" name="{E(joinName)}" aria-label="Match all or any of these">{JoinOptions(group.Join)}</select>""");
        sb.Append("""<span class="m-qb-label">of the following</span>""");
        if (!root)
            sb.Append($"""<button type="submit" class="ant-btn ant-btn-text ant-btn-icon-only m-qb-remove" name="{E(name)}.op" value="remove:{E(path)}" aria-label="Remove group" title="Remove group">{ShellRenderer.Icon("close")}</button>""");
        sb.Append("</div>");

        sb.Append("""<div class="m-qb-items">""");
        if (group.Items.Count == 0)
            sb.Append($"""<p class="m-qb-empty">{(root ? "No conditions yet. Add a rule to narrow the list." : "This group has no conditions yet.")}</p>""");
        var connector = group.Join == FilterJoin.And ? "and" : "or";
        for (var i = 0; i < group.Items.Count; i++)
        {
            var child = path.Length == 0 ? i.ToString(CultureInfo.InvariantCulture) : $"{path}.{i.ToString(CultureInfo.InvariantCulture)}";
            if (group.Items[i] is FilterGroup sub) Group(sb, schema, sub, child, depth + 1, name, root: false);
            else if (group.Items[i] is FilterRule rule) Rule(sb, schema, rule, child, name, i == 0 ? "" : connector);
        }
        sb.Append("</div>");

        sb.Append("""<div class="m-qb-actions">""");
        sb.Append($"""<button type="submit" class="ant-btn ant-btn-dashed ant-btn-sm m-qb-add" name="{E(name)}.op" value="add-rule:{E(path)}">{ShellRenderer.Icon("plus")}<span>Add rule</span></button>""");
        if (depth + 1 < FilterLimits.MaxDepth)
            sb.Append($"""<button type="submit" class="ant-btn ant-btn-dashed ant-btn-sm m-qb-add" name="{E(name)}.op" value="add-group:{E(path)}">{ShellRenderer.Icon("plus")}<span>Add group</span></button>""");
        sb.Append("</div></div>");
    }

    private static void Rule(StringBuilder sb, IReadOnlyList<QueryField> schema, FilterRule rule, string path, string name, string connector)
    {
        var field = schema.FirstOrDefault(f => f.Key == rule.Field);
        var kind = field?.Kind ?? QueryFieldKind.Text;
        var ops = QueryFieldOperators.For(kind);
        var prefix = $"{E(name)}.{E(path)}";
        sb.Append("""<div class="m-qb-rule">""");
        sb.Append($"""<span class="m-qb-connector" aria-hidden="true">{E(connector)}</span>""");
        sb.Append($"""<input type="hidden" name="{prefix}.kind" value="rule" />""");

        // A new field has its own operators, so the field and the operator submit at once to redraw the rule.
        sb.Append($"""<select class="m-select m-qb-field" name="{prefix}.field" aria-label="Field" onchange="this.form.requestSubmit()">""");
        if (field is null) sb.Append("""<option value="" selected>Choose a field</option>""");
        foreach (var f in schema) sb.Append($"""<option value="{E(f.Key)}"{Selected(f.Key == rule.Field)}>{E(f.Title)}</option>""");
        sb.Append("</select>");

        sb.Append($"""<select class="m-select m-qb-op" name="{prefix}.op" aria-label="Operator" onchange="this.form.requestSubmit()">""");
        foreach (var op in ops) sb.Append($"""<option value="{op}"{Selected(op == rule.Operator)}>{E(QueryFieldOperators.Label(op))}</option>""");
        sb.Append("</select>");

        var hidden = QueryFieldOperators.NeedsValue(rule.Operator) ? "" : " hidden";
        sb.Append($"""<span class="m-qb-value"{hidden}>""");
        if (kind == QueryFieldKind.Choice)
        {
            // A dropdown of checkboxes (the shell opens and closes it like the other menus); each checked box posts its value.
            var picked = (rule.Value ?? "").Split(ColumnSearch.ValueSeparator, StringSplitOptions.RemoveEmptyEntries);
            var chosen = field!.Options.Where(o => picked.Contains(o.Value)).Select(o => o.Label).ToList();
            var summary = chosen.Count == 0 ? "Choose values" : string.Join(", ", chosen);
            sb.Append("""<details class="m-dropdown m-qb-multi">""");
            sb.Append($"""<summary class="ant-btn ant-btn-default m-dropdown-trigger m-qb-multi-trigger" aria-label="Values">""");
            sb.Append($"""<span class="m-qb-multi-text{(chosen.Count == 0 ? " m-qb-multi-empty" : "")}">{E(summary)}</span>""");
            if (chosen.Count > 0) sb.Append($"""<span class="m-qb-multi-count">{chosen.Count.ToString(CultureInfo.InvariantCulture)}</span>""");
            sb.Append($"""<span class="m-qb-multi-chevron" aria-hidden="true">{ShellRenderer.Icon("down")}</span></summary>""");
            sb.Append("""<div class="ant-dropdown m-dropdown-panel m-dropdown-left m-qb-multi-panel" role="group" aria-label="Values">""");
            foreach (var o in field.Options)
            {
                var on = picked.Contains(o.Value);
                sb.Append($"""<label class="ant-checkbox-wrapper{(on ? " ant-checkbox-wrapper-checked" : "")} m-qb-multi-option"><span class="ant-checkbox{(on ? " ant-checkbox-checked" : "")}"><input type="checkbox" class="ant-checkbox-input" name="{prefix}.value" value="{E(o.Value)}"{(on ? " checked" : "")} /><span class="ant-checkbox-inner"></span></span><span>{E(o.Label)}</span></label>""");
            }
            sb.Append("</div></details>");
        }
        else
        {
            sb.Append(Input(kind, $"{prefix}.value", rule.Value, "Value"));
            var between = QueryFieldOperators.NeedsTo(rule.Operator) ? "" : " hidden";
            sb.Append($"""<span class="m-qb-to"{between}><span class="m-qb-label">and</span>{Input(kind, $"{prefix}.to", rule.To, "Up to")}</span>""");
        }
        sb.Append("</span>");

        sb.Append($"""<button type="submit" class="ant-btn ant-btn-text ant-btn-icon-only m-qb-remove" name="{E(name)}.op" value="remove:{E(path)}" aria-label="Remove rule" title="Remove rule">{ShellRenderer.Icon("close")}</button>""");
        sb.Append("</div>");
    }

    private static string Input(QueryFieldKind kind, string name, string? value, string label) => kind switch
    {
        QueryFieldKind.Date => $"""<input class="ant-input" type="date" name="{name}" value="{E(value)}" aria-label="{E(label)}" />""",
        QueryFieldKind.Number => $"""<input class="ant-input" type="text" inputmode="decimal" autocomplete="off" name="{name}" value="{E(value)}" aria-label="{E(label)}" />""",
        _ => $"""<input class="ant-input" type="text" autocomplete="off" name="{name}" value="{E(value)}" aria-label="{E(label)}" />""",
    };

    private static string JoinOptions(FilterJoin join) =>
        $"""<option value="And"{Selected(join == FilterJoin.And)}>all</option><option value="Or"{Selected(join == FilterJoin.Or)}>any</option>""";

    private static string Selected(bool on) => on ? " selected" : "";

    private static string E(string? text) => WebUtility.HtmlEncode(text ?? "");
}
