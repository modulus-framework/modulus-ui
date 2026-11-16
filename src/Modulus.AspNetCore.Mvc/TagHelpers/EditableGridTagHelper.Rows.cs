using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using AntDesign;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Plugins;
using SortDirection = Modulus.Ui.Abstractions.SortDirection;
using Modulus.Ui.Abstractions.Shell;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

public partial class EditableGridTagHelper : TagHelper
{
    // ---- Rows and cells ----

    private string Row(GridRecord row, IReadOnlyList<GridColumn> visible, IReadOnlyDictionary<string, int> offsets,
        Dictionary<(string, string), string> errors, bool detail, int colspan, CultureInfo culture, bool newRow, string gridId)
    {
        var n = Name;
        var hasLeft = visible.Any(c => c.Fixed == GridFixed.Left);
        // A read-only or locked row says why in its tooltip; a row error (a rule over several cells) tints it red.
        var rowError = errors.GetValueOrDefault((row.Id, ""));
        var stateText = GridSchema.StateText(row);
        var locked = row.State == GridRowState.Locked;
        var state = row.Editable ? "" : $""" data-m-row-state="{(locked ? "locked" : "readonly")}" title="{Html.E(stateText)}" """.TrimEnd();
        // The batch actions that act on this row (a new row isn't saved, so none do).
        if (Batch is { Count: > 0 } && !newRow) state += $""" data-m-batch="{Html.E(GridSchema.BatchKeys(Batch, row))}" """.TrimEnd();
        var sb = new StringBuilder($"""<tr data-row-key="{Html.E(row.Id)}" class="{Html.Cx("ant-table-row", "ant-table-row-level-0", newRow ? "m-grid-row-new" : null, GridSchema.StateClass(row), rowError is null ? null : "m-grid-row-error")}"{state}>""");
        // Hidden columns still post their values, so hiding a column and saving keeps its data.
        var index = $"""<input type="hidden" name="{Html.E(n)}.index" value="{Html.E(row.Id)}" />""" +
                    string.Concat(Columns.Where(c => c.Hidden).Select(c =>
                        $"""<input type="hidden" name="{Html.E(n)}[{Html.E(row.Id)}].{Html.E(c.Key)}" value="{Html.E(row[c.Key] ?? "")}" />"""));
        if (RowDraggable)
        {
            var label = visible.Count > 0 && GridSchema.Format(visible[0], row[visible[0].Key], culture) is { Length: > 0 } text ? text : "row";
            sb.Append($"""<td class="{Html.Cx("ant-table-cell", "m-row-drag-cell", hasLeft ? "ant-table-cell-fix-left" : null)}"{(hasLeft ? " style=\"position: sticky; left: 0px;\"" : "")}>{index}{SortableHtml.RowHandle(row.Id, label, HelpId(gridId), ServerSorted || locked, locked ? stateText : null)}</td>""");
            index = "";
        }
        if (Selectable)
        {
            // The checkbox has no name: selection lives in the browser and is not posted with the grid.
            sb.Append($"""<td class="{Html.Cx("ant-table-cell", "ant-table-selection-column", hasLeft ? "ant-table-cell-fix-left" : null)}"{(hasLeft ? $" style=\"position: sticky; left: {DragWidth}px;\"" : "")}>{index}{Checkbox("Select row", " data-select-row @change=\"selectRow($el)\"")}</td>""");
            index = "";
        }
        if (detail)
        {
            var fix = hasLeft ? $" style=\"position: sticky; left: {DragWidth + (Selectable ? SelectionWidth : 0)}px;\"" : "";
            var expandable = !newRow && (RowExpandable?.Invoke(row) ?? true);
            var button = newRow
                ? """<button type="button" class="ant-table-row-expand-icon ant-table-row-expand-icon-spaced" aria-hidden="true" tabindex="-1"></button>"""
                : expandable
                    ? """<button type="button" class="ant-table-row-expand-icon ant-table-row-expand-icon-collapsed" aria-label="Expand row" title="Show or hide the details" aria-expanded="false" @click="toggleRow($el)"></button>"""
                    : "";
            sb.Append($"""<td class="{Html.Cx("ant-table-cell", "ant-table-row-expand-icon-cell", hasLeft ? "ant-table-cell-fix-left" : null)}"{fix}>{index}{button}</td>""");
            index = "";
        }
        for (var i = 0; i < visible.Count; i++)
        {
            var c = visible[i];
            var (cls, style) = Fix(visible, i, offsets, "m-grid-td");
            sb.Append($"""<td class="{cls}"{style} data-key="{Html.E(c.Key)}" data-label="{Html.E(c.Title)}">{index}{Cell(row, c, errors.GetValueOrDefault((row.Id, c.Key)), culture, i == 0 ? Mark(row) : "")}{(i == 0 ? RowMessage(rowError) : "")}</td>""");
            index = "";
        }
        if (AllowDeleteRow)
            sb.Append($"""<td class="m-grid-actions ant-table-cell ant-table-cell-fix-right ant-table-cell-fix-right-first" style="text-align: center; position: sticky; right: 0px;">{index}""")
              .Append(locked
                  ? $"""<button type="button" class="ant-btn ant-btn-text ant-btn-sm ant-btn-icon-only" disabled aria-label="Delete row" title="{Html.E(stateText)}">{AntIcons.Span("delete")}</button>"""
                  : $"""<button type="submit" name="{Html.E(n)}.op" value="delete-row:{Html.E(row.Id)}" class="ant-btn ant-btn-text ant-btn-sm ant-btn-icon-only ant-btn-dangerous" aria-label="Delete row" title="Delete row" @click.prevent="deleteRow($el)">{AntIcons.Span("delete")}</button>""")
              .Append("</td>");
        sb.Append("</tr>");
        if (detail && !newRow)
        {
            using var writer = new StringWriter();
            RowDetail!(row).WriteTo(writer, HtmlEncoder.Default);
            sb.Append($"""<tr class="ant-table-expanded-row ant-table-expanded-row-level-1" data-detail-for="{Html.E(row.Id)}" style="display: none;"><td class="ant-table-cell" colspan="{colspan}">{writer}</td></tr>""");
        }
        return sb.ToString();
    }

