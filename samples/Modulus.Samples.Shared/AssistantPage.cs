using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Modulus.Ui.Abstractions.Assistant;
using Modulus.Ui.Abstractions.Chat;
using Modulus.Flow.Connectors;
using Modulus.Ui.Abstractions.Language;

namespace Modulus.Samples.Shared;

/// <summary>The help centre the assistant answers from: a few articles with the links they live at.</summary>
public static class HelpCenter
{
    public static readonly IReadOnlyList<KnowledgeChunk> Articles =
    [
        new("refunds", "Refunds", "Refunds are paid back to the original payment method within 5 working days of the return being received.", "/help/refunds"),
        new("returns", "Returns", "Goods can be returned within 30 days of delivery. The return label is emailed when the return is opened in the account.", "/help/returns"),
        new("shipping", "Shipping times", "Orders ship within 2 working days. Standard delivery takes 3 to 5 working days; express takes one.", "/help/shipping"),
        new("tracking", "Tracking an order", "Every shipment has a reference such as SHP-000123. Enter it on the orders page to see where it is.", "/help/tracking"),
        new("invoices", "Invoices", "An invoice is emailed when an order ships. Past invoices can be downloaded from the billing page as PDF.", "/help/invoices"),
        new("password", "Password reset", "Choose Forgot password on the sign-in page. A reset link is valid for 30 minutes and works once.", "/help/password"),
        new("payment", "Payment methods", "We accept Visa, Mastercard, American Express and bank transfer. Bank transfers clear in 2 working days.", "/help/payment"),
        new("hours", "Support hours", "Support answers by chat and email from 08:00 to 18:00 on working days, in the time zone of the customer's account.", "/help/hours"),
    ];
}

/// <summary>
/// What the assistant page shows: the conversation, the mode it was asked in, and the conversation as text for the forms. The
/// knowledge base (the documents people uploaded, with the help centre) is <see cref="AssistantPage.Uploads"/>.
/// </summary>
public sealed record AssistantView(IReadOnlyList<ChatTurn> Turns, string Transcript, string Mode, IReadOnlyList<KnowledgeDocument>? Documents = null, string ModelName = "")
{
    public const string Path = "/apps/assistant";

    /// <summary>Answers from the knowledge base, with the passages as citations.</summary>
    public const string KnowledgeBase = "kb";

    /// <summary>An agent: the model calls tools (search, orders, analytics, forecast, purchase orders) and answers from what they return.</summary>
    public const string Agent = "agent";

    public const string Examples = "Try: how long do refunds take? · sales by city · forecast sales for Paris · approve PO-1001";
}

/// <summary>The tools an agent run may use, and what they produced for the page (the analytics tables).</summary>
public sealed class AssistantContext
{
    public List<ChatTable> Tables { get; } = [];
}

/// <summary>The AI assistant: questions answered from the knowledge base (RAG) or by an agent with tools, documents uploaded into the knowledge base, and purchase orders that wait for approval.</summary>
public static class AssistantPage
{
    /// <summary>The documents people uploaded (the help centre is searched alongside them).</summary>
    public static readonly KnowledgeStore Uploads = new();

    public const string CreatePurchaseOrder = "create_purchase_order";

    /// <summary>Searches the uploaded documents and the help centre together.</summary>
    public static readonly IKnowledgeSearch Search = new CombinedSearch();

    public static IReadOnlyList<AgentTool> Tools(AssistantContext context) =>
    [
        new("search_knowledge", "Searches the help centre and the uploaded documents. The input is the question's keywords. Returns the best matching passages.",
            async (input, ct) =>
            {
                var found = await Search.SearchAsync(input, 3, ct);
                return found.Count == 0 ? "No passages match." : string.Join(" | ", found.Select(c => $"{c.Title}: {c.Text}"));
            }),
        new("lookup_order", "Looks up a shipment by its reference (SHP-000123). Returns its status, amount and date.",
            (input, _) => Task.FromResult(Lookup(input))),
        new("sales_by_city", "Totals the sales by city, largest first. The input is how many cities to show. Shows a table.",
            (input, _) =>
            {
                var (text, table) = SalesAnalytics.TopCities(int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 5);
                context.Tables.Add(table);
                return Task.FromResult(text);
            }),
        new("forecast_sales", "Predicts the next month's sales with a straight-line trend. The input is a city, or all. Shows a table.",
            (input, _) =>
            {
                var (text, table) = SalesAnalytics.Forecast(input);
                context.Tables.Add(table);
                return Task.FromResult(text);
            }),
        new(CreatePurchaseOrder, "Creates the order for an uploaded purchase order, once the buyer approves it. The input is the order number, such as PO-1001.",
            async (input, _) =>
            {
                var number = NormalisePo(input);
                var state = await PurchaseOrderBook.DecideAsync(number, approve: true);
                return state is null ? $"No purchase order {number} is waiting for approval." : $"{number}: {Describe(state.Value)}.";
            }, RequiresApproval: true),
    ];

