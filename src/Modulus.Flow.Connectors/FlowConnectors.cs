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

    private static string? Optional(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
