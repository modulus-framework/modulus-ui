using Microsoft.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc.Htmx;
using Modulus.Samples.Shared;
using Modulus.Samples.Web.Models;
using Modulus.Ui.Abstractions;

namespace Modulus.AdminTemplate.RazorPages.Pages.Apps.Email;

/// <summary>Email app: same URLs as the MVC sample (/star/{id}, /read, /move, /compose, /send).</summary>
public class EmailModel(SampleStore store) : SamplePageModel
{
    public const string RefreshEvent = "mail:refresh";
    private const string ComposeView = "~/Views/Shared/Ui/Apps/Email/_ComposeForm.cshtml";

    public EmailPage View { get; private set; } = default!;

    public void OnGet(string? folder, string? label, string? keyword) => View = EmailPage.Load(store, folder, label, keyword);

    public IActionResult OnPostStar(int id)
    {
        store.UpdateMail(id, m => m.Starred = !m.Starred);
        Response.HxTrigger(RefreshEvent);
        return Request.IsHtmx() ? NoContent() : Redirect($"~/apps/email/{id}");
    }

    public IActionResult OnPostRead([FromForm] int[] ids, [FromForm] bool read)
    {
        foreach (var id in ids) store.UpdateMail(id, m => m.Read = read);
        Response.HxMessage(MessageType.Success, read ? "Marked as read" : "Marked as unread").HxTrigger(RefreshEvent);
        return Request.IsHtmx() ? NoContent() : Redirect("~/apps/email");
    }

    public IActionResult OnPostMove([FromForm] int[] ids, [FromForm] MailFolder to, string? folder, string? label)
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

    public IActionResult OnGetCompose(int? reply, int? forward) =>
        Partial(ComposeView, ComposeInput.For(store.FindMail(reply ?? forward ?? 0), forward is not null));

    public IActionResult OnPostSend(ComposeInput input, bool draft)
    {
        if (draft) ModelState.Clear();
        if (!ModelState.IsValid) return Partial(ComposeView, input);
        store.SendMail(input, draft);
        Response.HxMessage(MessageType.Success, draft ? "Draft saved" : "Message sent").HxCloseDrawer().HxTrigger(RefreshEvent);
        return Request.IsHtmx() ? NoContent() : Redirect("~/apps/email");
    }
}
