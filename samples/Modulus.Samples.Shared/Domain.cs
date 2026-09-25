using System.ComponentModel.DataAnnotations;

namespace Modulus.Samples.Shared;

// Domain models for the sample apps (users, invoices, mail, chat, calendar, kanban, orders …).
// Shared by the Blazor, MVC and Razor Pages samples; SampleStore holds the data.

/// <summary>The signed-in demo user shown in the header, profile and account pages.</summary>
public static class CurrentUser
{
    public const int Id = 1;
    public const string Name = "Serena Park";
    public const string Email = "serena.park@modulus.dev";
    public const string Role = "Admin";
    public const string Title = "Head of Operations";
}

// Users, roles and permissions ---------------------------------------------------

public enum UserStatus { Active, Pending, Inactive }

public sealed class AppUser
{
    public int Id { get; init; }
    public string Name { get; set; } = "";
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "Subscriber";
    public string Plan { get; set; } = "Basic";
    public UserStatus Status { get; set; }
    public string Company { get; set; } = "";
    public string Country { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Language { get; set; } = "English";
    public DateTime JoinedAt { get; set; }
    public DateTime LastActiveAt { get; set; }
    public int TasksDone { get; set; }
    public int Projects { get; set; }
    public decimal Billed { get; set; }

    /// <summary>1–8, picks the avatar tint (m-av-N).</summary>
    public int Tint => Id % 8 + 1;
}

public sealed class UserInput
{
    [Required(ErrorMessage = "Enter the user's full name"), StringLength(80)]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Enter an email address")]
    [RegularExpression(EmailPattern.Value, ErrorMessage = "Enter a valid email address")]
    public string Email { get; set; } = "";

    [StringLength(60)]
    public string Company { get; set; } = "";

    [StringLength(60)]
    public string Country { get; set; } = "";

    [StringLength(30)]
    public string Phone { get; set; } = "";

    [Required(ErrorMessage = "Choose a role")]
    public string Role { get; set; } = "Subscriber";

    [Required(ErrorMessage = "Choose a plan")]
    public string Plan { get; set; } = "Basic";

    public UserStatus Status { get; set; } = UserStatus.Pending;

    public static UserInput From(AppUser u) => new()
    {
        Name = u.Name, Email = u.Email, Company = u.Company, Country = u.Country, Phone = u.Phone,
        Role = u.Role, Plan = u.Plan, Status = u.Status,
    };
}

public sealed record UserQuery(string? Keyword = null, string? Role = null, string? Plan = null, UserStatus? Status = null);

[Flags]
public enum Access { None = 0, Read = 1, Write = 2, Create = 4 }

public sealed class AppRole
{
    public int Id { get; init; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public Dictionary<string, Access> Permissions { get; set; } = [];
}

public sealed class RoleInput
{
    [Required(ErrorMessage = "Enter a role name"), StringLength(40)]
    public string Name { get; set; } = "";

    [StringLength(160)]
    public string Description { get; set; } = "";

    /// <summary>Module → granted access.</summary>
    public Dictionary<string, Access> Permissions { get; set; } = [];

    /// <summary>
    /// The same grants as "Module|Read" strings — what server-rendered forms post (one checkbox per grant).
    /// </summary>
    public List<string> Grants
    {
        get => [.. Permissions.SelectMany(p => AccessLevels.Where(a => p.Value.HasFlag(a)).Select(a => $"{p.Key}|{a}"))];
        set => Permissions = value
            .Select(g => g.Split('|'))
            .Where(g => g.Length == 2 && Enum.TryParse<Access>(g[1], out _))
            .GroupBy(g => g[0], g => Enum.Parse<Access>(g[1]))
            .ToDictionary(g => g.Key, g => g.Aggregate(Access.None, (all, a) => all | a));
    }

    public static readonly Access[] AccessLevels = [Access.Read, Access.Write, Access.Create];

    public static RoleInput From(AppRole r) => new() { Name = r.Name, Description = r.Description, Permissions = new(r.Permissions) };
}

public sealed class AppPermission
{
    public int Id { get; init; }
    public string Name { get; set; } = "";
    public List<string> Roles { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public bool Core { get; set; }
}

public sealed class PermissionInput
{
    [Required(ErrorMessage = "Enter a permission name"), StringLength(60)]
    public string Name { get; set; } = "";

    public List<string> Roles { get; set; } = [];

