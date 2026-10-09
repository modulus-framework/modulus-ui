using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Modulus.Ui.Abstractions.Forms;

/// <summary>The control a schema field is drawn with.</summary>
public enum FormFieldKind
{
    /// <summary>One line of text.</summary>
    Text,

    /// <summary>Several lines of text.</summary>
    Multiline,

    /// <summary>A number with a fixed number of decimal places.</summary>
    Number,

    /// <summary>An amount in a currency: a number, shown with its currency code.</summary>
    Money,

    /// <summary>A calendar date, posted as yyyy-MM-dd.</summary>
    Date,

    /// <summary>One value from a list of options.</summary>
    Choice,

    /// <summary>A yes or no, posted as true or left out.</summary>
    Switch,
}

/// <summary>One option of a <see cref="FormFieldKind.Choice"/> field: the value posted and the label shown.</summary>
public sealed record FormOption(string Value, string Label);

/// <summary>A message about one field, from <see cref="FormSchema.Validate"/>.</summary>
public sealed record FormProblem(string Key, string Message);

/// <summary>
/// One field of a dynamic form. Values arrive as text (a form post) and <see cref="FormSchema.Validate"/> checks them against
/// the rules here. The rules are data, so a form can be read from JSON (<see cref="FormSchema.Parse"/>).
/// </summary>
public sealed record FormField(string Key, string Label, FormFieldKind Kind)
{
    /// <summary>The field must have a value. For a switch, it must be on.</summary>
    public bool Required { get; init; }

    /// <summary>Text and multiline: the longest value, in characters.</summary>
    public int? MaxLength { get; init; }

    /// <summary>Text: a regular expression the whole value must match.</summary>
    public string? Pattern { get; init; }

    /// <summary>Number and money: the smallest value allowed.</summary>
    public decimal? Min { get; init; }

    /// <summary>Number and money: the largest value allowed.</summary>
    public decimal? Max { get; init; }

    /// <summary>Number and money: how many decimal places a value may have, from 0 to 6.</summary>
    public int Decimals { get; init; } = 2;

    /// <summary>Money: the currency code shown in front of the amount.</summary>
    public string? Currency { get; init; }

    /// <summary>Choice: the options, in order.</summary>
    public IReadOnlyList<FormOption> Options { get; init; } = [];

    /// <summary>
    /// Shown in an empty field. For a choice it is the text of the empty first option; for a switch it is the caption
    /// beside the box (default "Yes").
    /// </summary>
    public string? Placeholder { get; init; }

    /// <summary>Help text under the field; hidden while an error is shown.</summary>
    public string? Help { get; init; }
}

/// <summary>
/// A dynamic form: a named list of fields, checked and normalised on the server. The same schema drives the Blazor
/// <c>AppSchemaForm</c>, the MVC <c>app-schema-form</c> and the server's checks, so the three cannot disagree. Build it
/// in code, or read it from JSON with <see cref="Parse"/> so a form can change without a new build.
/// </summary>
public sealed partial class FormSchema
{
    private static readonly HashSet<string> SwitchValues = new(StringComparer.OrdinalIgnoreCase) { "", "true", "false", "on", "off" };

    private readonly Dictionary<string, Regex> _patterns = new(StringComparer.Ordinal);

