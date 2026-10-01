using System.Globalization;
using System.Text;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Charts;

namespace Modulus.Samples.Shared;

/// <summary>
/// Content and page models of the Extra pages (activity, billing, logs, music, uptime …), shared by the Blazor,
/// MVC and Razor Pages samples. Query-driven pages build their model here so the three flavors filter alike.
/// </summary>
public static class ExtraContent
{
    private static readonly DateTime Today = DateTime.Today;

    /// <summary>When the sample data was made: "today at 11:00" in the data below is this moment, so nothing is in the future.</summary>
    public static readonly DateTime Now = new(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour, DateTime.Now.Minute, 0);

    private static DateTime At(int daysAgo, int hour, int minute = 0) => Now.AddDays(-daysAgo).AddHours(hour - 11).AddMinutes(minute);

    /// <summary>Splits text around case-insensitive matches of <paramref name="q"/>, for &lt;mark&gt; highlighting.</summary>
    public static IEnumerable<(string Text, bool Hit)> Highlight(string text, string? q)
    {
        if (string.IsNullOrWhiteSpace(q)) { yield return (text, false); yield break; }
        var start = 0;
        while (start < text.Length)
        {
            var i = text.IndexOf(q, start, StringComparison.OrdinalIgnoreCase);
            if (i < 0) break;
            if (i > start) yield return (text[start..i], false);
            yield return (text.Substring(i, q.Length), true);
            start = i + q.Length;
        }
        if (start < text.Length) yield return (text[start..], false);
    }

    private static bool Has(string? text, string? q) => text is not null && text.Contains(q!, StringComparison.OrdinalIgnoreCase);

    // Activity -----------------------------------------------------------------------------------------------

    public static readonly IReadOnlyList<ActivityEvent> Activity =
    [
        new("orders", "shopping-cart", "primary", "Paweł Kuna", "placed order", "#ORD-10482", At(0, 10, 42), "3 items · $1,240.00"),
        new("comments", "message", "azure", "Jeffie Lewzey", "commented on", "Q3 roadmap", At(0, 9, 58), "“Can we move the analytics milestone to the first sprint? Design is ready.”"),
        new("invoices", "file-text", "green", "Mallory Hulme", "paid invoice", "INV-4471", At(0, 9, 15), "$3,480.00 via bank transfer"),
        new("users", "user-add", "purple", "Dunn Slane", "joined the team", "Sales", At(0, 8, 30)),
        new("orders", "truck", "orange", "Warehouse bot", "shipped order", "#ORD-10477", At(0, 7, 5), "UPS 1Z 999 AA1 0123 4567 84"),
        new("files", "upload", "teal", "Emmy Levet", "uploaded", "brand-guidelines-v4.pdf", At(1, 17, 20), "4.2 MB"),
        new("comments", "message", "azure", "Maryjo Lebarree", "replied in", "Onboarding checklist", At(1, 15, 48), "“Done — the SSO step now links to the new article.”"),
        new("invoices", "account-book", "yellow", "Egan Poetz", "sent invoice", "INV-4472", At(1, 14, 2), "$860.00 due in 14 days"),
        new("users", "ri:shield-keyhole", "red", "Kellie Skingley", "changed role of", "Leesa Beaty", At(1, 11, 36), "Viewer → Editor"),
        new("orders", "undo", "gray", "Avivah Mugleston", "refunded order", "#ORD-10431", At(1, 9, 10), "$129.00 returned to card"),
        new("files", "folder", "teal", "Arlie Armstead", "created folder", "Contracts 2026", At(2, 16, 0)),
        new("comments", "message", "azure", "Tessie Curzon", "mentioned you in", "Pricing page copy", At(2, 13, 25), "“@you can you check the yearly discount wording?”"),
        new("invoices", "file-text", "green", "Tabler Store", "received payment for", "INV-4465", At(2, 10, 5), "$12,400.00 via Stripe"),
        new("users", "user-delete", "gray", "System", "deactivated", "former.contractor@modulus.dev", At(3, 18, 12)),
        new("orders", "shopping-cart", "primary", "Nicolas Ducker", "placed order", "#ORD-10470", At(3, 12, 44), "1 item · $89.00"),
    ];

    public static readonly IReadOnlyList<(string Key, string Label)> ActivityTypes =
        [("", "All"), ("orders", "Orders"), ("invoices", "Invoices"), ("comments", "Comments"), ("users", "Team"), ("files", "Files")];

    public static ActivityPage ActivityFeed(string? type)
    {
        type = ActivityTypes.Any(t => t.Key == type) ? type ?? "" : "";
        var items = Activity.Where(a => type == "" || a.Kind == type).ToList();
        var groups = items.GroupBy(a => a.At.Date)
            .Select(g => (Day: g.Key == Now.Date ? "Today" : g.Key == Now.Date.AddDays(-1) ? "Yesterday" : Fmt.Date(g.Key), Items: (IReadOnlyList<ActivityEvent>)g.ToList()))
            .ToList();
        var counts = ActivityTypes.ToDictionary(t => t.Key, t => t.Key == "" ? Activity.Count : Activity.Count(a => a.Kind == t.Key));
        return new(type, groups, counts);
    }

    // Billing ------------------------------------------------------------------------------------------------

