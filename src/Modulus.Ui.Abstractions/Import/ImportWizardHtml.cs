using System.Globalization;
using System.Net;
using System.Text;
using Modulus.Ui.Abstractions.Query;

namespace Modulus.Ui.Abstractions.Import;

/// <summary>
/// The wizard's markup for the MVC tag helper: a steps bar, then the form of the current step. Each step is a form
/// that posts the file's text (hidden as <c>csv</c>) and the mapping (<c>m.{key}</c>) back, with a <c>step</c> button
/// saying where to go next, so the server needs no session. The Blazor <c>ImportWizard</c> component draws the same
/// classes and steps.
/// </summary>
public static class ImportWizardHtml
{
    /// <summary>The number of rows the preview lists (the import takes them all).</summary>
    public const int PreviewRows = 50;

    /// <summary>Renders the current step. <paramref name="action"/> is the page the forms post to.</summary>
    public static string Render(ImportWizardView view, string action)
    {
        var sb = new StringBuilder();
        sb.Append("""<div class="m-import" data-m-import>""");
        Steps(sb, view.Step);
        if (view.Error is { Length: > 0 } error) sb.Append($"""<div class="m-import-error" role="alert">{E(error)}</div>""");
        switch (view.Step)
        {
            case ImportStep.Upload: Upload(sb, view, action); break;
            case ImportStep.Map: Map(sb, view, action); break;
            case ImportStep.Preview: Preview(sb, view, action); break;
            case ImportStep.Done: Done(sb, view, action); break;
        }
        return sb.Append("</div>").ToString();
    }

    private static readonly (ImportStep Step, string Label)[] Stages =
    [
        (ImportStep.Upload, "Upload"),
        (ImportStep.Map, "Map columns"),
        (ImportStep.Preview, "Check rows"),
        (ImportStep.Done, "Import"),
    ];

    private static void Steps(StringBuilder sb, ImportStep current)
    {
        sb.Append("""<ol class="m-import-steps">""");
        foreach (var (step, label) in Stages)
        {
            var state = step < current ? "done" : step == current ? "current" : "todo";
            sb.Append($"""<li class="m-import-step" data-state="{state}">{E(label)}</li>""");
        }
        sb.Append("</ol>");
    }

    private static void Upload(StringBuilder sb, ImportWizardView view, string action)
    {
        sb.Append($"""<form class="m-import-form" method="post" enctype="multipart/form-data" action="{E(action)}">""");
        sb.Append("""<label class="m-import-file"><span>CSV file</span><input type="file" name="file" accept=".csv,text/csv" required /></label>""");
        sb.Append($"""<p class="m-import-hint">Up to {ImportLimits.MaxBytes / 1_000_000} MB and {ImportLimits.MaxRows:N0} rows. The first line is the header.</p>""");
        sb.Append("""<h4 class="m-import-subhead">Columns this import reads</h4><ul class="m-import-columns">""");
        foreach (var field in view.Fields)
            sb.Append($"""<li>{E(field.Title)}{(field.Required ? """ <span class="m-import-required">required</span>""" : "")}</li>""");
        sb.Append("</ul>");
        sb.Append("""<div class="m-import-actions"><button type="submit" name="step" value="upload" class="ant-btn ant-btn-primary">Upload file</button></div></form>""");
    }

    private static void Map(StringBuilder sb, ImportWizardView view, string action)
    {
        var headers = view.Table?.Headers ?? [];
        sb.Append($"""<form class="m-import-form" method="post" action="{E(action)}">""");
        sb.Append($"""<input type="hidden" name="csv" value="{E(view.Csv)}" />""");
        sb.Append("""<div class="m-import-map">""");
        foreach (var field in view.Fields)
        {
            sb.Append($"""<label class="m-import-field"><span>{E(field.Title)}{(field.Required ? """ <span class="m-import-required">*</span>""" : "")}</span><select class="m-import-select" name="{E(ImportMapping.FieldPrefix + field.Key)}">""");
            sb.Append("""<option value="">Not in the file</option>""");
            var chosen = view.Mapping?.Column(field.Key);
            for (var c = 0; c < headers.Count; c++)
                sb.Append($"""<option value="{c.ToString(CultureInfo.InvariantCulture)}"{(chosen == c ? " selected" : "")}>{E(headers[c])}</option>""");
            sb.Append("</select></label>");
        }
        sb.Append("</div>");
        sb.Append("""<div class="m-import-actions"><button type="submit" name="step" value="preview" class="ant-btn ant-btn-primary">Check rows</button>""");
        sb.Append($"""<a class="ant-btn ant-btn-text" href="{E(action)}">Start over</a></div></form>""");
    }

