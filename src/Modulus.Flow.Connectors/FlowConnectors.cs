using System.Net.Mail;
using Modulus.Ui.Abstractions.Flow;
using Modulus.Ui.Abstractions.Language;

namespace Modulus.Flow.Connectors;

/// <summary>Registers the connectors on a runner: each one handles the catalog node type it serves.</summary>
public static class FlowConnectors
{
    /// <summary>
    /// <c>ai.chat</c>: the node's prompt is the instruction, the item's <c>message</c> is the input, and the reply is set as
    /// <c>reply</c>. The node's <c>model</c> setting picks the model when the client has more than one.
    /// </summary>
    public static FlowRunner UseLanguageModel(this FlowRunner runner, ILanguageModel model) =>
        runner.OnEach("ai.chat", async (ctx, item, ct) =>
        {
            var input = item.Text("message");
            if (input.Length == 0) throw new InvalidOperationException("The item has no message to answer.");
            var reply = await model.CompleteAsync(ctx.Setting("prompt", item), input, 1024, ct, Optional(ctx.Setting("model")));
            return item.With("reply", reply);
        });

    /// <summary>
    /// <c>action.email</c>: sends to the node's <c>to</c> (a valid address is required), with its <c>subject</c> and <c>body</c>
    /// (the body defaults to the item's <c>reply</c>). The item gets <c>emailed</c> set to the address.
    /// </summary>
    public static FlowRunner UseEmail(this FlowRunner runner, IEmailSender sender) =>
        runner.OnEach("action.email", async (ctx, item, ct) =>
        {
            var to = ctx.Setting("to", item).Trim();
            _ = new MailAddress(to);
            var body = Optional(ctx.Setting("body", item)) ?? item.Text("reply");
            await sender.SendAsync(new EmailMessage(to, ctx.Setting("subject", item), body), ct);
            return item.With("emailed", to);
        });

    /// <summary>
    /// <c>action.slack</c>: posts the node's <c>text</c>, or the item's <c>reply</c> when the node has none. The channel is set by
    /// the webhook, so the node's <c>channel</c> setting is a label only. The item gets <c>posted</c> set to true.
    /// </summary>
    public static FlowRunner UseSlack(this FlowRunner runner, SlackWebhook webhook) =>
        runner.OnEach("action.slack", async (ctx, item, ct) =>
        {
            var text = Optional(ctx.Setting("text", item)) ?? item.Text("reply");
            if (text.Length == 0) throw new InvalidOperationException("There is no text to post.");
            await webhook.PostAsync(text, ct);
            return item.With("posted", true);
        });

    /// <summary>
    /// <c>ai.classify</c>: the model names one of the three categories for each item's <c>message</c>; the item goes out on the
    /// matching port. Anything the model does not name as positive or negative goes to "other".
    /// </summary>
    public static FlowRunner UseClassifier(this FlowRunner runner, ILanguageModel model) =>
        runner.On("ai.classify", async (ctx, ct) =>
        {
            var positive = new List<FlowItem>();
            var negative = new List<FlowItem>();
            var other = new List<FlowItem>();
            foreach (var item in ctx.Items)
            {
                var input = item.Text("message");
                if (input.Length == 0) throw new InvalidOperationException("An item has no message to classify.");
                var reply = (await model.CompleteAsync(ClassifyInstruction, input, 8, ct)).Trim().ToLowerInvariant();
                (reply.StartsWith("positive", StringComparison.Ordinal) ? positive : reply.StartsWith("negative", StringComparison.Ordinal) ? negative : other).Add(item);
            }
            return new FlowResult { Note = $"{positive.Count} positive, {negative.Count} negative, {other.Count} other" }
                .To("positive", positive).To("negative", negative).To("other", other);
        });

    private const string ClassifyInstruction = "Classify the text as exactly one word: positive, negative or other. Reply with that word only.";

    /// <summary>
    /// <c>ai.extract</c>: the model returns a JSON object with the fields the node's <c>schema</c> describes; they are set on the item
    /// as <c>extracted</c>. A reply that is not a JSON object fails the node.
    /// </summary>
    public static FlowRunner UseExtraction(this FlowRunner runner, ILanguageModel model) =>
        runner.OnEach("ai.extract", async (ctx, item, ct) =>
        {
            var input = item.Text("message");
            if (input.Length == 0) throw new InvalidOperationException("The item has no message to extract from.");
            var schema = ctx.Setting("schema", item);
            var reply = await model.CompleteAsync($"Extract the fields described by this JSON schema from the text. Reply with one JSON object only. Schema: {schema}", input, 512, ct);
            var start = reply.IndexOf('{');
            var end = reply.LastIndexOf('}');
            if (start < 0 || end <= start) throw new InvalidOperationException("The model did not return a JSON object.");
            using var json = System.Text.Json.JsonDocument.Parse(reply[start..(end + 1)]);
            return item.With("extracted", ToValue(json.RootElement));
        });

    /// <summary>
    /// <c>data.http</c>: calls a URL with the node's <c>method</c> and an optional <c>body</c>, both filled from the item. Only hosts
    /// the app lists in <paramref name="allowedHosts"/> are called (a workflow cannot reach other servers). The item gets <c>status</c>,
    /// <c>body</c> (the text, cut to 10,000 characters) and, for a JSON answer, <c>json</c>. An error status fails the node.
    /// </summary>
    public static FlowRunner UseHttp(this FlowRunner runner, HttpClient http, IEnumerable<string> allowedHosts)
    {
        var hosts = allowedHosts.Select(h => h.ToLowerInvariant()).ToHashSet();
        return runner.OnEach("data.http", async (ctx, item, ct) =>
        {
            var url = ctx.Setting("url", item).Trim();
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
                throw new InvalidOperationException("The URL must be an absolute http or https address.");
            if (!hosts.Contains(uri.Host.ToLowerInvariant()))
                throw new InvalidOperationException($"The host {uri.Host} is not in the list of hosts this workflow may call.");

            var method = HttpMethod.Parse(string.IsNullOrWhiteSpace(ctx.Setting("method")) ? "GET" : ctx.Setting("method").Trim().ToUpperInvariant());
            using var request = new HttpRequestMessage(method, uri);
            var body = Optional(ctx.Setting("body", item));
            if (body is not null) request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
            using var response = await http.SendAsync(request, ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode) throw new HttpRequestException($"The request answered {(int)response.StatusCode}.", null, response.StatusCode);

            var result = item.With("status", (int)response.StatusCode).With("body", text.Length > 10_000 ? text[..10_000] : text);
            try
            {
                using var json = System.Text.Json.JsonDocument.Parse(text);
                result = result.With("json", ToValue(json.RootElement));
            }
            catch (System.Text.Json.JsonException)
            {
                // Not JSON: the text is all there is.
            }
            return result;
        });
    }

    /// <summary>A JSON value as the plain values the items hold (text, numbers, booleans, lists and nested values).</summary>
    private static object? ToValue(System.Text.Json.JsonElement e) => e.ValueKind switch
    {
        System.Text.Json.JsonValueKind.String => e.GetString(),
        System.Text.Json.JsonValueKind.Number => e.TryGetInt64(out var l) ? l : e.GetDouble(),
        System.Text.Json.JsonValueKind.True => true,
        System.Text.Json.JsonValueKind.False => false,
        System.Text.Json.JsonValueKind.Object => e.EnumerateObject().ToDictionary(p => p.Name, p => ToValue(p.Value)),
        System.Text.Json.JsonValueKind.Array => e.EnumerateArray().Select(ToValue).ToList(),
        _ => null,
    };

    private static string? Optional(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
