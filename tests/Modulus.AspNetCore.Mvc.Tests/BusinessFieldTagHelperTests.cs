using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.AspNetCore.Mvc.TagHelpers;
using Modulus.Ui.Abstractions.Forms;
using Xunit;

namespace Modulus.AspNetCore.Mvc.Tests;

/// <summary>The business field tag helpers (app-money-input … app-rich-text): same DOM as the Blazor fields, plus the
/// hidden inputs that post invariant values.</summary>
public class BusinessFieldTagHelperTests
{
    private static readonly EmptyModelMetadataProvider Metadata = new();

    private static ModelExpression For<T>(string name, T model) => new(name, Metadata.GetModelExplorerForType(typeof(T), model));

    private static ViewContext WithError(string field)
    {
        var view = new ViewContext { ViewData = new ViewDataDictionary(Metadata, new ModelStateDictionary()) };
        view.ViewData.ModelState.AddModelError(field, "Required");
        return view;
    }

    private static async Task<string> RenderAsync(TagHelper helper, string tag, TagHelperAttributeList? attributes = null)
    {
        var output = new TagHelperOutput(tag, attributes ?? [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        await helper.ProcessAsync(new TagHelperContext(tag, [], new Dictionary<object, object>(), "u1"), output);
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    [Fact]
    public async Task Money_RoundsToTheCurrency_PostsTheAmountAndTheCurrency()
    {
        var html = await RenderAsync(new MoneyInputTagHelper { For = For<decimal?>("UnitPrice", 1234.567m), CurrencyFor = For("Currency", "EUR") }, "app-money-input");

        Assert.StartsWith("<span class=\"ant-input-group-wrapper m-field m-money\"", html);
        Assert.Contains("<span class=\"m-addon-select-text\" aria-hidden=\"true\" x-text=\"unit\">EUR</span>", html);
        Assert.Contains("<select name=\"Currency\" aria-label=\"Currency\"", html);
        Assert.Contains("<option value=\"EUR\" selected>EUR · Euro</option>", html);
        Assert.Contains("id=\"UnitPrice\" value=\"1,234.57\"", html);
        Assert.Contains("<input type=\"hidden\" name=\"UnitPrice\" value=\"1234.57\" x-ref=\"value\" :value=\"raw\" />", html);
    }

    [Fact]
    public async Task Money_FixedCurrency_NegativeAndErrorStates()
    {
        var html = await RenderAsync(new MoneyInputTagHelper { For = For<decimal?>("Total", -5m), Currency = "JPY", ViewContext = WithError("Total") }, "app-money-input");

        Assert.Contains("<span class=\"ant-input-group-addon\">JPY</span>", html);
        Assert.DoesNotContain("<select", html);
        Assert.Contains("ant-input-group-wrapper-status-error", html);
        Assert.Contains("m-money-negative", html);
        Assert.Contains("value=\"-5\"", html);
        Assert.Contains("aria-invalid=\"true\"", html);
    }

    [Fact]
    public async Task Quantity_GroupsUnitsAndRoundsToTheUnit()
    {
        var html = await RenderAsync(new QuantityInputTagHelper { For = For<decimal?>("Qty", 1.23456m), UnitFor = For("Unit", "kg") }, "app-quantity-input");

        Assert.Contains("<optgroup label=\"Weight\">", html);
        Assert.Contains("<option value=\"kg\" selected>kg</option>", html);
        Assert.Contains("<select name=\"Unit\"", html);
        Assert.Contains("value=\"1.235\"", html);
        Assert.Contains("name=\"Qty\" value=\"1.235\"", html);
    }

    [Fact]
    public async Task Duration_ShowsHoursAndMinutes_PostsMinutes()
    {
        var html = await RenderAsync(new DurationInputTagHelper { For = For<int?>("Setup", 1500), Max = 600 }, "app-duration-input");

        Assert.StartsWith("<span class=\"ant-input-affix-wrapper m-field m-duration\"", html);
        Assert.Contains("placeholder=\"0:00\"", html);
        Assert.Contains("value=\"10:00\"", html);
        Assert.Contains("name=\"Setup\" value=\"600\"", html);
        Assert.Contains("<span class=\"ant-input-suffix\">h</span>", html);
    }

    [Fact]
    public async Task NumberRange_PostsBothEnds_AndMarksAnInvertedRange()
    {
        var html = await RenderAsync(new NumberRangeTagHelper
        {
            FromFor = For<decimal?>("LeadFrom", 10m), ToFor = For<decimal?>("LeadTo", 5m), Decimals = 0, Suffix = "days", AriaLabel = "Lead time",
        }, "app-number-range");

        Assert.StartsWith("<div class=\"ant-picker ant-picker-range ant-picker-status-error m-field m-number-range\"", html);
        Assert.Contains("id=\"LeadFrom\" placeholder=\"Min\" aria-label=\"Lead time from\" aria-invalid=\"true\"", html);
        Assert.Contains("aria-label=\"Lead time to\" aria-invalid=\"true\"", html);
        Assert.Contains("name=\"LeadFrom\" value=\"10\"", html);
        Assert.Contains("name=\"LeadTo\" value=\"5\"", html);
        Assert.Contains("<span class=\"ant-picker-suffix\">days</span>", html);
    }

    [Fact]
    public async Task Phone_SplitsTheNumber_AndPrefersTheDefaultCountryForSharedCodes()
    {
        var html = await RenderAsync(new PhoneInputTagHelper { For = For("Phone", "+44 20 7946 0958") }, "app-phone-input");
        Assert.Contains("<option value=\"GB\" data-dial=\"44\" selected>", html);
        Assert.Contains("value=\"20 7946 0958\"", html);
        Assert.Contains("name=\"Phone\" value=\"&#x2B;44 20 7946 0958\"", html); // "+" encoded
        Assert.Contains("&quot;home&quot;:&quot;US&quot;", html); // the script's default for shared codes

        var canada = await RenderAsync(new PhoneInputTagHelper { For = For("Phone", "+1 416 555 0100"), DefaultCountry = "CA" }, "app-phone-input");
        Assert.Contains("<option value=\"CA\" data-dial=\"1\" selected>", canada);
    }

    [Fact]
    public async Task Masked_RendersTheMaskedInput()
    {
        var html = await RenderAsync(new MaskedInputTagHelper { For = For("TaxId", "123456789"), Mask = "99-9999999" }, "app-masked-input");

        Assert.StartsWith("<input class=\"ant-input m-masked\" type=\"text\" name=\"TaxId\" id=\"TaxId\" data-m-mask=\"99-9999999\" placeholder=\"__-_______\" maxlength=\"10\"", html);
        Assert.Contains("inputmode=\"numeric\" value=\"12-3456789\"", html);
    }

    [Fact]
    public async Task Otp_OneBoxPerCharacter_PostsTheJoinedCode()
    {
        var html = await RenderAsync(new OtpInputTagHelper { For = For("Code", "123"), Separator = 3 }, "app-otp-input");

        Assert.StartsWith("<div class=\"ant-otp\" role=\"group\" aria-label=\"Verification code\" data-m-otp=\"numeric\">", html);
        Assert.Equal(6, html.Split("ant-otp-input").Length - 1);
        Assert.Single(html.Split("ant-otp-separator").Skip(1));
        Assert.Contains("id=\"Code\" inputmode=\"numeric\" autocomplete=\"one-time-code\" aria-label=\"Character 1 of 6\" value=\"1\"", html);
        Assert.Contains("aria-label=\"Character 4 of 6\" value=\"\"", html);
        Assert.Contains("<input type=\"hidden\" name=\"Code\" value=\"123\" />", html);
    }

    private static readonly LookupItem[] Customers =
    [
        new("C1", "Azure Interior") { Description = "Fremont", Fields = new Dictionary<string, string> { ["city"] = "Fremont" } },
        new("C2", "Deco Addict"),
    ];

    [Fact]
    public async Task Lookup_ShowsTheLabel_SendsItemsToTheScript_AndDrawsTheDialog()
    {
        var html = await RenderAsync(new LookupTagHelper
        {
            For = For("Customer", "C1"), Items = Customers, AriaLabel = "Customer",
            Columns = [new("value", "Code"), new("label", "Customer"), new("city", "City")],
        }, "app-lookup");

        Assert.StartsWith("<span class=\"m-lookup\" x-data=\"modulusLookup(", html);
        Assert.Contains("<input type=\"hidden\" name=\"Customer\" value=\"C1\" x-ref=\"value\" :value=\"value\" />", html);
        Assert.Contains("id=\"Customer\" autocomplete=\"off\" role=\"combobox\"", html);
        Assert.Contains("aria-controls=\"m-lookup-u1\"", html);
        Assert.Contains("value=\"Azure Interior\" x-ref=\"input\"", html);
        Assert.Contains("&quot;l&quot;:&quot;Deco Addict&quot;", html);
        Assert.Contains("<div class=\"ant-modal-title\" id=\"m-lookup-u1-title\">Search: Customer</div>", html);
        Assert.Contains("<th class=\"ant-table-cell\">Code</th><th class=\"ant-table-cell\">Customer</th><th class=\"ant-table-cell\">City</th>", html);
        Assert.Contains("colspan=\"3\"", html);
    }

    [Fact]
    public async Task Lookup_ServerSearch_UsesTheLabelGiven()
    {
        var html = await RenderAsync(new LookupTagHelper { For = For("Product", "P7"), SearchHref = "/products/search", Label = "Part 7" }, "app-lookup");

        Assert.Contains("value=\"Part 7\"", html);
        Assert.Contains("&quot;href&quot;:&quot;/products/search&quot;", html);
        Assert.Contains("&quot;items&quot;:null", html);
    }

    private static readonly WorkflowStage[] Stages =
        [new("draft", "Draft"), new("sent", "Sent"), new("cancel", "Cancelled") { Folded = true }];

    [Fact]
    public async Task StatusBar_IsARadioPerStage()
    {
        var html = await RenderAsync(new StatusBarTagHelper { For = For("Stage", "sent"), Stages = Stages }, "app-status-bar");

        Assert.StartsWith("<div class=\"m-statusbar\" role=\"radiogroup\" aria-label=\"Status\">", html);
        Assert.Contains("<input type=\"radio\" class=\"m-statusbar-radio\" name=\"Stage\" value=\"sent\" checked />", html);
        Assert.DoesNotContain("Cancelled", html);
        Assert.DoesNotContain("type=\"hidden\"", html);
    }

    [Fact]
    public async Task StatusBar_ReadOnly_StillPostsTheStage_AndShowsAFoldedCurrentStage()
    {
        var html = await RenderAsync(new StatusBarTagHelper { For = For("Stage", "cancel"), Stages = Stages, Clickable = false }, "app-status-bar");

        Assert.Contains("m-statusbar-readonly", html);
        Assert.Contains("value=\"cancel\" checked disabled />", html);
        Assert.Contains("<input type=\"hidden\" name=\"Stage\" value=\"cancel\" />", html);
    }

    [Fact]
    public async Task SignaturePad_KeepsOnlyAPngDataUrl()
    {
        var png = "data:image/png;base64," + Convert.ToBase64String(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0 });
        var signed = await RenderAsync(new SignaturePadTagHelper { For = For("Signature", png), Height = 120 }, "app-signature-pad");
        Assert.StartsWith("<div class=\"m-signature m-signature-signed\" style=\"--m-signature-height:120px\" data-ink=\"#111827\" data-m-signature=\"\">", signed);
        Assert.Contains($"name=\"Signature\" value=\"{png}\"", signed);
        Assert.Contains("m-signature-type", signed);

        var junk = await RenderAsync(new SignaturePadTagHelper { For = For("Signature", "javascript:alert(1)"), Disabled = true }, "app-signature-pad");
        Assert.Contains("name=\"Signature\" value=\"\"", junk);
        Assert.Contains("m-signature-disabled", junk);
        Assert.DoesNotContain("m-signature-actions", junk);
    }

    [Fact]
    public async Task RichText_RendersAndPostsCleanHtml()
    {
        var html = await RenderAsync(new RichTextTagHelper { For = For("Notes", "<p onclick=\"x()\">Hi</p><script>alert(1)</script>"), Placeholder = "Terms" }, "app-rich-text");

        Assert.StartsWith("<div class=\"m-richtext\">", html);
        Assert.Contains("<div class=\"m-richtext-toolbar\" role=\"toolbar\" aria-label=\"Formatting\">", html);
        Assert.Contains("id=\"Notes\" contenteditable=\"true\" role=\"textbox\" aria-multiline=\"true\" aria-label=\"Rich text\" data-placeholder=\"Terms\" style=\"min-height:140px\"><p>Hi</p></div>", html);
        Assert.Contains("<input type=\"hidden\" name=\"Notes\" value=\"&lt;p&gt;Hi&lt;/p&gt;\" />", html);
        Assert.DoesNotContain("script", html);

        var empty = await RenderAsync(new RichTextTagHelper { For = For<string?>("Notes", null), Disabled = true, ViewContext = WithError("Notes") }, "app-rich-text");
        Assert.StartsWith("<div class=\"m-richtext m-richtext-empty m-richtext-disabled m-richtext-status-error\">", empty);
        Assert.Contains("contenteditable=\"false\"", empty);
        Assert.DoesNotContain("m-richtext-toolbar", empty);
    }
}
