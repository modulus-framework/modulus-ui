using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Theming;

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

    /// <summary>People for the AutoComplete, Mentions and Comment demos: handle, name, Tabler avatar tone.</summary>
    public static readonly (string Handle, string Name, string Tone)[] People =
    [
        ("serena", "Serena Park", "blue"), ("liam", "Liam Okafor", "green"), ("amelia", "Amelia Lindqvist", "purple"),
        ("noah", "Noah Tanaka", "orange"), ("sofia", "Sofia Reyes", "pink"), ("mateo", "Mateo Hartmann", "teal"),
    ];

    public static readonly string[] EmailDomains = ["modulus.dev", "gmail.com", "outlook.com", "proton.me"];

    /// <summary>Checkbox group options: value, label.</summary>
    public static readonly (string Value, string Label)[] Channels = [("email", "Email"), ("sms", "SMS"), ("slack", "Slack"), ("webhook", "Webhook")];

    /// <summary>End of the Countdown demo: always a few hours ahead, so the timer is running.</summary>
    public static DateTime Deadline() => DateTime.Now.AddHours(5).AddMinutes(30);

    /// <summary>Generated SVG "photos" for the Image demo (no binary assets ship with the samples).</summary>
    public static readonly (string Title, string Src)[] Photos =
    [
        ("Alpine lake", Scene("#8ec5fc", "#e0c3fc", "#4b6cb7", "#182848", "#5b86e5")),
        ("Desert dunes", Scene("#fbd786", "#f7797d", "#c9773d", "#8a4b24", "#e8a060")),
        ("Pine forest", Scene("#a8e063", "#56ab2f", "#2f6b3a", "#173d22", "#3f8f4e")),
    ];

    /// <summary>Images of the Lightbox demo: the Image photos plus a few more, with captions (one left without).</summary>
    public static readonly LightboxItem[] LightboxPhotos =
    [
        new(Photos[0].Src, Photos[0].Title, "Still water below the ridge, just after sunrise."),
        new(Photos[1].Src, Photos[1].Title, "Wind-carved ridges in the late afternoon light."),
        new(Photos[2].Src, Photos[2].Title, "Morning mist over the valley floor."),
        new(Scene("#ffecd2", "#fcb69f", "#b0706a", "#5e3a4a", "#d98f7e"), "Coastal cliffs", "Low tide on the south shore."),
        new(Scene("#c2e9fb", "#a1c4fd", "#7a8fb8", "#3b4a6b", "#8fb3e0"), "Glacier valley"),
        new(Scene("#2c3e50", "#4ca1af", "#1f3a4a", "#0d1b24", "#3a7d8a"), "Night harbour", "Boats at anchor under a clear sky."),
    ];

    /// <summary>Steps of the Tour demo; the targets are buttons in the Tour card.</summary>
    public static readonly TourStep[] TourSteps =
    [
        new("Upload files", "Drop invoices or receipts here. PDF, PNG and CSV up to 20 MB.", "#tour-upload"),
        new("Save your changes", "Saves the draft. Nothing is sent until you publish.", "#tour-save", Placement: "top"),
        new("More actions", "Duplicate, export or archive the document from this menu.", "#tour-more", Placement: "right"),
        new("You're all set", "Restart the tour any time from the Help menu."),
    ];

    /// <summary>Centre image of the QR code demo: the Modulus mark on a rounded square.</summary>
    public static readonly string QrIcon = "data:image/svg+xml;base64," + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
        """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 40 40"><rect width="40" height="40" rx="9" fill="#1677ff"/><path d="M10 29V12h4l6 9 6-9h4v17h-4V19l-6 8.5L14 19v10z" fill="#fff"/></svg>"""));

    /// <summary>Options of the "Select with Add item" demo: product colours, each with a tag colour.</summary>
    public static readonly GridOption[] ProductColors =
        [new("red", "Red", "red"), new("green", "Green", "green"), new("blue", "Blue", "blue"), new("gold", "Gold", "gold")];

    /// <summary>Tags of the multiple "Select with Add item" demo.</summary>
    public static readonly GridOption[] ProductTags =
        [new("new", "New", "cyan"), new("sale", "Sale", "volcano"), new("bestseller", "Bestseller", "purple")];

    /// <summary>The Color picker demo's own swatch row, shown above the recommended one.</summary>
    public static readonly ColorPreset[] ColorPresets =
    [
        new("Brand", ["#066fd1", "#4263eb", "#ae3ec9", "#d6336c", "#f76707", "#2fb344", "#0ca678", "#17a2b8"]),
        ColorPreset.Recommended,
    ];

    internal static string Scene(string sky1, string sky2, string far, string near, string water)
    {
        var svg = $"""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 320 200">
            <defs><linearGradient id="s" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="{sky1}"/><stop offset="1" stop-color="{sky2}"/></linearGradient></defs>
            <rect width="320" height="200" fill="url(#s)"/>
            <circle cx="248" cy="52" r="22" fill="#fff" fill-opacity=".85"/>
            <path d="M0 140 L70 70 L120 120 L170 60 L240 130 L320 90 L320 200 L0 200 Z" fill="{far}"/>
            <path d="M0 160 L60 120 L130 165 L210 115 L320 160 L320 200 L0 200 Z" fill="{near}"/>
            <rect y="172" width="320" height="28" fill="{water}" fill-opacity=".75"/>
            </svg>
            """;
        return "data:image/svg+xml;base64," + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(svg));
    }
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
    public string? Color { get; set; } = "#1677ff";
    public string View { get; set; } = "List";
    public double Discount { get; set; } = 20;
    public decimal Rating { get; set; } = 4;
    public string? Location { get; set; } = "berlin";
    public string? Team { get; set; } = "web";
    public string? Assignee { get; set; }
    public string? Comment { get; set; }
    public string Scheme { get; set; } = "https://";
    public string? Domain { get; set; } = "modulus.dev";
    public string[] Channels { get; set; } = ["email", "slack"];
}
