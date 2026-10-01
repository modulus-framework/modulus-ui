using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions;

namespace Modulus.Samples.Web.Models;

// View models for the shared MVC / Razor Pages page bodies. Pages whose data is a single store
// record (dashboards, invoices …) use that record directly.

/// <summary>A server-rendered, paged and sorted table page.</summary>
public sealed record TablePage<TItem, TQuery>(ProTableResponse<TItem> Page, ProTableRequest Request, TQuery Query);

/// <summary>Account settings: the active tab plus every section's form model.</summary>
public sealed record AccountSettingsPage(
    string Tab,
    ProfileSettingsInput Account,
    ChangePasswordInput Password,
    IReadOnlySet<string> Notifications,
    bool TwoFactor,
    IReadOnlySet<string> ConnectedApps)
{
    public static readonly (string Key, string Label, string Icon)[] Tabs =
        [("account", "Account", "user"), ("security", "Security", "lock"), ("billing", "Billing & plans", "account-book"), ("notifications", "Notifications", "bell"), ("connections", "Connections", "link")];

    public static string Normalize(string? tab) => Tabs.Any(t => t.Key == tab) ? tab! : "account";

    public static AccountSettingsPage Load(SampleStore store, string? tab, ProfileSettingsInput? account = null, ChangePasswordInput? password = null) =>
        new(Normalize(tab), account ?? store.Account(), password ?? new(), store.Notifications().Enabled, store.TwoFactorEnabled,
            SampleContent.ConnectedApps.Where(a => store.IsAppConnected(a.Name)).Select(a => a.Name).ToHashSet());
}

/// <summary>Users list: stat tiles plus one page of the filtered, sorted grid.</summary>
public sealed record UserListPage(UserStats Stats, ProTableResponse<AppUser> Page, ProTableRequest Request, UserQuery Query)
{
    public static UserListPage Load(SampleStore store, UserQuery query, ProTableRequest request) =>
        new(store.UserStats(), store.QueryUsers(query, request), request, query);
}

/// <summary>One user: profile and plan cards plus a tab (account, security, billing).</summary>
public sealed record UserViewPage(AppUser User, string Tab, UserPasswordInput Password, IReadOnlyList<Invoice> Invoices)
{
    public static readonly (string Key, string Label, string Icon)[] Tabs =
        [("account", "Account", "user"), ("security", "Security", "lock"), ("billing", "Billing & plan", "credit-card")];

    public static string Normalize(string? tab) => Tabs.Any(t => t.Key == tab) ? tab! : "account";

    public static UserViewPage Load(SampleStore store, AppUser user, string? tab, UserPasswordInput? password = null) =>
        new(user, Normalize(tab), password ?? new(),
            store.QueryInvoices(new InvoiceQuery(), new ProTableRequest { PageSize = 5 }).Data);
}

/// <summary>Roles: a card per role (member count and avatars) and the module × role access matrix.</summary>
public sealed record RolesPage(IReadOnlyList<AppRole> Roles, IReadOnlyList<AppUser> Users)
{
    public static RolesPage Load(SampleStore store) => new(store.Roles(), store.Users());
}

/// <summary>Permissions list filtered by a keyword.</summary>
public sealed record PermissionsPage(IReadOnlyList<AppPermission> Permissions, string? Keyword)
{
    public static PermissionsPage Load(SampleStore store, string? keyword) =>
        new(store.Permissions().Where(p => string.IsNullOrWhiteSpace(keyword) || p.Name.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase)).ToList(), keyword);
}

/// <summary>Invoice list: summary strip plus one page of the filtered, sorted grid.</summary>
public sealed record InvoiceListPage(InvoiceStats Stats, ProTableResponse<Invoice> Page, ProTableRequest Request, InvoiceQuery Query)
{
    public static InvoiceListPage Load(SampleStore store, InvoiceQuery query, ProTableRequest request) =>
        new(store.InvoiceStats(), store.QueryInvoices(query, request), request, query);
}

/// <summary>Add (Id null) or edit an invoice.</summary>
public sealed record InvoiceEditPage(int? Id, int Number, InvoiceInput Input);

