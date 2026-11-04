using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Modulus.Ui.Abstractions.Forms;

namespace Modulus.Samples.Shared;

// The business fields page (/forms/business): a sales order that uses every ERP field (money, quantity, duration,
// number range, phone, masked code, one-time code, lookup, status bar, signature and rich text). Every sample binds
// BusinessFormInput, so the rules and messages are the same in Blazor, MVC and Razor Pages.

/// <summary>A sales order on the business fields page.</summary>
public sealed class BusinessFormInput
{
    public string? Stage { get; set; } = "draft";

    [Required(ErrorMessage = "Pick a customer")]
    public string? Customer { get; set; }

    [Required(ErrorMessage = "Pick a product")]
    public string? Product { get; set; }

    [Required(ErrorMessage = "Enter a unit price"), Range(typeof(decimal), "0.01", "1000000", ErrorMessage = "Enter a price between 0.01 and 1,000,000")]
    public decimal? UnitPrice { get; set; }

    public string Currency { get; set; } = "USD";

    [Required(ErrorMessage = "Enter a quantity"), Range(typeof(decimal), "0.001", "1000000", ErrorMessage = "Enter a quantity above zero")]
    public decimal? Quantity { get; set; }

    public string Unit { get; set; } = "pcs";

    /// <summary>Setup time in minutes.</summary>
    [Range(0, 24 * 60, ErrorMessage = "Setup takes at most 24 hours")]
    public int? SetupMinutes { get; set; }

    /// <summary>Delivery window in days after confirmation (both ends are checked on <see cref="LeadTo"/>, where the field shows its error).</summary>
    public decimal? LeadFrom { get; set; }

    [NumberRangeValue(nameof(LeadFrom), 0, 365, ErrorMessage = "Use 0 to 365 days, the first day before the last")]
    public decimal? LeadTo { get; set; }

    [Required(ErrorMessage = "Enter a contact phone number"), PhoneNumberValue(ErrorMessage = "Enter the full number for the selected country")]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Enter the customer's tax id"), MaskComplete(BusinessFormsDemo.TaxIdMask, ErrorMessage = "Enter all 9 digits of the tax id")]
    public string? TaxId { get; set; }

    [Required(ErrorMessage = "Enter the approval code"), RegularExpression(@"^\d{6}$", ErrorMessage = "Enter all 6 digits")]
    public string? ApprovalCode { get; set; }

    /// <summary>Free-form labels, at most five (the tag input).</summary>
    [MaxLength(5, ErrorMessage = "Use at most 5 labels")]
    public List<string> Labels { get; set; } = [];

    /// <summary>Terms as HTML; cleaned with <see cref="RichText.Sanitize"/> before it is stored.</summary>
    [StringLength(20_000, ErrorMessage = "Keep the notes under 20,000 characters")]
    public string? Notes { get; set; }

    [Required(ErrorMessage = "Sign the order"), SignatureValue(ErrorMessage = "Draw or type a signature")]
    public string? Signature { get; set; }

    /// <summary>The order total, when price and quantity are set.</summary>
    public decimal? Total => UnitPrice is { } p && Quantity is { } q ? p * q : null;
}

/// <summary>Demo data for the business fields page.</summary>
public static class BusinessFormsDemo
{
    public const string TaxIdMask = "99-9999999";

    public static readonly IReadOnlyList<WorkflowStage> Stages =
    [
        new("draft", "Quotation"),
        new("sent", "Quotation sent"),
        new("sale", "Sales order"),
        new("done", "Locked"),
        new("cancel", "Cancelled") { Folded = true },
    ];

    public static readonly IReadOnlyList<LookupColumn> CustomerColumns =
        [new("value", "Code"), new("label", "Customer"), new("city", "City"), new("country", "Country")];

    public static readonly IReadOnlyList<LookupColumn> ProductColumns =
        [new("value", "SKU"), new("label", "Product"), new("category", "Category"), new("price", "List price")];

    /// <summary>A short list, sent to the browser whole: the lookup filters it there.</summary>
    public static readonly IReadOnlyList<LookupItem> Customers = BuildCustomers();

    /// <summary>A catalogue too large to send whole: the lookup searches it on the server (<see cref="SearchProducts"/>).</summary>
    public static readonly IReadOnlyList<LookupItem> Products = BuildProducts();

    /// <summary>The product search behind the lookup: at most <paramref name="max"/> matches of <paramref name="text"/>.</summary>
    public static IReadOnlyList<LookupItem> SearchProducts(string? text, int max) =>
        LookupSearch.Filter(Products, text, Math.Clamp(max, 1, 200));

    /// <summary>The label of a product code, shown by the server-searched lookup before it has searched.</summary>
    public static string? ProductLabel(string? code) => Products.FirstOrDefault(p => p.Value == code)?.Label;

    /// <summary>What each field posts, shown beside the form.</summary>
    public static readonly (string Field, string Stores)[] Storage =
    [
        ("Status bar", "The stage key: draft, sent, sale…"),
        ("Lookup", "The record's code; F4 opens a search dialog"),
        ("Money", "An invariant decimal, plus the currency code"),
        ("Quantity", "A decimal rounded to the unit, plus the unit code"),
        ("Duration", "Whole minutes; type 1:30, 1.5, 90m or 1h 30m"),
        ("Number range", "Two decimals; from above to is an error"),
        ("Phone", "\"+{calling code} {number}\""),
        ("Masked input", "The text with its literals: 12-3456789"),
        ("One-time code", "The joined characters: 123456"),
        ("Rich text", "HTML, cleaned on the server"),
        ("Signature", "A PNG data URL, checked on the server"),
    ];

