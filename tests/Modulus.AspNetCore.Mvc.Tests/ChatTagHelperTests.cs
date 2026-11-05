using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.AspNetCore.Mvc.TagHelpers;
using Modulus.Ui.Abstractions.Chat;
using Xunit;

namespace Modulus.AspNetCore.Mvc.Tests;

/// <summary>&lt;app-chat&gt;: the same markup as the shared ChatHtml renderer, with the conversation and filter in hidden fields.</summary>
public class ChatTagHelperTests
{
    private static async Task<string> RunAsync(ChatTagHelper helper)
    {
        var output = new TagHelperOutput("app-chat", [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        await helper.ProcessAsync(new TagHelperContext("app-chat", [], new Dictionary<object, object>(), "id"), output);
        using var writer = new StringWriter();
        output.Content.WriteTo(writer, HtmlEncoder.Default);
        Assert.Equal("section", output.TagName);
        return writer.ToString();
    }

    [Fact]
    public async Task Turns_MatchTheSharedRenderer_AndPostBackTheConversation()
    {
        var turns = new[] { new ChatTurn(ChatRole.User, "orders over 500"), new ChatTurn(ChatRole.Assistant, "Showing Amount > 500", ["Left out: 1."]) };
        var html = await RunAsync(new ChatTagHelper { Turns = turns, Filter = "{\"kind\":\"group\"}", Placeholder = "Try" });

        Assert.Contains(ChatHtml.Log(turns), html);
        Assert.Contains("name=\"transcript\"", html);
        Assert.Contains("name=\"filter\"", html);
        Assert.Contains("name=\"question\"", html);
        Assert.Contains("Left out: 1.", html);
    }

    [Fact]
    public async Task NoTurns_ShowsTheEmptyLog()
    {
        var html = await RunAsync(new ChatTagHelper());
        Assert.Contains("Ask a question to start.", html);
        Assert.DoesNotContain("name=\"filter\"", html);
    }
}