    /// <summary>Answers a question. In the agent mode a tool that needs approval stops the run with an approval card.</summary>
    public static async Task<AssistantView> AskAsync(string? transcript, string? mode, string? question, ILanguageModel model)
    {
        var turns = ChatTranscript.FromText(transcript).ToList();
        var agent = mode == AssistantView.Agent;
        if (!string.IsNullOrWhiteSpace(question))
        {
            turns.Add(new ChatTurn(ChatRole.User, question.Trim()));
            if (agent) turns.AddRange(await AgentTurns(question, model));
            else turns.Add(await KnowledgeTurn(question, model));
        }
        return View(turns, agent ? AssistantView.Agent : AssistantView.KnowledgeBase);
    }

    /// <summary>
    /// The buyer's answer to an approval card (<paramref name="decision"/> is "id:approve" or "id:reject"). The card is decided;
    /// an approved action runs (a purchase order is created or closed); the outcome is added to the conversation.
    /// </summary>
    public static async Task<AssistantView> DecideAsync(string? transcript, string? mode, string? decision)
    {
        var turns = ChatTranscript.FromText(transcript).ToList();
        var parts = (decision ?? "").Split(':');
        var index = parts.Length == 2 ? turns.FindIndex(t => t.Approval is { Status: ApprovalStatus.Pending } a && a.Id == parts[0]) : -1;
        if (index < 0)
        {
            turns.Add(new ChatTurn(ChatRole.Assistant, "That request is no longer waiting for approval."));
        }
        else
        {
            var approve = parts[1] == "approve";
            var request = turns[index].Approval!;
            turns[index] = turns[index] with { Approval = request.Decided(approve ? ApprovalStatus.Approved : ApprovalStatus.Rejected) };
            string outcome;
            if (request.Tool == CreatePurchaseOrder)
            {
                var state = await PurchaseOrderBook.DecideAsync(request.Input, approve);
                outcome = state is null ? $"{request.Input} is no longer waiting for approval." : $"{request.Input}: {Describe(state.Value)}.";
            }
            else
            {
                outcome = approve
                    ? await AgentRunner.ExecuteApproved(request.Decided(ApprovalStatus.Approved), Tools(new AssistantContext()))
                    : $"Rejected: {request.Title}.";
            }
            turns.Add(new ChatTurn(ChatRole.Assistant, outcome));
        }
        return View(turns, mode == AssistantView.Agent ? AssistantView.Agent : AssistantView.KnowledgeBase);
    }

    /// <summary>
    /// Adds uploaded files to the knowledge base. A file that is a purchase order also starts its approval: the process runs
    /// up to the buyer's decision, and an approval card asks for it.
    /// </summary>
    public static async Task<AssistantView> UploadAsync(string? transcript, string? mode, IReadOnlyList<(string Name, byte[] Bytes)> files)
    {
        var turns = ChatTranscript.FromText(transcript).ToList();
        foreach (var (name, bytes) in files)
        {
            var text = DocumentText.Read(name, bytes);
            if (text is null)
            {
                turns.Add(new ChatTurn(ChatRole.Assistant, $"{name} cannot be read. Use {DocumentText.Accept} files of up to 2 MB, in UTF-8."));
                continue;
            }
            var passages = Uploads.Add(name, text);
            turns.Add(new ChatTurn(ChatRole.Assistant, $"Added {name}: {passages} passage{(passages == 1 ? "" : "s")} in the knowledge base."));

            if (PurchaseOrderParser.Parse(text) is { } order)
            {
                var state = await PurchaseOrderBook.SubmitAsync(order);
                if (state == PurchaseOrderState.Waiting)
                {
                    var details = new List<ApprovalField> { new("Buyer", order.Buyer), new("Total", order.Total.ToString("N2", CultureInfo.InvariantCulture)) };
                    details.AddRange(order.Lines.Take(5).Select(l => new ApprovalField("Line", $"{l.Sku}: {l.Quantity} × {l.UnitPrice.ToString("N2", CultureInfo.InvariantCulture)}")));
                    var request = new ApprovalRequest(Guid.NewGuid().ToString("N")[..10], CreatePurchaseOrder, order.Number, $"Approve purchase order {order.Number} from {order.Buyer}", details);
                    turns.Add(new ChatTurn(ChatRole.Assistant, $"{order.Number} is ready for your approval.", Approval: request));
                }
            }
        }
        return View(turns, mode == AssistantView.Agent ? AssistantView.Agent : AssistantView.KnowledgeBase);
    }

