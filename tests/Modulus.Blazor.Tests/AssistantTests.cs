using Modulus.Ui.Abstractions.Assistant;
using Modulus.Ui.Abstractions.Chat;
using Modulus.Ui.Abstractions.Language;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>Answers each call with the next scripted reply and keeps what it was asked.</summary>
internal sealed class ScriptedModel(params string[] replies) : ILanguageModel
{
    private int _next;
    public List<(string System, string User)> Calls { get; } = [];

    public Task<string> CompleteAsync(string system, string user, int maxTokens, CancellationToken cancellationToken, string? model = null)
    {
        Calls.Add((system, user));
        return Task.FromResult(replies[Math.Min(_next++, replies.Length - 1)]);
    }
}

internal sealed class FixedSearch(params KnowledgeChunk[] chunks) : IKnowledgeSearch
{
    public Task<IReadOnlyList<KnowledgeChunk>> SearchAsync(string query, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<KnowledgeChunk>>(chunks.Take(limit).ToList());
}

public class RagAssistantTests
{
    private static readonly KnowledgeChunk Refunds = new("r", "Refunds", "Refunds take 5 days.", "/help/refunds");
    private static readonly KnowledgeChunk Shipping = new("s", "Shipping", "Orders ship within 2 days.", "https://help.example.com/shipping");

    [Fact]
    public async Task AnswersFromTheSources_AndReturnsThemAsCitations()
    {
        var model = new ScriptedModel("Refunds take 5 days [1].");
        var answer = await RagAssistant.AnswerAsync("how long is a refund", new FixedSearch(Refunds, Shipping), model);

        Assert.Null(answer.Error);
        Assert.Equal("Refunds take 5 days [1].", answer.Text);
        Assert.Equal(["Refunds", "Shipping"], answer.Sources.Select(s => s.Title));
        Assert.Equal("/help/refunds", answer.Sources[0].Url);
        var user = Assert.Single(model.Calls).User;
        Assert.Contains("[1] Refunds", user);
        Assert.Contains("Question: how long is a refund", user);
    }

    [Fact]
    public async Task NothingFound_DoesNotAskTheModel()
    {
        var model = new ScriptedModel("never used");
        var answer = await RagAssistant.AnswerAsync("zebra", new FixedSearch(), model);

        Assert.Equal("I could not find that in the knowledge base.", answer.Text);
        Assert.Empty(answer.Sources);
        Assert.Empty(model.Calls);
    }

    [Fact]
    public async Task TheContextIsCut_ToTheLimit()
    {
        var big = new KnowledgeChunk("b", "Big", new string('x', RagAssistant.MaxContextLength - 10));
        var model = new ScriptedModel("ok");
        var answer = await RagAssistant.AnswerAsync("q", new FixedSearch(big, Refunds), model);

        Assert.Single(answer.Sources);
        Assert.DoesNotContain("Refunds take", Assert.Single(model.Calls).User);
    }

    [Fact]
    public async Task ALongQuestion_IsRefused_WithoutSearching()
    {
        var model = new ScriptedModel("ok");
        var answer = await RagAssistant.AnswerAsync(new string('q', RagAssistant.MaxQuestionLength + 1), new FixedSearch(Refunds), model);
        Assert.NotNull(answer.Error);
        Assert.Empty(model.Calls);
    }
}

public class AgentRunnerTests
{
    private static AgentTool Lookup(Func<string, string> run) => new("lookup", "Looks up an order by reference.", (input, _) => Task.FromResult(run(input)));

    [Fact]
    public async Task CallsAToolThenAnswers_WithTheStepsRecorded()
    {
        var model = new ScriptedModel("{\"tool\":\"lookup\",\"input\":\"SHP-000123\"}", "{\"answer\":\"It is delivered.\"}");
        var result = await AgentRunner.RunAsync("where is SHP-000123", [Lookup(i => "Delivered on 2026-01-02")], model);

        Assert.Equal("It is delivered.", result.Answer);
        var step = Assert.Single(result.Steps);
        Assert.Equal("lookup", step.Tool);
        Assert.Equal("SHP-000123", step.Input);
        Assert.Equal("Delivered on 2026-01-02", step.Observation);
        Assert.Contains("1. lookup(SHP-000123) -> Delivered on 2026-01-02", model.Calls[1].User);
    }

