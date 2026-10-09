using Modulus.Ui.Abstractions.Forms;

namespace Modulus.Samples.Shared;

/// <summary>What the dynamic form page shows: the values as posted, the messages, and the saved values once the form is valid.</summary>
public sealed record DynamicFormState(IReadOnlyDictionary<string, string?> Values, IReadOnlyList<FormProblem> Problems, IReadOnlyDictionary<string, string>? Saved);

/// <summary>The dynamic form sample: an expense claim whose fields are read from JSON, so the page has no fields of its own.</summary>
public static class DynamicFormDemo
{
    /// <summary>The claim's fields, as the JSON a dynamic form is stored and sent in (<see cref="FormSchema.Parse"/>).</summary>
    public const string SchemaJson = """
        {
          "name": "Expense claim",
          "fields": [
            { "key": "employee", "label": "Employee", "kind": "Text", "required": true, "maxLength": 60, "placeholder": "Full name" },
            { "key": "costCentre", "label": "Cost centre", "kind": "Choice", "required": true, "placeholder": "Choose a cost centre",
              "options": [ { "value": "sales", "label": "Sales" }, { "value": "operations", "label": "Operations" }, { "value": "it", "label": "IT" } ] },
            { "key": "claimDate", "label": "Date of expense", "kind": "Date", "required": true },
            { "key": "amount", "label": "Amount", "kind": "Money", "required": true, "currency": "USD", "min": 0.01, "max": 10000, "decimals": 2 },
            { "key": "nights", "label": "Nights away", "kind": "Number", "min": 0, "max": 60, "decimals": 0 },
            { "key": "reference", "label": "Trip reference", "kind": "Text", "pattern": "[A-Z]{3}-[0-9]{4}", "help": "Three capitals, a dash and four digits, for example TRV-2041" },
            { "key": "notes", "label": "Notes", "kind": "Multiline", "maxLength": 500 },
            { "key": "receipt", "label": "Receipt", "kind": "Switch", "required": true, "placeholder": "I attach the receipt" }
          ]
        }
        """;

    /// <summary>The parsed claim form.</summary>
    public static FormSchema Schema { get; } = FormSchema.Parse(SchemaJson);

    /// <summary>A form nobody has submitted yet.</summary>
    public static DynamicFormState Empty { get; } = new(new Dictionary<string, string?>(), [], null);

    /// <summary>Checks the posted values: the messages when the claim is not valid, otherwise the values as they are stored.</summary>
    public static DynamicFormState Submit(IReadOnlyDictionary<string, string?> values)
    {
        var problems = Schema.Validate(values);
        return problems.Count == 0 ? new DynamicFormState(values, problems, Schema.Normalize(values)) : new DynamicFormState(values, problems, null);
    }

    /// <summary>The line the page shows after a claim is saved.</summary>
    public static string Summary(IReadOnlyDictionary<string, string> saved) =>
        $"Expense claim for {saved["employee"]} saved: {saved["amount"]} {Schema.Fields.First(f => f.Key == "amount").Currency}.";
}
