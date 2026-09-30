using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Modulus.AspNetCore.Mvc.Htmx;

public enum MessageType
{
    Success,
    Info,
    Warning,
    Error,
}

/// <summary>Server helpers for htmx requests and Modulus client events.</summary>
public static class HtmxExtensions
{
    public const string AntiforgeryHeader = "RequestVerificationToken";

    /// <summary>
    /// True when the request was issued by an htmx element (<c>hx-get</c>, <c>hx-post</c> …) and wants a partial answer.
    /// False for a history restore and for boosted navigation (<see cref="IsBoosted"/>), which stand in for an ordinary
    /// page load: answer those as you would a browser (a redirect, a page), and the layout sends only the page body.
    /// </summary>
    public static bool IsHtmx(this HttpRequest request) =>
        request.Headers["HX-Request"] == "true" && request.Headers["HX-History-Restore-Request"] != "true" && !request.IsBoosted();

    /// <summary>
    /// True for SPA navigation: a link or form inside an <c>hx-boost</c> element (<see cref="Modulus.Ui.Abstractions.Theming.ModulusThemeOptions.SpaNavigation"/>).
    /// </summary>
    public static bool IsBoosted(this HttpRequest request) =>
        request.Headers["HX-Boosted"] == "true" && request.Headers["HX-History-Restore-Request"] != "true";

    /// <summary>Swaps the response into another element than the one the request came from (a CSS selector).</summary>
    public static HttpResponse HxRetarget(this HttpResponse response, string selector)
    {
        response.Headers["HX-Retarget"] = selector;
        return response;
    }

    /// <summary>Uses only the part of the response that matches the selector.</summary>
    public static HttpResponse HxReselect(this HttpResponse response, string selector)
    {
        response.Headers["HX-Reselect"] = selector;
        return response;
    }

    /// <summary>Overrides how the response is swapped (<c>outerHTML show:window:top</c> …).</summary>
    public static HttpResponse HxReswap(this HttpResponse response, string swap)
    {
        response.Headers["HX-Reswap"] = swap;
        return response;
    }

    /// <summary>
    /// Queues a client event in the <c>HX-Trigger</c> header. Multiple calls merge into one JSON object,
    /// e.g. <c>Response.HxTrigger("customers:refresh")</c>.
    /// </summary>
    public static HttpResponse HxTrigger(this HttpResponse response, string eventName, object? detail = null)
    {
        var events = response.HttpContext.Items[TriggerKey] as Dictionary<string, object?> ?? [];
        events[eventName] = detail ?? true;
        response.HttpContext.Items[TriggerKey] = events;
        response.Headers["HX-Trigger"] = JsonSerializer.Serialize(events, JsonOptions);
        return response;
    }

    /// <summary>Shows an Ant Design message toast after the htmx request completes.</summary>
    public static HttpResponse HxMessage(this HttpResponse response, MessageType type, string content) =>
        response.HxTrigger("modulus:message", new { type = type.ToString().ToLowerInvariant(), content });

    /// <summary>Shows an Ant Design notification (title and description, top right).</summary>
    public static HttpResponse HxNotify(this HttpResponse response, MessageType type, string message, string? description = null) =>
        response.HxTrigger("modulus:notification", new { type = type.ToString().ToLowerInvariant(), message, description });

    /// <summary>Closes open &lt;app-modal&gt; dialogs.</summary>
    public static HttpResponse HxCloseModal(this HttpResponse response) =>
        response.HxTrigger("modulus:modal-close");

    /// <summary>
    /// Tells every <c>&lt;app-creatable-select add-modal="{list}"&gt;</c> on the page about a record created in its
    /// modal: they add the option, and the one that opened the modal selects it.
    /// </summary>
    public static HttpResponse HxOptionAdded(this HttpResponse response, string list, Modulus.Ui.Abstractions.GridOption option) =>
        response.HxTrigger("modulus:option-added", new { list, value = option.Value, label = option.Label, color = option.Color });

    /// <summary>Closes the Modulus drawer.</summary>
    public static HttpResponse HxCloseDrawer(this HttpResponse response) =>
        response.HxTrigger("modulus:drawer-close");

    /// <summary>Client-side redirect for htmx requests.</summary>
    public static HttpResponse HxRedirect(this HttpResponse response, string url)
    {
        response.Headers["HX-Redirect"] = url;
        return response;
    }

    private const string TriggerKey = "modulus:hx-trigger";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