    // The mark of a read-only or locked row, in its first cell: an eye or a lock, with the reason read out.
    private static string Mark(GridRecord row) =>
        GridSchema.StateIcon(row) is not { } icon ? ""
            : $"""<span class="m-grid-row-mark">{AntIcons.Span(icon)}<span class="m-sr-only">{Html.E(GridSchema.StateText(row))}</span></span>""";

    private static string RowMessage(string? error) =>
        error is null ? "" : $"""<div class="m-grid-cell-message m-grid-row-message" role="alert">{Html.E(error)}</div>""";

    private string Cell(GridRecord row, GridColumn c, string? error, CultureInfo culture, string mark = "")
    {
        var value = row[c.Key];
        var editable = GridSchema.CanEdit(c, row);
        var field = $"{Name}[{row.Id}].{c.Key}";
        var message = error is null ? "" : $"""<div class="m-grid-cell-message" role="alert">{Html.E(error)}</div>""";
        var errorClass = error is null ? null : "m-grid-cell-error";

        if (c.Type == GridCellType.Switch)
        {
            var on = GridSchema.Flag(value);
            var click = editable ? " @click=\"on = !on; $dispatch('grid-change')\"" : "";
            return $"""<div class="{Html.Cx("m-grid-cell", errorClass)}" tabindex="-1"><button type="button" role="switch" aria-label="{Html.E(c.Title)}" aria-checked="{Js(on)}" class="{Html.Cx("ant-switch", "ant-switch-small", on ? "ant-switch-checked" : null, editable ? null : "ant-switch-disabled")}"{(editable ? "" : " disabled")} x-data="{"{ on: " + Js(on) + " }"}" :aria-checked="on" :class="on && 'ant-switch-checked'"{click}><div class="ant-switch-handle"></div><span class="ant-switch-inner"></span><input type="hidden" name="{Html.E(field)}" value="{Js(on)}" :value="on" /></button></div>{message}""";
        }
        if (c.Type == GridCellType.Rating)
        {
            var count = (int)(c.Max ?? 5);
            var v = (int)Math.Round(GridSchema.Number(value) ?? 0);
            var star = AntIcons.Span("star", IconThemeType.Fill);
            var sb = new StringBuilder($"""<div class="{Html.Cx("m-grid-cell", errorClass)}" tabindex="-1"><span class="m-rate" x-data="{"{ v: " + v + ", h: 0 }"}"><ul class="{Html.Cx("m-grid-rate", "ant-rate", editable ? null : "ant-rate-disabled")}" role="radiogroup" aria-label="{Html.E(c.Title)}" @mouseleave="h = 0">""");
            for (var i = 1; i <= count; i++)
            {
                var click = editable ? $""" @click="v = v === {i} ? 0 : {i}; $dispatch('grid-change')" @mouseenter="h = {i}" @keydown.enter.prevent="v = {i}; $dispatch('grid-change')" """.TrimEnd() : "";
                sb.Append($"""<li class="ant-rate-star {(v >= i ? "ant-rate-star-full" : "ant-rate-star-zero")}" :class="(h || v) >= {i} ? 'ant-rate-star-full' : 'ant-rate-star-zero'"><div role="radio" aria-label="{i} star{(i == 1 ? "" : "s")}" aria-checked="{Js(v == i)}" :aria-checked="v === {i}" aria-posinset="{i}" aria-setsize="{count}" tabindex="{(editable ? 0 : -1)}"{click}><div class="ant-rate-star-first">{star}</div><div class="ant-rate-star-second">{star}</div></div></li>""");
            }
            sb.Append($"""</ul><input type="hidden" name="{Html.E(field)}" value="{v}" :value="v" /></span></div>{message}""");
            return sb.ToString();
        }

        var view = View(c, row, culture, editable);
        if (!editable)
            return $"""<div class="{Html.Cx("m-grid-cell", errorClass)}">{mark}{view}</div><input type="hidden" name="{Html.E(field)}" value="{Html.E(value)}" />{message}""";

        var label = $"Edit {c.Title}: {GridSchema.Format(c, value, culture)}";
        return $"""<div class="{Html.Cx("m-grid-cell", "m-grid-cell-editable", errorClass)}" tabindex="0" role="button" aria-label="{Html.E(label)}"{(error is null ? "" : $" title=\"{Html.E(error)}\"")} @click="edit($el)" @keydown.enter.prevent="edit($el)" @keydown.f2.prevent="edit($el)">{view}</div>""" +
               $"""<div class="m-grid-cell m-grid-cell-editing" hidden @focusout="leave($event)" @keydown.enter.prevent="commit($el)" @keydown.escape.stop.prevent="cancel($el)"{EditorEvents(c)}>{Editor(c, field, value, row.Id)}</div>{message}""";
    }

