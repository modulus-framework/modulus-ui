using System.Text;
using Modulus.Ui.Abstractions.Assistant;
using Modulus.Ui.Abstractions.Chat;
using Xunit;

namespace Modulus.Blazor.Tests;

public class DocumentTextTests
{
    [Fact]
    public void Read_KeepsTextFormats_AndRefusesTheRest()
    {
        Assert.Equal("Brand: Modulus", DocumentText.Read("style.md", Encoding.UTF8.GetBytes("Brand: Modulus\r\n")));
        Assert.Null(DocumentText.Read("po.pdf", Encoding.UTF8.GetBytes("%PDF")));
        Assert.Null(DocumentText.Read("empty.txt", []));
        Assert.Null(DocumentText.Read("binary.txt", [0xff, 0xfe, 0x00, 0x81]));
        Assert.Null(DocumentText.Read("big.txt", new byte[DocumentText.MaxDocumentBytes + 1]));
    }

    [Fact]
    public void Chunk_SplitsAtParagraphs_AndNeverExceedsTheLength()
    {
        var text = string.Join("\n\n", Enumerable.Range(0, 30).Select(i => $"Paragraph {i}. " + new string('w', 120)));
        var chunks = DocumentText.Chunk(text);
        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.True(c.Length <= DocumentText.ChunkLength));
        Assert.Contains(chunks, c => c.Contains("Paragraph 29."));
    }

    [Fact]
    public void Chunk_CutsAVeryLongWordlessRunAtTheLength()
    {
        var chunks = DocumentText.Chunk(new string('x', DocumentText.ChunkLength * 2 + 5));
        Assert.Equal(3, chunks.Count);
        Assert.All(chunks, c => Assert.True(c.Length <= DocumentText.ChunkLength));
    }
}

public class KnowledgeStoreTests
{
    [Fact]
    public async Task AddedDocuments_AreFoundByTheirWords_AndAReplacementReplacesIt()
    {
        var store = new KnowledgeStore();
        store.Add("style-guide.md", "Buttons use the primary colour. Headings are sentence case.");
        store.Add("buyer-terms.txt", "Payment is due within 30 days. Late payment carries interest.");

        var found = await store.SearchAsync("when is payment due", 3, default);
        Assert.Equal("buyer-terms.txt", found[0].Title);
        Assert.Equal(2, store.Documents.Count);

        store.Add("buyer-terms.txt", "Payment is due within 45 days.");
        Assert.Single(store.Documents.Where(d => d.Name == "buyer-terms.txt"));
        Assert.Contains("45 days", (await store.SearchAsync("payment", 3, default))[0].Text);
    }

    [Fact]
    public async Task AQuestionWithNoSharedWords_FindsNothing()
    {
        var store = new KnowledgeStore();
        store.Add("a.txt", "Buttons use the primary colour.");
        Assert.Empty(await store.SearchAsync("zebra", 3, default));
        Assert.Empty(await store.SearchAsync("is it", 3, default));
    }
}

public class ApprovalAgentTests
{
    private sealed class Scripted(params string[] replies) : Modulus.Ui.Abstractions.Language.ILanguageModel
    {
        private int _n;
        public Task<string> CompleteAsync(string system, string user, int maxTokens, CancellationToken cancellationToken, string? model = null) =>
            Task.FromResult(replies[Math.Min(_n++, replies.Length - 1)]);
    }

    [Fact]
    public async Task AnApprovalTool_IsNotRun_UntilApproved()
    {
        var ran = 0;
        var tools = new[] { new AgentTool("create_order", "Creates the order for a PO.", (input, _) => { ran++; return Task.FromResult($"Created {input}."); }, RequiresApproval: true) };
        var model = new Scripted("{\"tool\":\"create_order\",\"input\":\"PO-1\"}");

        var result = await AgentRunner.RunAsync("create the order for PO-1", tools, model);

        Assert.Equal(0, ran);
        Assert.NotNull(result.Approval);
        Assert.Equal("create_order", result.Approval!.Tool);
        Assert.Equal("PO-1", result.Approval.Input);
        Assert.Equal(ApprovalStatus.Pending, result.Approval.Status);
        Assert.Empty(result.Steps);

        var rejected = await AgentRunner.ExecuteApproved(result.Approval.Decided(ApprovalStatus.Rejected), tools);
        Assert.Equal(0, ran);
        Assert.Contains("not approved", rejected);

        var approved = await AgentRunner.ExecuteApproved(result.Approval.Decided(ApprovalStatus.Approved), tools);
        Assert.Equal(1, ran);
        Assert.Equal("Created PO-1.", approved);
    }
}

public class ApprovalAndTableHtmlTests
{
    private static readonly Modulus.Ui.Abstractions.Language.ILanguageModel Unused = null!;

    [Fact]
    public void ApprovalCard_HasApproveAndReject_WhilePending_AndPostsTheIdAndDecision()
    {
        var request = new ApprovalRequest("abc123", "create_order", "PO-1", "Approve purchase order PO-1", [new ApprovalField("Total", "1,200.00")]);
        var html = ApprovalHtml.Card(request, "assistant-decide");

        Assert.Contains("value=\"abc123:approve\"", html);
        Assert.Contains("value=\"abc123:reject\"", html);
        Assert.Contains("form=\"assistant-decide\"", html);
        Assert.Contains("<dt>Total</dt><dd>1,200.00</dd>", html);
    }

    [Fact]
    public void ApprovalCard_HasNoButtons_OnceDecided()
    {
        var html = ApprovalHtml.Card(new ApprovalRequest("x", "t", "i", "Approve", [], ApprovalStatus.Approved));
        Assert.DoesNotContain("<button", html);
        Assert.Contains("Approved", html);
    }

    [Fact]
    public void TheLog_RendersTablesAndApprovalsInsideTheTurn()
    {
        var turn = new ChatTurn(ChatRole.Assistant, "Sales by city",
            Table: new ChatTable("Top cities", ["City", "Total"], [["Paris", "1,000.00"]]),
            Approval: new ApprovalRequest("id1", "t", "i", "Approve it", []));
        var html = ChatHtml.Log([turn], decisionFormId: "decide");

        Assert.Contains("<table class=\"ant-table m-chat-table\"><caption>Top cities</caption>", html);
        Assert.Contains("<td class=\"ant-table-cell\">Paris</td>", html);
        Assert.Contains("form=\"decide\"", html);
    }

    [Fact]
    public void TheTableAndApprovalSurviveTheTranscript()
    {
        var turns = new[] { new ChatTurn(ChatRole.Assistant, "x", Table: new ChatTable("T", ["A"], [["1"]]), Approval: new ApprovalRequest("i", "t", "in", "Approve", [new ApprovalField("k", "v")])) };
        var back = ChatTranscript.FromText(ChatTranscript.ToText(turns)).Single();
        Assert.Equal("1", back.Table!.Rows[0][0]);
        Assert.Equal("v", back.Approval!.Details[0].Value);
    }
}
