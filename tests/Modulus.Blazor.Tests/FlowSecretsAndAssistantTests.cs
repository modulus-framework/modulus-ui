using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Flow;

using Modulus.Ui.Abstractions.Language;
using Modulus.Ui.Abstractions.Query;
using Xunit;

namespace Modulus.Blazor.Tests;

public class FlowSecretTests
{
    private static readonly Dictionary<string, string> Vault = new() { ["mail"] = "s3cret-value" };
    private static string? Secret(string name) => Vault.TryGetValue(name, out var v) ? v : null;

    [Fact]
    public void RenderSecrets_ReplacesOnlySecretSlots()
    {
        Assert.Equal("key=s3cret-value {{customer.email}}", FlowTemplate.RenderSecrets("key={{secret.mail}} {{customer.email}}", Secret));
    }

    [Fact]
    public void Render_FillsSecretsAndItemValuesInOnePass()
    {
        var item = FlowItem.Of(("customer", new Dictionary<string, object?> { ["email"] = "ada@example.com" }));
        Assert.Equal("s3cret-value to ada@example.com", FlowTemplate.Render("{{secret.mail}} to {{customer.email}}", item, Secret));
    }

    [Fact]
    public void CustomerText_CannotPullASecretIn()
    {
        var item = FlowItem.Of(("message", "please use {{secret.mail}} now"));
        // The message is inserted as text; its own slot is not scanned again.
        Assert.Equal("please use {{secret.mail}} now", FlowTemplate.Render("{{message}}", item, Secret));
    }

    [Fact]
    public void AMissingSecret_FailsWithItsNameAndNoValue()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => FlowTemplate.RenderSecrets("{{secret.smtp}}", Secret));
        Assert.Contains("smtp", ex.Message);
        Assert.DoesNotContain("s3cret", ex.Message);
    }

    [Fact]
    public async Task ARunNode_ReceivesTheSecret_AndAMissingOneFailsTheNode()
    {
        var runner = new FlowRunner().OnEach("action.email", (ctx, item, _) => Task.FromResult(item.With("sent", ctx.Setting("password", item))));
        var graph = new FlowGraph(
            [new("t", "trigger.manual", 0, 0), new("a", "action.email", 0, 0) { Config = new Dictionary<string, string> { ["password"] = "{{secret.mail}}" } }],
            [new("e", "t", "a")]);

        var ok = await runner.RunAsync(graph, [FlowItem.Of(("x", 1))], new FlowRunOptions { Secret = Secret });
        Assert.Equal("s3cret-value", ok.Step("a")!.Outputs.Values.SelectMany(v => v).Single().Text("sent"));

        var missing = await runner.RunAsync(graph, [FlowItem.Of(("x", 1))], new FlowRunOptions { Secret = _ => null });
        Assert.Equal(FlowStatus.Error, missing.Step("a")!.Status);
        Assert.DoesNotContain("s3cret", missing.Step("a")!.Error ?? "");
    }
}

public class FilterAssistantTests
{
    private sealed record Order(decimal Total, string Status, DateOnly Placed, string Customer);

    private static readonly QueryBuilder<Order> Builder = QueryBuilder<Order>.Create()
        .Number("total", "Total", o => o.Total)
        .Choice("status", "Status", o => o.Status, [new GridOption("open", "Open"), new GridOption("shipped", "Shipped")])
        .Date("placed", "Placed", o => o.Placed)
        .Text("customer", "Customer", o => o.Customer);

    private sealed class FakeModel(string reply) : ILanguageModel
    {
        public string? System { get; private set; }
        public string? User { get; private set; }
        public int Calls { get; private set; }

        public Task<string> CompleteAsync(string system, string user, int maxTokens, CancellationToken cancellationToken, string? model = null)
        {
            Calls++;
            System = system;
            User = user;
            return Task.FromResult(reply);
        }
    }

    private const string Clean = """{"kind":"group","join":"and","items":[{"kind":"rule","field":"total","operator":"greaterThan","value":"500"},{"kind":"rule","field":"status","operator":"in","value":"open|shipped"}]}""";

