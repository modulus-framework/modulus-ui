namespace Modulus.Samples.Shared;

/// <summary>Static content for the sample pages: FAQ, pricing, help center, profile.</summary>
public static class SampleContent
{
    public static readonly IReadOnlyList<FaqCategory> Faq =
    [
        new("payment", "Payment", "credit-card", "Get help with payment",
        [
            new("When is payment taken for my order?", "Payment is taken during checkout, when your order is placed. If the card is declined we keep the order for 48 hours so you can retry with another method."),
            new("How do I pay for my order?", "We accept Visa, Mastercard, American Express, PayPal and bank transfer. Enterprise customers can also pay by invoice with 30-day terms."),
            new("We sent you an invoice. How do I pay it?", "Open the invoice from the email or from Invoices in your account, then choose Pay now. Payments usually settle within one business day."),
            new("Can I use more than one payment method?", "Not for a single order. You can split a large purchase into several orders, each with its own payment method."),
        ]),
        new("delivery", "Delivery", "truck", "Shipping and tracking",
        [
            new("How would you ship my order?", "Hardware ships with express couriers (1–3 business days). Software and services are delivered instantly to your account."),
            new("What is the delivery cost?", "Delivery is free for orders over $250. Below that, a flat $12 fee applies. Express next-day delivery costs $24."),
            new("How can I track my shipment?", "You get a tracking link by email as soon as the parcel leaves our warehouse. It also appears on the order page."),
        ]),
        new("returns", "Cancellation & return", "undo", "Changed your mind?",
        [
            new("Can I cancel my order?", "Yes, until it's dispatched. Open the order and choose Cancel; the refund goes back to the original payment method."),
            new("What is the return policy?", "Unused hardware can be returned within 30 days for a full refund. Subscriptions can be cancelled any time and stay active until the period ends."),
            new("How long do refunds take?", "Refunds are issued within 2 business days of receiving the return. Your bank may take 3–5 more days to show the credit."),
        ]),
        new("orders", "My orders", "shopping", "Order changes and history",
        [
            new("Has my order been successful?", "You'll see it in Orders with the status Pending or later, and you'll receive a confirmation email within a few minutes."),
            new("Can I change my order after it's placed?", "Contact support before dispatch and we'll update items or the delivery address."),
            new("Where can I download past invoices?", "Every invoice is under Account settings → Billing & plans → Billing history, and on each order page."),
        ]),
        new("product", "Product & services", "product", "About our products",
        [
            new("Do you offer volume discounts?", "Yes. Orders of 50 seats or 20 devices get 10% off automatically; contact sales for larger volumes."),
            new("Is there a free trial?", "Every plan includes a 14-day free trial with all features. No card is required to start."),
            new("Which regions do you serve?", "We ship hardware to 40 countries and our cloud runs in the US, EU and Asia-Pacific regions."),
        ]),
    ];

    public static readonly IReadOnlyList<PricingPlan> Plans =
    [
        new("Basic", "For small teams getting started", "rocket", 0, 0, false,
            ["Up to 3 users", "10 GB storage", "Core dashboards", "Email support"]),
        new("Standard", "For growing businesses", "thunderbolt", 49, 39, true,
            ["Up to 25 users", "100 GB storage", "All dashboards & apps", "Invoices and payments", "Priority support"]),
        new("Enterprise", "For large organisations", "tb:building", 149, 119, false,
            ["Unlimited users", "1 TB storage", "SSO and audit log", "Custom roles & permissions", "Dedicated success manager"]),
    ];

    /// <summary>Feature comparison rows: feature, Basic, Standard, Enterprise ("✓", "—" or a value).</summary>
    public static readonly IReadOnlyList<(string Feature, string Basic, string Standard, string Enterprise)> PlanFeatures =
    [
        ("Users", "3", "25", "Unlimited"),
        ("Storage", "10 GB", "100 GB", "1 TB"),
        ("Dashboards", "✓", "✓", "✓"),
        ("Invoices & payments", "—", "✓", "✓"),
        ("Roles & permissions", "Basic", "Standard", "Custom"),
        ("API access", "—", "✓", "✓"),
        ("Single sign-on (SSO)", "—", "—", "✓"),
        ("Audit log", "—", "—", "✓"),
        ("Support", "Email", "Priority", "Dedicated manager"),
    ];

