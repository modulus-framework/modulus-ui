using System.Net.Http.Json;
using System.Text.Json;
using Modulus.Ui.Abstractions.Language;

namespace Modulus.Flow.Connectors;

/// <summary>Anthropic's Messages API. The key and the default model come from the app's configuration.</summary>
public sealed class AnthropicLanguageModel(HttpClient http, string apiKey, string defaultModel, Uri? endpoint = null) : ILanguageModel
{
    private static readonly Uri DefaultEndpoint = new("https://api.anthropic.com/v1/messages");

    public async Task<string> CompleteAsync(string system, string user, int maxTokens, CancellationToken cancellationToken, string? model = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint ?? DefaultEndpoint)
        {
            Content = JsonContent.Create(new
            {
                model = string.IsNullOrWhiteSpace(model) ? defaultModel : model,
                max_tokens = maxTokens,
                system,
                messages = new[] { new { role = "user", content = user } },
            }),
        };
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw LanguageModelFailure.For(response, body);

        using var json = JsonDocument.Parse(body);
        return string.Concat(json.RootElement.GetProperty("content").EnumerateArray()
            .Where(block => block.TryGetProperty("type", out var type) && type.GetString() == "text")
            .Select(block => block.GetProperty("text").GetString()));
    }
}

/// <summary>
/// Any server with the OpenAI chat completions API (OpenAI itself, or a local or hosted server that speaks it). The base
/// address is the one the server documents, ending in <c>/v1/</c>; the key is optional for local servers.
/// </summary>
public sealed class OpenAiLanguageModel(HttpClient http, Uri baseAddress, string defaultModel, string? apiKey = null) : ILanguageModel
{
    public async Task<string> CompleteAsync(string system, string user, int maxTokens, CancellationToken cancellationToken, string? model = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseAddress, "chat/completions"))
        {
            Content = JsonContent.Create(new
            {
                model = string.IsNullOrWhiteSpace(model) ? defaultModel : model,
                max_tokens = maxTokens,
                messages = new[] { new { role = "system", content = system }, new { role = "user", content = user } },
            }),
        };
        if (!string.IsNullOrEmpty(apiKey)) request.Headers.Add("Authorization", "Bearer " + apiKey);
        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw LanguageModelFailure.For(response, body);

        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
    }
}

internal static class LanguageModelFailure
{
    /// <summary>The status and the start of the server's answer. The key is never part of it (it is only sent, not echoed).</summary>
    public static HttpRequestException For(HttpResponseMessage response, string body) =>
        new($"The language model server answered {(int)response.StatusCode}: {(body.Length > 200 ? body[..200] : body)}", null, response.StatusCode);
}