    public bool Core { get; set; }
}

// Invoices ---------------------------------------------------------------------------

public enum InvoiceStatus { Draft, Sent, Paid, PartialPayment, PastDue, Downloaded }

public sealed class InvoiceLine
{
    [Required(ErrorMessage = "Describe the item"), StringLength(80)]
    public string Item { get; set; } = "";

    [StringLength(160)]
    public string Description { get; set; } = "";

    [Range(0, 1_000_000, ErrorMessage = "Enter a cost of 0 or more")]
    public decimal Cost { get; set; }

    [Range(1, 10_000, ErrorMessage = "At least 1")]
    public int Qty { get; set; } = 1;

    public decimal Total => Cost * Qty;
}

public sealed class Invoice
{
    public int Id { get; init; }
    public string ClientName { get; set; } = "";
    public string ClientCompany { get; set; } = "";
    public string ClientEmail { get; set; } = "";
    public string ClientAddress { get; set; } = "";
    public string ClientCountry { get; set; } = "";
    public DateTime IssuedAt { get; set; }
    public DateTime DueAt { get; set; }
    public InvoiceStatus Status { get; set; }
    public string Salesperson { get; set; } = CurrentUser.Name;
    public string Note { get; set; } = "";
    public decimal DiscountRate { get; set; }
    public decimal TaxRate { get; set; } = 0.21m;
    public decimal Paid { get; set; }
    public List<InvoiceLine> Lines { get; set; } = [];

    public string Number => $"#{Id}";
    public decimal Subtotal => Lines.Sum(l => l.Total);
    public decimal Discount => Math.Round(Subtotal * DiscountRate, 2);
    public decimal Tax => Math.Round((Subtotal - Discount) * TaxRate, 2);
    public decimal Total => Subtotal - Discount + Tax;
    public decimal Balance => Math.Max(0, Total - Paid);
    public int Tint => Id % 8 + 1;
}

public sealed class InvoiceInput
{
    [Required(ErrorMessage = "Enter the client's name"), StringLength(80)]
    public string ClientName { get; set; } = "";

    [StringLength(80)]
    public string ClientCompany { get; set; } = "";

    [Required(ErrorMessage = "Enter the client's email")]
    [RegularExpression(EmailPattern.Value, ErrorMessage = "Enter a valid email address")]
    public string ClientEmail { get; set; } = "";

    [StringLength(160)]
    public string ClientAddress { get; set; } = "";

    [StringLength(60)]
    public string ClientCountry { get; set; } = "";

    [Required(ErrorMessage = "Choose an issue date")]
    public DateTime? IssuedAt { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Choose a due date")]
    public DateTime? DueAt { get; set; } = DateTime.Today.AddDays(30);

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    [Range(0, 100, ErrorMessage = "0 to 100")]
    public decimal DiscountPercent { get; set; }

    [Range(0, 100, ErrorMessage = "0 to 100")]
    public decimal TaxPercent { get; set; } = 21;

    [StringLength(400)]
    public string Note { get; set; } = "It was a pleasure working with you. We hope you will keep us in mind for future projects.";

    public List<InvoiceLine> Lines { get; set; } = [new() { Item = "", Qty = 1 }];

    public static InvoiceInput From(Invoice i) => new()
    {
        ClientName = i.ClientName, ClientCompany = i.ClientCompany, ClientEmail = i.ClientEmail,
        ClientAddress = i.ClientAddress, ClientCountry = i.ClientCountry, IssuedAt = i.IssuedAt, DueAt = i.DueAt,
        Status = i.Status, DiscountPercent = i.DiscountRate * 100, TaxPercent = i.TaxRate * 100, Note = i.Note,
        Lines = i.Lines.Select(l => new InvoiceLine { Item = l.Item, Description = l.Description, Cost = l.Cost, Qty = l.Qty }).ToList(),
    };
}

public sealed class PaymentInput
{
    [Required(ErrorMessage = "Enter the amount received")]
    [Range(typeof(decimal), "0.01", "10000000", ErrorMessage = "Enter an amount above 0")]
    public decimal? Amount { get; set; }