    public static readonly IReadOnlyList<HelpCategory> Help =
    [
        new("getting-started", "Getting started", "rocket", 1,
        [
            new("create-account", "Create an account", "Sign up, verify your email and set up your workspace in a few minutes.", ["Go to the sign-up page and enter your name, email and a password of at least 8 characters.", "We send a verification link to your email. Open it to activate the account.", "Invite your team from Users → Add user, and choose a role for each person."]),
            new("first-dashboard", "Build your first dashboard", "Pick the metrics that matter and share them with your team.", ["Open Dashboards → Analytics to see sales, visits and conversion for the last 30 days.", "Use the range selector to compare the last 7, 30 or 90 days."]),
            new("invite-team", "Invite your team", "Add colleagues and give each the right level of access.", ["Open Apps → Users and choose Add user.", "Pick a role; you can fine-tune what each role can do in Roles."]),
        ]),
        new("account", "Account & billing", "credit-card", 2,
        [
            new("change-plan", "Change your plan", "Upgrade, downgrade or cancel at any time.", ["Open Account settings → Billing & plans and choose Upgrade plan.", "Changes apply immediately; we prorate the difference on your next invoice."]),
            new("update-card", "Update a payment method", "Add, remove or set a default card.", ["Go to Account settings → Billing & plans → Payment methods."]),
            new("download-invoices", "Download invoices", "Every invoice is available as a printable page.", ["Open Invoices, choose an invoice and press Print or Download."]),
        ]),
        new("security", "Security", "tb:shield-lock", 3,
        [
            new("two-factor", "Turn on two-step verification", "Protect your account with an authenticator app.", ["Open Account settings → Security → Two-step verification and follow the steps."]),
            new("sessions", "Review active sessions", "See where you're signed in and sign out other devices.", ["The Recent devices table in Security lists every active session."]),
        ]),
        new("apps", "Apps", "appstore", 4,
        [
            new("email", "Using Email", "Folders, labels, stars and bulk actions.", ["Select messages with the checkboxes to move, label or delete them in one go."]),
            new("kanban", "Using the Kanban board", "Plan work by dragging cards between columns.", ["Drag a card to another column to change its status; add tasks at the bottom of any column."]),
            new("calendar", "Using the Calendar", "Schedule events and filter by category.", ["Click a day to add an event, or an event to edit it. Use the filters to show only some categories."]),
        ]),
        new("integrations", "Integrations", "tb:plug", 5,
        [
            new("api-keys", "Create an API key", "Connect your own tools to the REST API.", ["Open Account settings → Connections and create a key with the scopes you need."]),
            new("webhooks", "Receive webhooks", "Get notified when invoices are paid or orders ship.", ["Add an HTTPS endpoint and choose which events to send."]),
        ]),
        new("troubleshooting", "Troubleshooting", "tool", 6,
        [
            new("login-issues", "I can't sign in", "Reset your password or ask an admin to unlock your account.", ["Use Forgot password on the sign-in page. If two-step verification is on and you lost the device, contact an admin."]),
            new("email-not-received", "I didn't get an email", "Check spam and your allow list.", ["Add notifications@modulus.dev to your contacts and check the spam folder."]),
        ]),
    ];

