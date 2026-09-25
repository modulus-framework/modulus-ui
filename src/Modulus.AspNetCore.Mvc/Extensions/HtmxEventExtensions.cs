using Microsoft.AspNetCore.Http;

namespace Modulus.AspNetCore.Mvc.Extensions;

/// <summary>
/// htmx event and response header helpers for MVC/Razor Pages.
/// </summary>
public static class HtmxEventExtensions
{
    /// <summary>
    /// Checks if the request is from htmx.
    /// </summary>
    public static bool IsHtmx(this HttpRequest request)
    {
        return request.Headers.ContainsKey("HX-Request");
    }

    /// <summary>
    /// Gets the triggered element ID from htmx request.
    /// </summary>
    public static string? GetHtmxTrigger(this HttpRequest request)
    {
        return request.Headers.TryGetValue("HX-Trigger", out var trigger) ? trigger.ToString() : null;
    }

    /// <summary>
    /// Gets the trigger name from htmx request.
    /// </summary>
    public static string? GetHtmxTriggerName(this HttpRequest request)
    {
        return request.Headers.TryGetValue("HX-Trigger-Name", out var triggerName) ? triggerName.ToString() : null;
    }

    /// <summary>
    /// Triggers an Alpine.js event via HX-Trigger response header.
    /// Used to notify Alpine components of server-side events.
    /// Example: response.TriggerAlpineEvent("message", new { type = "success", content = "Saved!" })
    /// </summary>
    public static void TriggerAlpineEvent(this HttpResponse response, string eventName, object? data = null)
    {
        var headerValue = eventName;
        if (data != null)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(data);
            headerValue = $"{eventName}:{json}";
        }
        response.Headers.Append("HX-Trigger", headerValue);
    }

    /// <summary>
    /// Redirects via HX-Redirect response header.
    /// </summary>
    public static void HtmxRedirect(this HttpResponse response, string url)
    {
        response.Headers.Append("HX-Redirect", url);
    }

    /// <summary>
    /// Refreshes the current page via htmx.
    /// </summary>
    public static void HtmxRefresh(this HttpResponse response)
    {
        response.Headers.Append("HX-Refresh", "true");
    }

    /// <summary>
    /// Prevents htmx from swapping the response.
    /// </summary>
    public static void HtmxNoSwap(this HttpResponse response)
    {
        response.Headers.Append("HX-No-Content", "true");
    }

    /// <summary>
    /// Sends a client-side validation error to Alpine form state.
    /// </summary>
    public static void SetFormErrors(this HttpResponse response, Dictionary<string, string> errors)
    {
        if (errors.Any())
        {
            var json = System.Text.Json.JsonSerializer.Serialize(errors);
            response.Headers.Append("HX-Trigger", $"formErrors:{json}");
        }
    }
}
