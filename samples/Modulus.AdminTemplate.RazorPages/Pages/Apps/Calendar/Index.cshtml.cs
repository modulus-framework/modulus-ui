using Microsoft.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc.Htmx;
using Modulus.AspNetCore.Mvc.TagHelpers;
using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Scheduler;

namespace Modulus.AdminTemplate.RazorPages.Pages.Apps.Calendar;

/// <summary>Calendar: same URLs as the MVC sample (/form/{id}, /save, /delete/{id}).</summary>
public class CalendarModel(SampleStore store) : SamplePageModel
{
    private const string FormView = "~/Views/Shared/Ui/Apps/Calendar/_EventForm.cshtml";

    public SchedulerView View { get; private set; } = default!;

    public void OnGet(string? month, string? view, [FromQuery] string[] cat, bool filtered) =>
        View = CalendarPage.Load(store, CalendarPage.Query(month, view, cat, filtered, DateTime.Today), DateTime.Now);

    public IActionResult OnGetForm(int? id, DateTime? date)
    {
        if (id is { } existing)
            return store.FindEvent(existing) is { } e ? Partial(FormView, EventInput.From(e)) : NotFound();
        var start = (date ?? DateTime.Today).Date.AddHours(9);
        return Partial(FormView, new EventInput { Start = start, End = start.AddHours(1) });
    }

    public IActionResult OnPostSave(EventInput input)
    {
        if (input.Start is { } s && input.End is { } e && e < s) ModelState.AddModelError(nameof(input.End), "End must be after the start");
        if (!ModelState.IsValid) return Partial(FormView, input);
        store.SaveEvent(input);
        Response.HxMessage(MessageType.Success, input.Id is null ? "Event added" : "Event saved").HxCloseDrawer().HxTrigger(SchedulerTagHelper.RefreshEvent);
        return Request.IsHtmx() ? NoContent() : Redirect("~/apps/calendar");
    }

    public IActionResult OnPostDelete(int id)
    {
        store.DeleteEvent(id);
        Response.HxMessage(MessageType.Success, "Event deleted").HxCloseDrawer().HxTrigger(SchedulerTagHelper.RefreshEvent);
        return Request.IsHtmx() ? NoContent() : Redirect("~/apps/calendar");
    }
}
