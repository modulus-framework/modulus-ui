using System.Globalization;
using System.Text.RegularExpressions;
using Modulus.Ui.Abstractions.Chat;
using Modulus.Ui.Abstractions.Language;
using Modulus.Ui.Abstractions.Query;

namespace Modulus.Samples.Shared;

/// <summary>
/// Stand-in for a language model in the samples: it reads a question with plain rules (amounts, status words, cities,
/// "last month", "this month") and answers with the filter JSON a real model would. An app puts a real client there
/// (<c>Modulus.Flow.Connectors</c>); the page does not change.
/// </summary>
public sealed class RuleLanguageModel(IEnumerable<string> cities) : ILanguageModel
{
    private static readonly string[] Statuses = Enum.GetNames<ShipmentStatus>();

    public Task<string> CompleteAsync(string system, string user, int maxTokens, CancellationToken cancellationToken, string? model = null)
    {
        var text = user.ToLowerInvariant();
        var items = new List<object>();
        var today = DateOnly.FromDateTime(DateTime.Today);

        var atLeast = Regex.Match(text, @"(?:at least|or more|or above)\s+\$?(\d+(?:\.\d+)?)");
        var over = Regex.Match(text, @"(?:over|above|more than|greater than)\s+\$?(\d+(?:\.\d+)?)");
        var under = Regex.Match(text, @"(?:under|below|less than|at most)\s+\$?(\d+(?:\.\d+)?)");
        if (atLeast.Success) items.Add(Rule("amount", "greaterOrEqual", atLeast.Groups[1].Value));
        else if (over.Success) items.Add(Rule("amount", "greaterThan", over.Groups[1].Value));
        if (under.Success) items.Add(Rule("amount", "lessThan", under.Groups[1].Value));

        var named = Statuses.Where(s => Regex.IsMatch(text, $@"\b{Regex.Escape(s.ToLowerInvariant())}\b")).ToList();
        if (named.Count > 0) items.Add(new { kind = "rule", field = "status", @operator = "in", value = string.Join("|", named) });

        foreach (var city in cities.Where(c => text.Contains(c.ToLowerInvariant(), StringComparison.Ordinal)))
            items.Add(Rule("city", "equals", city));

        if (text.Contains("last month", StringComparison.Ordinal))
        {
            var first = new DateOnly(today.Year, today.Month, 1).AddMonths(-1);
            items.Add(new { kind = "rule", field = "date", @operator = "between", value = Iso(first), to = Iso(first.AddMonths(1).AddDays(-1)) });
        }
        else if (text.Contains("this month", StringComparison.Ordinal))
        {
            var first = new DateOnly(today.Year, today.Month, 1);
            items.Add(new { kind = "rule", field = "date", @operator = "between", value = Iso(first), to = Iso(today) });
        }
        else
        {
            var year = Regex.Match(text, @"\bin (20\d\d)\b");
            if (year.Success)
            {
                var y = int.Parse(year.Groups[1].Value, CultureInfo.InvariantCulture);
                items.Add(new { kind = "rule", field = "date", @operator = "between", value = Iso(new DateOnly(y, 1, 1)), to = Iso(new DateOnly(y, 12, 31)) });
            }
        }

        return Task.FromResult(System.Text.Json.JsonSerializer.Serialize(new { kind = "group", join = "and", items }));
    }

    private static object Rule(string field, string op, string value) => new { kind = "rule", field, @operator = op, value };

    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

/// <summary>What the ask page shows: the conversation, the filter it applied (kept in a hidden field), and the rows it leaves.</summary>
public sealed record AskView(IReadOnlyList<ChatTurn> Turns, string Transcript, string Filter, QueryPage Results)
{
    public const string Path = "/tables/ask";

    /// <summary>The examples shown under the composer.</summary>
    public const string Examples = "Try: shipments over 500 in Paris, delivered in 2025, pending under 200";
}

/// <summary>The ask-your-data page: a question becomes a filter on the shipments (the same fields as the visual filter page).</summary>
public static class AskPage
{
    /// <summary>Answers a question with the model and loads the rows. A question that cannot be read keeps the filter in place.</summary>
    public static async Task<AskView> AskAsync(string? transcript, string? filterText, string? question, ILanguageModel model)
    {
        var turns = ChatTranscript.FromText(transcript).ToList();
        var filter = FilterJson.FromText(filterText) ?? FilterGroup.Empty;
        if (!string.IsNullOrWhiteSpace(question))
        {
            turns.Add(new ChatTurn(ChatRole.User, question.Trim()));
            var answer = await FilterAssistant.TranslateAsync(QueryPage.Builder, question, model, DateOnly.FromDateTime(DateTime.Today));
            if (answer.Error is null)
            {
                filter = answer.Filter;
                var described = QueryPage.Builder.Describe(filter);
                var text = filter.Items.Count == 0 ? "Showing all shipments." : $"Showing {described}.";
                turns.Add(new ChatTurn(ChatRole.Assistant, text, answer.Notes));
            }
            else
            {
                turns.Add(new ChatTurn(ChatRole.Assistant, answer.Error));
            }
        }
        var results = QueryPage.Load(filter);
        return new AskView(turns, ChatTranscript.ToText(turns), FilterJson.ToText(filter), results);
    }

    /// <summary>The model the samples use: the stand-in over the shipment cities.</summary>
    public static ILanguageModel Model() => new RuleLanguageModel(Shipments.Query.Select(s => s.City).Distinct().ToList());
}