    public static readonly IReadOnlyList<(string Label, double Used, double Limit, string Unit)> Usage =
        [("Seats", 18, 25, ""), ("Storage", 64.2, 100, "GB"), ("API calls this month", 72_400, 100_000, ""), ("Automations", 41, 50, "")];

    public static readonly IReadOnlyList<(string Brand, string Icon, string Label, string Detail, bool Default)> PaymentMethods =
    [
        ("Visa", "ri:visa", "Visa ending in 4242", "Expires 08/2028", true),
        ("Mastercard", "ri:mastercard", "Mastercard ending in 5454", "Expires 02/2027", false),
        ("PayPal", "ri:paypal", "PayPal · billing@modulus.dev", "Linked Mar 2026", false),
    ];

    public static readonly IReadOnlyList<(string Number, DateTime Date, string Description, decimal Amount, string Status)> BillingHistory =
    [
        ("MOD-2026-0918", At(10, 9), "Standard plan · 25 seats · Sep 2026", 1225m, "Paid"),
        ("MOD-2026-0818", At(41, 9), "Standard plan · 25 seats · Aug 2026", 1225m, "Paid"),
        ("MOD-2026-0801", At(58, 9), "Extra storage · 50 GB", 90m, "Paid"),
        ("MOD-2026-0718", At(72, 9), "Standard plan · 20 seats · Jul 2026", 980m, "Refunded"),
        ("MOD-2026-0618", At(102, 9), "Standard plan · 20 seats · Jun 2026", 980m, "Paid"),
    ];

    public static string BillingBadge(string status) => status switch { "Paid" => "m-badge-green-lt", "Refunded" => "m-badge-gray-lt", _ => "m-badge-yellow-lt" };

    // Cookie banner ------------------------------------------------------------------------------------------

    public static readonly IReadOnlyList<(string Key, string Title, string Text, bool Required)> CookieCategories =
    [
        ("necessary", "Strictly necessary", "Keep you signed in, remember the theme and protect forms. These can't be switched off.", true),
        ("preferences", "Preferences", "Remember your language, time zone and the layout choices you make.", false),
        ("analytics", "Analytics", "Count visits and see which pages are used, so we know what to improve. Data is aggregated.", false),
        ("marketing", "Marketing", "Measure campaigns and show you relevant offers on other sites.", false),
    ];

    /// <summary>Consent state from the query: null while the visitor hasn't chosen yet.</summary>
    public static CookieConsent Consent(string? consent, IEnumerable<string>? categories, bool settings)
    {
        var chosen = consent switch
        {
            "all" => CookieCategories.Select(c => c.Key).ToHashSet(),
            "necessary" => new HashSet<string> { "necessary" },
            "custom" => (categories ?? []).Append("necessary").ToHashSet(),
            _ => null,
        };
        return new(chosen, settings || chosen is null && consent == "settings");
    }

    // Empty states -------------------------------------------------------------------------------------------

    public static readonly IReadOnlyList<(string Icon, string Title, string Text, string Action)> EmptyStates =
    [
        ("search", "No results found", "Try adjusting your search or filters to find what you're looking for.", "Clear filters"),
        ("bell", "You're all caught up", "New notifications about orders, invoices and mentions will show up here.", "Notification settings"),
        ("folder", "No files yet", "Upload contracts, receipts and images to share them with your team.", "Upload a file"),
    ];

    // Gallery and photo grid ---------------------------------------------------------------------------------

    public static readonly IReadOnlyList<(string Key, string Label)> GalleryCategories =
        [("", "All"), ("mountains", "Mountains"), ("coast", "Coast"), ("desert", "Desert"), ("night", "Night")];

    public static readonly IReadOnlyList<GalleryPhoto> Photos =
    [
        new("mountains", "Alpine lake", "Paweł Kuna", 214, GalleryData.Photos[0].Src),
        new("desert", "Desert dunes", "Jeffie Lewzey", 132, GalleryData.Photos[1].Src),
        new("mountains", "Pine forest", "Mallory Hulme", 96, GalleryData.Photos[2].Src),
        new("coast", "Coastal cliffs", "Dunn Slane", 188, GalleryData.Scene("#ffecd2", "#fcb69f", "#b0706a", "#5e3a4a", "#d98f7e")),
        new("mountains", "Glacier valley", "Emmy Levet", 75, GalleryData.Scene("#c2e9fb", "#a1c4fd", "#7a8fb8", "#3b4a6b", "#8fb3e0")),
        new("night", "Night harbour", "Maryjo Lebarree", 301, GalleryData.Scene("#2c3e50", "#4ca1af", "#1f3a4a", "#0d1b24", "#3a7d8a")),
        new("coast", "Turquoise bay", "Egan Poetz", 158, GalleryData.Scene("#a1ffce", "#faffd1", "#5aa39b", "#2f6b66", "#3cc4c0")),
        new("desert", "Red canyon", "Kellie Skingley", 67, GalleryData.Scene("#f6d365", "#fda085", "#b5543a", "#6e2c1c", "#c9774f")),
        new("night", "Aurora ridge", "Leesa Beaty", 412, GalleryData.Scene("#0f2027", "#2c5364", "#16323f", "#081419", "#2f7f6f")),
        new("coast", "Harbour dawn", "Avivah Mugleston", 91, GalleryData.Scene("#fbc2eb", "#a6c1ee", "#8a7fb0", "#4a4270", "#9fb0e0")),
        new("mountains", "Highland moor", "Arlie Armstead", 54, GalleryData.Scene("#d4fc79", "#96e6a1", "#6c9a5a", "#35583a", "#7fb89a")),
        new("night", "Desert stars", "Tessie Curzon", 233, GalleryData.Scene("#141e30", "#243b55", "#3a2f4a", "#1a1424", "#35507a")),
    ];

