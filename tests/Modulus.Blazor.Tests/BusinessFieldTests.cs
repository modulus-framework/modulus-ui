using AntDesign;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Modulus.Blazor.Components.Fields;
using Modulus.Ui.Abstractions.Forms;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>The business fields (Components/Fields): money, quantity, duration, range, phone, mask, OTP, lookup,
/// status bar, rich text and signature.</summary>
public class BusinessFieldTests : AsyncBunitContext
{
    public BusinessFieldTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddAntDesign();
    }

    /// <summary>
    /// Renders a field inside <see cref="BindHost"/>, the way a page with <c>@bind</c> does: after a change callback
    /// the host re-renders, so the field gets the new value back (bUnit's own Bind only runs the callback).
    /// <paramref name="parameters"/> is read on every render.
    /// </summary>
    private IRenderedComponent<TField> Bound<TField>(Func<BindHost, (string Name, object? Value)[]> parameters) where TField : IComponent
    {
        var host = Render<BindHost>((ComponentParameterCollectionBuilder<BindHost> p) => p.Add(h => h.Content, h => b =>
        {
            b.OpenComponent<TField>(0);
            foreach (var (name, value) in parameters(h)) b.AddAttribute(1, name, value);
            b.CloseComponent();
        }));
        return host.FindComponent<TField>();
    }

    // ---- Money ----

    [Fact]
    public void Money_ShowsTheAmountGroupedWithAFixedCurrency_NegativesMarked()
    {
        var cut = Render<AppMoneyInput>(p => p.Add(m => m.Value, -1234.5m).Add(m => m.Currency, "EUR").Add(m => m.Id, "amt"));

        Assert.Equal("-1,234.50", cut.Find("input#amt").GetAttribute("value"));
        Assert.Equal("EUR", cut.Find(".ant-input-group-addon").TextContent);
        Assert.Empty(cut.FindAll("select"));
        Assert.Contains("m-money-negative", cut.Find(".m-money").ClassList);
    }

    [Fact]
    public async Task Money_RoundsToTheCurrencyClampsAndPutsBackText_ThatIsNotANumber()
    {
        decimal? value = 5m;
        var cut = Bound<AppMoneyInput>(h => [("Value", value), ("ValueChanged", h.Set<decimal?>(v => value = v)), ("Currency", "JPY"), ("Min", 0m)]);
        var input = cut.Find("input");

        input.Change("1,234.6");
        Assert.Equal(1235m, value);
        Assert.Equal("1,235", cut.Find("input").GetAttribute("value"));

        cut.Find("input").Change("-40");
        Assert.Equal(0m, value);

        cut.Find("input").Change("abc");
        Assert.Equal(0m, value);
        // The field restores its text after the handler yields. Wait for that continuation to run (a render-queued call runs after
        // it); checking the attribute alone passes before the restore, and the next change then races with it.
        await cut.InvokeAsync(() => { });
        Assert.Equal("0", cut.Find("input").GetAttribute("value"));

        cut.Find("input").Change("");
        Assert.Null(value);
    }

    [Fact]
    public void Money_ChangingTheCurrencyRoundsTheAmountToItsDecimals()
    {
        decimal? value = 10.25m;
        string? currency = "USD";
        var cut = Bound<AppMoneyInput>(h => [("Value", value), ("ValueChanged", h.Set<decimal?>(v => value = v)), ("Currency", currency), ("CurrencyChanged", h.Set<string?>(c => currency = c))]);

        Assert.Equal("USD", cut.Find(".m-addon-select-text").TextContent);
        cut.Find("select").Change("JPY");

        Assert.Equal("JPY", currency);
        Assert.Equal(10m, value);
        Assert.Equal("10", cut.Find("input").GetAttribute("value"));
    }

    // ---- Quantity ----

    [Fact]
    public void Quantity_RoundsToTheUnitAndGroupsTheUnitList()
    {
        decimal? value = null;
        string? unit = "kg";
        var cut = Bound<AppQuantityInput>(h => [("Value", value), ("ValueChanged", h.Set<decimal?>(v => value = v)), ("Unit", unit), ("UnitChanged", h.Set<string?>(u => unit = u))]);

        Assert.NotNull(cut.Find("optgroup[label=Weight] option[value=kg][selected]"));
        cut.Find("input").Change("1.23456");
        Assert.Equal(1.235m, value);
        Assert.Equal("1.235", cut.Find("input").GetAttribute("value"));

        cut.Find("select").Change("pcs");
        Assert.Equal("pcs", unit);
        Assert.Equal(1m, value);
    }

    [Fact]
    public void Quantity_WithoutUnitChangedShowsAFixedUnit_AndRefusesNegatives()
    {
        decimal? value = null;
        var cut = Bound<AppQuantityInput>(h => [("Value", value), ("ValueChanged", h.Set<decimal?>(v => value = v)), ("Unit", "box")]);

        Assert.Empty(cut.FindAll("select"));
        Assert.Contains("Boxes", cut.Markup);
        cut.Find("input").Change("-3");
        Assert.Equal(0m, value);
    }

    // ---- Duration ----

    [Fact]
    public void Duration_ReadsTimesheetTextAsMinutes_UpToMax()
    {
        int? minutes = null;
        var cut = Bound<AppDurationInput>(h => [("Value", minutes), ("ValueChanged", h.Set<int?>(v => minutes = v)), ("Max", 120)]);

        cut.Find("input").Change("1h 30m");
        Assert.Equal(90, minutes);
        Assert.Equal("1:30", cut.Find("input").GetAttribute("value"));

        cut.Find("input").Change("5h");
        Assert.Equal(120, minutes);
        Assert.Equal("h", cut.Find(".ant-input-suffix").TextContent);
    }

    // ---- Number range ----

    [Fact]
    public void NumberRange_BindsBothEnds_AndMarksAnInvertedRange()
    {
        decimal? from = null, to = 5m;
        var cut = Bound<AppNumberRange>(h => [("From", from), ("FromChanged", h.Set<decimal?>(v => from = v)), ("To", to), ("ToChanged", h.Set<decimal?>(v => to = v)), ("Decimals", 0), ("Suffix", "days"), ("AriaLabel", "Lead time")]);

        var inputs = cut.FindAll("input");
        Assert.Equal(["Lead time from", "Lead time to"], inputs.Select(i => i.GetAttribute("aria-label")));
        Assert.DoesNotContain("ant-picker-status-error", cut.Find(".m-number-range").ClassList);

        inputs[0].Change("10");
        Assert.Equal(10m, from);
        Assert.Contains("ant-picker-status-error", cut.Find(".m-number-range").ClassList);
        Assert.Equal("true", cut.FindAll("input")[1].GetAttribute("aria-invalid"));
        Assert.Equal("days", cut.Find(".ant-picker-suffix").TextContent);
    }

    // ---- Phone ----

    [Fact]
    public void Phone_SplitsTheValue_AndJoinsCountryAndNumber()
    {
        string? value = "+44 20 7946 0958";
        var cut = Bound<AppPhoneInput>(h => [("Value", value), ("ValueChanged", h.Set<string?>(v => value = v)), ("Id", "tel")]);

        Assert.NotNull(cut.Find("option[value=GB][selected]"));
        Assert.Equal("20 7946 0958", cut.Find("input#tel").GetAttribute("value"));
        Assert.Equal("GB +44", cut.Find(".m-addon-select-text").TextContent);

        cut.Find("select").Change("FR");
        Assert.Equal("+33 20 7946 0958", value);

        // Pasting a full number picks its country.
        cut.Find("input#tel").Change("+1 202 555 0111");
        Assert.Equal("+1 202 555 0111", value);
        Assert.Equal("US +1", cut.Find(".m-addon-select-text").TextContent);

        cut.Find("input#tel").Change("");
        Assert.Equal("", value);
    }

    // ---- Masked input ----

    [Fact]
    public void Masked_FitsTypedTextToTheMask()
    {
        string? value = null;
        var cut = Bound<AppMaskedInput>(h => [("Value", value), ("ValueChanged", h.Set<string?>(v => value = v)), ("Mask", "99-9999999")]);
        var input = cut.Find("input");

        Assert.Equal("__-_______", input.GetAttribute("placeholder"));
        Assert.Equal("10", input.GetAttribute("maxlength"));
        Assert.Equal("numeric", input.GetAttribute("inputmode"));
        Assert.Equal("99-9999999", input.GetAttribute("data-m-mask"));

        input.Input("12345678999");
        Assert.Equal("12-3456789", value);
        Assert.Equal("12-3456789", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public void Masked_LetterMasksUseTheTextKeyboard() =>
        Assert.Equal("text", Render<AppMaskedInput>(p => p.Add(m => m.Mask, "AAA-999")).Find("input").GetAttribute("inputmode"));

    // ---- One-time code ----

    [Fact]
    public void Otp_OneBoxPerCharacter_CompletesOnceAllAreFilled()
    {
        string? value = null, completed = null;
        var cut = Bound<AppOtpInput>(h => [("Value", value), ("ValueChanged", h.Set<string?>(v => value = v)), ("OnComplete", h.Set<string>(code => completed = code)), ("Separator", 3), ("Id", "otp")]);

        Assert.Equal(6, cut.FindAll("input.ant-otp-input").Count);
        Assert.Single(cut.FindAll(".ant-otp-separator"));
        Assert.Equal("one-time-code", cut.Find("input#otp").GetAttribute("autocomplete"));

        cut.FindAll("input")[0].Input("x"); // not a digit
        Assert.Equal("", value);
        for (var i = 0; i < 5; i++) cut.FindAll("input")[i].Input((i + 1).ToString());
        Assert.Equal("12345", value);
        Assert.Null(completed);

        cut.FindAll("input")[5].Input("6");
        Assert.Equal("123456", completed);
    }

    [Fact]
    public void Otp_ShowsAValueAndUpperCasesLetters_WhenNotNumeric()
    {
        string? value = "AB";
        var cut = Bound<AppOtpInput>(h => [("Value", value), ("ValueChanged", h.Set<string?>(v => value = v)), ("Length", 4), ("Numeric", false)]);

        Assert.Equal(["A", "B", "", ""], cut.FindAll("input").Select(i => i.GetAttribute("value") ?? ""));
        cut.FindAll("input")[2].Input("c");
        Assert.Equal("ABC", value);
    }

    // ---- Tag input ----

    [Fact]
    public void TagInput_EnterAddsATag_BackspaceRemovesTheLast_DuplicatesAndLimitsAreRefused()
    {
        IReadOnlyList<string>? value = ["red"];
        var cut = Bound<AppTagInput>(h => [("Value", value), ("ValueChanged", h.Set<IReadOnlyList<string>>(v => value = v)), ("Max", 3), ("Id", "tags")]);

        Assert.Single(cut.FindAll(".m-tags-item"));
        cut.Find("input#tags").Input("Green");
        cut.Find("input#tags").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        Assert.Equal(["red", "Green"], value);

        cut.Find("input#tags").Input("GREEN"); // a duplicate, without case
        cut.Find("input#tags").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        Assert.Equal(2, value!.Count);

        cut.Find("input#tags").Input("blue; amber");
        cut.Find("input#tags").KeyDown(new KeyboardEventArgs { Key = "," });
        Assert.Equal(["red", "Green", "blue"], value); // Max 3: amber is refused

        cut.Find("input#tags").KeyDown(new KeyboardEventArgs { Key = "Backspace" });
        Assert.Equal(["red", "Green"], value);
    }

    [Fact]
    public void TagInput_RemoveButtonDropsATag_AndDisabledHasNoButtons()
    {
        IReadOnlyList<string>? value = ["a", "b"];
        var cut = Bound<AppTagInput>(h => [("Value", value), ("ValueChanged", h.Set<IReadOnlyList<string>>(v => value = v))]);
        cut.FindAll(".m-tags-remove")[0].Click();
        Assert.Equal(["b"], value);

        var disabled = Bound<AppTagInput>(h => [("Value", (IReadOnlyList<string>)["a"]), ("Disabled", true)]);
        Assert.Empty(disabled.FindAll(".m-tags-remove"));
        Assert.Contains("m-tags-disabled", disabled.Find(".m-tags").ClassName);
    }

    // ---- Lookup ----

    private static readonly LookupItem[] Customers =
    [
        new("C1", "Azure Interior") { Description = "Fremont" },
        new("C2", "Deco Addict"),
        new("C3", "Gemini Furniture"),
        new("C4", "Lumber Azure"),
    ];

    [Fact]
    public void Lookup_SuggestsAsYouType_AndEnterPicks()
    {
        string? value = null;
        LookupItem? picked = null;
        var cut = Bound<AppLookup>(h => [("Value", value), ("ValueChanged", h.Set<string?>(v => value = v)), ("ItemChanged", h.Set<LookupItem?>(i => picked = i)), ("Items", Customers)]);

        cut.Find("input").Input("azure");
        var options = cut.FindAll(".ant-select-item-option");
        Assert.Equal(["Azure Interior", "Lumber Azure"], options.Select(o => o.QuerySelector(".m-lookup-option-label")!.TextContent));
        Assert.Contains("ant-select-item-option-active", options[0].ClassList);
        Assert.Equal("true", cut.Find("input").GetAttribute("aria-expanded"));

        cut.Find("input").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        cut.Find("input").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        Assert.Equal("C4", value);
        Assert.Equal("Lumber Azure", picked!.Label);
        Assert.Equal("Lumber Azure", cut.Find("input").GetAttribute("value"));
        Assert.Empty(cut.FindAll(".m-lookup-dropdown"));
    }

    [Fact]
    public void Lookup_LeavingResolvesTheOnlyMatch_OrPutsTheValueBack()
    {
        string? value = "C1";
        var cut = Bound<AppLookup>(h => [("Value", value), ("ValueChanged", h.Set<string?>(v => value = v)), ("Items", Customers)]);
        Assert.Equal("Azure Interior", cut.Find("input").GetAttribute("value"));

        cut.Find("input").Input("deco");
        cut.Find("input").Blur();
        Assert.Equal("C2", value);

        cut.Find("input").Input("azure"); // two matches
        cut.Find("input").Blur();
        Assert.Equal("C2", value);
        Assert.Equal("Deco Addict", cut.Find("input").GetAttribute("value"));

        cut.Find("input").Input("");
        cut.Find("input").Blur();
        Assert.Null(value);
    }

    [Fact]
    public void Lookup_F4OpensTheSearchDialog_WithColumnsAndACount()
    {
        string? value = null;
        var cut = Bound<AppLookup>(h => [("Value", value), ("ValueChanged", h.Set<string?>(v => value = v)), ("Items", Customers), ("AriaLabel", "Customer"), ("DialogPageSize", 3)]);

        cut.Find("input").KeyDown(new KeyboardEventArgs { Key = "F4" });
        Assert.Equal("Search: Customer", cut.Find(".ant-modal-title").TextContent);
        Assert.Equal(["Code", "Name", "Details"], cut.FindAll("thead th").Select(t => t.TextContent));
        Assert.Equal(3, cut.FindAll("tr.m-lookup-row").Count);
        Assert.Equal("Showing 3 of 4. Refine the search to see more.", cut.Find(".m-lookup-count").TextContent);

        cut.Find(".m-lookup-search-input").Input("gem");
        Assert.Equal("1 record", cut.Find(".m-lookup-count").TextContent);
        cut.Find(".m-lookup-search-input").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        Assert.Equal("C3", value);
        Assert.Empty(cut.FindAll(".m-lookup-dialog"));
    }

    [Fact]
    public void Lookup_ServerSearch_DoesNotClaimATotalItDoesNotKnow()
    {
        var requests = new List<(string Text, int Max)>();
        Task<IReadOnlyList<LookupItem>> Search(string text, int max, CancellationToken _)
        {
            requests.Add((text, max));
            IReadOnlyList<LookupItem> all = Enumerable.Range(1, 100).Select(i => new LookupItem($"P{i}", $"Part {i}")).ToList();
            return Task.FromResult<IReadOnlyList<LookupItem>>(LookupSearch.Filter(all, text, max));
        }

        var cut = Render<AppLookup>(p => p
            .Add(l => l.Value, "P7").Add(l => l.Label, "Part 7")
            .Add(l => l.SearchAsync, Search).Add(l => l.DialogPageSize, 10));

        Assert.Equal("Part 7", cut.Find("input").GetAttribute("value"));
        cut.Find(".m-lookup-more").Click();

        Assert.Equal(("", 11), requests.Single()); // one more than a page, to know whether there are more
        Assert.Equal(10, cut.FindAll("tr.m-lookup-row").Count);
        Assert.Equal("Showing the first 10. Refine the search to see more.", cut.Find(".m-lookup-count").TextContent);
        Assert.Contains("ant-table-row-selected", cut.FindAll("tr.m-lookup-row")[6].ClassList);
    }

    [Fact]
    public void Lookup_WithColumns_SuggestionsShowTheColumnsUnderAHeader()
    {
        var columns = new[] { new LookupColumn("value", "Code"), new LookupColumn("label", "Name"), new LookupColumn("description", "Details") };
        var cut = Bound<AppLookup>(h => [("Value", (string?)null), ("Items", Customers), ("Columns", columns)]);

        cut.Find("input").Input("azure");
        Assert.Equal(["Code", "Name", "Details"], cut.FindAll(".m-lookup-head .m-lookup-cell").Select(c => c.TextContent));
        var first = cut.FindAll(".ant-select-item-option")[0];
        Assert.Equal(["C1", "Azure Interior", "Fremont"], first.QuerySelectorAll(".m-lookup-cells .m-lookup-cell").Select(c => c.TextContent));
        Assert.Empty(cut.FindAll(".m-lookup-option-label"));
        Assert.Contains("m-lookup-multi", cut.Find(".m-lookup-dropdown").ClassList);
    }

    [Fact]
    public void Lookup_WithoutColumns_SuggestionsKeepTheLabelAndDetails()
    {
        var cut = Bound<AppLookup>(h => [("Value", (string?)null), ("Items", Customers)]);

        cut.Find("input").Input("azure");
        Assert.Empty(cut.FindAll(".m-lookup-head"));
        Assert.Empty(cut.FindAll(".m-lookup-cells"));
        Assert.Equal("Azure Interior", cut.Find(".m-lookup-option-label").TextContent);
        Assert.Equal("Fremont", cut.Find(".m-lookup-option-desc").TextContent);
    }

    [Fact]
    public void Lookup_ShowMore_AddsAPageUntilTheListIsWhole()
    {
        var cut = Bound<AppLookup>(h => [("Value", (string?)null), ("Items", Customers), ("DialogPageSize", 3)]);

        cut.Find("input").KeyDown(new KeyboardEventArgs { Key = "F4" });
        Assert.Equal(3, cut.FindAll("tr.m-lookup-row").Count);
        Assert.Equal("Show 3 more", cut.Find(".m-lookup-more-rows").TextContent);

        cut.Find(".m-lookup-more-rows").Click();
        Assert.Equal(4, cut.FindAll("tr.m-lookup-row").Count);
        Assert.Equal("4 records", cut.Find(".m-lookup-count").TextContent);
        Assert.Empty(cut.FindAll(".m-lookup-more-rows"));
    }

    [Fact]
    public void Lookup_ServerSearch_ShowMoreAsksForALongerList()
    {
        var requests = new List<int>();
        Task<IReadOnlyList<LookupItem>> Search(string text, int max, CancellationToken _)
        {
            requests.Add(max);
            IReadOnlyList<LookupItem> all = Enumerable.Range(1, 100).Select(i => new LookupItem($"P{i}", $"Part {i}")).ToList();
            return Task.FromResult<IReadOnlyList<LookupItem>>(LookupSearch.Filter(all, text, max));
        }
        var cut = Render<AppLookup>(p => p.Add(l => l.SearchAsync, Search).Add(l => l.DialogPageSize, 10));

        cut.Find(".m-lookup-more").Click();
        cut.Find(".m-lookup-more-rows").Click();

        Assert.Equal([11, 21], requests);
        Assert.Equal(20, cut.FindAll("tr.m-lookup-row").Count);
        Assert.Equal("Showing the first 20. Refine the search to see more.", cut.Find(".m-lookup-count").TextContent);
        Assert.Equal("Show 10 more", cut.Find(".m-lookup-more-rows").TextContent);
    }

    // ---- Status bar ----

    private static readonly WorkflowStage[] Stages =
        [new("draft", "Draft"), new("sent", "Sent"), new("done", "Done"), new("cancel", "Cancelled") { Folded = true }];

    [Fact]
    public void StatusBar_ChecksTheCurrentStage_AndMovesWhenBound()
    {
        string? value = "sent";
        var cut = Bound<AppStatusBar>(h => [("Value", value), ("ValueChanged", h.Set<string?>(v => value = v)), ("Stages", Stages)]);

        Assert.Equal(["Draft", "Sent", "Done"], cut.FindAll(".m-statusbar-label").Select(l => l.TextContent));
        Assert.True(cut.Find("input[value=sent]").HasAttribute("checked"));
        Assert.DoesNotContain("m-statusbar-readonly", cut.Find(".m-statusbar").ClassList);

        cut.Find("input[value=done]").Change(true);
        Assert.Equal("done", value);
    }

    [Fact]
    public void StatusBar_IsReadOnlyUnbound_AndShowsAFoldedStageOnlyWhenCurrent()
    {
        var cut = Render<AppStatusBar>(p => p.Add(s => s.Value, "cancel").Add(s => s.Stages, Stages));

        Assert.Contains("m-statusbar-readonly", cut.Find(".m-statusbar").ClassList);
        Assert.Equal(4, cut.FindAll("input:disabled").Count);
        Assert.Equal("Cancelled", cut.Find("input:checked + .m-statusbar-label").TextContent);
    }

    // ---- Rich text ----

    [Fact]
    public void RichText_RowAndColumnButtonsStartDisabled_AndTheTableButtonIsEnabled()
    {
        var cut = Bound<AppRichText>(h => [("Value", "<table><tr><td>a</td></tr></table>"), ("ValueChanged", h.Set<string?>(_ => { }))]);

        Assert.False(cut.Find("[data-cmd='table-insert']").HasAttribute("disabled"));
        Assert.Equal(4, cut.FindAll("[data-cmd^='table-']:not([data-cmd='table-insert'])").Count(b => b.HasAttribute("disabled")));
    }

    [Fact]
    public void RichText_RendersCleanHtml_AndReportsCleanHtml()
    {
        string? value = "<p onclick=\"x()\">Hi <b>there</b></p><script>alert(1)</script>";
        var cut = Bound<AppRichText>(h => [("Value", value), ("ValueChanged", h.Set<string?>(v => value = v)), ("Placeholder", "Notes")]);

        Assert.Equal("<p>Hi <b>there</b></p>", cut.Find(".m-richtext-editor").InnerHtml);
        Assert.Equal("Notes", cut.Find(".m-richtext-editor").GetAttribute("data-placeholder"));
        Assert.Equal(RichTextToolbar.Groups.Sum(g => g.Count), cut.FindAll(".m-richtext-btn").Count);
        Assert.Empty(JSInterop.Invocations); // the script reports through the hidden input, not interop

        // What modulus-shell.js does: write the editor's HTML to the hidden input and raise change.
        cut.Find(".m-richtext > input[type=hidden]").Change("<p>New <img src=x onerror=alert(1)></p>");
        Assert.Equal("<p>New </p>", value);

        cut.Find(".m-richtext > input[type=hidden]").Change("");
        Assert.Null(value);
        Assert.Contains("m-richtext-empty", cut.Find(".m-richtext").ClassList);
    }

    [Fact]
    public void RichText_DisabledHasNoToolbar_AndCannotBeEdited()
    {
        var cut = Render<AppRichText>(p => p.Add(r => r.Value, "<p>x</p>").Add(r => r.Disabled, true));

        Assert.Empty(cut.FindAll(".m-richtext-toolbar"));
        Assert.False(cut.Find(".m-richtext-editor").HasAttribute("contenteditable")); // Blazor drops a false attribute
    }

    // ---- Signature ----

    [Fact]
    public void SignaturePad_StartsTheScript_AndReportsTheImage()
    {
        string? value = null;
        var cut = Bound<AppSignaturePad>(h => [("Value", value), ("ValueChanged", h.Set<string?>(v => value = v)), ("Height", 120)]);

        Assert.Contains("--m-signature-height:120px", cut.Find(".m-signature").GetAttribute("style"));
        Assert.NotNull(cut.Find("input.m-signature-type"));
        Assert.True(cut.Find(".m-signature").HasAttribute("data-m-signature")); // modulus-plugins.js starts the pad
        Assert.Empty(JSInterop.Invocations);

        cut.Find(".m-signature > input[type=hidden]").Change("data:image/png;base64,AAAA");
        Assert.Equal("data:image/png;base64,AAAA", value);
        Assert.Contains("m-signature-signed", cut.Find(".m-signature").ClassList);

        cut.Find(".m-signature > input[type=hidden]").Change("");
        Assert.Null(value);
    }
}

/// <summary>A parent that re-renders after its callbacks, like a page using <c>@bind</c>.</summary>
public sealed class BindHost : ComponentBase
{
    [Parameter] public Func<BindHost, RenderFragment> Content { get; set; } = _ => _ => { };

    public EventCallback<T> Set<T>(Action<T> assign) => EventCallback.Factory.Create(this, assign);

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, Content(this));
}