    /// <summary>The read-only cell content; mirrors EditableGrid.View and modulusGrid.view() in modulus-widgets.js.</summary>
    internal static string View(GridColumn c, GridRecord row, CultureInfo culture, bool editable = true)
    {
        var value = row[c.Key];
        switch (c.Type)
        {
            case GridCellType.Tag when GridSchema.Option(c, value) is { } tag:
                return Tag(tag);
            case GridCellType.MultiSelect when GridSchema.Many(value).Count > 0:
                return $"""<span class="m-grid-tags">{string.Concat(GridSchema.Many(value).Select(v => GridSchema.Option(c, v) is { } o ? Tag(o) : $"<span class=\"ant-tag\">{Html.E(v)}</span>"))}</span>""";
            case GridCellType.Progress when GridSchema.Number(value) is { } p:
                var percent = Math.Clamp(p, 0, 100);
                var done = percent >= 100;
                var text = done ? AntIcons.Span("check-circle", IconThemeType.Fill) : $"{percent:0}%";
                return $"""<div class="ant-progress ant-progress-status-{(done ? "success" : "normal")} ant-progress-small ant-progress-line ant-progress-show-info"><div class="ant-progress-outer"><div class="ant-progress-inner"><div class="ant-progress-bg" style="width: {percent.ToString("0.##", CultureInfo.InvariantCulture)}%; height: 6px;"></div></div></div><span title="{percent:0}%" class="ant-progress-text">{text}</span></div>""";
            default:
                var formatted = GridSchema.Format(c, value, culture);
                if (formatted.Length == 0) return editable ? """<span class="m-grid-empty">Add</span>""" : """<span class="m-grid-empty">—</span>""";
                var icon = c.Type == GridCellType.Select && GridSchema.Option(c, value)?.Icon is { } name ? AntIcons.Span(name) : "";
                if (c.Avatar || c.SubtitleKey is not null)
                {
                    var subtitle = c.SubtitleKey is { } sub ? row[sub] : null;
                    var avatar = c.Avatar ? $"""<span class="m-avatar m-grid-avatar m-av-{GridSchema.Tint(formatted)}" aria-hidden="true">{Html.E(GridSchema.Initials(formatted))}</span>""" : "";
                    var desc = string.IsNullOrEmpty(subtitle) ? "" : $"""<span class="m-grid-entity-desc">{Html.E(subtitle)}</span>""";
                    return $"""<span class="m-grid-entity">{avatar}<span class="m-grid-entity-text"><span class="m-grid-entity-title">{icon}{Html.E(formatted)}</span>{desc}</span></span>""";
                }
                if (icon.Length > 0) return $"""<span class="m-grid-icon-text">{icon}{Html.E(formatted)}</span>""";
                return $"""<span{(c.AlignRight ? " class=\"m-num\"" : "")}>{Html.E(formatted)}</span>""";
        }
    }

