using Modulus.Ui.Abstractions.Forms;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>The shared rules behind the business fields (Modulus.Ui.Abstractions.Forms), used by Blazor and MVC alike.</summary>
public class BusinessFieldRuleTests
{
    [Theory]
    [InlineData("1234.5", 1234.5)]
    [InlineData("1,234.50", 1234.5)]
    [InlineData(" 1 234 567 ", 1234567)]
    [InlineData("1'000_000.25", 1000000.25)]
    [InlineData("(1,250.00)", -1250)]
    [InlineData("-3", -3)]
    [InlineData("3-", -3)]
    [InlineData("+.5", 0.5)]
    public void NumberText_ReadsWhatPeopleType(string text, double expected) =>
        Assert.Equal((decimal)expected, NumberText.Parse(text));

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(".")]
    [InlineData("1.2.3")]
    [InlineData("12a")]
    [InlineData("€5")]
    public void NumberText_RejectsWhatIsNotANumber(string text) => Assert.Null(NumberText.Parse(text));

    [Fact]
    public void NumberText_FormatsRoundsHalfAwayFromZeroAndPostsInvariant()
    {
        Assert.Equal("1,234.50", NumberText.Format(1234.5m, 2));
        Assert.Equal("1234.50", NumberText.Format(1234.5m, 2, group: false));
        Assert.Equal("1,235", NumberText.Format(1234.5m, 0));
        Assert.Equal("-2.13", NumberText.Format(-2.125m, 2));
        Assert.Equal("", NumberText.Format(null, 2));
        Assert.Equal("1234.5", NumberText.Invariant(1234.500m));
        Assert.Equal("", NumberText.Invariant(null));
    }

    [Theory]
    [InlineData("1:30", 90)]
    [InlineData("0:05", 5)]
    [InlineData("1.5", 90)]
    [InlineData("1,25", 75)]
    [InlineData("2h", 120)]
    [InlineData("90m", 90)]
    [InlineData("1h 30m", 90)]
    [InlineData("1h30", 90)]
    [InlineData("45 min", 45)]
    [InlineData("2 hours", 120)]
    public void DurationText_ReadsTimesheetEntries(string text, int minutes) => Assert.Equal(minutes, DurationText.Parse(text));

    [Theory]
    [InlineData("")]
    [InlineData("1:75")]
    [InlineData("abc")]
    [InlineData("h")]
    [InlineData("10000h")]
    public void DurationText_RejectsOtherText(string text) => Assert.Null(DurationText.Parse(text));

    [Fact]
    public void DurationText_FormatsHoursAndMinutes()
    {
        Assert.Equal("1:30", DurationText.Format(90));
        Assert.Equal("0:05", DurationText.Format(5));
        Assert.Equal("25:00", DurationText.Format(1500));
        Assert.Equal("", DurationText.Format(null));
    }

    [Fact]
    public void Currency_AndUnits_AreFoundByCodeWithTheirPrecision()
    {
        Assert.Equal(0, Currency.Find("jpy")!.Decimals);
        Assert.Equal(3, Currency.Find(" KWD ")!.Decimals);
        Assert.Null(Currency.Find("XXX"));
        Assert.Equal(3, UnitOfMeasure.Find("KG")!.Decimals);
        Assert.Equal("Weight", UnitOfMeasure.Find("kg")!.Category);
        Assert.Null(UnitOfMeasure.Find(null));
    }

    [Theory]
    [InlineData("99-9999999", "123456789", "12-3456789")]
    [InlineData("99-9999999", "12-3456789", "12-3456789")]
    [InlineData("99-9999999", "1a2b3", "12-3")]
    [InlineData("99-9999999", "12345678901", "12-3456789")]
    [InlineData("AAA-999", "abc123", "ABC-123")]
    [InlineData("**-**", "a1b2", "A1-B2")]
    [InlineData(@"\A99", "12", "A12")]
    [InlineData("(999) 999", "5551", "(555) 1")]
    public void InputMask_FitsTypedTextToTheMask(string mask, string typed, string expected) =>
        Assert.Equal(expected, InputMask.Apply(mask, typed));

    [Fact]
    public void InputMask_PlaceholderCompleteAndRaw()
    {
        Assert.Equal("__-_______", InputMask.Placeholder("99-9999999"));
        Assert.Equal("A__", InputMask.Placeholder(@"\A99"));
        Assert.True(InputMask.IsComplete("99-9999999", "12-3456789"));
        Assert.False(InputMask.IsComplete("99-9999999", "12-345"));
        Assert.False(InputMask.IsComplete("99-9999999", "123456789")); // not in the mask's shape
        Assert.False(InputMask.IsComplete("99-9999999", null));
        Assert.Equal("123456789", InputMask.Raw("99-9999999", "12-3456789"));
        Assert.Equal("", InputMask.Apply("", "123"));
    }

    [Fact]
    public void PhoneNumber_SplitsJoinsAndChecks()
    {
        var (gb, national) = PhoneNumber.Split("+44 20 7946 0958");
        Assert.Equal("GB", gb!.Code);
        Assert.Equal("20 7946 0958", national);

        // +1 is shared: the preferred country wins when it has the code.
        Assert.Equal("CA", PhoneNumber.Split("+1 416 555 0100", preferred: "CA").Country!.Code);
        Assert.Equal("US", PhoneNumber.Split("+1 202 555 0111", preferred: "US").Country!.Code);

        // No "+": a national number of the preferred country.
        var (de, local) = PhoneNumber.Split("030 1234567", preferred: "DE");
        Assert.Equal(("DE", "030 1234567"), (de!.Code, local));

        Assert.Equal("+1 202 555-0111", PhoneNumber.Join(Country.Find("US"), "  202   555-0111 "));
        Assert.Equal("", PhoneNumber.Join(Country.Find("US"), "abc"));

        Assert.True(PhoneNumber.IsValid("+1 202 555 0111"));
        Assert.True(PhoneNumber.IsValid("+44 (20) 7946-0958"));
        Assert.False(PhoneNumber.IsValid("202 555 0111"));   // no calling code
        Assert.False(PhoneNumber.IsValid("+1 555"));          // too short
        Assert.False(PhoneNumber.IsValid("+1 202 555 0111 x2")); // letters
        Assert.Equal("12025550111", PhoneNumber.Digits("+1 (202) 555-0111"));
    }

    private static readonly LookupItem[] Customers =
    [
        new("C1", "Azure Interior") { Description = "Fremont, United States" },
        new("C2", "Deco Addict") { Fields = new Dictionary<string, string> { ["city"] = "Pleasant Hill" } },
        new("AZ9", "Gemini Furniture"),
        new("C4", "Lumber Azure"),
    ];

    [Fact]
    public void LookupSearch_MatchesEveryWord_LabelsStartingWithTheTextFirst()
    {
        Assert.Equal(["C1", "AZ9", "C4"], LookupSearch.Filter(Customers, "az").Select(i => i.Value));
        Assert.Equal(["C2"], LookupSearch.Filter(Customers, "pleasant deco").Select(i => i.Value)); // a field and the label
        Assert.Equal(["C1"], LookupSearch.Filter(Customers, "united").Select(i => i.Value));        // the description
        Assert.Equal(4, LookupSearch.Filter(Customers, " ").Count);
        Assert.Single(LookupSearch.Filter(Customers, "", max: 1));
    }

    [Fact]
    public void LookupSearch_ResolvesExactOrOnlyMatches()
    {
        Assert.Equal("C4", LookupSearch.Resolve(Customers, "lumber azure")!.Value); // exact label
        Assert.Equal("AZ9", LookupSearch.Resolve(Customers, "az9")!.Value);         // exact value
        Assert.Equal("C2", LookupSearch.Resolve(Customers, "deco")!.Value);         // only match
        Assert.Null(LookupSearch.Resolve(Customers, "azure"));                      // two matches
        Assert.Null(LookupSearch.Resolve(Customers, ""));
        Assert.Equal("Pleasant Hill", LookupSearch.Cell(Customers[1], "city"));
        Assert.Equal("", LookupSearch.Cell(Customers[0], "city"));
    }

    [Fact]
    public void WorkflowStage_FoldedStagesShowOnlyWhenCurrent()
    {
        WorkflowStage[] stages = [new("draft", "Draft"), new("done", "Done"), new("cancel", "Cancelled") { Folded = true }];
        Assert.Equal(["draft", "done"], WorkflowStage.Visible(stages, "draft").Select(s => s.Key));
        Assert.Equal(["draft", "done", "cancel"], WorkflowStage.Visible(stages, "cancel").Select(s => s.Key));
    }

    [Theory]
    [InlineData("<p>Hi <b>there</b></p>", "<p>Hi <b>there</b></p>")]
    [InlineData("<p onclick=\"x()\" class=\"a\">Hi</p>", "<p>Hi</p>")]
    [InlineData("<script>alert(1)</script>ok", "ok")]
    [InlineData("<style>p{}</style><p>ok</p>", "<p>ok</p>")]
    [InlineData("<img src=x onerror=alert(1)>ok", "ok")]
    [InlineData("<a href=\"javascript:alert(1)\">x</a>", "<a>x</a>")]
    [InlineData("<a href=\"java\tscript:alert(1)\">x</a>", "<a>x</a>")]
    [InlineData("<a href=\"&#106;avascript:alert(1)\">x</a>", "<a>x</a>")]
    [InlineData("<a href='https://x.dev/?a=1&amp;b=2'>x</a>", "<a href=\"https://x.dev/?a=1&amp;b=2\" rel=\"noopener noreferrer\">x</a>")]
    [InlineData("<a href=/docs>x</a>", "<a href=\"/docs\" rel=\"noopener noreferrer\">x</a>")]
    [InlineData("<b><i>x</b>", "<b><i>x</i></b>")]
    [InlineData("<p>open", "<p>open</p>")]
    [InlineData("</div></body>x", "x")]
    [InlineData("a < b <!-- c --> & d", "a &lt; b  &amp; d")]
    [InlineData("line<br/>two<hr>", "line<br>two<hr>")]
    [InlineData("<table><tr><td>a</td><th>b</th></tr></table>", "<table><tr><td>a</td><th>b</th></tr></table>")]
    [InlineData("<table><thead><tr><th colspan=\"2\">h</th></tr></thead><tbody><tr><td rowspan=3>x</td></tr></tbody></table>", "<table><thead><tr><th colspan=\"2\">h</th></tr></thead><tbody><tr><td rowspan=\"3\">x</td></tr></tbody></table>")]
    [InlineData("<table><tr><td colspan=\"999\" style=\"color:red\" onclick=\"x()\">a</td><td colspan=1>b</td></tr></table>", "<table><tr><td>a</td><td>b</td></tr></table>")]
    [InlineData("<table><script>alert(1)</script><tr><td>ok</td></tr></table>", "<table><tr><td>ok</td></tr></table>")]
    [InlineData("<table><tr><td>open", "<table><tr><td>open</td></tr></table>")]
    public void RichText_KeepsFormattingAndDropsEverythingElse(string html, string expected) =>
        Assert.Equal(expected, RichText.Sanitize(html));

    [Fact]
    public void RichText_PlainTextForRequiredChecks()
    {
        Assert.Equal("Title One two", RichText.ToPlainText("<h3>Title</h3><p>One</p><p>two</p>"));
        Assert.Equal("<b> stays text", RichText.ToPlainText("&lt;b&gt; stays text"));
        Assert.Equal("", RichText.ToPlainText("<p><br></p>"));
        Assert.Equal("a b c", RichText.ToPlainText("<table><tr><td>a</td><td>b</td></tr><tr><td>c</td></tr></table>"));
        Assert.Equal("", RichText.Sanitize(null));
    }

    [Fact]
    public void SignatureImage_AcceptsOnlyPngDataUrls()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
        var url = "data:image/png;base64," + Convert.ToBase64String(png);
        Assert.True(SignatureImage.TryDecode(url, out var bytes));
        Assert.Equal(png, bytes);
        Assert.False(SignatureImage.IsValid("data:image/jpeg;base64," + Convert.ToBase64String(png)));
        Assert.False(SignatureImage.IsValid("data:image/png;base64,not base64!"));
        Assert.False(SignatureImage.IsValid("data:image/png;base64," + Convert.ToBase64String("GIF89a..."u8.ToArray())));
        Assert.False(SignatureImage.IsValid("data:image/png;base64," + new string('A', SignatureImage.MaxLength)));
        Assert.False(SignatureImage.IsValid(null));
    }

    [Fact]
    public void RichTextToolbar_ButtonsHaveLabelsAndAnIconOrText()
    {
        var buttons = RichTextToolbar.Groups.SelectMany(g => g).ToList();
        Assert.All(buttons, b => Assert.False(string.IsNullOrWhiteSpace(b.Label)));
        Assert.All(buttons, b => Assert.True(b.Icon is not null || b.Text is not null, b.Label));
        Assert.Contains(buttons, b => b is { Command: "formatBlock", Argument: "h3" });
        // Row and column buttons need the caret in a table; Insert table does not.
        Assert.Equal(["table-row-add", "table-col-add", "table-row-del", "table-col-del"], buttons.Where(b => b.NeedsTable).Select(b => b.Command));
        Assert.All(buttons.Where(b => b.NeedsTable), b => Assert.StartsWith("table-", b.Command));
    }
}
