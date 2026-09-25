using Microsoft.AspNetCore.Http;

namespace Modulus.AspNetCore.Mvc.Extensions;

/// <summary>
/// HTMX and form handling extensions for server-side views.
/// </summary>
public static class HtmxExtensions
{
    /// <summary>
    /// Detects if the request is an HTMX request.
    /// </summary>
    public static bool IsHtmx(this HttpRequest request)
    {
        return request.Headers.ContainsKey("HX-Request") &&
               request.Headers["HX-Request"].ToString() == "true";
    }

    /// <summary>
    /// Gets the HTMX trigger element name (which element initiated the request).
    /// </summary>
    public static string? GetHtmxTrigger(this HttpRequest request)
    {
        return request.Headers.TryGetValue("HX-Trigger", out var trigger) ? trigger.ToString() : null;
    }

    /// <summary>
    /// Sends an HTMX trigger event via response header (for client-side event dispatch).
    /// </summary>
    public static void TriggerHtmxEvent(this HttpResponse response, string eventName, string? detail = null)
    {
        var headerValue = detail is not null
            ? $"""{{"{eventName}": {detail}}}"""
            : eventName;
        response.Headers.Append("HX-Trigger", headerValue);
    }

    /// <summary>
    /// Sends HTMX redirect (navigates to a new URL).
    /// </summary>
    public static void HtmxRedirect(this HttpResponse response, string url)
    {
        response.Headers.Append("HX-Redirect", url);
    }
}
