using Microsoft.AspNetCore.Mvc;
using Modulus.Documents;
using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions.Plugins;

namespace Modulus.AdminTemplate.Mvc.Controllers;

/// <summary>Plugins pages: charts advanced, colour picker, date picker, dropzone, lightbox, tour. Query-driven ones read the query in the shared view.</summary>
[Route("plugins")]
public class PluginsController : SampleController
{
    [HttpGet("charts-advanced")]
    public IActionResult ChartsAdvanced() => Page("Charts advanced", "Plugins/ChartsAdvanced");

    [HttpGet("color-picker")]
    public IActionResult ColorPicker() => Page("Color picker", "Plugins/ColorPicker");

    [HttpGet("date-picker")]
    public IActionResult DatePicker() => Page("Date picker", "Plugins/DatePicker");

    [HttpGet("dropzone")]
    public IActionResult Dropzone() => Page("Dropzone", "Plugins/Dropzone");

    // The dropzone's upload target: checks each file and throws it away (the demo stores nothing).
    [HttpPost("dropzone/upload")]
    [RequestSizeLimit(PluginsContent.MaxUploadMb * 1024 * 1024 + 64 * 1024)]
    public IActionResult Upload(IFormFile? file) =>
        PluginsContent.CheckUpload(file?.FileName, file?.Length ?? 0) is { } error ? BadRequest(error) : Ok();

    [HttpGet("lightbox")]
    public IActionResult Lightbox() => Page("Lightbox", "Plugins/Lightbox");

    [HttpGet("tour")]
    public IActionResult Tour() => Page("Tour", "Plugins/Tour");

    [HttpGet("player")]
    public IActionResult Player() => Page("Inline player", "Plugins/Player");

    // The player's audio track, made on the server (the Blazor and Razor Pages samples serve the same URL).
    [HttpGet("player/track.wav")]
    public IActionResult Track() => File(PluginsContent.DemoTrack(), "audio/wav", enableRangeProcessing: true);

    // The editor posts its HTML back to the page, which shows it cleaned.
    [HttpGet("editor"), HttpPost("editor")]
    public IActionResult Editor() => Page("WYSIWYG editor", "Plugins/Editor");

    // The spreadsheet posts its sheets as JSON back to the page, which shows them saved.
    [HttpGet("spreadsheet")]
    public IActionResult Spreadsheet() => Page("Spreadsheet", "Plugins/Spreadsheet");

    // The same post with a picked xlsx: the workbook replaces the grid (the view reads the result from ViewData).
    [HttpPost("spreadsheet")]
    [RequestSizeLimit(PluginsContent.MaxUploadMb * 1024 * 1024 + 64 * 1024)]
    public IActionResult SpreadsheetUpload(string? view, IFormFile? file)
    {
        ViewData["SpreadsheetView"] = PluginsContent.SpreadsheetView(view); // the demo to return to
        if (file is not null)
        {
            if (PluginsContent.CheckSpreadsheetUpload(file.FileName, file.Length) is { } check) ViewData["SpreadsheetError"] = check;
            else
            {
                using var stream = file.OpenReadStream();
                var (sheets, error) = PluginsContent.ImportSpreadsheet(stream);
                if (error is not null) ViewData["SpreadsheetError"] = error;
                else { ViewData["SpreadsheetSheets"] = sheets; ViewData["SpreadsheetImport"] = file.FileName; }
            }
        }
        return Page("Spreadsheet", "Plugins/Spreadsheet");
    }

    // Excel download of the spreadsheet page: the posted sheets (or the demo budget) as an xlsx workbook.
    [HttpPost("spreadsheet/download")]
    public IActionResult SpreadsheetDownload() =>
        File(SpreadsheetXlsx.Write(PluginsContent.PostedSheets(Request.Form["Budget"]) ?? PluginsContent.Budget),
            SpreadsheetXlsx.ContentType, PluginsContent.SpreadsheetDownloadName);

    // The document editor posts its JSON back to the page, which shows it saved.
    [HttpGet("document")]
    public IActionResult Document() => Page("Document", "Plugins/Document");

    // The same post with a picked docx: the document replaces the pages (the view reads the result from ViewData).
    [HttpPost("document")]
    [RequestSizeLimit(PluginsContent.MaxUploadMb * 1024 * 1024 + 64 * 1024)]
    public IActionResult DocumentUpload(string? view, IFormFile? file)
    {
        ViewData["DocumentView"] = PluginsContent.DocumentView(view); // the demo to return to
        if (file is not null)
        {
            if (PluginsContent.CheckDocumentUpload(file.FileName, file.Length) is { } check) ViewData["DocumentError"] = check;
            else
            {
                using var stream = file.OpenReadStream();
                var (document, error) = PluginsContent.ImportDocument(stream, Path.GetFileNameWithoutExtension(file.FileName));
                if (error is not null) ViewData["DocumentError"] = error;
                else { ViewData["DocumentJson"] = document; ViewData["DocumentImport"] = file.FileName; }
            }
        }
        return Page("Document", "Plugins/Document");
    }

    // Word download of the document page: the posted document (or the contract template) as a docx file.
    [HttpPost("document/download")]
    public IActionResult DocumentDownload() =>
        File(DocumentDocx.Write(PluginsContent.PostedDocument(Request.Form["Document"]) ?? PluginsContent.Contract),
            DocumentDocx.ContentType, PluginsContent.DocumentDownloadName);

    [HttpGet("sortable")]
    public IActionResult Sortable() => Page("Sortable", "Plugins/Sortable");

    [HttpGet("map")]
    public IActionResult Map() => Page("Map", "Plugins/Map");

    [HttpGet("map-fullsize")]
    public IActionResult MapFullsize() => Page("Map fullsize", "Plugins/MapFullsize");

    [HttpGet("map-vector")]
    public IActionResult MapVector() => Page("Map vector", "Plugins/MapVector");
}
