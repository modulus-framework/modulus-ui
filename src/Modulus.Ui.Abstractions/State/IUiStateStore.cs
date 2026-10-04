using System.Collections.Concurrent;
using System.Security.Claims;

namespace Modulus.Ui.Abstractions.State;

/// <summary>
/// Per-user UI state: saved views, column layouts, dashboard tiles. A value is a short string (usually JSON or a query
/// string) under a key such as <c>views:/tables/pivot</c>; null removes it. Modulus registers
/// <see cref="MemoryUiStateStore"/> when the app registers none; an app that keeps state across restarts registers its
/// own (a table keyed by user and key is enough).
/// </summary>
public interface IUiStateStore
{
    /// <summary>The value of <paramref name="key"/> for <paramref name="user"/> (<see cref="UiUser.Key"/>), or null.</summary>
    ValueTask<string?> GetAsync(string user, string key, CancellationToken cancellationToken = default);

    /// <summary>Stores <paramref name="value"/> under <paramref name="key"/> for <paramref name="user"/>; null removes it.</summary>
    ValueTask SetAsync(string user, string key, string? value, CancellationToken cancellationToken = default);
}

/// <summary>Who UI state belongs to.</summary>
public static class UiUser
{
    /// <summary>
    /// The store key of a signed-in user: the name identifier claim, else the name; "" for an anonymous visitor, whose
    /// state is not kept (it would be shared by everyone who is not signed in).
    /// </summary>
    public static string Key(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true) return "";
        return user.FindFirst(ClaimTypes.NameIdentifier)?.Value is { Length: > 0 } id ? id : user.Identity.Name ?? "";
    }
}

/// <summary>
/// The default <see cref="IUiStateStore"/>: state in memory, per user, lost on restart. Values over
/// <see cref="MaxValueLength"/> and keys beyond <see cref="MaxKeysPerUser"/> are refused; an anonymous user ("") keeps
/// nothing.
/// </summary>
public sealed class MemoryUiStateStore : IUiStateStore
{
    public const int MaxValueLength = 64 * 1024;
    public const int MaxKeysPerUser = 500;

    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _users = new(StringComparer.Ordinal);

    public ValueTask<string?> GetAsync(string user, string key, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(user.Length > 0 && _users.TryGetValue(user, out var values) && values.TryGetValue(key, out var value) ? value : null);

    public ValueTask SetAsync(string user, string key, string? value, CancellationToken cancellationToken = default)
    {
        if (user.Length == 0) return ValueTask.CompletedTask;
        var values = _users.GetOrAdd(user, _ => new ConcurrentDictionary<string, string>(StringComparer.Ordinal));
        if (value is null) values.TryRemove(key, out _);
        else if (value.Length > MaxValueLength) throw new ArgumentException($"UI state values are limited to {MaxValueLength} characters.", nameof(value));
        else if (values.Count >= MaxKeysPerUser && !values.ContainsKey(key)) throw new InvalidOperationException($"A user keeps at most {MaxKeysPerUser} UI state keys.");
        else values[key] = value;
        return ValueTask.CompletedTask;
    }
}
