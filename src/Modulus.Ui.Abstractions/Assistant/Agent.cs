using System.Text.Json;
using Modulus.Ui.Abstractions.Language;

namespace Modulus.Ui.Abstractions.Assistant;

/// <summary>
/// A tool an agent may call: a name the model uses, what it does, and the function that runs it on the model's input. A tool with
/// <paramref name="RequiresApproval"/> is not run by the agent: the run stops with an <see cref="ApprovalRequest"/> that a person
/// approves or rejects, and only an approved request is run (<see cref="AgentRunner.ExecuteApproved"/>).
/// </summary>
public sealed record AgentTool(string Name, string Description, Func<string, CancellationToken, Task<string>> Run, bool RequiresApproval = false);

/// <summary>Where an approval request stands.</summary>
public enum ApprovalStatus
{
    Pending,
    Approved,
    Rejected,
}

/// <summary>A label and a value shown on an approval request (the buyer, the total, the quantity).</summary>
public sealed record ApprovalField(string Label, string Value);

/// <summary>
/// An action a person must approve before it runs: what it is, the tool and input that would run, the details to check, and
/// its status. The id is what the approve and reject buttons send back.
/// </summary>
public sealed record ApprovalRequest(string Id, string Tool, string Input, string Title, IReadOnlyList<ApprovalField> Details, ApprovalStatus Status = ApprovalStatus.Pending)
{
    public ApprovalRequest Decided(ApprovalStatus status) => this with { Status = status };
}

/// <summary>One tool call and what it returned.</summary>
public sealed record AgentStep(int Number, string Tool, string Input, string Observation);

/// <summary>The outcome of an agent run: the answer, the steps taken, and why it stopped early (or null).</summary>
/// <param name="Approval">Set when the run stopped at a tool that needs a person's approval; nothing was run for it.</param>
public sealed record AgentResult(string Answer, IReadOnlyList<AgentStep> Steps, string? Error, ApprovalRequest? Approval = null);

/// <summary>
/// An agent: the model decides, one step at a time, whether to call one of the tools it is given or to answer. The
/// tools are an allowlist (the model cannot name one that is not listed), inputs and results are cut to a length, a tool that fails
/// reports its failure as the result, and the run stops after a fixed number of steps. A reply that is not the JSON protocol is
/// taken as the answer.
/// </summary>
public static class AgentRunner
{
    public const int DefaultMaxSteps = 5;
    public const int MaxQuestionLength = 500;
    public const int MaxInputLength = 300;
    public const int MaxObservationLength = 1000;

    /// <summary>The instruction: the tools, and the two replies the model may give.</summary>
    public static string Instruction(IReadOnlyList<AgentTool> tools)
    {
        var lines = tools.Select(t => $"- {t.Name}: {t.Description}");
        return "You answer a question, using tools when they help. Reply with JSON only. To call a tool: {\"tool\":\"name\",\"input\":\"text\"}. " +
            "When you know the answer: {\"answer\":\"text\"}. Use only these tools:\n" + string.Join("\n", lines);
    }

    public static async Task<AgentResult> RunAsync(string question, IReadOnlyList<AgentTool> tools, ILanguageModel model, int maxSteps = DefaultMaxSteps, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question)) return new("", [], "Ask a question first.");
        if (question.Length > MaxQuestionLength) return new("", [], $"Ask in fewer than {MaxQuestionLength} characters.");
        var limit = Math.Clamp(maxSteps, 1, 10);
        var steps = new List<AgentStep>();
        var system = Instruction(tools);

        for (var number = 1; number <= limit; number++)
        {
            var soFar = steps.Count == 0 ? "" : "\n\nSteps so far:\n" + string.Join("\n", steps.Select(s => $"{s.Number}. {s.Tool}({s.Input}) -> {s.Observation}"));
            var reply = await model.CompleteAsync(system, $"Question: {question.Trim()}{soFar}", 400, cancellationToken);

            if (!Decision(reply, out var answer, out var toolName, out var input))
                return new(reply.Trim(), steps, null);
            if (answer is not null)
                return new(answer, steps, null);

            var tool = tools.FirstOrDefault(t => string.Equals(t.Name, toolName, StringComparison.OrdinalIgnoreCase));
            if (tool is { RequiresApproval: true })
            {
                var request = new ApprovalRequest(
                    Guid.NewGuid().ToString("N")[..10],
                    tool.Name,
                    Cut(input ?? "", MaxInputLength),
                    tool.Description,
                    [new ApprovalField("Tool", tool.Name), new ApprovalField("Input", Cut(input ?? "", MaxInputLength))]);
                return new AgentResult("I need your approval before I do that.", steps, null, request);
            }
            var observation = await Observe(tool, toolName, Cut(input ?? "", MaxInputLength), cancellationToken);
            steps.Add(new AgentStep(number, toolName ?? "", Cut(input ?? "", MaxInputLength), observation));
        }
        return new("I could not reach an answer within the step limit.", steps, $"Stopped after {limit} steps.");
    }

    /// <summary>Runs an approved request's tool and returns what it produced (or why it failed). Only approved requests run.</summary>
    public static async Task<string> ExecuteApproved(ApprovalRequest request, IReadOnlyList<AgentTool> tools, CancellationToken cancellationToken = default)
    {
        if (request.Status != ApprovalStatus.Approved) return "Nothing ran: the request was not approved.";
        var tool = tools.FirstOrDefault(t => string.Equals(t.Name, request.Tool, StringComparison.OrdinalIgnoreCase));
        return await Observe(tool, request.Tool, request.Input, cancellationToken);
    }

    private static async Task<string> Observe(AgentTool? tool, string? name, string input, CancellationToken cancellationToken)
    {
        if (tool is null) return $"There is no tool named '{name}'.";
        try
        {
            return Cut(await tool.Run(input, cancellationToken), MaxObservationLength);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return "The tool failed: " + e.Message;
        }
    }

    /// <summary>Reads a reply in the protocol. False when the reply is not a JSON object with an answer or a tool.</summary>
    private static bool Decision(string reply, out string? answer, out string? tool, out string? input)
    {
        answer = tool = input = null;
        var start = reply.IndexOf('{');
        var end = reply.LastIndexOf('}');
        if (start < 0 || end <= start) return false;
        try
        {
            using var json = JsonDocument.Parse(reply[start..(end + 1)]);
            var root = json.RootElement;
            if (root.TryGetProperty("answer", out var a) && a.ValueKind == JsonValueKind.String)
            {
                answer = a.GetString() ?? "";
                return true;
            }
            if (root.TryGetProperty("tool", out var t) && t.ValueKind == JsonValueKind.String)
            {
                tool = t.GetString();
                input = root.TryGetProperty("input", out var i) && i.ValueKind == JsonValueKind.String ? i.GetString() : "";
                return true;
            }
        }
        catch (JsonException)
        {
            // Not the protocol: the reply is taken as text.
        }
        return false;
    }

    private static string Cut(string text, int length) => text.Length <= length ? text : text[..length] + "…";
}