    public static IReadOnlyList<GalleryPhoto> GalleryPhotos(string? category) =>
        string.IsNullOrEmpty(category) ? Photos : Photos.Where(p => p.Category == category).ToList();

    public static IReadOnlyList<LightboxItem> Lightbox(IEnumerable<GalleryPhoto> photos) =>
        photos.Select(p => new LightboxItem(p.Src, p.Title, $"Photo by {p.Author}")).ToList();

    // Pay ----------------------------------------------------------------------------------------------------

    public static readonly IReadOnlyList<(string Key, string Label, string Icon)> PayMethods =
        [("card", "Card", "credit-card"), ("paypal", "PayPal", "ri:paypal"), ("bank", "Bank transfer", "bank")];

    public static PayPage Pay(string? plan, string? method)
    {
        var p = SampleContent.Plans.FirstOrDefault(x => string.Equals(x.Name, plan, StringComparison.OrdinalIgnoreCase) && x.Monthly > 0)
            ?? SampleContent.Plans.First(x => x.Popular);
        var subtotal = p.Yearly * 12;
        var discount = Math.Round(subtotal * 0.1m, 2);
        var tax = Math.Round((subtotal - discount) * 0.08m, 2);
        return new(p, PayMethods.Any(m => m.Key == method) ? method! : "card", subtotal, discount, tax, subtotal - discount + tax);
    }

    // Job listing --------------------------------------------------------------------------------------------

    public static readonly IReadOnlyList<Job> Jobs =
    [
        new("senior-frontend", "Senior Frontend Engineer", "Engineering", "Remote · Europe", "Full-time", "$110k – $140k", At(1, 9), ["Blazor", "TypeScript", "Design systems"], true),
        new("backend-dotnet", "Backend Engineer (.NET)", "Engineering", "Warsaw, PL", "Full-time", "$90k – $120k", At(2, 9), ["C#", "PostgreSQL", "Azure"], false),
        new("product-designer", "Product Designer", "Design", "Remote · Worldwide", "Full-time", "$85k – $115k", At(3, 9), ["Figma", "Prototyping", "Research"], true),
        new("support-lead", "Customer Support Lead", "Support", "Austin, TX", "Full-time", "$60k – $75k", At(5, 9), ["Zendesk", "Coaching"], false),
        new("content-writer", "Technical Content Writer", "Marketing", "Remote · Americas", "Part-time", "$40 – $55 / hour", At(6, 9), ["Docs", "SEO"], false),
        new("devops", "DevOps Engineer", "Engineering", "Berlin, DE", "Contract", "€650 / day", At(8, 9), ["Kubernetes", "Terraform", "CI/CD"], false),
        new("account-exec", "Account Executive", "Sales", "London, UK", "Full-time", "£70k + commission", At(9, 9), ["SaaS", "Enterprise"], false),
        new("data-analyst", "Data Analyst", "Operations", "Remote · Europe", "Full-time", "$75k – $95k", At(12, 9), ["SQL", "Power BI", "Python"], false),
        new("design-intern", "Design Intern", "Design", "Warsaw, PL", "Internship", "Paid · 6 months", At(14, 9), ["Figma", "Illustration"], false),
    ];

    public static readonly string[] JobTypes = ["Full-time", "Part-time", "Contract", "Internship"];

    public static readonly string[] JobDepartments = ["Engineering", "Design", "Marketing", "Sales", "Support", "Operations"];

    public static IReadOnlyList<Job> FindJobs(string? q, IReadOnlyCollection<string>? types, string? department, bool remote) =>
        Jobs.Where(j => string.IsNullOrWhiteSpace(q) || Has(j.Title, q) || j.Tags.Any(t => Has(t, q)))
            .Where(j => types is null || types.Count == 0 || types.Contains(j.Type))
            .Where(j => string.IsNullOrEmpty(department) || j.Department == department)
            .Where(j => !remote || j.Location.StartsWith("Remote", StringComparison.Ordinal))
            .ToList();

    // License ------------------------------------------------------------------------------------------------

