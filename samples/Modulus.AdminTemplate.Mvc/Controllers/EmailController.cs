using Microsoft.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc.Htmx;
using Modulus.Samples.Shared;
using Modulus.Samples.Web.Models;
using Modulus.Ui.Abstractions;

namespace Modulus.AdminTemplate.Mvc.Controllers;

/// <summary>
/// Email app: folders, labels, search and the reading view are GETs (boosted); star / read / move post over
/// htmx and fire mail:refresh; compose loads into the drawer.
/// </summary>
[Route("apps/email")]
public class EmailController(SampleStore store) : SampleController
{
    public const string RefreshEvent = "mail:refresh";
    private const string ComposeView = "~/Views/Shared/Ui/Apps/Email/_ComposeForm.cshtml";

    [HttpGet("")]
    public IActionResult Index(string? folder, string? label, string? keyword) =>
        Page("Email", "Apps/Email/Email", EmailPage.Load(store, folder, label, keyword));

    [HttpGet("{id:int}")]
    public IActionResult Read(int id, string? folder, string? label)
    {
        var page = EmailPage.Load(store, folder, label, null, id);
        return page.Open is null ? NotFound() : Page(page.Open.Subject, "Apps/Email/Email", page);
    }

    [HttpPost("star/{id:int}")]
    public IActionResult Star(int id)
    {
        store.UpdateMail(id, m => m.Starred = !m.Starred);
        Response.HxTrigger(RefreshEvent);
        return Request.IsHtmx() ? NoContent() : Redirect($"~/apps/email/{id}");
    }

    [HttpPost("read")]
    public IActionResult MarkRead([FromForm] int[] ids, [FromForm] bool read)
    {
        foreach (var id in ids) store.UpdateMail(id, m => m.Read = read);
        Response.HxMessage(MessageType.Success, read ? "Marked as read" : "Marked as unread").HxTrigger(RefreshEvent);
        return Request.IsHtmx() ? NoContent() : Redirect("~/apps/email");
    }

    /// <summary>Moves messages (trash, spam, back to the inbox); from the reading view it returns to the list.</summary>
    [HttpPost("move")]
    public IActionResult Move([FromForm] int[] ids, [FromForm] MailFolder to, string? folder, string? label)
    {
        var permanent = to == MailFolder.Trash && ids.Select(store.FindMail).All(m => m?.Folder == MailFolder.Trash);
        store.MoveMail(ids, to);
        var text = permanent ? "Deleted forever" : to switch { MailFolder.Trash => "Moved to trash", MailFolder.Spam => "Reported as spam", _ => $"Moved to {to}" };
        if (Request.IsHtmx())
        {
            Response.HxMessage(MessageType.Success, ids.Length > 1 ? $"{text} ({ids.Length})" : text).HxTrigger(RefreshEvent);
            return NoContent();
        }
        Flash(text);
        return Redirect(EmailPage.Load(store, folder, label, null).ListUrl);
    }

    [HttpGet("compose")]
    public IActionResult Compose(int? reply, int? forward) =>
        PartialView(ComposeView, ComposeInput.For(store.FindMail(reply ?? forward ?? 0), forward is not null));

    [HttpPost("send")]
    public IActionResult Send(ComposeInput input, bool draft)
    {
        if (draft) ModelState.Clear();
        if (!ModelState.IsValid) return PartialView(ComposeView, input);
        store.SendMail(input, draft);
        Response.HxMessage(MessageType.Success, draft ? "Draft saved" : "Message sent").HxCloseDrawer().HxTrigger(RefreshEvent);
        return Request.IsHtmx() ? NoContent() : Redirect("~/apps/email");
    }
}