    [Required(ErrorMessage = "Choose the payment date")]
    public DateTime? PaidAt { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Choose a payment method")]
    public string Method { get; set; } = "Bank transfer";

    [StringLength(200)]
    public string Note { get; set; } = "";

    public static readonly string[] Methods = ["Bank transfer", "Credit card", "PayPal", "Cash", "Cheque"];
}

public sealed record InvoiceQuery(string? Keyword = null, InvoiceStatus? Status = null);

// Email ------------------------------------------------------------------------------

public enum MailFolder { Inbox, Sent, Draft, Starred, Spam, Trash }

public sealed record MailAttachment(string Name, string Size);

public sealed class MailMessage
{
    public int Id { get; init; }
    public string FromName { get; set; } = "";
    public string FromEmail { get; set; } = "";
    public string To { get; set; } = CurrentUser.Email;
    public string Subject { get; set; } = "";
    public List<string> Body { get; set; } = [];
    public DateTime At { get; set; }
    public MailFolder Folder { get; set; } = MailFolder.Inbox;
    public List<string> Labels { get; set; } = [];
    public bool Starred { get; set; }
    public bool Read { get; set; }
    public List<MailAttachment> Attachments { get; set; } = [];
    public int Tint => FromName.Length % 8 + 1;
    public string Preview => Body.Count == 0 ? "" : Body[0];
}

public sealed class ComposeInput
{
    [Required(ErrorMessage = "Add a recipient")]
    [RegularExpression(EmailPattern.Value, ErrorMessage = "Enter a valid email address")]
    public string To { get; set; } = "";

    [Required(ErrorMessage = "Add a subject"), StringLength(120)]
    public string Subject { get; set; } = "";

    [StringLength(4000)]
    public string Body { get; set; } = "";

    /// <summary>A reply (Re:, quoted) or forward (Fwd:) of a message, or a blank message.</summary>
    public static ComposeInput For(MailMessage? message, bool forward) => message is null ? new() : new()
    {
        To = forward ? "" : message.FromEmail,
        Subject = (forward ? "Fwd: " : "Re: ") + message.Subject,
        Body = $"\n\nOn {message.At:MMM d, yyyy HH:mm}, {message.FromName} wrote:\n" + string.Join("\n", message.Body.Select(p => "> " + p)),
    };
}

// Chat -------------------------------------------------------------------------------

public enum Presence { Online, Away, Busy, Offline }

public sealed class ChatContact
{
    public int Id { get; init; }
    public string Name { get; set; } = "";
    public string About { get; set; } = "";
    public Presence Presence { get; set; }
    public int Tint => Id % 8 + 1;
}

public sealed record ChatMessage(int ContactId, bool FromMe, string Text, DateTime At);

// Calendar ---------------------------------------------------------------------------

public enum EventCategory { Business, Personal, Family, Holiday, Other }

public sealed class CalendarEvent
{
    public int Id { get; init; }
    public string Title { get; set; } = "";
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public bool AllDay { get; set; }
    public EventCategory Category { get; set; }
    public string Location { get; set; } = "";
    public string Description { get; set; } = "";
}

public sealed class EventInput
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Give the event a title"), StringLength(80)]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Choose a start")]
    public DateTime? Start { get; set; }

    [Required(ErrorMessage = "Choose an end")]
    public DateTime? End { get; set; }

    public bool AllDay { get; set; }

    public EventCategory Category { get; set; }

    [StringLength(80)]
    public string Location { get; set; } = "";

    [StringLength(400)]
    public string Description { get; set; } = "";

    public static EventInput From(CalendarEvent e) => new()
    {
        Id = e.Id, Title = e.Title, Start = e.Start, End = e.End, AllDay = e.AllDay, Category = e.Category,
        Location = e.Location, Description = e.Description,
    };
}

// Kanban -----------------------------------------------------------------------------

public sealed record KanbanColumn(string Id, string Title);

public sealed class KanbanTask
{
    public int Id { get; init; }
    public string Column { get; set; } = "";
    public int Order { get; set; }
    public string Title { get; set; } = "";
    public string Label { get; set; } = "";
    public DateTime? Due { get; set; }
    public List<string> Assignees { get; set; } = [];
    public int Comments { get; set; }
    public int Attachments { get; set; }
}

public sealed class KanbanTaskInput
{
    [Required(ErrorMessage = "Give the task a title"), StringLength(120)]
    public string Title { get; set; } = "";

    [Required]
    public string Column { get; set; } = "todo";

