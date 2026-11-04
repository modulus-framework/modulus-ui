using System.Net;
using System.Text;
using System.Text.Json;
using Modulus.Flow.Connectors;
using Modulus.Ui.Abstractions.Flow;
using Modulus.Ui.Abstractions.Language;
using Xunit;

namespace Modulus.Flow.Connectors.Tests;

/// <summary>Answers each request with a canned response and keeps what was sent.</summary>
internal sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
{
    public HttpRequestMessage? Request { get; private set; }
    public string? RequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Request = request;
        RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}

internal sealed class FakeModel : ILanguageModel
{
    public string? System { get; private set; }
    public string? User { get; private set; }
    public string? Model { get; private set; }

    public Task<string> CompleteAsync(string system, string user, int maxTokens, CancellationToken cancellationToken, string? model = null)
    {
        System = system;
        User = user;
        Model = model;
        return Task.FromResult("A reply");
    }
}

internal sealed class FakeEmail : IEmailSender
{
    public List<EmailMessage> Sent { get; } = [];

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        Sent.Add(message);
        return Task.CompletedTask;
    }
}

public class LanguageModelTests
{
    private const string AnthropicReply = """{"id":"m1","content":[{"type":"text","text":"Hello "},{"type":"text","text":"there"}]}""";

    [Fact]
    public async Task Anthropic_SendsTheKeyVersionAndBody_AndReadsTheText()
    {
        var handler = new StubHandler(HttpStatusCode.OK, AnthropicReply);
        var model = new AnthropicLanguageModel(new HttpClient(handler), "key-123", "claude-default");

        var reply = await model.CompleteAsync("Be brief.", "Hi", 300, default, "claude-other");

        Assert.Equal("Hello there", reply);
        Assert.Equal("key-123", handler.Request!.Headers.GetValues("x-api-key").Single());
        Assert.Equal("2023-06-01", handler.Request.Headers.GetValues("anthropic-version").Single());
        using var body = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("claude-other", body.RootElement.GetProperty("model").GetString());
        Assert.Equal(300, body.RootElement.GetProperty("max_tokens").GetInt32());
        Assert.Equal("Be brief.", body.RootElement.GetProperty("system").GetString());
        Assert.Equal("Hi", body.RootElement.GetProperty("messages")[0].GetProperty("content").GetString());
    }

    [Fact]
    public async Task Anthropic_UsesTheDefaultModelWhenTheNodeNamesNone()
    {
        var handler = new StubHandler(HttpStatusCode.OK, AnthropicReply);
        await new AnthropicLanguageModel(new HttpClient(handler), "k", "claude-default").CompleteAsync("s", "u", 10, default, " ");
        using var body = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("claude-default", body.RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public async Task AFailedAnswer_NamesTheStatus_AndNeverTheKey()
    {
        var handler = new StubHandler(HttpStatusCode.Unauthorized, """{"error":"invalid x-api-key"}""");
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            new AnthropicLanguageModel(new HttpClient(handler), "secret-key-value", "m").CompleteAsync("s", "u", 10, default));
        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.Contains("401", ex.Message);
        Assert.DoesNotContain("secret-key-value", ex.Message);
    }

    [Fact]
    public async Task OpenAi_PostsToTheChatPath_WithTheOptionalKey_AndReadsTheChoice()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"choices":[{"message":{"role":"assistant","content":"Done"}}]}""");
        var model = new OpenAiLanguageModel(new HttpClient(handler), new Uri("http://localhost:11434/v1/"), "llama-default", "tok");

        var reply = await model.CompleteAsync("Sys", "User", 50, default);

        Assert.Equal("Done", reply);
        Assert.Equal("http://localhost:11434/v1/chat/completions", handler.Request!.RequestUri!.ToString());
        Assert.Equal("Bearer tok", handler.Request.Headers.Authorization!.ToString());
        using var body = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("llama-default", body.RootElement.GetProperty("model").GetString());
        Assert.Equal("system", body.RootElement.GetProperty("messages")[0].GetProperty("role").GetString());
    }

    [Fact]
    public async Task OpenAi_WithoutAKey_SendsNoAuthorization()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"choices":[{"message":{"content":"x"}}]}""");
        await new OpenAiLanguageModel(new HttpClient(handler), new Uri("http://localhost:1234/v1/"), "m").CompleteAsync("s", "u", 5, default);
        Assert.Null(handler.Request!.Headers.Authorization);
    }
}