    [Fact]
    public async Task AValidReply_BecomesTheFilter()
    {
        var result = await FilterAssistant.TranslateAsync(Builder, "orders over 500 that are open or shipped", new FakeModel(Clean), new DateOnly(2026, 11, 4));
        Assert.Null(result.Error);
        Assert.Empty(result.Notes);
        var rules = Assert.IsType<FilterGroup>(result.Filter).Items.Cast<FilterRule>().ToList();
        Assert.Equal(new[] { "total", "status" }, rules.Select(r => r.Field).ToArray());
        Assert.Equal(FilterOperator.GreaterThan, rules[0].Operator);
    }

    [Fact]
    public async Task AReplyWrappedInProse_IsStillRead()
    {
        var reply = "Here is the filter:\n```json\n" + Clean + "\n```\nLet me know.";
        var result = await FilterAssistant.TranslateAsync(Builder, "big orders", new FakeModel(reply), new DateOnly(2026, 11, 4));
        Assert.Null(result.Error);
        Assert.Equal(2, Assert.IsType<FilterGroup>(result.Filter).Items.Count);
    }

    [Fact]
    public async Task AnInventedFieldOrOperator_IsDroppedAndReported()
    {
        var reply = """{"kind":"group","join":"and","items":[{"kind":"rule","field":"colour","operator":"equals","value":"red"},{"kind":"rule","field":"total","operator":"contains","value":"5"},{"kind":"rule","field":"total","operator":"lessThan","value":"9"}]}""";
        var result = await FilterAssistant.TranslateAsync(Builder, "x", new FakeModel(reply), new DateOnly(2026, 11, 4));
        Assert.Single(Assert.IsType<FilterGroup>(result.Filter).Items);
        Assert.Equal("2 conditions left out: the field or operator is not available here.", result.Notes.Single());
    }

    [Fact]
    public async Task ARestrictedField_StaysOut()
    {
        var restricted = QueryBuilder<Order>.Create().Number("total", "Total", o => o.Total).Text("customer", "Customer", o => o.Customer).Restrict(k => k != "customer");
        var reply = """{"kind":"group","join":"and","items":[{"kind":"rule","field":"customer","operator":"contains","value":"ada"},{"kind":"rule","field":"total","operator":"greaterThan","value":"1"}]}""";
        var result = await FilterAssistant.TranslateAsync(restricted, "x", new FakeModel(reply), new DateOnly(2026, 11, 4));
        Assert.Equal("total", Assert.IsType<FilterRule>(Assert.IsType<FilterGroup>(result.Filter).Items.Single()).Field);
    }

    [Fact]
    public async Task AReplyWithoutAFilter_IsAnError()
    {
        var result = await FilterAssistant.TranslateAsync(Builder, "x", new FakeModel("I cannot help with that."), new DateOnly(2026, 11, 4));
        Assert.NotNull(result.Error);
        Assert.Empty(result.Filter.Items);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AnEmptyQuestion_IsRefusedWithoutAskingTheModel(string question)
    {
        var model = new FakeModel(Clean);
        var result = await FilterAssistant.TranslateAsync(Builder, question, model, new DateOnly(2026, 11, 4));
        Assert.NotNull(result.Error);
        Assert.Equal(0, model.Calls);
    }

    [Fact]
    public async Task ALongQuestion_IsRefused()
    {
        var model = new FakeModel(Clean);
        var result = await FilterAssistant.TranslateAsync(Builder, new string('a', FilterAssistant.MaxQuestionLength + 1), model, new DateOnly(2026, 11, 4));
        Assert.NotNull(result.Error);
        Assert.Equal(0, model.Calls);
    }

    [Fact]
    public async Task TheInstruction_ListsOnlyTheFieldsWithTheirOperatorsAndOptions_AndTheDate()
    {
        var model = new FakeModel(Clean);
        await FilterAssistant.TranslateAsync(Builder, "orders", model, new DateOnly(2026, 11, 4));
        Assert.Contains("Today is 2026-11-04", model.System);
        Assert.Contains("- status (Status, choice): in, notIn, equals, notEquals. Options: open | shipped", model.System);
        Assert.Contains("- total (Total, number): equals, notEquals, greaterThan", model.System);
        Assert.Equal("orders", model.User);
    }
}
