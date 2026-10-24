using Bunit;
using Modulus.Blazor.Components.Fields;
using Modulus.Ui.Abstractions.Forms;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>The dynamic form: the schema's refusals, its JSON, the checks and the stored values, the shared markup and the Blazor component.</summary>
public class DynamicFormTests : AsyncBunitContext
{
    private const string SampleJson = """
        {
          "name": "Claim",
          "fields": [
            { "key": "employee", "label": "Employee", "kind": "Text", "required": true, "maxLength": 10 },
            { "key": "reference", "label": "Reference", "kind": "text", "pattern": "[A-Z]{3}-[0-9]{4}" },
            { "key": "cost", "label": "Cost", "kind": "Choice", "options": [ { "value": "a", "label": "Alpha" }, { "value": "b", "label": "Beta" } ], "placeholder": "Pick" },
            { "key": "amount", "label": "Amount", "kind": "Money", "currency": "USD", "min": 0.01, "max": 100, "decimals": 2 },
            { "key": "nights", "label": "Nights", "kind": "Number", "max": 9, "decimals": 0 },
            { "key": "day", "label": "Day", "kind": "Date" },
            { "key": "notes", "label": "Notes", "kind": "Multiline", "maxLength": 20, "help": "Short please" },
            { "key": "receipt", "label": "Receipt", "kind": "Switch", "required": true, "placeholder": "Attached" }
          ]
        }
        """;

    private static FormField Field(string key = "name", FormFieldKind kind = FormFieldKind.Text, bool required = false) => new(key, "Name", kind) { Required = required };

    private static Dictionary<string, string?> Values(params (string Key, string? Value)[] pairs) => pairs.ToDictionary(p => p.Key, p => p.Value);

    // The schema's refusals ------------------------------------------------------------------------------------

    [Theory]
    [InlineData("1name")]
    [InlineData("Name")]
    [InlineData("has-dash")]
    [InlineData("")]
    public void AFieldKeyIsALetterThenLettersDigitsOrUnderscores(string key) =>
        Assert.Throws<ArgumentException>(() => new FormSchema("S", [Field(key)]));