    private static string Tag(GridOption o) =>
        $"""<span class="{Html.Cx("ant-tag", o.Color is null or "default" ? null : $"ant-tag-{o.Color}")}">{(o.Icon is null ? "" : AntIcons.Span(o.Icon))}{Html.E(o.Label)}</span>""";

    private static string Checkbox(string label, string attributes) =>
        $"""<label class="ant-checkbox-wrapper"><span class="ant-checkbox"><input type="checkbox" class="ant-checkbox-input" aria-label="{Html.E(label)}"{attributes} /><span class="ant-checkbox-inner"></span></span></label>""";

    // The option select ends the edit when it closes and posts "Create new …" (add-option); the date picker ends it on
    // a pick. On the editor, so the grid's commit and submitOp answer (inside the widget its own methods would).
    private static string EditorEvents(GridColumn c) => c.Type switch
    {
        GridCellType.Select or GridCellType.Tag or GridCellType.MultiSelect => """ @m-cselect-close="commit($el)" @m-cselect-add="submitOp($event.detail)" """.TrimEnd(),
        GridCellType.Date => """ @m-picker-pick="commit($el)" """.TrimEnd(),
        _ => "",
    };

    private static string SafeId(string value) =>
        "m-cselect-" + string.Concat(value.Select(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_'));

    private string Editor(GridColumn c, string field, string? value, string rowId)
    {
        var name = Html.E(field);
        var aria = Html.E(c.Title);
        switch (c.Type)
        {
            case GridCellType.Number or GridCellType.Currency or GridCellType.Percent or GridCellType.Progress:
                return $"""<input type="number" class="ant-input m-num" name="{name}" value="{Html.E(value)}" step="any" inputmode="decimal"{Html.Attr("min", c.Min?.ToString(CultureInfo.InvariantCulture))}{Html.Attr("max", c.Max?.ToString(CultureInfo.InvariantCulture))} aria-label="{aria}" />""";
            case GridCellType.Date:
                // Ant Design's picker popup (modulusPicker, as app-date-picker), like the Blazor grid's DatePicker; a
                // pick ends the edit (m-picker-pick). The hidden input posts yyyy-MM-dd.
                var date = GridSchema.Date(value) is { } d ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "";
                var cfg = Widgets.Config(new { picker = "date", showTime = false, values = new[] { date }, disabled = false });
                return $"""<div class="ant-picker m-picker" style="width: 100%;" x-data="modulusPicker({cfg})" @click.outside="close(true)" @mousedown="rootDown($event)">""" +
                       """<div class="ant-picker-input">""" +
                       PickerText.TextInput(0, PickerText.Placeholder("date"), date, null, false, false, "") +
                       $"""<span class="ant-picker-suffix" aria-hidden="true">{AntIcons.Span("calendar")}</span>""" +
                       (c.Required ? "" : PickerText.Clear("values[0]", false)) + "</div>" +
                       PickerText.Hidden(field, date, 0) + PickerText.Drop + "</div>";
            case GridCellType.Select or GridCellType.Tag or GridCellType.MultiSelect:
                // The Blazor grid's AppCreatableSelect (modulusCreatableSelect): coloured options, a check on the picked
                // ones and "Create new …", which posts add-option:{key}:{row} so the option joins the column's schema.
                var multiple = c.Type == GridCellType.MultiSelect;
                return CreatableSelectTagHelper.Render(new CreatableSelectModel(field, SafeId($"{field}-list"), c.Options)
                {
                    Multiple = multiple,
                    Value = multiple ? null : value,
                    Values = multiple ? GridSchema.Many(value) : [],
                    AllowClear = !c.Required,
                    AriaLabel = c.Title,
                    AddPlaceholder = "New option",
                    CreateText = $"Create new {c.Title.ToLowerInvariant()}",
                    AllowAdd = c.Options.Count < GridSchema.MaxOptions,
                    GridOp = $"add-option:{c.Key}:{rowId}",
                    OptionField = EditableGridForm.OptionField(Name, rowId, c.Key),
                    ColorField = EditableGridForm.ColorField(Name, rowId, c.Key),
                }, " style=\"width: 100%;\"");
            default:
                return $"""<input type="text" class="ant-input" name="{name}" value="{Html.E(value)}" maxlength="500" aria-label="{aria}" />""";
        }
    }

}
