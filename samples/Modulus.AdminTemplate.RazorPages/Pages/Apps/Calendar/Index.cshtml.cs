using Microsoft.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc.Htmx;
using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions;

namespace Modulus.AdminTemplate.RazorPages.Pages.Apps.Calendar;

/// <summary>Calendar: same URLs as the MVC sample (/form/{id}, /save, /delete/{id}).</summary>
public class CalendarModel(SampleStore store) : SamplePageModel
{
    public const string RefreshEvent = "calendar:refresh";
    private const string FormView = "~/Views/Shared/Ui/Apps/Calendar/_EventForm.cshtml";

    public CalendarView View { get; private set; } = default!;

    public void OnGet(string? month, string? view, [FromQuery] string[] cat, bool filtered) =>
        View = CalendarView.Load(store, CalendarView.ParseMonth(month), view == "list", CalendarView.ParseCategories(cat, filtered));

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
        Response.HxMessage(MessageType.Success, input.Id is null ? "Event added" : "Event saved").HxCloseDrawer().HxTrigger(RefreshEvent);
        return Request.IsHtmx() ? NoContent() : Redirect("~/apps/calendar");
    }

    public IActionResult OnPostDelete(int id)
    {
        store.DeleteEvent(id);
        Response.HxMessage(MessageType.Success, "Event deleted").HxCloseDrawer().HxTrigger(RefreshEvent);
        return Request.IsHtmx() ? NoContent() : Redirect("~/apps/calendar");
    }
}
