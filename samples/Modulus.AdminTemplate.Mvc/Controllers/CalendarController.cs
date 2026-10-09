using Microsoft.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc.Htmx;
using Modulus.AspNetCore.Mvc.TagHelpers;
using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions;

namespace Modulus.AdminTemplate.Mvc.Controllers;

/// <summary>Calendar: month grid / agenda as boosted GETs; the event drawer saves and deletes over htmx.</summary>
[Route("apps/calendar")]
public class CalendarController(SampleStore store) : SampleController
{
    private const string FormView = "~/Views/Shared/Ui/Apps/Calendar/_EventForm.cshtml";

    [HttpGet("")]
    public IActionResult Index(string? month, string? view, [FromQuery] string[] cat, bool filtered) =>
        Page("Calendar", "Apps/Calendar/Calendar",
            CalendarPage.Load(store, CalendarPage.Query(month, view, cat, filtered, DateTime.Today), DateTime.Now));

    [HttpGet("form/{id:int?}")]
    public IActionResult Form(int? id, DateTime? date)
    {
        if (id is { } existing)
            return store.FindEvent(existing) is { } e ? PartialView(FormView, EventInput.From(e)) : NotFound();
        var start = (date ?? DateTime.Today).Date.AddHours(9);
        return PartialView(FormView, new EventInput { Start = start, End = start.AddHours(1) });
    }

    [HttpPost("save")]
    public IActionResult Save(EventInput input)
    {
        if (input.Start is { } s && input.End is { } e && e < s) ModelState.AddModelError(nameof(input.End), "End must be after the start");
        if (!ModelState.IsValid) return PartialView(FormView, input);
        store.SaveEvent(input);
        Response.HxMessage(MessageType.Success, input.Id is null ? "Event added" : "Event saved").HxCloseDrawer().HxTrigger(SchedulerTagHelper.RefreshEvent);
        return Request.IsHtmx() ? NoContent() : Redirect("~/apps/calendar");
    }

    [HttpPost("delete/{id:int}")]
    public IActionResult Delete(int id)
    {
        store.DeleteEvent(id);
        Response.HxMessage(MessageType.Success, "Event deleted").HxCloseDrawer().HxTrigger(SchedulerTagHelper.RefreshEvent);
        return Request.IsHtmx() ? NoContent() : Redirect("~/apps/calendar");
    }
}
