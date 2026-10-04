using Microsoft.AspNetCore.Mvc;
using Modulus.Documents;
using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions.Plugins;

namespace Modulus.AdminTemplate.RazorPages.Pages.Plugins;

// Room for the largest accepted xlsx import (the check below still rejects anything over the limit).
[RequestSizeLimit(PluginsContent.MaxUploadMb * 1024 * 1024 + 64 * 1024)]
public class SpreadsheetModel : SamplePageModel
{
    public void OnGet() { }

    // The spreadsheet posts its sheets as JSON back to the page, which shows them saved; a picked xlsx replaces the grid.
    public void OnPost(string? view, IFormFile? file)
    {
        ViewData["SpreadsheetView"] = PluginsContent.SpreadsheetView(view); // the demo to return to
        if (file is null) return; // saved: the view reads the posted sheets
        if (PluginsContent.CheckSpreadsheetUpload(file.FileName, file.Length) is { } check)
        {
            ViewData["SpreadsheetError"] = check;
            return;
        }
        using var stream = file.OpenReadStream();
        var (sheets, error) = PluginsContent.ImportSpreadsheet(stream);
        if (error is not null) ViewData["SpreadsheetError"] = error;
        else
        {
            ViewData["SpreadsheetSheets"] = sheets;
            ViewData["SpreadsheetImport"] = file.FileName;
        }
    }

    // Excel download of the page: the posted sheets (or the demo budget) as an xlsx workbook (the same URL as the MVC sample).
    public IActionResult OnPostDownload() =>
        File(SpreadsheetXlsx.Write(Posted() ?? PluginsContent.Budget), SpreadsheetXlsx.ContentType, PluginsContent.SpreadsheetDownloadName);

    private IReadOnlyList<SpreadsheetSheet>? Posted() =>
        Request.HasFormContentType ? PluginsContent.PostedSheets(Request.Form["Budget"]) : null;
}