    public static readonly IReadOnlyList<(string Id, string Title, string[] Paragraphs)> License =
    [
        ("grant", "1. License grant", [
            "Subject to these terms and payment of the applicable fee, Modulus grants you a non-exclusive, non-transferable, worldwide license to use, copy and modify the Software to build End Products for yourself or for one client.",
            "A Standard license covers one End Product. An Extended license covers an unlimited number of End Products for a single organisation."]),
        ("use", "2. Permitted use", [
            "You may use the Software in commercial and non-commercial End Products, change the source code, and combine it with other works.",
            "You may let your client use the End Product you built, provided they do not extract the Software and use it separately."]),
        ("restrictions", "3. Restrictions", [
            "You may not sell, sublicense or redistribute the Software, or any part of it, on its own or as a template, theme, UI kit or starter project, even if modified.",
            "You may not use the Software in a product that lets end users build their own applications or sites from it (for example, a website builder)."]),
        ("ownership", "4. Ownership", [
            "The Software is licensed, not sold. Modulus keeps all rights, title and interest in the Software, including all intellectual property rights.",
            "Third-party components included in the Software (such as icons and fonts) are subject to their own licenses, listed in the NOTICE file."]),
        ("updates", "5. Updates and support", [
            "Your license includes 12 months of updates and support from the date of purchase. After that the Software keeps working; renew to receive newer versions."]),
        ("warranty", "6. No warranty", [
            "The Software is provided “as is”, without warranty of any kind, express or implied, including merchantability, fitness for a particular purpose and non-infringement."]),
        ("liability", "7. Limitation of liability", [
            "In no event shall Modulus be liable for any claim, damages or other liability arising from the use of the Software, beyond the amount you paid for the license."]),
        ("termination", "8. Termination", [
            "This license ends automatically if you breach any of its terms. On termination you must stop using the Software and delete all copies, except End Products already delivered to clients."]),
    ];

    // Logs ---------------------------------------------------------------------------------------------------

    public static readonly string[] LogLevels = ["debug", "info", "warn", "error"];

    public static readonly IReadOnlyList<LogEntry> Logs = BuildLogs();

    private static IReadOnlyList<LogEntry> BuildLogs()
    {
        (string Level, string Source, string Message)[] lines =
        [
            ("info", "api", "GET /api/orders?page=2 200 in 48 ms"),
            ("info", "auth", "User paweł.kuna@modulus.dev signed in (2FA: totp)"),
            ("debug", "cache", "Hit orders:page:2 (ttl 58 s)"),
            ("warn", "api", "Slow request: GET /api/reports/revenue took 2,431 ms"),
            ("info", "jobs", "Job invoice-reminders finished: 14 sent, 0 failed"),
            ("error", "payments", "Stripe webhook signature mismatch for evt_3Qx8… (rejected)"),
            ("info", "api", "POST /api/invoices 201 in 112 ms"),
            ("debug", "db", "Executed DbCommand (7 ms) SELECT … FROM invoices WHERE status = @p0"),
            ("warn", "storage", "Bucket modulus-uploads at 82% of quota"),
            ("info", "mail", "Delivered “Your invoice INV-4472” to billing@acme.io"),
            ("error", "api", "Unhandled exception on GET /api/customers/991: CustomerNotFoundException"),
            ("info", "auth", "Password reset requested for leesa.beaty@modulus.dev"),
            ("debug", "cache", "Miss dashboard:analytics:30d, rebuilding"),
            ("warn", "auth", "5 failed sign-ins for admin@modulus.dev from 185.220.101.4 (locked 15 min)"),
            ("info", "jobs", "Job nightly-backup started"),
            ("info", "jobs", "Job nightly-backup finished in 3 min 12 s (4.8 GB)"),
            ("error", "mail", "SMTP 421 from mx.example.net, retrying in 60 s"),
            ("info", "api", "GET /api/products 200 in 31 ms"),
            ("debug", "db", "Connection pool: 12 active, 38 idle"),
            ("warn", "payments", "Card declined for order #ORD-10479 (insufficient_funds)"),
            ("info", "deploy", "Deployed modulus-web 2.14.0 (build 4812) to production"),
            ("info", "api", "PUT /api/users/42/roles 204 in 64 ms"),
            ("error", "jobs", "Job sync-crm failed: HubSpot API rate limit (429), next retry 10:45"),
            ("info", "auth", "Session expired for dunn.slane@modulus.dev"),
        ];
        var start = Now.AddMinutes(-2);
        return lines.Select((l, i) => new LogEntry(start.AddSeconds(-i * 97 - i * i * 3), l.Level, l.Source, l.Message, $"req-{(0x7a3f + i * 7919) % 0xffff:x4}")).ToList();
    }

    public static IReadOnlyList<LogEntry> FindLogs(string? level, string? q) =>
        Logs.Where(l => string.IsNullOrEmpty(level) || l.Level == level)
            .Where(l => string.IsNullOrWhiteSpace(q) || Has(l.Message, q) || Has(l.Source, q) || Has(l.RequestId, q))
            .ToList();

    public static string LogBadge(string level) => level switch
    {
        "error" => "m-badge-red-lt",
        "warn" => "m-badge-yellow-lt",
        "info" => "m-badge-azure-lt",
        _ => "m-badge-gray-lt",
    };

    // Marketing and text features ----------------------------------------------------------------------------

    public static readonly IReadOnlyList<(string Icon, string Tone, string Title, string Text)> Features =
    [
        ("dashboard", "primary", "Dashboards that ship", "Analytics, CRM, eCommerce and ERP dashboards with real charts, not screenshots."),
        ("form", "green", "Business-grade forms", "Money, phone, lookup, signature and rich text fields with validation on both sides."),
        ("table", "orange", "Data grids", "Sorting, column search, resizing, inline editing and nested rows in one table design."),
        ("ri:palette", "purple", "Themes and layouts", "Fifteen layout presets, light and dark themes and your brand colour in one setting."),
        ("mobile", "teal", "Responsive by default", "Every page works from a 390 px phone to an ultrawide monitor, left-to-right or right-to-left."),
        ("code", "red", "Blazor, MVC and Razor Pages", "The same components and markup in all three, so teams can share one design system."),
    ];

