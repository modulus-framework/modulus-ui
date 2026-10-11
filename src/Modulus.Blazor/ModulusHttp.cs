using System.Text.Json;
using Microsoft.JSInterop;

namespace Modulus.Blazor;

/// <summary>
/// The outcome of a <see cref="ModulusHttp"/> call. <see cref="Status"/> is 0 when the request never reached the server.
/// </summary>
public sealed record ModulusHttpResult(int Status, bool Ok, JsonElement? Body)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The JSON body as <typeparamref name="T"/>, or default when the response had none.</summary>
    public T? Read<T>() =>
        Body is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) } body ? body.Deserialize<T>(Json) : default;

    /// <summary>The <c>error</c> property of a JSON error body, if there is one.</summary>
    public string? Error =>
        Body is { ValueKind: JsonValueKind.Object } body && body.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String
            ? error.GetString()
            : null;
}

/// <summary>
/// Same-origin JSON requests made by the browser, for pages that call the host's own endpoints (<c>/account/...</c>,
/// <c>/authorization/...</c>). A Blazor Server circuit runs on the server, so an injected <see cref="HttpClient"/> never
/// carries the user's cookies; these calls go through the browser, which sends the sign-in cookie and the antiforgery
/// token (rendered by <c>&lt;ModulusHead /&gt;</c>). Needs JS interop, so call it after the first render, not while prerendering.
/// </summary>
public sealed class ModulusHttp(IJSRuntime js) : IAsyncDisposable
{
    private const string Module = "./_content/Modulus.Ui.Assets/js/modulus-http.js";

    private IJSObjectReference? _module;

    public Task<ModulusHttpResult> GetAsync(string url) => SendAsync("GET", url);

    public Task<ModulusHttpResult> PostAsync(string url, object? body = null) => SendAsync("POST", url, body);

    public Task<ModulusHttpResult> DeleteAsync(string url) => SendAsync("DELETE", url);

    public async Task<ModulusHttpResult> SendAsync(string method, string url, object? body = null)
    {
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", Module);
        return await _module.InvokeAsync<ModulusHttpResult>("send", method, url, body);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is null)
            return;
        try { await _module.DisposeAsync(); }
        catch (JSDisconnectedException) { /* the circuit is already gone */ }
    }
}
