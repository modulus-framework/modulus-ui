namespace Modulus.Samples.Shared;

/// <summary>
/// Display mappings shared by every sample flavor: labels, badge classes and colours for statuses.
/// Status colours use the Tabler status tones and always ship with a text label.
/// </summary>
public static class SampleUi
{
    /// <summary>Tabler tones for decorative icon tiles, indexed by a tint number.</summary>
    public static readonly string[] Tones = ["blue", "green", "orange", "purple", "teal", "red", "indigo", "pink"];

    public static string Tone(int tint) => Tones[(tint - 1 + Tones.Length) % Tones.Length];

    public static string Label(OrderStatus s) => s switch
    {
        OrderStatus.OutForDelivery => "Out for delivery",
        _ => s.ToString(),
    };

    public static string StatusColor(OrderStatus s) => s switch
    {
        OrderStatus.Delivered => "var(--m-success)",
        OrderStatus.OutForDelivery => "var(--m-primary)",
        OrderStatus.Dispatched => "var(--m-azure)",
        OrderStatus.Pending => "var(--m-warning)",
        _ => "var(--m-gray)",
    };

    public static string OrderBadge(OrderStatus s) => s switch
    {
        OrderStatus.Delivered => "m-badge-green-lt",
        OrderStatus.OutForDelivery => "m-badge-primary-lt",
        OrderStatus.Dispatched => "m-badge-azure-lt",
        OrderStatus.Pending => "m-badge-yellow-lt",
        _ => "m-badge-gray-lt",
    };

    public static string PaymentTone(PaymentStatus p) => p switch
    {
        PaymentStatus.Paid => "green",
        PaymentStatus.Pending => "yellow",
        PaymentStatus.Failed => "red",
        _ => "gray",
    };

    public static string Label(InvoiceStatus s) => s switch
    {
        InvoiceStatus.PartialPayment => "Partial payment",
        InvoiceStatus.PastDue => "Past due",
        _ => s.ToString(),
    };

    public static string InvoiceTone(InvoiceStatus s) => s switch
    {
        InvoiceStatus.Paid => "green",
        InvoiceStatus.Sent => "primary",
        InvoiceStatus.PartialPayment => "azure",
        InvoiceStatus.PastDue => "red",
        InvoiceStatus.Downloaded => "purple",
        _ => "gray",
    };

    public static string InvoiceBadge(InvoiceStatus s) => $"m-badge-{InvoiceTone(s)}-lt";

    public static string InvoiceIcon(InvoiceStatus s) => s switch
    {
        InvoiceStatus.Paid => "circle-check",
        InvoiceStatus.Sent => "send",
        InvoiceStatus.PartialPayment => "chart-pie",
        InvoiceStatus.PastDue => "alert-circle",
        InvoiceStatus.Downloaded => "download",
        _ => "device-floppy",
    };

    public static string ProductBadge(ProductStatus s) => s switch
    {
        ProductStatus.Published => "m-badge-green-lt",
        ProductStatus.Scheduled => "m-badge-yellow-lt",
        _ => "m-badge-gray-lt",
    };

    /// <summary>Stock cell text and status tone (green in stock, yellow low, red out).</summary>
    public static (string Text, string Tone) Stock(Product p) =>
        p.OutOfStock ? ("Out of stock", "red") : p.LowStock ? ($"{p.Stock} left", "yellow") : ($"{p.Stock:N0} in stock", "green");

    public static string CategoryIcon(string category) => category switch
    {
        "Hardware" => "cpu",
        "Software" => "code",
        "Services" => "headset",
        _ => "plug",
    };

    public static string CustomerBadge(CustomerStatus s) => s switch
    {
        CustomerStatus.Active => "m-badge-green-lt",
        CustomerStatus.Trial => "m-badge-azure-lt",
        _ => "m-badge-gray-lt",
    };

    public static string TierTone(CustomerTier t) => t switch
    {
        CustomerTier.Enterprise => "purple",
        CustomerTier.Growth => "azure",
        _ => "gray",
    };

    /// <summary>The next step in the fulfilment flow, or null when the order is closed.</summary>
    public static OrderStatus? NextStatus(OrderStatus s) => s switch
    {
        OrderStatus.Pending => OrderStatus.Dispatched,
        OrderStatus.Dispatched => OrderStatus.OutForDelivery,
        OrderStatus.OutForDelivery => OrderStatus.Delivered,
        _ => null,
    };

    public static string NextStatusAction(OrderStatus next) => next switch
    {
        OrderStatus.Dispatched => "Mark as dispatched",
        OrderStatus.OutForDelivery => "Mark out for delivery",
        _ => "Mark as delivered",
    };