    /// <summary>Checks the fields and compiles their patterns. Refuses a schema that could not be drawn or checked.</summary>
    public FormSchema(string name, IReadOnlyList<FormField> fields)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A schema needs a name.", nameof(name));
        ArgumentNullException.ThrowIfNull(fields);

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in fields)
        {
            if (!KeyRule().IsMatch(f.Key)) throw new ArgumentException($"A field key is a letter, then up to 39 letters, digits or underscores: \"{f.Key}\".", nameof(fields));
            if (!keys.Add(f.Key)) throw new ArgumentException($"The field key \"{f.Key}\" is used twice.", nameof(fields));
            if (string.IsNullOrWhiteSpace(f.Label)) throw new ArgumentException($"The field \"{f.Key}\" needs a label.", nameof(fields));
            if (f.Decimals is < 0 or > 6) throw new ArgumentOutOfRangeException(nameof(fields), $"The decimal places of \"{f.Key}\" are 0 to 6.");
            if (f.MaxLength is < 1) throw new ArgumentOutOfRangeException(nameof(fields), $"The longest length of \"{f.Key}\" is at least 1.");
            if (f.Min > f.Max) throw new ArgumentException($"The minimum of \"{f.Key}\" is above its maximum.", nameof(fields));
            if ((f.Min is not null || f.Max is not null) && f.Kind is not (FormFieldKind.Number or FormFieldKind.Money))
                throw new ArgumentException($"Only a number or money field takes a minimum or maximum: \"{f.Key}\".", nameof(fields));
            if (f.MaxLength is not null && f.Kind is not (FormFieldKind.Text or FormFieldKind.Multiline))
                throw new ArgumentException($"Only a text field takes a maximum length: \"{f.Key}\".", nameof(fields));
            if (f.Kind == FormFieldKind.Choice && f.Options.Count == 0) throw new ArgumentException($"The choice \"{f.Key}\" has no options.", nameof(fields));
            if (f.Kind != FormFieldKind.Choice && f.Options.Count > 0) throw new ArgumentException($"Only a choice has options: \"{f.Key}\".", nameof(fields));
            if (f.Pattern is null) continue;
            if (f.Kind != FormFieldKind.Text) throw new ArgumentException($"Only a text field takes a pattern: \"{f.Key}\".", nameof(fields));
            try
            {
                _patterns[f.Key] = new Regex($"^(?:{f.Pattern})$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            }
            catch (ArgumentException e)
            {
                throw new ArgumentException($"The pattern of \"{f.Key}\" is not a regular expression.", nameof(fields), e);
            }
        }

        Name = name;
        Fields = fields.ToArray();
    }

    public string Name { get; }

    public IReadOnlyList<FormField> Fields { get; }

    /// <summary>Whether a posted switch value means on: "true" or "on" (any case). Anything else, or nothing, is off.</summary>
    public static bool IsOn(string? value) => value?.Trim().ToLowerInvariant() is "true" or "on";

    /// <summary>
    /// Reads a schema from JSON: <c>{"name": "...", "fields": [{"key": "...", "label": "...", "kind": "Text", ...}]}</c>. The
    /// field properties are those of <see cref="FormField"/>, in camel case; kinds and option keys are not case-sensitive.
    /// Refuses malformed JSON and anything <see cref="FormSchema(string, IReadOnlyList{FormField})"/> refuses.
    /// </summary>
    public static FormSchema Parse(string json)
    {
        SchemaDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<SchemaDocument>(json, JsonOptions);
        }
        catch (JsonException e)
        {
            throw new ArgumentException("The schema is not valid JSON: " + e.Message, nameof(json), e);
        }
        if (document is null) throw new ArgumentException("The schema is empty.", nameof(json));

        var fields = (document.Fields ?? []).Select(ReadField).ToList();
        return new FormSchema(document.Name ?? "", fields);
    }

    /// <summary>The schema as JSON that <see cref="Parse"/> reads back.</summary>
    public string ToJson()
    {
        var document = new SchemaDocument
        {
            Name = Name,
            Fields = Fields.Select(f => new FieldDocument
            {
                Key = f.Key, Label = f.Label, Kind = f.Kind.ToString(), Required = f.Required, MaxLength = f.MaxLength,
                Pattern = f.Pattern, Min = f.Min, Max = f.Max, Decimals = f.Decimals, Currency = f.Currency,
                Options = f.Options.Count == 0 ? null : f.Options.Select(o => new OptionDocument { Value = o.Value, Label = o.Label }).ToList(),
                Placeholder = f.Placeholder, Help = f.Help,
            }).ToList(),
        };
        return JsonSerializer.Serialize(document, JsonOptions);
    }

    /// <summary>
    /// Checks the posted values (keys the schema does not name are ignored) and returns one problem per field that fails,
    /// in field order. An empty list means the form is valid.
    /// </summary>
    public IReadOnlyList<FormProblem> Validate(IReadOnlyDictionary<string, string?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var problems = new List<FormProblem>();
        foreach (var f in Fields)
        {
            var message = Check(f, Text(values, f.Key));
            if (message is not null) problems.Add(new FormProblem(f.Key, message));
        }
        return problems;
    }

    /// <summary>
    /// The values of the schema's fields as they are stored: trimmed text, invariant numbers, yyyy-MM-dd dates, and true or
    /// false for a switch. A missing value is empty (false for a switch). Run <see cref="Validate"/> first; a value that
    /// does not parse is kept as posted.
    /// </summary>
    public IReadOnlyDictionary<string, string> Normalize(IReadOnlyDictionary<string, string?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var f in Fields)
        {
            var raw = Text(values, f.Key);
            result[f.Key] = f.Kind switch
            {
                FormFieldKind.Switch => IsOn(raw) ? "true" : "false",
                FormFieldKind.Number or FormFieldKind.Money when TryNumber(raw, out var n) => n.ToString(CultureInfo.InvariantCulture),
                FormFieldKind.Date when DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                _ => raw,
            };
        }
        return result;
    }

    private string? Check(FormField f, string raw)
    {
        if (f.Kind == FormFieldKind.Switch)
        {
            if (!SwitchValues.Contains(raw)) return $"{f.Label} must be yes or no.";
            return f.Required && !IsOn(raw) ? $"{f.Label} must be checked." : null;
        }
        if (raw.Length == 0) return f.Required ? $"{f.Label} is required." : null;

        switch (f.Kind)
        {
            case FormFieldKind.Text or FormFieldKind.Multiline:
                if (f.MaxLength is { } max && raw.Length > max) return $"{f.Label} is longer than {max} characters.";
                return _patterns.TryGetValue(f.Key, out var pattern) && !pattern.IsMatch(raw) ? $"{f.Label} is not in the form it needs." : null;

            case FormFieldKind.Number or FormFieldKind.Money:
                return NumberProblem(f, raw);

            case FormFieldKind.Date:
                return DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ? null : $"{f.Label} needs a date as yyyy-MM-dd.";

            case FormFieldKind.Choice:
                return f.Options.Any(o => o.Value == raw) ? null : $"Choose one of the options for {f.Label}.";

            default:
                return null;
        }
    }

    private static string? NumberProblem(FormField f, string raw)
    {
        if (!TryNumber(raw, out var n)) return $"{f.Label} needs a number.";
        var dot = raw.IndexOf('.');
        var places = dot < 0 ? 0 : raw.Length - dot - 1;
        if (places > f.Decimals) return f.Decimals == 0 ? $"{f.Label} needs a whole number." : $"{f.Label} allows {f.Decimals} decimal places.";
        if (f.Min is { } min && n < min) return $"{f.Label} must be {min.ToString(CultureInfo.InvariantCulture)} or more.";
        if (f.Max is { } max && n > max) return $"{f.Label} must be {max.ToString(CultureInfo.InvariantCulture)} or less.";
        return null;
    }

    private static bool TryNumber(string raw, out decimal value) =>
        decimal.TryParse(raw, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);

    private static string Text(IReadOnlyDictionary<string, string?> values, string key) =>
        values.TryGetValue(key, out var v) ? (v ?? "").Trim() : "";

    private static FormField ReadField(FieldDocument d)
    {
        if (string.IsNullOrEmpty(d.Key)) throw new ArgumentException("A field needs a key.");
        if (!Enum.TryParse<FormFieldKind>(d.Kind, ignoreCase: true, out var kind))
            throw new ArgumentException($"The kind of \"{d.Key}\" is not one of: {string.Join(", ", Enum.GetNames<FormFieldKind>())}.");
        return new FormField(d.Key, d.Label ?? "", kind)
        {
            Required = d.Required,
            MaxLength = d.MaxLength,
            Pattern = d.Pattern,
            Min = d.Min,
            Max = d.Max,
            Decimals = d.Decimals ?? 2,
            Currency = d.Currency,
            Options = (d.Options ?? []).Select(o => new FormOption(o.Value ?? "", o.Label ?? o.Value ?? "")).ToList(),
            Placeholder = d.Placeholder,
            Help = d.Help,
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    internal sealed class SchemaDocument
    {
        public string? Name { get; set; }
        public List<FieldDocument>? Fields { get; set; }
    }

    internal sealed class FieldDocument
    {
        public string? Key { get; set; }
        public string? Label { get; set; }
        public string? Kind { get; set; }
        public bool Required { get; set; }
        public int? MaxLength { get; set; }
        public string? Pattern { get; set; }
        public decimal? Min { get; set; }
        public decimal? Max { get; set; }
        public int? Decimals { get; set; }
        public string? Currency { get; set; }
        public List<OptionDocument>? Options { get; set; }
        public string? Placeholder { get; set; }
        public string? Help { get; set; }
    }

    internal sealed class OptionDocument
    {
        public string? Value { get; set; }
        public string? Label { get; set; }
    }

    [GeneratedRegex("^[a-z][A-Za-z0-9_]{0,39}$")]
    private static partial Regex KeyRule();
}
