using System.Text;
using Modulus.Ui.Abstractions.Language;

namespace Modulus.Ui.Abstractions.Query;

/// <summary>What the assistant made of a question: the filter (kept within the builder's fields and operators), and notes for the user.</summary>
/// <param name="Filter">The filter to apply. Empty when the question could not be read (<paramref name="Error"/> says why).</param>
/// <param name="Notes">Plain sentences about what was left out, for the chat reply.</param>
/// <param name="Error">Why no filter was made, or null.</param>
public sealed record FilterTranslation(FilterGroup Filter, IReadOnlyList<string> Notes, string? Error);

/// <summary>
/// Turns a plain question ("orders over 500 from last month") into a <see cref="FilterGroup"/> with a language model. The
/// model is told the fields, their kinds and operators, and the options of choice fields; its reply is read as JSON and
/// then kept within the builder (<see cref="QueryBuilder{T}.Normalize"/>), so a field or operator it invents is dropped and
/// reported. The filter is never run as code, and a restricted field (<see cref="QueryBuilder{T}.Restrict"/>) stays out.
/// </summary>
public static class FilterAssistant
{
    /// <summary>The longest question read; a longer one is refused, not cut.</summary>
    public const int MaxQuestionLength = 500;

    /// <summary>The instruction given to the model: today's date, each field with its operators and options, and the JSON shape.</summary>
    public static string Instruction(IReadOnlyList<QueryField> fields, DateOnly today)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You turn a question about a list of records into a filter. Reply with JSON only, no text around it.");
        sb.AppendLine($"Today is {today:yyyy-MM-dd}. Use it for words like \"last month\" or \"this year\", written as dates or numbers.");
        sb.AppendLine("A group is {\"kind\":\"group\",\"join\":\"and\" or \"or\",\"items\":[...]}. A condition is {\"kind\":\"rule\",\"field\":key,\"operator\":op,\"value\":text} (and \"to\" for between).");
        sb.AppendLine("Values are text: numbers as written, dates as yyyy-MM-dd, several choices joined with |.");
        sb.AppendLine("Use only these fields, with the operators listed for each:");
        foreach (var field in fields)
        {
            var ops = string.Join(", ", QueryFieldOperators.For(field.Kind).Select(Camel));
            sb.Append($"- {field.Key} ({field.Title}, {field.Kind.ToString().ToLowerInvariant()}): {ops}");
            if (field.Options.Count > 0) sb.Append($". Options: {string.Join(" | ", field.Options.Select(o => o.Value))}");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    /// <summary>Asks the model for a filter and keeps it within the builder's fields and operators.</summary>
    public static async Task<FilterTranslation> TranslateAsync<T>(QueryBuilder<T> builder, string question, ILanguageModel model, DateOnly today, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question)) return new(FilterGroup.Empty, [], "Ask a question first.");
        if (question.Length > MaxQuestionLength) return new(FilterGroup.Empty, [], $"Ask in fewer than {MaxQuestionLength} characters.");

        var reply = await model.CompleteAsync(Instruction(builder.Schema, today), question.Trim(), 600, cancellationToken);
        var parsed = FilterJson.FromText(JsonObject(reply));
        if (parsed is null) return new(FilterGroup.Empty, [], "The assistant did not return a filter. Try asking again, or use the filter form.");

        var kept = builder.Normalize(parsed);
        var dropped = Rules(parsed) - Rules(kept);
        var notes = dropped > 0
            ? new[] { $"{dropped} condition{(dropped == 1 ? "" : "s")} left out: the field or operator is not available here." }
            : Array.Empty<string>();
        return new(kept, notes, null);
    }

    private static string Camel(FilterOperator op) => char.ToLowerInvariant(op.ToString()[0]) + op.ToString()[1..];

    private static int Rules(FilterNode node) => node switch
    {
        FilterRule => 1,
        FilterGroup group => group.Items.Sum(Rules),
        _ => 0,
    };

    /// <summary>The first JSON object in a reply (models often wrap it in a sentence or a code fence), or null.</summary>
    private static string? JsonObject(string reply)
    {
        var start = reply.IndexOf('{');
        var end = reply.LastIndexOf('}');
        return start >= 0 && end > start ? reply[start..(end + 1)] : null;
    }
}