    public static readonly IReadOnlyList<(string Value, string Label)> MarketingStats =
        [("120+", "Components"), ("44", "Ready pages"), ("3", "ASP.NET flavors"), ("0", "JavaScript frameworks")];

    public static readonly IReadOnlyList<(string Quote, string Name, string Role)> Testimonials =
    [
        ("We replaced three internal admin apps with one Modulus shell in a month. The grids alone saved us a quarter.", "Mallory Hulme", "CTO, Northwind Freight"),
        ("Our MVC and Blazor teams finally ship the same UI. Designers stopped filing “it looks different here” bugs.", "Egan Poetz", "Head of Product, Fabrikam"),
        ("The ERP-style menus and business fields fit how our accountants actually work. Onboarding took an afternoon.", "Leesa Beaty", "Finance Ops Lead, Contoso"),
    ];

    public static readonly IReadOnlyList<(string Title, string Text)> Steps =
    [
        ("Install the package", "Add Modulus to your ASP.NET app and register the shell in Program.cs."),
        ("Describe your menu", "One menu contributor feeds the sidebar, the app launcher and breadcrumbs."),
        ("Build your pages", "Use the components as Blazor components or tag helpers — same markup, same CSS."),
    ];

    // Music --------------------------------------------------------------------------------------------------

    public static readonly IReadOnlyList<Album> Albums =
    [
        new("Midnight Transit", "Neon Harbor", "#4a3aa7", "#e87ba4", 2026),
        new("Field Notes", "Paper Lanterns", "#1baf7a", "#eda100", 2025),
        new("Low Tide", "Coastline Radio", "#2a78d6", "#8ec5fc", 2026),
        new("Dust & Chrome", "The Mileposts", "#eb6834", "#fbd786", 2024),
        new("Glass Garden", "Aster Vale", "#008300", "#a8e063", 2025),
        new("After Hours", "Neon Harbor", "#e34948", "#4a3aa7", 2023),
    ];

    public static readonly IReadOnlyList<Track> Tracks =
    [
        new(1, "City Lights Fade", 0, 214, 1_204_331),
        new(2, "Northbound", 0, 187, 882_190),
        new(3, "Paper Planes", 1, 243, 2_310_004),
        new(4, "Low Tide", 2, 198, 640_512),
        new(5, "Undertow", 2, 256, 511_870),
        new(6, "Highway Ghosts", 3, 231, 1_780_455),
        new(7, "Evergreen", 4, 205, 402_118),
        new(8, "Last Train Home", 5, 269, 3_004_761),
    ];

    public static string Duration(int seconds) => $"{seconds / 60}:{seconds % 60:00}";

