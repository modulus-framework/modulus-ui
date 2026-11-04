using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.AspNetCore.Mvc.TagHelpers;
using Modulus.Ui.Abstractions.Assistant;
using Modulus.Ui.Abstractions.Chat;
using Xunit;

namespace Modulus.AspNetCore.Mvc.Tests;

public class AssistantTagHelperTests
{
    private static async Task<string> RunAsync(TagHelper helper, string tag)
    {
        var output = new TagHelperOutput(tag, [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        await helper.ProcessAsync(new TagHelperContext(tag, [], new Dictionary<object, object>(), "id"), output);
        using var writer = new StringWriter();
        output.Content.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    [Fact]
    public async Task DocumentUpload_IsAMultipartFileField_ListingTheDocuments()
    {
        var html = await RunAsync(new DocumentUploadTagHelper { Name = "documents", Documents = [new KnowledgeDocument("style.md", 3)] }, "app-document-upload");

        Assert.Equal(UploadHtml.Field("documents", [new KnowledgeDocument("style.md", 3)]), html.Replace("<section class=\"m-upload-section\">", "").Replace("</section>", ""));
        Assert.Contains("type=\"file\" name=\"documents\" multiple", html);
        Assert.Contains("style.md", html);
        Assert.Contains("3 passages", html);
    }

    [Fact]
    public async Task ApprovalCard_PostsTheDecisionToTheNamedForm()
    {
        var html = await RunAsync(new ApprovalCardTagHelper { Request = new ApprovalRequest("a1", "create_purchase_order", "PO-1", "Approve PO-1", [new ApprovalField("Total", "10.00")]), Form = "assistant-decide" }, "app-approval-card");

        Assert.Contains("form=\"assistant-decide\"", html);
        Assert.Contains("value=\"a1:approve\"", html);
        Assert.Contains("<dd>10.00</dd>", html);
    }

    [Fact]
    public async Task Chat_WithADecisionForm_PutsTheApprovalButtonsInIt()
    {
        var turns = new[] { new ChatTurn(ChatRole.Assistant, "ready", Approval: new ApprovalRequest("b2", "t", "i", "Approve", [])) };
        var html = await RunAsync(new ChatTagHelper { Turns = turns, DecisionForm = "assistant-decide" }, "app-chat");

        Assert.Contains("form=\"assistant-decide\"", html);
        Assert.Contains("name=\"question\"", html);
    }
}
