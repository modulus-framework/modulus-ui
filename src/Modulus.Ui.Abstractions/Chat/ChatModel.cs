using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Modulus.Ui.Abstractions.Assistant;

namespace Modulus.Ui.Abstractions.Chat;

/// <summary>Who said a turn of a conversation.</summary>
public enum ChatRole
{
    User,
    Assistant,
}

/// <summary>A passage an answer was given from: its title, where it is (a link, when it has one), and a short excerpt.</summary>
public sealed record ChatSource(string Title, string? Url, string Excerpt);

/// <summary>A small table an answer carries (analytics: sales by city, a forecast by month).</summary>
public sealed record ChatTable(string Title, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows);

/// <summary>
/// One turn: what was said. An assistant turn may carry its <paramref name="Notes"/> (what was left out, what was applied), its
/// <paramref name="Sources"/> (the passages an answer used), its <paramref name="Trace"/> (the steps an agent took), a
/// <paramref name="Table"/> (analytics), or an <paramref name="Approval"/> waiting for a person.
/// </summary>
public sealed record ChatTurn(
    ChatRole Role,
    string Text,
    IReadOnlyList<string>? Notes = null,
    IReadOnlyList<ChatSource>? Sources = null,
    IReadOnlyList<string>? Trace = null,
    ChatTable? Table = null,
    ApprovalRequest? Approval = null);

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
    /// <summary>
    /// The turns, in order, as a log a screen reader announces as it grows. Empty when there are none. The approval buttons
    /// belong to the form named <paramref name="decisionFormId"/> (set it when the page has one; it posts <c>approval</c>).
    /// </summary>
    public static string Log(IEnumerable<ChatTurn> turns, string emptyText = "Ask a question to start.", string? decisionFormId = null)
    {
        var sb = new StringBuilder("<ol class=\"m-chat-log\" role=\"log\" aria-live=\"polite\">");
        var any = false;
        foreach (var turn in turns)
        {
            any = true;
            sb.Append(Turn(turn, decisionFormId));
        }
        if (!any) sb.Append($"<li class=\"m-chat-empty m-muted\">{E(emptyText)}</li>");
        return sb.Append("</ol>").ToString();
    }

    /// <summary>One turn as a list item: the text, notes, sources, agent steps, table, and approval, as the turn holds them.</summary>
    public static string Turn(ChatTurn turn, string? decisionFormId = null)
    {
        var role = turn.Role == ChatRole.User ? "user" : "assistant";
        var sb = new StringBuilder();
        sb.Append($"<li class=\"m-chat-turn m-chat-{role}\"><span class=\"m-chat-who\">{(turn.Role == ChatRole.User ? "You" : "Assistant")}</span>");
        if (turn.Text.Length > 0) sb.Append($"<div class=\"m-chat-bubble\">{E(turn.Text)}</div>");
        if (turn.Notes is { Count: > 0 })
        {
            sb.Append("<ul class=\"m-chat-notes\">");
            foreach (var note in turn.Notes) sb.Append($"<li>{E(note)}</li>");
            sb.Append("</ul>");
        }
        if (turn.Sources is { Count: > 0 })
        {
            sb.Append("<ul class=\"m-chat-sources\" aria-label=\"Sources\">");
            foreach (var source in turn.Sources)
            {
                var title = IsSafeUrl(source.Url) ? $"<a href=\"{E(source.Url)}\" rel=\"noopener\">{E(source.Title)}</a>" : E(source.Title);
                sb.Append($"<li>{title}<span class=\"m-muted\"> {E(source.Excerpt)}</span></li>");
            }
            sb.Append("</ul>");
        }
        if (turn.Trace is { Count: > 0 })
        {
            sb.Append($"<details class=\"m-chat-trace\"><summary>Steps ({turn.Trace.Count})</summary><ol>");
            foreach (var step in turn.Trace) sb.Append($"<li>{E(step)}</li>");
            sb.Append("</ol></details>");
        }
        if (turn.Table is not null) sb.Append(TableHtml(turn.Table));
        if (turn.Approval is not null) sb.Append(ApprovalHtml.Card(turn.Approval, decisionFormId));
        return sb.Append("</li>").ToString();
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

    /// <summary>A table with a caption, for analytics answers.</summary>
    public static string TableHtml(ChatTable table)
    {
        var sb = new StringBuilder("<table class=\"ant-table m-chat-table\">");
        sb.Append($"<caption>{E(table.Title)}</caption><thead class=\"ant-table-thead\"><tr>");
        foreach (var h in table.Headers) sb.Append($"<th class=\"ant-table-cell\">{E(h)}</th>");
        sb.Append("</tr></thead><tbody class=\"ant-table-tbody\">");
        foreach (var row in table.Rows)
        {
            sb.Append("<tr class=\"ant-table-row\">");
            foreach (var cell in row) sb.Append($"<td class=\"ant-table-cell\">{E(cell)}</td>");
            sb.Append("</tr>");
        }
        return sb.Append("</tbody></table>").ToString();
    }

    /// <summary>A link is kept only when it is a site path or an http(s) address; anything else is shown as plain text.</summary>
    private static bool IsSafeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri)) return false;
        return uri.IsAbsoluteUri ? uri.Scheme is "http" or "https" : url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal);
    }

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
}

/// <summary>An approval request as a card: what it is, its details, its status, and the approve and reject buttons while it is pending.</summary>
public static class ApprovalHtml
{
    /// <summary>The card. When <paramref name="formId"/> is set the buttons submit that form (their <c>form</c> attribute), so they can sit inside another form.</summary>
    public static string Card(ApprovalRequest request, string? formId = null)
    {
        var sb = new StringBuilder();
        sb.Append($"<section class=\"m-approval m-approval-{request.Status.ToString().ToLowerInvariant()}\" aria-label=\"{E(request.Title)}\">");
        sb.Append($"<div class=\"m-approval-title\">{E(request.Title)}</div>");
        if (request.Details.Count > 0)
        {
            sb.Append("<dl class=\"m-approval-details\">");
            foreach (var field in request.Details) sb.Append($"<dt>{E(field.Label)}</dt><dd>{E(field.Value)}</dd>");
            sb.Append("</dl>");
        }
        sb.Append($"<div class=\"m-approval-status\">{StatusText(request.Status)}</div>");
        if (request.Status == ApprovalStatus.Pending)
        {
            var form = formId is null ? "" : $" form=\"{E(formId)}\"";
            sb.Append("<div class=\"m-approval-actions\">");
            sb.Append($"<button type=\"submit\"{form} name=\"approval\" value=\"{E(request.Id)}:approve\" class=\"ant-btn ant-btn-primary\">Approve</button>");
            sb.Append($"<button type=\"submit\"{form} name=\"approval\" value=\"{E(request.Id)}:reject\" class=\"ant-btn ant-btn-default\">Reject</button>");
            sb.Append("</div>");
        }
        return sb.Append("</section>").ToString();
    }

    public static string StatusText(ApprovalStatus status) => status switch
    {
        ApprovalStatus.Approved => "Approved",
        ApprovalStatus.Rejected => "Rejected",
        _ => "Waiting for your approval",
    };

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
}