    private static void Preview(StringBuilder sb, ImportWizardView view, string action)
    {
        var preview = view.Preview ?? new ImportPreview([], []);
        var fields = view.Fields;
        sb.Append("""<div class="m-import-summary">""");
        sb.Append($"""<span class="m-badge m-badge-green-lt">{preview.ValidCount.ToString("N0", CultureInfo.InvariantCulture)} ready</span>""");
        sb.Append($"""<span class="m-badge {(preview.InvalidCount > 0 ? "m-badge-red-lt" : "m-badge-gray-lt")}">{preview.InvalidCount.ToString("N0", CultureInfo.InvariantCulture)} with problems</span>""");
        sb.Append("</div>");
        if (preview.Problems.Count > 0)
        {
            sb.Append("""<ul class="m-import-problems">""");
            foreach (var problem in preview.Problems) sb.Append($"<li>{E(problem)}</li>");
            sb.Append("</ul>");
        }
        if (preview.Rows.Count > 0)
        {
            sb.Append("""<div class="m-import-table-wrap"><table class="ant-table m-import-table"><thead class="ant-table-thead"><tr><th class="ant-table-cell">Row</th>""");
            foreach (var field in fields) sb.Append($"""<th class="ant-table-cell">{E(field.Title)}</th>""");
            sb.Append("""<th class="ant-table-cell">Problems</th></tr></thead><tbody class="ant-table-tbody">""");
            foreach (var row in preview.Rows.Take(PreviewRows))
            {
                sb.Append($"""<tr class="ant-table-row" data-valid="{(row.Valid ? "true" : "false")}"><td class="ant-table-cell">{row.Row.ToString(CultureInfo.InvariantCulture)}</td>""");
                foreach (var field in fields)
                {
                    var bad = row.Issues.Any(i => i.Field == field.Key);
                    sb.Append($"""<td class="ant-table-cell{(bad ? " m-import-bad" : "")}">{E(Display(row.Values.GetValueOrDefault(field.Key)))}</td>""");
                }
                sb.Append($"""<td class="ant-table-cell">{E(string.Join("; ", row.Issues.Select(i => $"{Title(fields, i.Field)}: {i.Message}")))}</td></tr>""");
            }
            sb.Append("</tbody></table></div>");
            if (preview.Rows.Count > PreviewRows)
                sb.Append($"""<p class="m-import-hint">Showing the first {PreviewRows} of {preview.Rows.Count.ToString("N0", CultureInfo.InvariantCulture)} rows.</p>""");
        }

        // The mapping and the file travel with the form, so the import re-checks exactly what was previewed.
        sb.Append($"""<form class="m-import-form" method="post" action="{E(action)}">""");
        sb.Append($"""<input type="hidden" name="csv" value="{E(view.Csv)}" />""");
        foreach (var field in fields)
            if (view.Mapping?.Column(field.Key) is { } c)
                sb.Append($"""<input type="hidden" name="{E(ImportMapping.FieldPrefix + field.Key)}" value="{c.ToString(CultureInfo.InvariantCulture)}" />""");
        sb.Append("""<div class="m-import-actions"><button type="submit" name="step" value="map" class="ant-btn ant-btn-text">Back to columns</button>""");
        sb.Append($"""<button type="submit" name="step" value="import" class="ant-btn ant-btn-primary"{(preview.CanCommit ? "" : " disabled")}>Import {preview.ValidCount.ToString("N0", CultureInfo.InvariantCulture)} rows</button></div>""");
        sb.Append("</form>");
    }

    private static void Done(StringBuilder sb, ImportWizardView view, string action) =>
        sb.Append($"""<div class="m-import-done"><p>{E(view.Message)}</p><a class="ant-btn ant-btn-primary" href="{E(action)}">Import another file</a></div>""");

    private static string Title(IReadOnlyList<ImportField> fields, string key) => fields.FirstOrDefault(f => f.Key == key)?.Title ?? key;

    private static string Display(object? value) => value switch
    {
        null => "",
        decimal number => number.ToString("N2", CultureInfo.InvariantCulture),
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };

    private static string E(string? text) => WebUtility.HtmlEncode(text ?? "");
}
