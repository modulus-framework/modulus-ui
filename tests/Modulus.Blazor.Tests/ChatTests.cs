using Modulus.Ui.Abstractions.Chat;
using Xunit;

namespace Modulus.Blazor.Tests;

public class ChatTranscriptTests
{
    [Fact]
    public void RoundTrips_TurnsAndTheirNotes()
    {
        var turns = new[] { new ChatTurn(ChatRole.User, "orders over 500"), new ChatTurn(ChatRole.Assistant, "Showing Amount > 500", ["1 condition left out."]) };
        var back = ChatTranscript.FromText(ChatTranscript.ToText(turns));
        Assert.Equal(turns.Length, back.Count);
        Assert.Equal(ChatRole.Assistant, back[1].Role);
        Assert.Equal("1 condition left out.", back[1].Notes!.Single());
    }

    [Fact]
    public void Missing_OrNotAConversation_IsEmpty_AndNeverThrows()
    {
        Assert.Empty(ChatTranscript.FromText(null));
        Assert.Empty(ChatTranscript.FromText("   "));
        Assert.Empty(ChatTranscript.FromText("<script>not json"));
        Assert.Empty(ChatTranscript.FromText("{\"kind\":\"group\"}"));
    }

    [Fact]
    public void KeepsTheNewestTurns_AndCutsLongTexts()
    {
        var many = Enumerable.Range(0, ChatTranscript.MaxTurns + 10).Select(i => new ChatTurn(ChatRole.User, "q" + i)).ToList();
        var kept = ChatTranscript.FromText(ChatTranscript.ToText(many));
        Assert.Equal(ChatTranscript.MaxTurns, kept.Count);
        Assert.Equal("q" + (ChatTranscript.MaxTurns + 9), kept[^1].Text);

        var long_ = ChatTranscript.FromText(ChatTranscript.ToText([new ChatTurn(ChatRole.User, new string('x', ChatTranscript.MaxTextLength + 500))]));
        Assert.Equal(ChatTranscript.MaxTextLength, long_[0].Text.Length);
    }
}

public class ChatHtmlTests
{
    [Fact]
    public void Log_EscapesEveryTextAndWritesTheNotes()
    {
        var html = ChatHtml.Log([new ChatTurn(ChatRole.User, "<b>x</b>"), new ChatTurn(ChatRole.Assistant, "ok", ["a & b"])]);
        Assert.Contains("&lt;b&gt;x&lt;/b&gt;", html);
        Assert.DoesNotContain("<b>x</b>", html);
        Assert.Contains("a &amp; b", html);
        Assert.Contains("role=\"log\"", html);
        Assert.Contains("m-chat-user", html);
        Assert.Contains("m-chat-assistant", html);
    }

    [Fact]
    public void Log_WithNoTurns_ShowsTheEmptyText()
    {
        Assert.Contains("Ask a question to start.", ChatHtml.Log([]));
    }

    [Fact]
    public void Composer_CarriesTheTranscriptAndTheFilterAsHiddenFields_AndLimitsTheQuestion()
    {
        var html = ChatHtml.Composer("[]", "{\"kind\":\"group\"}", name: "question", placeholder: "Try \"x\"");
        Assert.Contains("name=\"transcript\"", html);
        Assert.Contains("name=\"filter\" value=\"{&quot;kind&quot;:&quot;group&quot;}\"", html);
        Assert.Contains($"maxlength=\"{ChatTranscript.MaxTextLength}\"", html);
        Assert.Contains("placeholder=\"Try &quot;x&quot;\"", html);
    }
}
