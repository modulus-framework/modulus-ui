using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Modulus.Ui.Abstractions.Chat;

/// <summary>Who said a turn of a conversation.</summary>
public enum ChatRole
{
    User,
    Assistant,
}

/// <summary>One turn: what was said, and for an assistant turn, the notes that go under it (what was left out, what was applied).</summary>
public sealed record ChatTurn(ChatRole Role, string Text, IReadOnlyList<string>? Notes = null);

/// <summary>
/// A conversation kept as text (JSON) in a hidden field, so a plain form posts it back: MVC and Razor Pages keep the
/// conversation without server state. The limits hold whatever a post sends: the newest turns are kept, and long texts are cut.
/// </summary>
public static class ChatTranscript
{
    /// <summary>Turns kept; older ones are dropped.</summary>
    public const int MaxTurns = 40;

    /// <summary>Longest text kept in a turn.</summary>
    public const int MaxTextLength = 1000;

    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string ToText(IEnumerable<ChatTurn> turns) =>
        JsonSerializer.Serialize(turns.TakeLast(MaxTurns).Select(t => t with { Text = Cut(t.Text) }).ToList(), Options);

    /// <summary>The turns a text holds: empty when it is missing or not a conversation (never an exception).</summary>
    public static IReadOnlyList<ChatTurn> FromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        try
        {
            var turns = JsonSerializer.Deserialize<List<ChatTurn>>(text, Options) ?? [];
            return turns.Where(t => t is not null && t.Text is not null)
                .Select(t => t with { Text = Cut(t.Text) })
                .TakeLast(MaxTurns).ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string Cut(string text) => text.Length <= MaxTextLength ? text : text[..MaxTextLength];
}

/// <summary>The markup of a conversation and its composer, the same in MVC, Razor Pages and Blazor.</summary>
public static class ChatHtml
{
    /// <summary>The turns, in order, as a log a screen reader announces as it grows. Empty when there are none.</summary>
    public static string Log(IEnumerable<ChatTurn> turns, string emptyText = "Ask a question to start.")
    {
        var sb = new StringBuilder("<ol class=\"m-chat-log\" role=\"log\" aria-live=\"polite\">");
        var any = false;
        foreach (var turn in turns)
        {
            any = true;
            var role = turn.Role == ChatRole.User ? "user" : "assistant";
            sb.Append($"<li class=\"m-chat-turn m-chat-{role}\"><span class=\"m-chat-who\">{(turn.Role == ChatRole.User ? "You" : "Assistant")}</span>");
            sb.Append($"<div class=\"m-chat-bubble\">{E(turn.Text)}</div>");
            if (turn.Notes is { Count: > 0 })
            {
                sb.Append("<ul class=\"m-chat-notes\">");
                foreach (var note in turn.Notes) sb.Append($"<li>{E(note)}</li>");
                sb.Append("</ul>");
            }
            sb.Append("</li>");
        }
        if (!any) sb.Append($"<li class=\"m-chat-empty m-muted\">{E(emptyText)}</li>");
        return sb.Append("</ol>").ToString();
    }

    /// <summary>
    /// The composer's fields: the question (a text area, limited to <see cref="ChatTranscript.MaxTextLength"/>), the
    /// conversation so far and the filter in effect as hidden fields, and the send button. The host puts them in its own
    /// form (with antiforgery).
    /// </summary>
    public static string Composer(string transcript, string? filter = null, string name = "question", string label = "Your question", string placeholder = "", string button = "Ask")
    {
        var hiddenFilter = filter is null ? "" : $"<input type=\"hidden\" name=\"filter\" value=\"{E(filter)}\" />";
        return "<div class=\"m-chat-composer\">" +
            $"<label class=\"m-chat-label\" for=\"{E(name)}\">{E(label)}</label>" +
            $"<textarea id=\"{E(name)}\" class=\"ant-input m-chat-input\" name=\"{E(name)}\" rows=\"2\" maxlength=\"{ChatTranscript.MaxTextLength}\" placeholder=\"{E(placeholder)}\"></textarea>" +
            $"<input type=\"hidden\" name=\"transcript\" value=\"{E(transcript)}\" />" + hiddenFilter +
            $"<button type=\"submit\" class=\"ant-btn ant-btn-primary\">{E(button)}</button>" +
            "</div>";
    }

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
}