/// <summary>Products list: summary strip plus one page of the filtered, sorted grid.</summary>
public sealed record ProductListPage(ProductStats Stats, ProTableResponse<Product> Page, ProTableRequest Request, ProductQuery Query)
{
    public static ProductListPage Load(SampleStore store, ProductQuery query, ProTableRequest request) =>
        new(store.ProductStats(), store.QueryProducts(query, request), request, query);
}

/// <summary>Orders list: summary strip plus one page of the filtered, sorted grid.</summary>
public sealed record OrderListPage(OrderStats Stats, ProTableResponse<Order> Page, ProTableRequest Request, OrderQuery Query)
{
    public static OrderListPage Load(SampleStore store, OrderQuery query, ProTableRequest request) =>
        new(store.OrderStats(), store.QueryOrders(query, request), request, query);
}

/// <summary>One order with its customer (null when the customer was deleted).</summary>
public sealed record OrderDetailsPage(Order Order, Customer? Customer, int CustomerOrders)
{
    public static OrderDetailsPage Load(SampleStore store, Order order) =>
        new(order, store.FindCustomer(order.CustomerId), store.CustomerOrders(order.CustomerId).Count);
}

/// <summary>Customers list: stat tiles plus one page of the filtered, sorted grid.</summary>
public sealed record CustomerListPage(CustomerStats Stats, IReadOnlyList<string> Cities, ProTableResponse<Customer> Page, ProTableRequest Request, CustomerQuery Query)
{
    public static CustomerListPage Load(SampleStore store, CustomerQuery query, ProTableRequest request) =>
        new(store.CustomerStats(), store.CustomerCities(), store.QueryCustomers(query, request), request, query);
}

/// <summary>One customer with their order history.</summary>
public sealed record CustomerViewPage(Customer Customer, IReadOnlyList<Order> Orders)
{
    public decimal Spent => Orders.Where(o => o.Payment == PaymentStatus.Paid).Sum(o => o.Total);

    public static CustomerViewPage Load(SampleStore store, Customer customer) => new(customer, store.CustomerOrders(customer.Id));
}

/// <summary>Email: folder / label list (or one open message) with the folder counts for the side menu.</summary>
public sealed record EmailPage(MailFolder Folder, string? Label, string? Keyword, IReadOnlyList<MailMessage> Messages, IReadOnlyDictionary<MailFolder, int> Counts, MailMessage? Open)
{
    /// <summary>The list URL for this folder / label (the reading view links back to it).</summary>
    public string ListUrl => "/apps/email" + Scope;

    /// <summary>Query string that keeps the folder / label when opening a message.</summary>
    public string Scope => SampleUi.MailScope(Folder, Label);

    public static EmailPage Load(SampleStore store, string? folder, string? label, string? keyword, int? open = null)
    {
        var f = SampleUi.ParseFolder(folder);
        MailMessage? message = null;
        if (open is { } id && store.FindMail(id) is { } m)
        {
            if (!m.Read) store.UpdateMail(id, x => x.Read = true);
            message = store.FindMail(id);
        }
        return new(f, label, keyword, store.Mail(f, label, keyword), store.MailCounts(), message);
    }
}

/// <summary>Chat: contacts (with their last message) and the open conversation.</summary>
public sealed record ChatPage(IReadOnlyList<(ChatContact Contact, ChatMessage? Last)> Contacts, ChatContact? Active, IReadOnlyList<ChatMessage> Conversation, string? Keyword)
{
    public static ChatPage Load(SampleStore store, int? id, string? keyword)
    {
        var contacts = store.ChatContacts()
            .Where(c => string.IsNullOrWhiteSpace(keyword) || c.Name.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(c => (c, store.LastMessage(c.Id))).ToList();
        var active = id is { } i ? store.FindContact(i) : null;
        return new(contacts, active, active is null ? [] : store.Conversation(active.Id), keyword);
    }
}

/// <summary>Form layouts page: three independent forms; an invalid post re-renders only its own inputs.</summary>
public sealed record FormLayoutsPage(ContactFormInput Contact, SignupFormInput Signup, ShippingFormInput Shipping)
{
    public static FormLayoutsPage Load(ContactFormInput? contact = null, SignupFormInput? signup = null, ShippingFormInput? shipping = null) =>
        new(contact ?? new(), signup ?? new(), shipping ?? new());
}

/// <summary>Account wizard: the current step's fields are inputs, the other steps travel as hidden fields.</summary>
public sealed record WizardPage(WizardInput Input, int Step)
{
    public const int ReviewStep = 3;

