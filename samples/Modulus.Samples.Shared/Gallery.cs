using Modulus.Ui.Abstractions;

namespace Modulus.Samples.Shared;

/// <summary>Sample data for the component gallery page in both sample apps.</summary>
public sealed record GalleryInvoice(string Number, string Customer, decimal Amount, string Status);

public static class GalleryData
{
    public static readonly GalleryInvoice[] Invoices =
    [
        new("INV-1042", "Acme Corporation", 12_400, "Paid"),
        new("INV-1043", "Globex", 3_250, "Pending"),
        new("INV-1044", "Initech", 8_900, "Overdue"),
        new("INV-1045", "Umbrella Health", 1_120, "Paid"),
    ];

    public static readonly string[] Plans = ["Starter", "Business", "Enterprise"];
    public static readonly string[] Views = ["List", "Board", "Calendar"];
    public static readonly string[] Icons = ["home", "user", "team", "setting", "search", "bell", "mail", "calendar", "file-text", "cloud-upload", "delete", "edit"];

    /// <summary>Country → city, for the Cascader demo.</summary>
    public static readonly TreeOption[] Locations =
    [
        new("de", "Germany", [new("berlin", "Berlin"), new("munich", "Munich"), new("hamburg", "Hamburg")]),
        new("uk", "United Kingdom", [new("london", "London"), new("manchester", "Manchester")]),
        new("us", "United States", [new("new-york", "New York"), new("san-francisco", "San Francisco"), new("austin", "Austin", Disabled: true)]),
        new("jp", "Japan", [new("tokyo", "Tokyo"), new("osaka", "Osaka")]),
    ];

    /// <summary>Department → team, for the TreeSelect demo.</summary>
    public static readonly TreeOption[] Teams =
    [
        new("engineering", "Engineering", [new("platform", "Platform"), new("web", "Web"), new("mobile", "Mobile")]),
        new("design", "Design", [new("product-design", "Product design"), new("brand", "Brand")]),
        new("sales", "Sales", [new("emea", "EMEA"), new("americas", "Americas")]),
    ];

    /// <summary>A project's files, for the Tree demo.</summary>
    public static readonly TreeOption[] Files =
    [
        new("src", "src",
        [
            new("components", "components", [new("button", "Button.razor"), new("table", "Table.razor"), new("modal", "Modal.razor")]),
            new("pages", "pages", [new("index", "Index.razor"), new("settings", "Settings.razor", Disabled: true)]),
        ]),
        new("docs", "docs", [new("readme", "README.md"), new("changelog", "CHANGELOG.md")]),
    ];

    public static readonly string[] FilesExpanded = ["src", "components"];
    public static readonly string[] FilesChecked = ["button", "table"];

    /// <summary>Permissions a role can be given, for the Transfer demo.</summary>
    public static readonly TreeOption[] Permissions =
    [
        new("customers.view", "View customers"), new("customers.edit", "Edit customers"), new("customers.delete", "Delete customers"),
        new("orders.view", "View orders"), new("orders.refund", "Refund orders"), new("data.export", "Export data"),
        new("billing.manage", "Manage billing"), new("users.invite", "Invite users"), new("roles.manage", "Manage roles", Disabled: true),
    ];

    public static readonly string[] GrantedPermissions = ["customers.view", "orders.view"];

    /// <summary>Carousel slides: title, text and a Tabler tone.</summary>
    public static readonly (string Title, string Text, string Tone)[] Slides =
    [
        ("Quarterly review", "Revenue grew 18% on the back of enterprise renewals.", "blue"),
        ("New regions", "Singapore and Tokyo offices opened in September.", "purple"),
        ("Support", "Median first response time is down to 42 minutes.", "green"),
    ];

    /// <summary>The month the Calendar demo shows (fixed, so both samples render the same page).</summary>
    public static readonly DateTime CalendarDate = new(2026, 10, 14);
}

/// <summary>Form model behind the gallery's data-entry demos.</summary>
public sealed class GalleryForm
{
    public string? Name { get; set; }
    public string? Website { get; set; }
    public string? Password { get; set; }
    public string? Query { get; set; }
    public string? Notes { get; set; }
    public string Plan { get; set; } = "Business";
    public int Seats { get; set; } = 25;
    public DateTime? Start { get; set; } = new DateTime(2026, 10, 1);
    public DateTime?[] Period { get; set; } = [new DateTime(2026, 10, 1), new DateTime(2026, 10, 31)];
    public DateTime? ReportAt { get; set; } = new DateTime(2026, 1, 1, 9, 0, 0);
    public bool Alerts { get; set; } = true;
    public bool Email { get; set; } = true;
    public bool Sms { get; set; }
    public string Priority { get; set; } = "normal";
    public string Size { get; set; } = "m";
    public string View { get; set; } = "List";
    public double Discount { get; set; } = 20;
    public decimal Rating { get; set; } = 4;
    public string? Location { get; set; } = "berlin";
    public string? Team { get; set; } = "web";
}