    /// <summary>Shipping activity for the order details timeline: (title, note, time or null when not reached, done).</summary>
    public static IReadOnlyList<(string Title, string Note, DateTime? At, bool Done)> OrderTimeline(Order o)
    {
        var step = o.Status == OrderStatus.Cancelled ? -1 : (int)o.Status;
        (string Title, string Note, double Hours)[] steps =
        [
            ("Order placed", $"Order {o.Number} was placed by {o.CustomerName}", 0),
            ("Dispatched", "Picked, packed and handed to the courier", 20),
            ("Out for delivery", "The courier is on the way", 44),
            ("Delivered", $"Delivered to {o.ShippingAddress}", 52),
        ];
        var items = steps.Select((s, i) => (s.Title, s.Note, i <= step || i == 0 ? o.PlacedAt.AddHours(s.Hours) : (DateTime?)null, i <= step || i == 0)).ToList();
        if (o.Status == OrderStatus.Cancelled) items = [items[0], ("Cancelled", o.Payment == PaymentStatus.Refunded ? "The order was cancelled and refunded" : "The order was cancelled", o.PlacedAt.AddHours(6), true)];
        return items;
    }

    /// <summary>Email side menu: folders in order with their icons.</summary>
    public static readonly (MailFolder Folder, string Icon)[] MailFolders =
        [(MailFolder.Inbox, "mail"), (MailFolder.Sent, "send"), (MailFolder.Draft, "file-text"), (MailFolder.Starred, "star"), (MailFolder.Spam, "alert-octagon"), (MailFolder.Trash, "trash")];

    public static MailFolder ParseFolder(string? folder) => Enum.TryParse<MailFolder>(folder, true, out var f) ? f : MailFolder.Inbox;

    public static string MailCountBadge(MailFolder f) => f switch
    {
        MailFolder.Inbox => "m-badge-primary-lt",
        MailFolder.Spam => "m-badge-red-lt",
        _ => "m-badge-gray-lt",
    };

    /// <summary>Query string that scopes the email app to a folder or label ("" for the inbox).</summary>
    public static string MailScope(MailFolder folder, string? label) =>
        label is { Length: > 0 } ? $"?label={Uri.EscapeDataString(label)}" : folder == MailFolder.Inbox ? "" : $"?folder={folder.ToString().ToLowerInvariant()}";

    public static string UserBadge(UserStatus s) => s switch
    {
        UserStatus.Active => "m-badge-green-lt",
        UserStatus.Pending => "m-badge-yellow-lt",
        _ => "m-badge-gray-lt",
    };

    /// <summary>The four stat tiles above the Users list.</summary>
    public static IReadOnlyList<(string Label, string Value, string Note, string Icon, string Tone)> UserStatTiles(UserStats s) =>
    [
        ("Total users", s.Total.ToString("N0"), "All accounts in the workspace", "users", "primary"),
        ("Paid users", s.PaidPlans.ToString("N0"), $"{Fmt.Percent(s.PaidPlans / (double)Math.Max(1, s.Total))} on a paid plan", "user-plus", "red"),
        ("Active users", s.Active.ToString("N0"), "Signed in during the last 30 days", "user-check", "green"),
        ("Pending users", s.Pending.ToString("N0"), "Invited, not signed in yet", "user-x", "yellow"),
    ];

    public static string RoleIcon(string role) => role switch
    {
        "Admin" => "crown",
        "Editor" => "edit",
        "Author" => "pencil",
        "Maintainer" => "tool",
        _ => "user",
    };

    public static string RoleTone(string role) => role switch
    {
        "Admin" => "red",
        "Editor" => "azure",
        "Author" => "orange",
        "Maintainer" => "green",
        _ => "purple",
    };

    /// <summary>Chat list time: "14:05" today, "Mon" this week, else "Sep 3".</summary>
    public static string ChatTime(DateTime at) =>
        at.Date == DateTime.Today ? Fmt.Time(at) : at > DateTime.Today.AddDays(-6) ? at.ToString("ddd", System.Globalization.CultureInfo.InvariantCulture) : at.ToString("MMM d", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Day separator in a conversation.</summary>
    public static string ChatDay(DateTime day) =>
        day == DateTime.Today ? "Today" : day == DateTime.Today.AddDays(-1) ? "Yesterday" : day.ToString("dddd, MMM d", System.Globalization.CultureInfo.InvariantCulture);

    public static string PresenceTone(Presence p) => p switch
    {
        Presence.Online => "green",
        Presence.Away => "yellow",
        Presence.Busy => "red",
        _ => "gray",
    };

    /// <summary>Calendar categories take the categorical chart slots (identity, not status).</summary>
    public static string EventColor(EventCategory c) => $"var(--m-series-{(int)c + 1})";

    public static string LabelColor(string label) => label switch
    {
        "Personal" => "var(--m-series-3)",
        "Company" => "var(--m-series-1)",
        "Important" => "var(--m-series-2)",
        "Private" => "var(--m-series-7)",
        _ => "var(--m-gray)",
    };

    /// <summary>Due-date chip text and whether it's overdue (open tasks only).</summary>
    public static (string Text, bool Late) KanbanDue(KanbanTask t) =>
        t.Due is not { } d ? ("", false) : (d.ToString("MMM d", System.Globalization.CultureInfo.InvariantCulture), d < DateTime.Today && t.Column != "done");

    public static string KanbanLabelTone(string label) => label switch
    {
        "UX" => "green",
        "Code review" => "red",
        "Dashboard" => "azure",
        "Images" => "orange",
        "App" => "purple",
        _ => "teal",
    };
}
