namespace Modulus.Ui.Abstractions.Language;

/// <summary>
/// A language model the framework calls: the AI nodes of a workflow and the natural-language filter. The app supplies one
/// (<c>Modulus.Flow.Connectors</c> has clients for Anthropic and OpenAI-compatible servers, or any other implementation).
/// The reply is text; nothing the framework does with it runs as code.
/// </summary>
public interface ILanguageModel
{
    /// <summary>
    /// One request: an instruction (<paramref name="system"/>), the input (<paramref name="user"/>), and a limit on the reply.
    /// <paramref name="model"/> picks a model by name; null uses the client's default.
    /// </summary>
    Task<string> CompleteAsync(string system, string user, int maxTokens, CancellationToken cancellationToken, string? model = null);
}