    private static AssistantView View(IReadOnlyList<ChatTurn> turns, string mode) =>
        new(turns, ChatTranscript.ToText(turns), mode, Uploads.Documents, ModelDescription);

    private static ILanguageModel? _configured;
    private static string _description = "A rule-based stand-in model (no model is configured)";

    /// <summary>
    /// Chooses the model from configuration (read through <paramref name="setting"/>): <c>Assistant:Provider</c> is "anthropic" (with
    /// <c>Assistant:Anthropic:ApiKey</c>) or "openai" (with <c>Assistant:OpenAi:BaseUrl</c>, and an optional key); <c>Assistant:Model</c> names
    /// the model. Without a usable setting the rule-based stand-in is used, and the page says so.
    /// </summary>
    public static void Configure(Func<string, string?> setting)
    {
        var provider = (setting("Assistant:Provider") ?? "").Trim().ToLowerInvariant();
        var model = setting("Assistant:Model");
        if (provider == "anthropic" && setting("Assistant:Anthropic:ApiKey") is { Length: > 0 } key)
        {
            _configured = new AnthropicLanguageModel(new HttpClient(), key, string.IsNullOrWhiteSpace(model) ? "claude-sonnet-5-5" : model);
            _description = $"{(string.IsNullOrWhiteSpace(model) ? "claude-sonnet-5-5" : model)} (Anthropic)";
        }
        else if (provider == "openai" && setting("Assistant:OpenAi:BaseUrl") is { Length: > 0 } baseUrl && Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            var openAiKey = setting("Assistant:OpenAi:ApiKey");
            _configured = new OpenAiLanguageModel(new HttpClient(), uri, string.IsNullOrWhiteSpace(model) ? "default" : model, openAiKey);
            _description = $"{(string.IsNullOrWhiteSpace(model) ? "default" : model)} (OpenAI-compatible at {uri.Host})";
        }
        else
        {
            _configured = null;
            _description = "A rule-based stand-in model (no model is configured)";
        }
    }

    /// <summary>What answers the questions: the configured model, or the rule-based stand-in.</summary>
    public static ILanguageModel Model() => _configured ?? new AssistantModel();

    /// <summary>The model's name, for the page.</summary>
    public static string ModelDescription => _description;

    private static async Task<ChatTurn> KnowledgeTurn(string question, ILanguageModel model)
    {
        var answer = await RagAssistant.AnswerAsync(question, Search, model);
        return answer.Error is null
            ? new ChatTurn(ChatRole.Assistant, answer.Text, Sources: answer.Sources)
            : new ChatTurn(ChatRole.Assistant, answer.Error);
    }

    private static async Task<IReadOnlyList<ChatTurn>> AgentTurns(string question, ILanguageModel model)
    {
        var context = new AssistantContext();
        var result = await AgentRunner.RunAsync(question, Tools(context), model);
        var turns = new List<ChatTurn>();
        if (result.Approval is { } approval)
        {
            turns.Add(new ChatTurn(ChatRole.Assistant, result.Answer, Approval: approval, Trace: result.Steps.Select(s => $"{s.Tool}({s.Input}) → {s.Observation}").ToList()));
        }
        else
        {
            var trace = result.Steps.Select(s => $"{s.Tool}({s.Input}) → {s.Observation}").ToList();
            turns.Add(new ChatTurn(ChatRole.Assistant, result.Answer, result.Error is null ? null : [result.Error], Trace: trace));
        }
        turns.AddRange(context.Tables.Select(t => new ChatTurn(ChatRole.Assistant, "", Table: t)));
        return turns;
    }

    private static string Describe(PurchaseOrderState state) => state switch
    {
        PurchaseOrderState.Approved => "approved, and the order is created",
        PurchaseOrderState.Rejected => "rejected, and closed",
        _ => "waiting for approval",
    };

    /// <summary>"po 1001" and "PO1001" both read as PO-1001.</summary>
    public static string NormalisePo(string text)
    {
        var match = Regex.Match(text.ToUpperInvariant(), @"PO[- ]?(\d{3,})");
        return match.Success ? "PO-" + match.Groups[1].Value : text.Trim();
    }