    public string Label { get; set; } = "UX";
}

// eCommerce orders -------------------------------------------------------------------

public enum OrderStatus { Pending, Dispatched, OutForDelivery, Delivered, Cancelled }

public enum PaymentStatus { Paid, Pending, Failed, Refunded }

public sealed record OrderLine(int ProductId, string Product, string Sku, int Qty, decimal Price)
{
    public decimal Total => Qty * Price;
}

public sealed class Order
{
    public int Id { get; init; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string CustomerEmail { get; set; } = "";
    public DateTime PlacedAt { get; set; }
    public OrderStatus Status { get; set; }
    public PaymentStatus Payment { get; set; }
    public string Method { get; set; } = "";
    public string ShippingAddress { get; set; } = "";
    public List<OrderLine> Lines { get; set; } = [];
    public decimal Shipping { get; set; }
    public string Number => $"#{Id}";
    public decimal Subtotal => Lines.Sum(l => l.Total);
    public decimal Tax => Math.Round(Subtotal * 0.08m, 2);
    public decimal Total => Subtotal + Shipping + Tax;
    public int Tint => CustomerId % 8 + 1;
}

public sealed record OrderQuery(string? Keyword = null, OrderStatus? Status = null, PaymentStatus? Payment = null);

// Projects, content -------------------------------------------------------------------

public sealed record Project(int Id, string Name, string Client, string Leader, IReadOnlyList<string> Team, int Progress, int TasksDone, int TasksTotal, DateTime Deadline, decimal Budget, decimal Spent, string Icon);

public sealed record FaqCategory(string Key, string Title, string Icon, string Subtitle, IReadOnlyList<FaqItem> Items);

public sealed record FaqItem(string Question, string Answer);

public sealed record PricingPlan(string Name, string Tagline, string Icon, decimal Monthly, decimal Yearly, bool Popular, IReadOnlyList<string> Features);

public sealed record HelpCategory(string Key, string Title, string Icon, int Tint, IReadOnlyList<HelpArticle> Articles);

public sealed record HelpArticle(string Slug, string Title, string Excerpt, IReadOnlyList<string> Body);

public sealed record Notice(string Icon, string Title, string Description, DateTime At, bool Read, int Tint);

public sealed record Connection(string Name, string Role, int Tint, int Projects, int Tasks, int Connections, bool Connected);

public sealed record Team(string Name, string Description, string Icon, int Tint, IReadOnlyList<string> Members, IReadOnlyList<string> Tags);

public sealed record LoginDevice(string Browser, string Device, string Location, DateTime At, string Icon);

public sealed record Transaction(string Title, string Subtitle, decimal Amount, string Icon, int Tint);

public sealed record CountrySales(string Code, string Country, decimal Sales, double Change);

public sealed record ChannelShare(string Name, double Value);

// Account settings forms ----------------------------------------------------------------

public sealed class ProfileSettingsInput
{
    [Required(ErrorMessage = "Enter your first name"), StringLength(40)]
    public string FirstName { get; set; } = "Serena";

    [Required(ErrorMessage = "Enter your last name"), StringLength(40)]
    public string LastName { get; set; } = "Park";

    [Required(ErrorMessage = "Enter an email address")]
    [RegularExpression(EmailPattern.Value, ErrorMessage = "Enter a valid email address")]
    public string Email { get; set; } = CurrentUser.Email;

    [StringLength(60)]
    public string Organization { get; set; } = "Modulus";

    [StringLength(30)]
    public string Phone { get; set; } = "+1 202 555 0111";

    [StringLength(120)]
    public string Address { get; set; } = "1 Market Street";

    [StringLength(60)]
    public string City { get; set; } = "San Francisco";

    [StringLength(20)]
    public string Zip { get; set; } = "94105";

    public string Country { get; set; } = "United States";

    public string Language { get; set; } = "English";

    public string TimeZone { get; set; } = "(GMT-08:00) Pacific Time";

    public string Currency { get; set; } = "USD";
}

public sealed class ChangePasswordInput
{
    [Required(ErrorMessage = "Enter your current password")]
    public string Current { get; set; } = "";