    /// <summary>The message shown after a valid submit.</summary>
    public static string Summary(BusinessFormInput input)
    {
        var customer = Customers.FirstOrDefault(c => c.Value == input.Customer)?.Label ?? input.Customer;
        var currency = Currency.Find(input.Currency);
        var total = NumberText.Format(input.Total, currency?.Decimals ?? 2);
        return $"Order for {customer} saved: {total} {input.Currency}";
    }

    private static List<LookupItem> BuildCustomers()
    {
        (string Name, string City, string Country)[] rows =
        [
            ("Azure Interior", "Fremont", "United States"), ("Deco Addict", "Pleasant Hill", "United States"),
            ("Gemini Furniture", "Fairfield", "United States"), ("Lumber Inc", "Stockton", "United States"),
            ("Ready Mat", "Tracy", "United States"), ("The Jackson Group", "Lodi", "United States"),
            ("Wood Corner", "Turlock", "United States"), ("Nordic Oak AB", "Malmö", "Sweden"),
            ("Brightline GmbH", "Hamburg", "Germany"), ("Atelier Lune", "Lyon", "France"),
            ("Harbour & Co", "Bristol", "United Kingdom"), ("Kaiju Trading", "Osaka", "Japan"),
            ("Meridian Textiles", "Dhaka", "Bangladesh"), ("Southern Cross Supply", "Brisbane", "Australia"),
            ("Maple Works", "Toronto", "Canada"), ("Casa Verde", "Monterrey", "Mexico"),
            ("Desert Bloom LLC", "Dubai", "United Arab Emirates"), ("Alpine Timber", "Zürich", "Switzerland"),
        ];
        return rows.Select((r, i) => new LookupItem($"C{1001 + i}", r.Name)
        {
            Description = $"{r.City}, {r.Country}",
            Fields = new Dictionary<string, string> { ["city"] = r.City, ["country"] = r.Country },
        }).ToList();
    }

    private static List<LookupItem> BuildProducts()
    {
        (string Category, string Prefix, string[] Items, decimal Base)[] groups =
        [
            ("Furniture", "FUR", ["Office chair", "Standing desk", "Bookshelf", "Filing cabinet", "Conference table", "Sofa", "Bar stool", "Coffee table"], 120m),
            ("Fabric", "FAB", ["Cotton twill", "Linen blend", "Denim 12oz", "Wool flannel", "Polyester satin", "Canvas duck"], 6.5m),
            ("Hardware", "HDW", ["Hinge", "Drawer slide", "Cabinet knob", "Shelf bracket", "Caster wheel", "Wood screw"], 1.2m),
            ("Finishes", "FIN", ["Oak stain", "Clear lacquer", "Matte varnish", "Wax polish", "Primer"], 18m),
        ];
        string[] variants = ["Black", "White", "Natural", "Walnut", "Grey", "Blue", "Green", "Red", "Sand", "Charcoal"];
        var list = new List<LookupItem>();
        foreach (var (category, prefix, items, basePrice) in groups)
            for (var i = 0; i < items.Length; i++)
                for (var v = 0; v < variants.Length; v++)
                {
                    var price = Math.Round(basePrice * (1 + i * 0.35m + v * 0.05m), 2);
                    list.Add(new LookupItem($"{prefix}-{i + 1:00}{v:0}", $"{items[i]}, {variants[v]}")
                    {
                        Description = $"{category} · {price.ToString("N2", CultureInfo.InvariantCulture)}",
                        Fields = new Dictionary<string, string> { ["category"] = category, ["price"] = price.ToString("N2", CultureInfo.InvariantCulture) },
                    });
                }
        return list;
    }
}

/// <summary>
/// A number range, put on its "to": both ends between <paramref name="min"/> and <paramref name="max"/> and the
/// "from" (the <paramref name="from"/> property) not above the "to".
/// </summary>
public sealed class NumberRangeValueAttribute(string from, double min, double max) : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        var start = context.ObjectType.GetProperty(from)?.GetValue(context.ObjectInstance) as decimal?;
        var end = value as decimal?;
        bool Out(decimal? d) => d is { } x && ((double)x < min || (double)x > max);
        return Out(start) || Out(end) || start > end
            ? new ValidationResult(ErrorMessage, [context.MemberName!])
            : ValidationResult.Success;
    }
}

/// <summary>A phone number with a calling code and enough digits (<see cref="PhoneNumber.IsValid"/>).</summary>
public sealed class PhoneNumberValueAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is not string s || s.Length == 0 || PhoneNumber.IsValid(s);
}

/// <summary>Every slot of the mask is filled (<see cref="InputMask.IsComplete"/>).</summary>
public sealed class MaskCompleteAttribute(string mask) : ValidationAttribute
{
    public override bool IsValid(object? value) => value is not string s || s.Length == 0 || InputMask.IsComplete(mask, s);
}

/// <summary>A PNG data URL from the signature pad (<see cref="SignatureImage.IsValid"/>).</summary>
public sealed class SignatureValueAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is not string s || s.Length == 0 || SignatureImage.IsValid(s);
}