    [Fact]
    public async Task AnUnknownTool_IsNotRun_AndTheModelIsTold()
    {
        var ran = false;
        var model = new ScriptedModel("{\"tool\":\"delete_everything\",\"input\":\"x\"}", "{\"answer\":\"ok\"}");
        var result = await AgentRunner.RunAsync("q", [Lookup(i => { ran = true; return ""; })], model);

        Assert.False(ran);
        Assert.Contains("no tool named 'delete_everything'", result.Steps[0].Observation);
        Assert.Equal("ok", result.Answer);
    }

    [Fact]
    public async Task AToolThatThrows_ReportsItsFailureAsTheResult()
    {
        var failing = new AgentTool("lookup", "x", (_, _) => throw new InvalidOperationException("the index is down"));
        var model = new ScriptedModel("{\"tool\":\"lookup\",\"input\":\"a\"}", "{\"answer\":\"sorry\"}");
        var result = await AgentRunner.RunAsync("q", [failing], model);

        Assert.Equal("The tool failed: the index is down", result.Steps[0].Observation);
        Assert.Equal("sorry", result.Answer);
    }

    [Fact]
    public async Task TheStepLimit_StopsTheRun()
    {
        var model = new ScriptedModel("{\"tool\":\"lookup\",\"input\":\"again\"}");
        var result = await AgentRunner.RunAsync("q", [Lookup(_ => "still looking")], model, maxSteps: 3);

        Assert.Equal(3, result.Steps.Count);
        Assert.Equal("Stopped after 3 steps.", result.Error);
        Assert.Equal(3, model.Calls.Count);
    }

    [Fact]
    public async Task AReplyThatIsNotTheProtocol_IsTheAnswer()
    {
        var model = new ScriptedModel("Just a plain sentence.");
        var result = await AgentRunner.RunAsync("q", [Lookup(_ => "x")], model);
        Assert.Equal("Just a plain sentence.", result.Answer);
        Assert.Empty(result.Steps);
    }

    [Fact]
    public async Task AnOverlongInput_IsCutBeforeTheTool_AndTheObservationIsCut()
    {
        var seen = "";
        var tool = Lookup(i => { seen = i; return new string('o', AgentRunner.MaxObservationLength + 50); });
        var model = new ScriptedModel("{\"tool\":\"lookup\",\"input\":\"" + new string('i', AgentRunner.MaxInputLength + 50) + "\"}", "{\"answer\":\"done\"}");
        var result = await AgentRunner.RunAsync("q", [tool], model);

        Assert.Equal(AgentRunner.MaxInputLength + 1, seen.Length);
        Assert.Equal(AgentRunner.MaxObservationLength + 1, result.Steps[0].Observation.Length);
    }

    [Fact]
    public void TheInstruction_ListsOnlyTheGivenTools()
    {
        var text = AgentRunner.Instruction([Lookup(_ => "")]);
        Assert.Contains("- lookup: Looks up an order by reference.", text);
        Assert.DoesNotContain("delete", text);
    }
}

public class ChatSourcesAndTraceTests
{
    [Fact]
    public void Sources_AreLinked_OnlyWhenTheLinkIsSafe()
    {
        var html = ChatHtml.Log([new ChatTurn(ChatRole.Assistant, "a",
            Sources: [new ChatSource("Good", "/help/x", "e1"), new ChatSource("Abs", "https://help.example.com/y", "e2"), new ChatSource("Bad", "javascript:alert(1)", "e3"), new ChatSource("Proto", "//evil.example", "e4")])]);

        Assert.Contains("<a href=\"/help/x\" rel=\"noopener\">Good</a>", html);
        Assert.Contains("<a href=\"https://help.example.com/y\" rel=\"noopener\">Abs</a>", html);
        Assert.DoesNotContain("href=\"javascript", html);
        Assert.DoesNotContain("href=\"//evil", html);
        Assert.Contains(">Bad<", html);
    }

    [Fact]
    public void Trace_IsACollapsibleList_AndItsTextIsEscaped()
    {
        var html = ChatHtml.Log([new ChatTurn(ChatRole.Assistant, "a", Trace: ["search(<b>x</b>) -> found"])]);
        Assert.Contains("<details class=\"m-chat-trace\"><summary>Steps (1)</summary>", html);
        Assert.Contains("search(&lt;b&gt;x&lt;/b&gt;)", html);
    }

    [Fact]
    public void SourcesAndTrace_SurviveTheTranscript()
    {
        var turns = new[] { new ChatTurn(ChatRole.Assistant, "a", Sources: [new ChatSource("T", "/t", "ex")], Trace: ["step"]) };
        var back = ChatTranscript.FromText(ChatTranscript.ToText(turns)).Single();
        Assert.Equal("T", back.Sources!.Single().Title);
        Assert.Equal("step", back.Trace!.Single());
    }
}
