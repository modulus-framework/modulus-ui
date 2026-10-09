using Microsoft.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc.Htmx;
using Modulus.AspNetCore.Mvc.TagHelpers;
using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Files;

namespace Modulus.AdminTemplate.RazorPages.Pages.Apps.Files;

/// <summary>File manager: the same URLs as the MVC sample (?folder=, ?q=, /folder, /rename, /delete/{id}).</summary>
public class FilesModel(FileStore store) : SamplePageModel
{
    public FileManagerView View { get; private set; } = FileManagerModel.Build([], null, null, DateTime.Now);

    public void OnGet(int? folder, string? q) => View = FileManagerModel.Build(store.Entries(), folder, q, DateTime.Now);

    public IActionResult OnPostFolder([FromForm] int? parentId, [FromForm] string? name) =>
        Done(store.Create(new FileCreate(parentId, name ?? ""), DateTime.Now), "Folder created", parentId);

    public IActionResult OnPostRename([FromForm] int id, [FromForm] string? name) =>
        Done(store.Rename(new FileRename(id, name ?? ""), DateTime.Now), "Renamed", null);

    public IActionResult OnPostDelete(int id)
    {
        if (!store.Delete(id)) return NotFound();
        Response.HxMessage(MessageType.Success, "Deleted").HxTrigger(FileManagerTagHelper.RefreshEvent);
        return Request.IsHtmx() ? NoContent() : Redirect("~/apps/files");
    }

    private IActionResult Done(string? error, string success, int? folder)
    {
        if (error is not null)
        {
            Response.HxMessage(MessageType.Error, error);
            return Request.IsHtmx() ? new UnprocessableEntityResult() : Redirect("~/apps/files");
        }
        Response.HxMessage(MessageType.Success, success).HxTrigger(FileManagerTagHelper.RefreshEvent);
        return Request.IsHtmx() ? NoContent() : Redirect(folder is { } f ? $"~/apps/files?folder={f}" : "~/apps/files");
    }
}