    /// <summary>Generated album cover: a two-colour gradient with a vinyl disc (no binary assets ship with the samples).</summary>
    public static string Cover(Album a)
    {
        var svg = $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 200 200"><defs><linearGradient id="g" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="{a.From}"/><stop offset="1" stop-color="{a.To}"/></linearGradient></defs><rect width="200" height="200" fill="url(#g)"/><circle cx="140" cy="140" r="70" fill="#000" fill-opacity=".18"/><circle cx="140" cy="140" r="22" fill="#fff" fill-opacity=".35"/><path d="M0 60 Q50 20 100 60 T200 60" stroke="#fff" stroke-opacity=".35" stroke-width="3" fill="none"/></svg>""";
        return "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));
    }

    // Pricing table ------------------------------------------------------------------------------------------

    public static readonly IReadOnlyList<(string Name, decimal Price, string Note, bool Popular)> TablePlans =
    [
        ("Free", 0, "Forever", false),
        ("Starter", 19, "per user / month", false),
        ("Business", 49, "per user / month", true),
        ("Enterprise", 99, "per user / month", false),
    ];

    /// <summary>Rows grouped under headings; values per plan are "✓", "—" or text.</summary>
    public static readonly IReadOnlyList<(string Group, (string Feature, string[] Values)[] Rows)> PricingTable =
    [
        ("Workspace", [
            ("Users", ["2", "10", "50", "Unlimited"]),
            ("Storage", ["2 GB", "50 GB", "500 GB", "Unlimited"]),
            ("Projects", ["3", "Unlimited", "Unlimited", "Unlimited"]),
            ("Custom domain", ["—", "✓", "✓", "✓"])]),
        ("Features", [
            ("Dashboards & reports", ["Basic", "✓", "✓", "✓"]),
            ("Invoices & payments", ["—", "✓", "✓", "✓"]),
            ("Automations", ["—", "100 / mo", "5,000 / mo", "Unlimited"]),
            ("API access", ["—", "—", "✓", "✓"])]),
        ("Security", [
            ("Two-factor authentication", ["✓", "✓", "✓", "✓"]),
            ("Single sign-on (SAML)", ["—", "—", "✓", "✓"]),
            ("Audit log", ["—", "—", "90 days", "Unlimited"]),
            ("Data residency", ["—", "—", "—", "✓"])]),
        ("Support", [
            ("Help center", ["✓", "✓", "✓", "✓"]),
            ("Email support", ["—", "✓", "✓", "✓"]),
            ("Phone & chat support", ["—", "—", "✓", "✓"]),
            ("Dedicated success manager", ["—", "—", "—", "✓"])]),
    ];

    // Search results -----------------------------------------------------------------------------------------

    public static readonly IReadOnlyList<(string Key, string Label)> SearchTabs =
        [("", "All"), ("pages", "Pages"), ("customers", "Customers"), ("files", "Files"), ("help", "Help")];

    public static readonly IReadOnlyList<SearchHit> SearchIndex =
    [
        new("pages", "dashboard", "Analytics dashboard", "/", "Overview › Analytics", "Revenue, sessions and conversion for the last 7, 30 or 90 days, with the top pages and recent payments."),
        new("pages", "ri:building", "ERP analytics dashboard", "/dashboards/erp", "Overview › ERP analytics", "Cash position, receivables aging, inventory turnover and margin by product line in one dashboard."),
        new("pages", "ri:group", "CRM dashboard", "/dashboards/crm", "Overview › CRM", "Pipeline value, deals won and lost per month and the activity of your sales team."),
        new("pages", "file-text", "Invoices", "/apps/invoice/list", "Invoicing › Invoices", "Every invoice with its status, due date and balance. Filter, export, or send reminders."),
        new("pages", "setting", "Account settings", "/pages/account-settings", "Settings › General", "Profile details, password and two-factor authentication, notifications and connected apps."),
        new("customers", "user", "Acme Logistics", "/apps/customers", "Customers › Enterprise", "Customer since 2023 · 42 orders · $128,400 lifetime value. Primary contact: Mallory Hulme."),
        new("customers", "user", "Northwind Freight", "/apps/customers", "Customers › Business", "Customer since 2024 · 18 orders · uses the ERP dashboard for weekly reviews."),
        new("customers", "user", "Fabrikam Studio", "/apps/customers", "Customers › Business", "Customer since 2025 · 7 orders · invoice INV-4472 due in 14 days."),
        new("files", "file-text", "Q3 dashboard review.pdf", "#", "Files › Reports · 2.4 MB", "Slides from the quarterly review: dashboard adoption, revenue by region and next quarter's goals."),
        new("files", "profile", "brand-guidelines-v4.pdf", "#", "Files › Design · 4.2 MB", "Logo, colour, typography and the dashboard illustration style used across the product."),
        new("files", "account-book", "invoice-template.docx", "#", "Files › Finance · 180 KB", "Company invoice template with payment terms and bank details."),
        new("help", "question-circle", "How do I customise a dashboard?", "/pages/help-center", "Help center › Getting started", "Add, move and resize cards, pick a date range and save the layout as your default dashboard."),
        new("help", "question-circle", "Exporting reports to Excel", "/pages/help-center", "Help center › Reports", "Every table and dashboard card can be exported as CSV or XLSX from its menu."),
        new("help", "question-circle", "Setting up two-factor authentication", "/pages/help-center", "Help center › Security", "Use an authenticator app or security key to protect your account."),
    ];

    public static SearchPage Search(string? q, string? tab)
    {
        q = q?.Trim() ?? "";
        tab = SearchTabs.Any(t => t.Key == tab) ? tab ?? "" : "";
        var matches = SearchIndex.Where(h => q.Length == 0 || Has(h.Title, q) || Has(h.Snippet, q) || Has(h.Path, q)).ToList();
        var counts = SearchTabs.ToDictionary(t => t.Key, t => t.Key == "" ? matches.Count : matches.Count(m => m.Kind == t.Key));
        return new(q, tab, matches.Where(m => tab == "" || m.Kind == tab).ToList(), counts);
    }

    // Settings: plan -----------------------------------------------------------------------------------------

    public static PlanQuote Quote(string? plan, string? cycle, int? seats)
    {
        var p = SampleContent.Plans.FirstOrDefault(x => string.Equals(x.Name, plan, StringComparison.OrdinalIgnoreCase)) ?? SampleContent.Plans.First(x => x.Popular);
        var yearly = cycle != "monthly";
        var n = Math.Clamp(seats ?? 18, 1, 500);
        var perSeat = yearly ? p.Yearly : p.Monthly;
        var months = yearly ? 12 : 1;
        return new(p, yearly, n, perSeat, perSeat * n * months);
    }

    public static readonly IReadOnlyList<(string Href, string Icon, string Label)> SettingsNav =
    [
        ("/pages/account-settings", "ri:account-circle", "My account"),
        ("/extra/settings-plan", "crown", "Plan"),
        ("/extra/billing", "credit-card", "Billing"),
        ("/pages/account-settings?tab=notifications", "bell", "Notifications"),
        ("/pages/account-settings?tab=security", "ri:shield-keyhole", "Security"),
    ];

    // Signatures ---------------------------------------------------------------------------------------------

    public static readonly IReadOnlyList<(string Name, string Role, string Document, DateTime At, string Status)> Signatures =
    [
        ("Paweł Kuna", "CEO", "Master services agreement — Acme Logistics", At(0, 9, 12), "Signed"),
        ("Mallory Hulme", "CTO, Northwind Freight", "Data processing addendum", At(1, 16, 40), "Signed"),
        ("Egan Poetz", "Head of Product, Fabrikam", "Statement of work #14", At(2, 11, 5), "Signed"),
        ("Leesa Beaty", "Finance Ops Lead", "Purchase order PO-2291", At(3, 14, 20), "Pending"),
        ("Dunn Slane", "Account Executive", "Order form — Business plan", At(4, 10, 0), "Signed"),
        ("Kellie Skingley", "Legal counsel", "Mutual NDA — Contoso", At(6, 15, 45), "Declined"),
    ];

    /// <summary>A handwritten-looking signature as an SVG image (script font, slanted, with an underline flourish).</summary>
    public static string SignatureSvg(string name)
    {
        var e = System.Net.WebUtility.HtmlEncode(name);
        var svg = $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 320 110"><text x="24" y="66" font-family="'Segoe Script','Brush Script MT','Snell Roundhand',cursive" font-size="40" font-style="italic" fill="#1f2a44" transform="rotate(-4 160 55)">{e}</text><path d="M28 86 C 110 72, 190 96, 296 78" stroke="#1f2a44" stroke-width="2" fill="none" stroke-linecap="round"/></svg>""";
        return "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));
    }

    public static string SignatureBadge(string status) => status switch { "Signed" => "m-badge-green-lt", "Declined" => "m-badge-red-lt", _ => "m-badge-yellow-lt" };

    // Tasks --------------------------------------------------------------------------------------------------

    public static readonly IReadOnlyList<TaskItem> Tasks =
    [
        new("Review Q3 dashboard feedback", "Analytics revamp", 0, "High", "Paweł Kuna", false, ["Research"]),
        new("Fix chart tooltip in RTL layouts", "Analytics revamp", 0, "Medium", "Jeffie Lewzey", true, ["Bug"]),
        new("Write release notes for 2.14", "Release 2.14", 0, "Low", "Emmy Levet", false, ["Docs"]),
        new("Approve invoice INV-4472", "Finance", 0, "High", "Mallory Hulme", false, []),
        new("Prepare onboarding checklist", "Customer success", 1, "Medium", "Maryjo Lebarree", false, ["Process"]),
        new("Migrate reports to the new grid", "Analytics revamp", 2, "High", "Egan Poetz", false, ["Frontend"]),
        new("Renew SSL certificates", "Infrastructure", 3, "Medium", "Kellie Skingley", false, ["Ops"]),
        new("Interview frontend candidates", "Hiring", 4, "Low", "Dunn Slane", false, []),
        new("Update pricing page copy", "Release 2.14", -1, "Medium", "Tessie Curzon", true, ["Marketing"]),
        new("Archive 2025 contracts", "Finance", -2, "Low", "Arlie Armstead", true, []),
        new("Set up nightly backups", "Infrastructure", -3, "High", "Kellie Skingley", true, ["Ops"]),
    ];

    public static readonly IReadOnlyList<(string Key, string Label)> TaskViews =
        [("", "All"), ("today", "Today"), ("upcoming", "Upcoming"), ("done", "Completed")];

    public static TasksPage TaskList(string? view)
    {
        view = TaskViews.Any(v => v.Key == view) ? view ?? "" : "";
        bool In(TaskItem t, string v) => v switch
        {
            "today" => !t.Done && t.DueInDays <= 0,
            "upcoming" => !t.Done && t.DueInDays > 0,
            "done" => t.Done,
            _ => true,
        };
        var items = Tasks.Where(t => In(t, view)).ToList();
        var groups = items.GroupBy(t => t.Project).Select(g => (g.Key, (IReadOnlyList<TaskItem>)g.ToList())).ToList();
        return new(view, groups, TaskViews.ToDictionary(v => v.Key, v => Tasks.Count(t => In(t, v.Key))), Tasks.Count(t => t.Done), Tasks.Count);
    }

    public static string Due(int days) => days switch
    {
        0 => "Today",
        1 => "Tomorrow",
        -1 => "Yesterday",
        _ => Fmt.Date(Today.AddDays(days)),
    };

    public static string PriorityBadge(string p) => p switch { "High" => "m-badge-red-lt", "Medium" => "m-badge-yellow-lt", _ => "m-badge-gray-lt" };

    // Uptime -------------------------------------------------------------------------------------------------

    public static readonly IReadOnlyList<Monitor> Monitors =
    [
        new("Website", "https://modulus.dev", "up", 99.98, 182, Bars(1, [])),
        new("API", "https://api.modulus.dev/health", "up", 99.95, 96, Bars(2, [41])),
        new("Dashboard app", "https://app.modulus.dev", "degraded", 99.71, 640, Bars(3, [12, 58, 59])),
        new("Payments webhook", "https://api.modulus.dev/webhooks/stripe", "up", 100, 74, Bars(4, [])),
        new("Mail relay", "smtp.modulus.dev:587", "down", 98.62, 0, Bars(5, [22, 23, 59])),
        new("CDN", "https://cdn.modulus.dev", "up", 99.99, 38, Bars(6, [])),
    ];

    /// <summary>60 daily states, oldest first: "up", "degraded" (a few minutes down) or "down".</summary>
    private static string[] Bars(int seed, int[] down)
    {
        var bars = new string[60];
        for (var i = 0; i < 60; i++) bars[i] = down.Contains(i) ? "down" : (i * 7 + seed * 13) % 29 == 0 ? "degraded" : "up";
        return bars;
    }

    public static readonly IReadOnlyList<string> UptimeHours = Enumerable.Range(0, 24).Select(h => $"{h:00}:00").ToList();

    public static readonly IReadOnlyList<ChartSeries> ResponseTimes =
    [
        new("API", Enumerable.Range(0, 24).Select(h => 80 + 18 * Math.Sin(h / 3.0) + (h is >= 9 and <= 17 ? 22 : 0)).Select(v => Math.Round(v)).ToList()),
        new("Website", Enumerable.Range(0, 24).Select(h => 160 + 30 * Math.Cos(h / 4.0) + (h is >= 9 and <= 17 ? 35 : 0)).Select(v => Math.Round(v)).ToList()),
    ];

    public static readonly IReadOnlyList<(string Title, string Monitor, string Status, DateTime At, string Duration, string Text)> Incidents =
    [
        ("Mail relay unreachable", "Mail relay", "Investigating", At(0, 9, 48), "42 min", "Connections to smtp.modulus.dev time out. Outgoing mail is queued and will be delivered once the relay is back."),
        ("Elevated dashboard latency", "Dashboard app", "Monitoring", At(0, 7, 15), "2 h 10 min", "Report queries were slow after the 2.14 deploy. A fix is out; we're watching response times."),
        ("API partial outage", "API", "Resolved", At(18, 13, 2), "11 min", "A bad config push returned 502 for 8% of requests. Rolled back at 13:13."),
    ];

    public static string MonitorTone(string status) => status switch { "up" => "success", "degraded" => "warning", _ => "danger" };

    public static string MonitorLabel(string status) => status switch { "up" => "Operational", "degraded" => "Degraded", _ => "Down" };

    public static string IncidentBadge(string status) => status switch { "Resolved" => "m-badge-green-lt", "Monitoring" => "m-badge-azure-lt", _ => "m-badge-red-lt" };

    // Widgets ------------------------------------------------------------------------------------------------

    /// <summary>The current month as weeks of day numbers (0 = padding), Monday first.</summary>
    public static IReadOnlyList<int[]> MonthGrid(DateTime month)
    {
        var first = new DateTime(month.Year, month.Month, 1);
        var pad = ((int)first.DayOfWeek + 6) % 7;
        var days = DateTime.DaysInMonth(month.Year, month.Month);
        var cells = Enumerable.Repeat(0, pad).Concat(Enumerable.Range(1, days)).ToList();
        while (cells.Count % 7 != 0) cells.Add(0);
        return cells.Chunk(7).ToList();
    }

    public static string MonthName(DateTime d) => d.ToString("MMMM yyyy", CultureInfo.InvariantCulture);

    public static readonly IReadOnlyList<ChartPoint> VisitorsTrend =
        new double[] { 42, 48, 45, 60, 58, 72, 69, 81, 77, 90, 94, 102 }.Select((v, i) => new ChartPoint($"Week {i + 1}", v * 20)).ToArray();

    public static readonly IReadOnlyList<ChartSlice> StorageSlices =
        [new("Documents", 21.4), new("Images", 18.2), new("Backups", 16.9), new("Other", 7.7)];

    public static readonly IReadOnlyList<(string Label, int Done, int Goal, string Tone)> Goals =
        [("New customers", 38, 50, "primary"), ("Invoices paid", 112, 120, "green"), ("Support tickets closed", 64, 100, "orange")];
}

public sealed record ActivityEvent(string Kind, string Icon, string Tone, string Actor, string Action, string Target, DateTime At, string? Detail = null);

public sealed record ActivityPage(string Type, IReadOnlyList<(string Day, IReadOnlyList<ActivityEvent> Items)> Groups, IReadOnlyDictionary<string, int> Counts);

public sealed record CookieConsent(IReadOnlySet<string>? Chosen, bool ShowSettings)
{
    public bool Decided => Chosen is not null;
    public bool Allows(string key) => Chosen?.Contains(key) ?? key == "necessary";
}

public sealed record GalleryPhoto(string Category, string Title, string Author, int Likes, string Src);

public sealed record PayPage(PricingPlan Plan, string Method, decimal Subtotal, decimal Discount, decimal Tax, decimal Total);

public sealed record Job(string Id, string Title, string Department, string Location, string Type, string Salary, DateTime Posted, string[] Tags, bool Featured);

public sealed record LogEntry(DateTime At, string Level, string Source, string Message, string RequestId);

public sealed record Album(string Title, string Artist, string From, string To, int Year);

public sealed record Track(int No, string Title, int Album, int Seconds, int Plays);

public sealed record SearchHit(string Kind, string Icon, string Title, string Href, string Path, string Snippet);

public sealed record SearchPage(string Q, string Tab, IReadOnlyList<SearchHit> Hits, IReadOnlyDictionary<string, int> Counts);

public sealed record PlanQuote(PricingPlan Plan, bool Yearly, int Seats, decimal PerSeat, decimal Total);

public sealed record TaskItem(string Title, string Project, int DueInDays, string Priority, string Assignee, bool Done, string[] Tags);

public sealed record TasksPage(string View, IReadOnlyList<(string Project, IReadOnlyList<TaskItem> Items)> Groups, IReadOnlyDictionary<string, int> Counts, int Done, int Total);

public sealed record Monitor(string Name, string Url, string Status, double Uptime, int ResponseMs, string[] Days);