    [Fact]
    public void ASchema_RefusesDuplicateKeysBlankLabelsAndBadRanges()
    {
        Assert.Throws<ArgumentException>(() => new FormSchema("S", [Field("a"), Field("a")]));
        Assert.Throws<ArgumentException>(() => new FormSchema("S", [new FormField("a", " ", FormFieldKind.Text)]));
        Assert.Throws<ArgumentException>(() => new FormSchema(" ", [Field()]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FormSchema("S", [new FormField("a", "A", FormFieldKind.Number) { Decimals = 7 }]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FormSchema("S", [new FormField("a", "A", FormFieldKind.Text) { MaxLength = 0 }]));
        Assert.Throws<ArgumentException>(() => new FormSchema("S", [new FormField("a", "A", FormFieldKind.Number) { Min = 5, Max = 1 }]));
    }

    [Fact]
    public void ASchema_RefusesRulesThatDoNotFitTheKind()
    {
        Assert.Throws<ArgumentException>(() => new FormSchema("S", [new FormField("a", "A", FormFieldKind.Text) { Min = 1 }]));
        Assert.Throws<ArgumentException>(() => new FormSchema("S", [new FormField("a", "A", FormFieldKind.Number) { MaxLength = 3 }]));
        Assert.Throws<ArgumentException>(() => new FormSchema("S", [new FormField("a", "A", FormFieldKind.Choice)]));
        Assert.Throws<ArgumentException>(() => new FormSchema("S", [new FormField("a", "A", FormFieldKind.Text) { Options = [new("x", "X")] }]));
        Assert.Throws<ArgumentException>(() => new FormSchema("S", [new FormField("a", "A", FormFieldKind.Number) { Pattern = "[0-9]+" }]));
        Assert.Throws<ArgumentException>(() => new FormSchema("S", [new FormField("a", "A", FormFieldKind.Text) { Pattern = "([" }]));
    }

    // JSON ---------------------------------------------------------------------------------------------------

    [Fact]
    public void ASchemaReadsFromJson_AndWritesBackTheSameFields()
    {
        var schema = FormSchema.Parse(SampleJson);
        Assert.Equal("Claim", schema.Name);
        Assert.Equal(["employee", "reference", "cost", "amount", "nights", "day", "notes", "receipt"], schema.Fields.Select(f => f.Key));
        Assert.Equal(FormFieldKind.Text, schema.Fields[1].Kind); // kinds are not case-sensitive
        Assert.Equal(2, schema.Fields[2].Options.Count);
        Assert.Equal(0, schema.Fields[4].Decimals);
        Assert.Equal(2, schema.Fields[3].Decimals); // the default

        var again = FormSchema.Parse(schema.ToJson());
        Assert.Equal(schema.ToJson(), again.ToJson());
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"name\": \"S\", \"fields\": [{ \"key\": \"a\", \"label\": \"A\", \"kind\": \"Video\" }]}")]
    [InlineData("{\"name\": \"S\", \"fields\": [{ \"label\": \"A\", \"kind\": \"Text\" }]}")]
    public void MalformedOrUnknownJson_IsRefused(string json) =>
        Assert.Throws<ArgumentException>(() => FormSchema.Parse(json));

    // Validation ---------------------------------------------------------------------------------------------

    [Fact]
    public void AnEmptyRequiredFieldIsReported_AndAnOptionalOneIsNot()
    {
        var schema = new FormSchema("S", [Field("name", required: true), Field("other")]);

        var problems = schema.Validate(Values(("name", "   ")));

        Assert.Equal([new FormProblem("name", "Name is required.")], problems);
    }

    [Fact]
    public void TextIsCheckedForLengthAndPattern_AndKeysOutsideTheSchemaAreIgnored()
    {
        var schema = FormSchema.Parse(SampleJson);

        Assert.DoesNotContain(schema.Validate(Values(("employee", "Ana"), ("reference", "TRV-2041"), ("stray", "anything"), ("amount", "5"))), p => p.Key is "employee" or "reference");
        var problems = schema.Validate(Values(("employee", "Far too long a name"), ("reference", "trv-2041"), ("notes", new string('x', 21))));
        Assert.Contains(problems, p => p.Key == "employee" && p.Message == "Employee is longer than 10 characters.");
        Assert.Contains(problems, p => p.Key == "reference" && p.Message == "Reference is not in the form it needs.");
        Assert.Contains(problems, p => p.Key == "notes" && p.Message == "Notes is longer than 20 characters.");
    }

    [Theory]
    [InlineData("12.345", "Amount allows 2 decimal places.")]
    [InlineData("0", "Amount must be 0.01 or more.")]
    [InlineData("100.01", "Amount must be 100 or less.")]
    [InlineData("1,000", "Amount needs a number.")]
    [InlineData("1e3", "Amount needs a number.")]
    public void AnAmountIsANumberWithItsDecimalsAndRange(string value, string message)
    {
        var schema = FormSchema.Parse(SampleJson);
        Assert.Contains(new FormProblem("amount", message), schema.Validate(Values(("amount", value))));
    }

    [Fact]
    public void AWholeNumberRefusesDecimals_AndAValidAmountPasses()
    {
        var schema = FormSchema.Parse(SampleJson);

        Assert.Contains(new FormProblem("nights", "Nights needs a whole number."), schema.Validate(Values(("nights", "2.5"))));
        Assert.DoesNotContain(schema.Validate(Values(("nights", "2"), ("amount", "99.99"))), p => p.Key is "nights" or "amount");
    }

    [Fact]
    public void ADateIsYyyyMmDd_AndAChoiceIsOneOfItsOptions()
    {
        var schema = FormSchema.Parse(SampleJson);

        Assert.Contains(new FormProblem("day", "Day needs a date as yyyy-MM-dd."), schema.Validate(Values(("day", "09/10/2026"))));
        Assert.DoesNotContain(schema.Validate(Values(("day", "2026-10-09"), ("cost", "b"))), p => p.Key is "day" or "cost");
        Assert.Contains(new FormProblem("cost", "Choose one of the options for Cost."), schema.Validate(Values(("cost", "z"))));
    }

    [Fact]
    public void ASwitchIsOnOrOff_AndARequiredSwitchMustBeOn()
    {
        var schema = FormSchema.Parse(SampleJson);

        Assert.Contains(new FormProblem("receipt", "Receipt must be checked."), schema.Validate(Values(("receipt", null))));
        Assert.DoesNotContain(schema.Validate(Values(("receipt", "true"))), p => p.Key == "receipt");
        Assert.DoesNotContain(schema.Validate(Values(("receipt", "ON"))), p => p.Key == "receipt");
        Assert.Contains(new FormProblem("receipt", "Receipt must be yes or no."), schema.Validate(Values(("receipt", "maybe"))));
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData(" On ", true)]
    [InlineData("false", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsOnReadsAPostedSwitch(string? value, bool on) =>
        Assert.Equal(on, FormSchema.IsOn(value));

    [Fact]
    public void Normalize_StoresTrimmedTextInvariantNumbersDatesAndSwitches()
    {
        var schema = FormSchema.Parse(SampleJson);

        var saved = schema.Normalize(Values(("employee", "  Ana  "), ("amount", "12.50"), ("nights", "3"), ("day", "2026-10-09"), ("receipt", "on")));

        Assert.Equal("Ana", saved["employee"]);
        Assert.Equal("12.50", saved["amount"]);
        Assert.Equal("3", saved["nights"]);
        Assert.Equal("2026-10-09", saved["day"]);
        Assert.Equal("true", saved["receipt"]);
        Assert.Equal("", saved["notes"]); // a missing text is empty
        Assert.Equal(schema.Fields.Count, saved.Count);
    }

    // Shared markup ------------------------------------------------------------------------------------------

    [Fact]
    public void TheItemWrapperCarriesTheLabelRequiredMarkAndErrorsOrHelp()
    {
        var field = new FormField("amount", "Amount <b>", FormFieldKind.Text) { Required = true, Help = "Enter <i>it</i>" };

        var open = FormSchemaHtml.ItemOpen(field, "claim-amount", []);
        var closed = FormSchemaHtml.ItemClose(field, []);
        Assert.Contains("<label for=\"claim-amount\" class=\"ant-form-item-required\">Amount &lt;b&gt;</label>", open);
        Assert.Contains("data-key=\"amount\"", open);
        Assert.Contains("<div class=\"ant-form-item-explain\"><div>Enter &lt;i&gt;it&lt;/i&gt;</div></div>", closed);

        var errored = FormSchemaHtml.ItemClose(field, ["Too <big>"]);
        Assert.Contains("ant-form-item-explain-error\">Too &lt;big&gt;</div>", errored);
        Assert.DoesNotContain("ant-form-item-explain\"><div>Enter", errored);
        Assert.Contains("ant-form-item-has-error", FormSchemaHtml.ItemOpen(field, "x", ["m"]));
    }

    [Fact]
    public void TheControlsCarryTheirValuesAndFlagsAndEscapeThem()
    {
        var text = FormSchemaHtml.Control(new FormField("name", "Name", FormFieldKind.Text) { MaxLength = 5, Placeholder = "\"Full\"" }, "claim-name", "<x>", invalid: true);
        Assert.Equal("<input type=\"text\" class=\"ant-input\" name=\"name\" id=\"claim-name\" value=\"&lt;x&gt;\" maxlength=\"5\" placeholder=\"&quot;Full&quot;\" aria-invalid=\"true\">", text);

        var money = FormSchemaHtml.Control(new FormField("amount", "Amount", FormFieldKind.Money) { Currency = "USD", Max = 9 }, "claim-amount", "2.50", invalid: false);
        Assert.Contains("<span class=\"ant-input-group-addon\">USD</span>", money);
        Assert.Contains("value=\"2.50\" max=\"9\"", money);

        var choice = FormSchemaHtml.Control(new FormField("cost", "Cost", FormFieldKind.Choice) { Options = [new("a", "Alpha"), new("b", "Beta")] }, "claim-cost", "b", invalid: false);
        Assert.Equal("<select class=\"m-select\" name=\"cost\" id=\"claim-cost\"><option value=\"a\">Alpha</option><option value=\"b\" selected>Beta</option></select>", choice);

        var on = FormSchemaHtml.Control(new FormField("receipt", "Receipt", FormFieldKind.Switch), "claim-receipt", "true", invalid: false);
        Assert.Contains("value=\"true\" checked", on);
        Assert.Contains("<span class=\"ant-checkbox-inner\"></span></span><span>Yes</span></label>", on);
        Assert.DoesNotContain("checked", FormSchemaHtml.Control(new FormField("receipt", "Receipt", FormFieldKind.Switch), "c", "false", invalid: false));
    }

    [Fact]
    public void TheFormBody_HasOneItemPerFieldWithItsMessages()
    {
        var schema = FormSchema.Parse(SampleJson);
        var problems = schema.Validate(Values(("employee", "")));

        var html = FormSchemaHtml.Fields(schema, Values(("employee", "Ana")), problems, "claim");

        Assert.Equal(schema.Fields.Count, System.Text.RegularExpressions.Regex.Matches(html, "data-key=").Count);
        Assert.Contains("ant-form-item-explain-error\">Employee is required.</div>", html);
        Assert.Contains("name=\"employee\" id=\"claim-employee\" value=\"Ana\"", html);
        Assert.Throws<ArgumentException>(() => FormSchemaHtml.Fields(schema, Values(), problems, " "));
    }

    // The Blazor component -----------------------------------------------------------------------------------

    [Fact]
    public async Task TheBlazorForm_DrawsEveryKindAndReportsEachChange()
    {
        var schema = FormSchema.Parse(SampleJson);
        IReadOnlyDictionary<string, string?>? reported = null;
        var cut = Render<AppSchemaForm>(p => p
            .Add(x => x.Schema, schema)
            .Add(x => x.Values, Values(("employee", "Ana")))
            .Add(x => x.ValuesChanged, (IReadOnlyDictionary<string, string?> v) => reported = v)
            .Add(x => x.Id, "claim"));

        Assert.Equal(schema.Fields.Count, cut.FindAll(".ant-form-item").Count);
        Assert.Equal("Ana", cut.Find("#claim-employee").GetAttribute("value"));
        Assert.NotNull(cut.Find("#claim-amount").ParentElement?.QuerySelector(".ant-input-group-addon"));

        await cut.Find("#claim-employee").ChangeAsync("Bo");
        Assert.Equal("Bo", reported!["employee"]);

        await cut.Find("#claim-receipt").ChangeAsync(true);
        Assert.Equal("true", reported!["receipt"]);
    }

    [Fact]
    public void TheBlazorForm_ShowsItsProblemsOnTheirFields()
    {
        var schema = FormSchema.Parse(SampleJson);
        var problems = schema.Validate(Values(("employee", "")));

        var cut = Render<AppSchemaForm>(p => p.Add(x => x.Schema, schema).Add(x => x.Problems, problems).Add(x => x.Id, "claim"));

        Assert.Equal("Employee is required.", cut.Find(".ant-form-item-explain-error").TextContent);
        Assert.Equal("true", cut.Find("#claim-employee").GetAttribute("aria-invalid"));
    }
}
