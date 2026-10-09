using Microsoft.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc.Htmx;
using Modulus.AspNetCore.Mvc.TagHelpers;
using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Files;

namespace Modulus.AdminTemplate.Mvc.Controllers;

/// <summary>File manager: folders and search are GETs; create, rename and delete post over htmx and fire files:refresh.</summary>
[Route("apps/files")]
public class FilesController(FileStore store) : SampleController
{
    [HttpGet("")]
    public IActionResult Index(int? folder, string? q) =>
        Page("Files", "Apps/Files/Files", FileManagerModel.Build(store.Entries(), folder, q, DateTime.Now));

    [HttpPost("folder")]
    public IActionResult CreateFolder([FromForm] int? parentId, [FromForm] string? name)
    {
        var error = store.Create(new FileCreate(parentId, name ?? ""), DateTime.Now);
        return Done(error, "Folder created", parentId);
    }

    [HttpPost("rename")]
    public IActionResult Rename([FromForm] int id, [FromForm] string? name)
    {
        var error = store.Rename(new FileRename(id, name ?? ""), DateTime.Now);
        return Done(error, "Renamed", null);
    }

    [HttpPost("delete/{id:int}")]
    public IActionResult Delete(int id)
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
            return Request.IsHtmx() ? UnprocessableEntity() : Redirect("~/apps/files");
        }
        Response.HxMessage(MessageType.Success, success).HxTrigger(FileManagerTagHelper.RefreshEvent);
        return Request.IsHtmx() ? NoContent() : Redirect(folder is { } f ? $"~/apps/files?folder={f}" : "~/apps/files");
    }
}