    /// <summary>Fields of the other steps, carried as hidden inputs so one form holds the whole wizard.</summary>
    public IEnumerable<(string Name, string? Value)> HiddenFields() =>
        WizardInput.StepFields.Where((_, i) => i != Step).SelectMany(f => f)
            .Select(f => (f, typeof(WizardInput).GetProperty(f)!.GetValue(Input) switch { bool b => b ? "true" : "false", var v => v?.ToString() }));
}

/// <summary>Data table page: one page of the employee directory, the filters, hidden columns and a summary.</summary>
public sealed record EmployeeTablePage(ProTableResponse<Employee> Page, ProTableRequest Request, EmployeeQuery Query, IReadOnlySet<string> Hidden, (int Count, decimal AverageSalary) Summary)
{
    public static EmployeeTablePage Load(EmployeeQuery query, ProTableRequest request, string? hide) =>
        new(EmployeeDirectory.Query(query, request), request, query, EmployeeDirectory.ParseHidden(hide), EmployeeDirectory.Summary(query));

    public bool Shows(string column) => !Hidden.Contains(column);
}

/// <summary>Editable grid page: the schema and rows (saved, or as posted), cell errors and whether they differ from the saved grid.</summary>
public sealed record ProjectGridPage(List<GridColumn> Columns, List<GridRecord> Rows, IReadOnlyList<GridError> Errors, bool Dirty)
{
    public static ProjectGridPage Saved(ProjectGridStore store)
    {
        var (columns, rows) = store.Load();
        return new(columns, rows, [], false);
    }
}

/// <summary>
/// Large-data table (100,000 shipments) in one of three modes: numbered pages, infinite scrolling (keyset cursor) or a
/// virtual table (blocks of rows by page number). <see cref="QueryMs"/> is the time the query took on the server.
/// </summary>
public sealed record LargeTablePage(string Mode, ShipmentFilter Filter, ProTableRequest Request, Modulus.Ui.Abstractions.Data.TablePage<Shipment> Page, double QueryMs)
{
    public static readonly IReadOnlyList<(string Key, string Label)> Modes = [("paged", "Pages"), ("infinite", "Infinite scroll"), ("virtual", "Virtual")];

    /// <summary>Rows per page for infinite scrolling, and per block for the virtual table.</summary>
    public const int InfiniteSize = 50, VirtualBlock = 100;

    public static string ModeOf(string? mode) => Modes.Any(m => m.Key == mode) ? mode! : "paged";

    /// <summary>The page itself: the first page of the mode, counted (the pager and the virtual table need the total).</summary>
    public static LargeTablePage Load(string? mode, ShipmentFilter filter, ProTableRequest request)
    {
        var m = ModeOf(mode);
        if (m != "paged")
        {
            request.Current = 1;
            request.Cursor = null;
            request.PageSize = m == "infinite" ? InfiniteSize : VirtualBlock;
        }
        return Run(m, filter, request, Modulus.Ui.Abstractions.Data.CountMode.Exact);
    }

    /// <summary>More rows (the next infinite page after a cursor, or a virtual block): not counted.</summary>
    public static LargeTablePage Rows(string? mode, ShipmentFilter filter, ProTableRequest request) =>
        Run(ModeOf(mode), filter, request, Modulus.Ui.Abstractions.Data.CountMode.None);

    private static LargeTablePage Run(string mode, ShipmentFilter filter, ProTableRequest request, Modulus.Ui.Abstractions.Data.CountMode count)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var page = Shipments.Page(filter, request, count);
        return new(mode, filter, request, page, watch.Elapsed.TotalMilliseconds);
    }
}