    /// <summary>FAQ entries whose question or answer contains every word of the query.</summary>
    public static List<(string Category, FaqItem Item)> SearchFaq(string? query)
    {
        var words = (query ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return [];
        return [.. from c in Faq from i in c.Items
                   where words.All(w => i.Question.Contains(w, StringComparison.OrdinalIgnoreCase) || i.Answer.Contains(w, StringComparison.OrdinalIgnoreCase))
                   select (c.Title, i)];
    }

    /// <summary>Help articles whose title or excerpt contains every word of the query.</summary>
    public static List<(HelpCategory Category, HelpArticle Article)> SearchHelp(string? query)
    {
        var words = (query ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return [];
        return [.. from c in Help from a in c.Articles
                   where words.All(w => a.Title.Contains(w, StringComparison.OrdinalIgnoreCase) || a.Excerpt.Contains(w, StringComparison.OrdinalIgnoreCase))
                   select (c, a)];
    }

    public static HelpArticle? FindArticle(string slug, out HelpCategory? category)
    {
        foreach (var c in Help)
        {
            var article = c.Articles.FirstOrDefault(a => a.Slug == slug);
            if (article is not null) { category = c; return article; }
        }
        category = null;
        return null;
    }

    public static readonly IReadOnlyList<Project> Projects =
    [
        new(1, "Modulus UI v2", "Internal", "Serena Park", ["Noah Tanaka", "Yuki Tanaka", "Omar Hartmann"], 78, 128, 164, DateTime.Today.AddDays(18), 42_000, 31_900, "block"),
        new(2, "Northwind rollout", "Northwind", "Olivia Bennett", ["Serena Park", "Kwame Mensah"], 52, 46, 88, DateTime.Today.AddDays(40), 128_000, 58_400, "truck"),
        new(3, "Billing migration", "Internal", "Ingrid Lindqvist", ["Priya Silva", "Noah Tanaka"], 91, 71, 78, DateTime.Today.AddDays(6), 36_000, 34_200, "credit-card"),
        new(4, "Mobile app", "Contoso", "Hiro Park", ["Yuki Tanaka", "Elena Rossi", "Mateo Reyes", "Aisha Khan"], 34, 39, 115, DateTime.Today.AddDays(75), 96_000, 21_300, "mobile"),
        new(5, "Security review", "Woodgrove Bank", "Chloe Dubois", ["Serena Park"], 12, 4, 32, DateTime.Today.AddDays(28), 18_000, 1_900, "safety-certificate"),
    ];

    public static readonly IReadOnlyList<Team> Teams =
    [
        new("Design", "Product design and research", "tb:palette", 5, ["Yuki Tanaka", "Elena Rossi", "Hiro Park"], ["UX", "Figma"]),
        new("Engineering", "Frontend, backend and infrastructure", "code", 1, ["Noah Tanaka", "Omar Hartmann", "Arjun Costa", "Lucas Novak"], ["Blazor", "htmx", ".NET"]),
        new("Sales", "New business and renewals", "rise", 2, ["Mateo Reyes", "Priya Silva", "Kwame Mensah"], ["CRM", "EMEA"]),
        new("Finance", "Billing, payroll and reporting", "account-book", 3, ["Ingrid Lindqvist", "Samuel Mensah"], ["Payroll"]),
    ];

    public static readonly IReadOnlyList<Connection> Connections =
    [
        new("Olivia Bennett", "Account manager", 1, 18, 834, 129, true),
        new("Noah Tanaka", "Frontend engineer", 2, 42, 1_280, 312, true),
        new("Aisha Khan", "Operations lead", 3, 9, 402, 88, false),
        new("Yuki Tanaka", "Product designer", 4, 27, 960, 204, true),
        new("Mateo Reyes", "Sales lead", 5, 15, 512, 176, false),
        new("Ingrid Lindqvist", "Finance", 6, 6, 233, 61, true),
    ];

    /// <summary>Recent activity on the profile page (who is always the current user).</summary>
    public static IReadOnlyList<ActivityItem> ProfileActivity() =>
    [
        new(CurrentUser.Name, "paid invoice", "#4981", DateTime.Now.AddMinutes(-12), "tb:file-invoice"),
        new(CurrentUser.Name, "created the project", "Security review", DateTime.Now.AddHours(-3), "tb:briefcase"),
        new(CurrentUser.Name, "added 3 members to", "Engineering", DateTime.Now.AddHours(-7), "tb:users-group"),
        new(CurrentUser.Name, "shipped", "Modulus UI v2 beta", DateTime.Now.AddDays(-1), "rocket"),
        new(CurrentUser.Name, "commented on", "Billing migration", DateTime.Now.AddDays(-2), "message"),
    ];

    public static IReadOnlyList<LoginDevice> Devices() =>
    [
        new("Chrome on Windows", "Desktop", "San Francisco, US", DateTime.Now.AddMinutes(-2), "windows"),
        new("Safari on iPhone", "Mobile", "San Francisco, US", DateTime.Now.AddHours(-5), "mobile"),
        new("Chrome on macOS", "Laptop", "New York, US", DateTime.Now.AddDays(-2), "apple"),
        new("Edge on Windows", "Desktop", "London, UK", DateTime.Now.AddDays(-9), "tb:brand-edge"),
    ];

    public static readonly IReadOnlyList<(string Brand, string Name, string Number, string Expires, bool Primary)> PaymentCards =
        [("tb:brand-visa", "Serena Park", "4242", "12/28", true), ("tb:brand-mastercard", "Serena Park", "5454", "03/27", false)];

    public static readonly IReadOnlyList<(string Icon, string Name, string? Handle)> SocialAccounts =
        [("github", "GitHub", "@serena-park"), ("x", "X", null), ("linkedin", "LinkedIn", "in/serenapark"), ("dribbble", "Dribbble", null)];

    /// <summary>The Standard plan renews on the first of next month.</summary>
    public static DateTime PlanRenewsOn => new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1);

    public static int PlanDaysUsed => Math.Min(30, DateTime.Today.Day);

    public static readonly IReadOnlyList<(string Name, string Description, string Icon, bool Connected)> ConnectedApps =
    [
        ("Google", "Calendar and contacts", "google", true),
        ("Slack", "Notifications in your channels", "slack", true),
        ("GitHub", "Link commits to tasks", "github", false),
        ("Figma", "Embed designs in cards", "tb:brand-figma", false),
        ("Stripe", "Card payments for invoices", "tb:credit-card", true),
    ];

    // User view (Apps → Users → a user) ----------------------------------------------------------

    /// <summary>Price and headline features of the plans users are on.</summary>
    public static (decimal Monthly, string[] Features) UserPlan(string plan) => plan switch
    {
        "Enterprise" => (249, ["Unlimited users", "Unlimited storage", "SSO and audit log", "Dedicated support"]),
        "Company" => (99, ["50 users", "500 GB storage", "Advanced reports", "Priority support"]),
        "Team" => (49, ["10 users", "100 GB storage", "Shared workspaces", "Email support"]),
        _ => (19, ["1 user", "10 GB storage", "Basic reports", "Community support"]),
    };

    /// <summary>Four of the sample projects, picked by user so each user looks different.</summary>
    public static IReadOnlyList<Project> UserProjects(AppUser user) =>
        Enumerable.Range(0, 4).Select(i => Projects[(user.Id + i) % Projects.Count]).ToList();

    public static IReadOnlyList<ActivityItem> UserActivity(AppUser user)
    {
        var projects = UserProjects(user);
        return
        [
            new(user.Name, "completed 4 tasks in", projects[0].Name, DateTime.Now.AddMinutes(-(user.Id * 7 % 50 + 5)), "tb:checks"),
            new(user.Name, "uploaded", "brand-guidelines.pdf", DateTime.Now.AddHours(-(user.Id % 5 + 2)), "upload"),
            new(user.Name, "commented on", projects[1].Name, DateTime.Now.AddHours(-(user.Id % 9 + 9)), "message"),
            new(user.Name, "was added to", projects[2].Name, DateTime.Now.AddDays(-(user.Id % 4 + 1)), "user-add"),
            new(user.Name, "signed in from", "a new device", DateTime.Now.AddDays(-(user.Id % 6 + 5)), "laptop"),
        ];
    }

    // Invoices -----------------------------------------------------------------------------------

    /// <summary>The company issuing the sample invoices.</summary>
    public static readonly (string Name, string[] Address, string Email, string Phone) Seller =
        ("Modulus Inc.", ["548 Market Street, Suite 400", "San Francisco, CA 94104", "United States"], "billing@modulus.dev", "+1 (415) 555-0132");

    public static readonly (string Label, string Value)[] BankDetails =
        [("Bank", "American Bank"), ("Account", "8827 2011 4466"), ("SWIFT", "AMBKUS33"), ("IBAN", "US60 AMBK 0000 8827 2011 44")];
}