    [Required(ErrorMessage = "Enter a new password")]
    [MinLength(8, ErrorMessage = "Use at least 8 characters")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$", ErrorMessage = "Mix upper and lower case letters and a number")]
    public string New { get; set; } = "";

    [Required(ErrorMessage = "Repeat the new password")]
    [Compare(nameof(New), ErrorMessage = "The passwords don't match")]
    public string Confirm { get; set; } = "";
}

/// <summary>An admin setting a user's password (no current password needed).</summary>
public sealed class UserPasswordInput
{
    [Required(ErrorMessage = "Enter a new password")]
    [MinLength(8, ErrorMessage = "Use at least 8 characters")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$", ErrorMessage = "Mix upper and lower case letters and a number")]
    public string New { get; set; } = "";

    [Required(ErrorMessage = "Repeat the new password")]
    [Compare(nameof(New), ErrorMessage = "The passwords don't match")]
    public string Confirm { get; set; } = "";
}

public sealed class NotificationPrefs
{
    public static readonly string[] Events = ["New for you", "Account activity", "A new browser used to sign in", "A new device is linked", "Invoice paid", "Weekly report"];
    public static readonly string[] Channels = ["Email", "Browser", "App"];

    /// <summary>Event|Channel keys that are on.</summary>
    public HashSet<string> Enabled { get; set; } = ["New for you|Email", "New for you|Browser", "New for you|App", "Account activity|Email", "Account activity|Browser", "A new browser used to sign in|Email", "A new device is linked|Email", "A new device is linked|App", "Invoice paid|Email", "Invoice paid|App", "Weekly report|Email"];

    public static string Key(string e, string c) => $"{e}|{c}";
}

// Auth forms ------------------------------------------------------------------------------

public sealed class LoginInput
{
    [Required(ErrorMessage = "Enter your email")]
    [RegularExpression(EmailPattern.Value, ErrorMessage = "Enter a valid email")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Enter your password")]
    [MinLength(6, ErrorMessage = "At least 6 characters")]
    public string Password { get; set; } = "";

    public bool Remember { get; set; } = true;
}

public sealed class RegisterInput
{
    [Required(ErrorMessage = "Choose a username"), StringLength(30, MinimumLength = 3, ErrorMessage = "3 to 30 characters")]
    public string Username { get; set; } = "";

    [Required(ErrorMessage = "Enter your email")]
    [RegularExpression(EmailPattern.Value, ErrorMessage = "Enter a valid email")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Create a password")]
    [MinLength(8, ErrorMessage = "Use at least 8 characters")]
    public string Password { get; set; } = "";

    [Range(typeof(bool), "true", "true", ErrorMessage = "Accept the terms to continue")]
    public bool Terms { get; set; }
}

public sealed class ForgotPasswordInput
{
    [Required(ErrorMessage = "Enter your email")]
    [RegularExpression(EmailPattern.Value, ErrorMessage = "Enter a valid email")]
    public string Email { get; set; } = "";
}

public sealed class ResetPasswordInput
{
    [Required(ErrorMessage = "Create a password")]
    [MinLength(8, ErrorMessage = "Use at least 8 characters")]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Repeat the password")]
    [Compare(nameof(Password), ErrorMessage = "The passwords don't match")]
    public string Confirm { get; set; } = "";
}

public sealed class TwoStepInput
{
    [Required(ErrorMessage = "Enter the 6-digit code")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Enter the 6-digit code")]
    public string Code { get; set; } = "";
}

public static class Lookups
{
    public static readonly string[] Roles = ["Admin", "Editor", "Author", "Maintainer", "Subscriber"];
    public static readonly string[] Plans = ["Basic", "Team", "Company", "Enterprise"];
    public static readonly string[] Countries = ["Australia", "Bangladesh", "Brazil", "Canada", "France", "Germany", "India", "Japan", "Nigeria", "Singapore", "Sweden", "United Arab Emirates", "United Kingdom", "United States"];
    public static readonly string[] Languages = ["English", "French", "German", "Japanese", "Portuguese", "Spanish"];
    public static readonly string[] PermissionModules = ["User management", "Content management", "Disputes management", "Database management", "Financial management", "Reporting", "API control", "Repository management", "Payroll"];
    public static readonly string[] TimeZones = ["(GMT-08:00) Pacific Time", "(GMT-05:00) Eastern Time", "(GMT+00:00) London", "(GMT+01:00) Berlin", "(GMT+05:30) India", "(GMT+06:00) Dhaka", "(GMT+09:00) Tokyo", "(GMT+10:00) Sydney"];
    public static readonly string[] Currencies = ["USD", "EUR", "GBP", "JPY", "INR", "BDT", "AUD"];
    public static readonly string[] MailLabels = ["Personal", "Company", "Important", "Private"];
    public static readonly string[] KanbanLabels = ["UX", "Code review", "Dashboard", "Images", "App", "Charts & maps"];
}