    private static string Lookup(string input)
    {
        var reference = Regex.Match(input.ToUpperInvariant(), @"SHP-\d{6}").Value;
        if (reference.Length == 0) return "Give a reference such as SHP-000123.";
        var shipment = Shipments.Query.FirstOrDefault(s => s.Reference == reference);
        return shipment is null ? $"No shipment {reference}." : $"{shipment.Reference}: {shipment.Status}, {shipment.Amount.ToString("N2", CultureInfo.InvariantCulture)}, dated {shipment.Date:yyyy-MM-dd}.";
    }

    /// <summary>Searches the uploaded documents and the help centre, and ranks the passages together.</summary>
    private sealed class CombinedSearch : IKnowledgeSearch
    {
        public async Task<IReadOnlyList<KnowledgeChunk>> SearchAsync(string query, int limit, CancellationToken cancellationToken)
        {
            var uploaded = await Uploads.SearchAsync(query, limit, cancellationToken);
            return KeywordRanking.Rank(uploaded.Concat(HelpCenter.Articles), query, limit);
        }
    }
}

/// <summary>
/// The stand-in model of the assistant sample: it reads the two protocols (the sources in the knowledge prompt, the tool list in
/// the agent prompt) and answers from them with plain rules. It is not a language model; the page does not change when a real one
/// is put in.
/// </summary>
public sealed class AssistantModel : ILanguageModel
{
    public Task<string> CompleteAsync(string system, string user, int maxTokens, CancellationToken cancellationToken, string? model = null)
    {
        var question = Regex.Match(user, @"Question:\s*(.+?)(?:\n\nSteps so far:|$)", RegexOptions.Singleline).Groups[1].Value.Trim();
        var reply = system.Contains("Reply with JSON only", StringComparison.Ordinal) ? Agent(question, user) : Knowledge(user);
        return Task.FromResult(reply);
    }

    /// <summary>Quotes the sentence of the first source that shares the most words with the question.</summary>
    private static string Knowledge(string user)
    {
        var first = Regex.Match(user, @"\[1\] (.+?)\n([\s\S]+?)(?=\n\n\[\d+\] |\n\nQuestion:|\z)");
        if (!first.Success) return "I could not find that in the knowledge base.";
        var question = Regex.Match(user, @"Question:\s*(.+)$", RegexOptions.Singleline).Groups[1].Value;
        var words = KeywordRanking.Words(question);
        var sentence = Regex.Split(first.Groups[2].Value.Trim(), @"\n\s*\n|(?<=[.!?])\s+")
            .OrderByDescending(x => words.Intersect(KeywordRanking.Words(x)).Count())
            .First();
        return $"According to {first.Groups[1].Value.Trim()}: {sentence.Trim()} [1]";
    }

    private static string Agent(string question, string user)
    {
        var steps = Regex.Matches(user, @"^\d+\. (\w+)\(.*?\) -> (.*)$", RegexOptions.Multiline);
        var q = question.ToLowerInvariant();
        if (steps.Count == 0)
        {
            var upper = question.ToUpperInvariant();
            var po = Regex.Match(upper, @"PO[- ]?\d{3,}").Value;
            if (po.Length > 0 && Regex.IsMatch(q, @"approve|create|submit|order"))
                return Json(("tool", AssistantPage.CreatePurchaseOrder), ("input", AssistantPage.NormalisePo(po)));
            var shipment = Regex.Match(upper, @"SHP-\d{6}").Value;
            if (shipment.Length > 0) return Json(("tool", "lookup_order"), ("input", shipment));
            if (q.Contains("forecast") || q.Contains("predict"))
                return Json(("tool", "forecast_sales"), ("input", Shipments.Cities.FirstOrDefault(c => q.Contains(c.ToLowerInvariant())) ?? "all"));
            if (q.Contains("sales") || q.Contains("city") || q.Contains("cities"))
                return Json(("tool", "sales_by_city"), ("input", "5"));
            return Json(("tool", "search_knowledge"), ("input", question));
        }

        var last = steps[^1];
        var tool = last.Groups[1].Value;
        var observation = last.Groups[2].Value.Trim();
        if (tool != "search_knowledge") return Json(("answer", observation));
        if (observation.StartsWith("No passages", StringComparison.Ordinal)) return Json(("answer", "I could not find that in the knowledge base."));
        var passage = observation.Split(" | ")[0];
        var colon = passage.IndexOf(':');
        return Json(("answer", "From the knowledge base: " + (colon >= 0 ? passage[(colon + 1)..].Trim() : passage)));
    }

    private static string Json(params (string Key, string Value)[] pairs) =>
        "{" + string.Join(",", pairs.Select(p => $"\"{p.Key}\":{JsonSerializer.Serialize(p.Value)}")) + "}";
}
