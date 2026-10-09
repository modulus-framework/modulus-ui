using Microsoft.AspNetCore.Mvc;
using Modulus.Documents;
using Modulus.Samples.Shared;

namespace Modulus.AdminTemplate.RazorPages.Pages.Plugins;

// Room for the largest accepted docx import (the check below still rejects anything over the limit).
[RequestSizeLimit(PluginsContent.MaxUploadMb * 1024 * 1024 + 64 * 1024)]
public class DocumentModel : SamplePageModel
{
    public void OnGet() { }

    // The editor posts its JSON back to the page, which shows it saved; a picked docx replaces the pages.
    public void OnPost(string? view, IFormFile? file)
    {
        ViewData["DocumentView"] = PluginsContent.DocumentView(view); // the demo to return to
        if (file is null) return; // saved: the view reads the posted document
        if (PluginsContent.CheckDocumentUpload(file.FileName, file.Length) is { } check)
        {
            ViewData["DocumentError"] = check;
            return;
        }
        using var stream = file.OpenReadStream();
        var (document, error) = PluginsContent.ImportDocument(stream, Path.GetFileNameWithoutExtension(file.FileName));
        if (error is not null) ViewData["DocumentError"] = error;
        else
        {
            ViewData["DocumentJson"] = document;
            ViewData["DocumentImport"] = file.FileName;
        }
    }

    // Word download of the page: the posted document (or the contract template) as a docx file (the same URL as the MVC sample).
    public IActionResult OnPostDownload() =>
        File(DocumentDocx.Write(Posted() ?? PluginsContent.Contract), DocumentDocx.ContentType, PluginsContent.DocumentDownloadName);

    // PDF download of the page: the same document as a PDF file (the same URL as the MVC sample).
    public IActionResult OnPostPdf() =>
        File(DocumentPdf.Write(Posted() ?? PluginsContent.Contract), DocumentPdf.ContentType, PluginsContent.DocumentPdfName);

    private string? Posted() =>
        Request.HasFormContentType ? PluginsContent.PostedDocument(Request.Form["Document"]) : null;
}