public class FlowConnectorTests
{
    private static FlowGraph Chain(string type, Dictionary<string, string> config) => new(
        [new("t", "trigger.manual", 0, 0), new("n", type, 0, 0) { Config = config }],
        [new("e", "t", "n")]);

    [Fact]
    public async Task ChatNode_AnswersTheMessage_WithItsPromptAsTheInstruction()
    {
        var model = new FakeModel();
        var runner = new FlowRunner().UseLanguageModel(model);
        var graph = Chain("ai.chat", new() { ["prompt"] = "Reply to {{customer.name}}.", ["model"] = "claude-other" });

        var run = await runner.RunAsync(graph, [FlowItem.Of(("message", "Where is my order?"), ("customer", new Dictionary<string, object?> { ["name"] = "Ada" }))]);

        Assert.Equal(FlowStatus.Success, run.Status);
        Assert.Equal("Reply to Ada.", model.System);
        Assert.Equal("Where is my order?", model.User);
        Assert.Equal("claude-other", model.Model);
        Assert.Equal("A reply", run.Step("n")!.Outputs.Values.SelectMany(v => v).Single().Text("reply"));
    }

    [Fact]
    public async Task ChatNode_WithoutAMessage_FailsTheNode()
    {
        var runner = new FlowRunner().UseLanguageModel(new FakeModel());
        var run = await runner.RunAsync(Chain("ai.chat", new() { ["prompt"] = "x" }), [FlowItem.Of(("other", 1))]);
        Assert.Equal(FlowStatus.Error, run.Step("n")!.Status);
    }

    [Fact]
    public async Task EmailNode_SendsToTheAddressWithItsSubjectAndBody()
    {
        var email = new FakeEmail();
        var runner = new FlowRunner().UseEmail(email);
        var graph = Chain("action.email", new() { ["to"] = "{{customer.email}}", ["subject"] = "Re: {{message}}" });

        var run = await runner.RunAsync(graph, [FlowItem.Of(("message", "refund"), ("reply", "We will refund you."), ("customer", new Dictionary<string, object?> { ["email"] = "ada@example.com" }))]);

        Assert.Equal(FlowStatus.Success, run.Status);
        var sent = Assert.Single(email.Sent);
        Assert.Equal("ada@example.com", sent.To);
        Assert.Equal("Re: refund", sent.Subject);
        Assert.Equal("We will refund you.", sent.Body);
    }

    [Theory]
    [InlineData("not an address")]
    [InlineData("")]
    public async Task EmailNode_WithoutAValidAddress_FailsAndSendsNothing(string to)
    {
        var email = new FakeEmail();
        var runner = new FlowRunner().UseEmail(email);
        var run = await runner.RunAsync(Chain("action.email", new() { ["to"] = to }), [FlowItem.Of(("reply", "x"))]);
        Assert.Equal(FlowStatus.Error, run.Step("n")!.Status);
        Assert.Empty(email.Sent);
    }

    [Fact]
    public async Task SlackNode_PostsTheReplyToTheWebhook()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "ok");
        var runner = new FlowRunner().UseSlack(new SlackWebhook(new HttpClient(handler), new Uri("https://hooks.example.com/services/T/B/X")));

        var run = await runner.RunAsync(Chain("action.slack", new() { ["channel"] = "#support" }), [FlowItem.Of(("reply", "Refund approved"))]);

        Assert.Equal(FlowStatus.Success, run.Step("n")!.Status);
        using var body = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("Refund approved", body.RootElement.GetProperty("text").GetString());
    }

    [Fact]
    public async Task SlackNode_AWebhookError_FailsTheNode()
    {
        var handler = new StubHandler(HttpStatusCode.NotFound, "no_team");
        var runner = new FlowRunner().UseSlack(new SlackWebhook(new HttpClient(handler), new Uri("https://hooks.example.com/x")));
        var run = await runner.RunAsync(Chain("action.slack", new()), [FlowItem.Of(("reply", "x"))]);
        Assert.Equal(FlowStatus.Error, run.Step("n")!.Status);
        Assert.Contains("404", run.Step("n")!.Error);
    }
}
