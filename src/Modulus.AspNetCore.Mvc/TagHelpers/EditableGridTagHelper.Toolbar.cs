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
    // The selection bar, as ProTable's: count, Clear, then "Set value", the app's actions, Export selected and Delete.
    private string BatchBar(string? extra)
    {
        var sb = new StringBuilder("""<div class="ant-alert ant-alert-info ant-alert-no-icon m-table-alert m-grid-batch" role="status" x-show="selected > 0" style="display: none;"><div class="ant-alert-content"><div class="ant-alert-message">""");
        sb.Append("""<span>Selected <strong x-text="selected"></strong> <span x-text="selected === 1 ? 'row' : 'rows'"></span></span>""");
        sb.Append("""<button type="button" class="ant-btn ant-btn-link ant-btn-sm" @click="selectAll(false)"><span>Clear</span></button><span class="m-spacer"></span>""");
        var batchColumns = Columns.Where(c => c.Editable && c.Type is GridCellType.Select or GridCellType.Tag or GridCellType.Switch).ToList();
        if (batchColumns.Count > 0)
        {
            sb.Append("""<select class="m-select m-grid-batch-set" aria-label="Set a value on the selected rows" @change="setSelected($el)"><option value="">Set value…</option>""");
            foreach (var c in batchColumns)
            {
                sb.Append($"""<optgroup label="{Html.E(c.Title)}">""");
                foreach (var o in GridSchema.FilterOptions(c)) sb.Append($"""<option value="{Html.E($"{c.Key}={o.Value}")}">{Html.E(o.Label)}</option>""");
                sb.Append("</optgroup>");
            }
            sb.Append("</select>");
        }
        foreach (var a in Batch ?? [])
        {
            if (!GridSchema.IsBatchKey(a.Key)) continue;
            var key = Html.E(a.Key);
            var cls = Html.Cx("ant-btn", a.Primary ? "ant-btn-primary" : "ant-btn-default", a.Danger ? "ant-btn-dangerous" : null, "ant-btn-sm", "m-grid-batch-action");
            // The count of rows it applies to, and disabled (with why) when none do; Print takes every selected row.
            var count = a.Print ? "" : $"""<span class="m-grid-batch-count" x-text="batchCount('{key}', selected)"></span>""";
            var off = a.Print ? "" : $""" :disabled="!batchCount('{key}', selected)" :title="batchCount('{key}', selected) ? '' : {Html.E(JsonSerializer.Serialize(GridSchema.BatchNone(a)))}" """.TrimEnd();
            sb.Append($"""<button type="button" class="{cls}" data-m-grid-batch="{key}"{off} @click="runBatch('{key}')">{(a.Icon is null ? "" : AntIcons.Span(a.Icon))}<span>{Html.E(a.Label)}</span>{count}</button>""");
        }
        sb.Append(extra);
        sb.Append($"""<button type="button" class="ant-btn ant-btn-default ant-btn-sm" @click="exportCsv(true)">{AntIcons.Span("download")}<span>Export selected</span></button>""");
        if (AllowDeleteRow)
            sb.Append($"""<button type="button" class="ant-btn ant-btn-default ant-btn-dangerous ant-btn-sm" data-m-grid-delete-selected @click="deleteSelected()">{AntIcons.Span("delete")}<span>Delete</span></button>""");
        return sb.Append("</div></div></div>").ToString();
    }
}
