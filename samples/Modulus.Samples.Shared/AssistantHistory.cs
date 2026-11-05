using System.Security.Claims;
using Modulus.Ui.Abstractions.State;

namespace Modulus.Samples.Shared;

/// <summary>
/// The assistant conversation kept on the server for a signed-in user (through the app's <see cref="IUiStateStore"/>). An anonymous
/// visitor's conversation stays in the form (the hidden transcript field), because nothing is kept for anonymous visitors.
/// </summary>
public static class AssistantHistory
{
    public const string Key = "assistant.conversation";

    public static async Task<string?> LoadAsync(IUiStateStore store, ClaimsPrincipal? user)
    {
        var who = UiUser.Key(user);
        return who.Length == 0 ? null : await store.GetAsync(who, Key);
    }

    public static async Task SaveAsync(IUiStateStore store, ClaimsPrincipal? user, string transcript)
    {
        var who = UiUser.Key(user);
        if (who.Length > 0) await store.SetAsync(who, Key, transcript);
    }
}
