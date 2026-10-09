using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Kanban;

namespace Modulus.Samples.Shared;

// App data: users, roles, permissions, invoices, mail, chat, calendar, kanban and orders.
// Every read returns a snapshot (a copied list) and every write takes the lock, so Blazor
// circuits and MVC requests can share the singleton safely.
public sealed partial class SampleStore
{
    private readonly List<AppUser> _users = [];
    private readonly List<AppRole> _roles = [];
    private readonly List<AppPermission> _permissions = [];
    private readonly List<Invoice> _invoices = [];
    private readonly List<MailMessage> _mail = [];
    private readonly List<ChatContact> _contacts = [];
    private readonly List<ChatMessage> _messages = [];
    private readonly List<CalendarEvent> _events = [];
    private readonly List<KanbanTask> _tasks = [];
    private readonly List<Order> _orders = [];
    private int _nextUserId, _nextRoleId, _nextPermissionId, _nextInvoiceId, _nextMailId, _nextEventId, _nextTaskId;

    public static readonly IReadOnlyList<KanbanColumn> KanbanColumns =
    [
        new("todo", "To do"),
        new("progress", "In progress"),
        new("review", "In review"),
        new("done", "Done"),
    ];

    private void SeedApps()
    {
        var rng = new Random(11);
        var now = DateTime.Now;
        var today = DateTime.Today;

        // Users -------------------------------------------------------------------------
        string[] roles = ["Admin", "Editor", "Author", "Maintainer", "Subscriber"];
        string[] plans = ["Basic", "Team", "Company", "Enterprise"];
        _users.Add(new AppUser
        {
            Id = 1, Name = CurrentUser.Name, Username = "serena", Email = CurrentUser.Email, Role = "Admin", Plan = "Enterprise",
            Status = UserStatus.Active, Company = "Modulus", Country = "United States", Phone = "+1 202 555 0111",
            JoinedAt = today.AddYears(-3).AddDays(-41), LastActiveAt = now, TasksDone = 1_230, Projects = 568, Billed = 12_480,
        });
        for (var id = 2; id <= 64; id++)
        {
            var first = FirstNames[rng.Next(FirstNames.Length)];
            var last = LastNames[rng.Next(LastNames.Length)];
            var company = Companies[rng.Next(Companies.Length)];
            _users.Add(new AppUser
            {
                Id = id,
                Name = $"{first} {last}",
                Username = $"{first.ToLowerInvariant()}{last[0]}".Replace("á", "a"),
                Email = $"{first.ToLowerInvariant()}.{last.ToLowerInvariant()}@{company.ToLowerInvariant().Replace(" ", "").Replace("'", "")}.com".Replace("á", "a"),
                Role = roles[rng.Next(10) switch { 0 => 0, < 3 => 1, < 5 => 2, < 7 => 3, _ => 4 }],
                Plan = plans[rng.Next(plans.Length)],
                Status = (UserStatus)(rng.Next(10) switch { < 6 => 0, < 8 => 1, _ => 2 }),
                Company = company,
                Country = Lookups.Countries[rng.Next(Lookups.Countries.Length)],
                Phone = $"+1 ({rng.Next(200, 999)}) {rng.Next(200, 999)}-{rng.Next(1000, 9999)}",
                Language = Lookups.Languages[rng.Next(Lookups.Languages.Length)],
                JoinedAt = today.AddDays(-rng.Next(30, 1_100)),
                LastActiveAt = now.AddMinutes(-rng.Next(2, 60 * 24 * 20)),
                TasksDone = rng.Next(20, 1_400),
                Projects = rng.Next(1, 90),
                Billed = rng.Next(0, 12_000),
            });
        }
        _nextUserId = _users.Count + 1;

        // Roles and permissions ------------------------------------------------------------
        var all = Access.Read | Access.Write | Access.Create;
        _roles.AddRange(
        [
            new AppRole { Id = 1, Name = "Admin", Description = "Full access to every module, billing and user management.", Permissions = Lookups.PermissionModules.ToDictionary(m => m, _ => all) },
            new AppRole { Id = 2, Name = "Editor", Description = "Creates and publishes content, manages disputes.", Permissions = new() { ["Content management"] = all, ["Disputes management"] = Access.Read | Access.Write, ["Reporting"] = Access.Read, ["User management"] = Access.Read } },
            new AppRole { Id = 3, Name = "Author", Description = "Writes content and reads reports.", Permissions = new() { ["Content management"] = Access.Read | Access.Create, ["Reporting"] = Access.Read } },
            new AppRole { Id = 4, Name = "Maintainer", Description = "Keeps databases, repositories and the API healthy.", Permissions = new() { ["Database management"] = all, ["Repository management"] = all, ["API control"] = Access.Read | Access.Write } },
            new AppRole { Id = 5, Name = "Subscriber", Description = "Reads content and their own reports.", Permissions = new() { ["Reporting"] = Access.Read } },
        ]);
        _nextRoleId = _roles.Count + 1;

        (string Name, string[] Roles, bool Core)[] permissions =
        [
            ("Management", ["Admin"], true),
            ("Manage billing & roles", ["Admin"], true),
            ("Add & remove users", ["Admin", "Maintainer"], true),
            ("Project planning", ["Admin", "Editor", "Author"], false),
            ("Manage email sequences", ["Admin", "Editor"], false),
            ("Client communication", ["Admin", "Maintainer"], false),
            ("Only view", ["Admin", "Subscriber"], false),
            ("Financial management", ["Admin", "Editor"], false),
            ("Manage others' tasks", ["Admin"], false),
            ("Export reports", ["Admin", "Editor", "Subscriber"], false),
        ];
        _permissions.AddRange(permissions.Select((p, i) => new AppPermission { Id = i + 1, Name = p.Name, Roles = [.. p.Roles], Core = p.Core, CreatedAt = today.AddDays(-9 * (i + 1)).AddHours(10 + i) }));
        _nextPermissionId = _permissions.Count + 1;

        // Invoices ---------------------------------------------------------------------------
        (string Item, string Description, decimal Cost)[] catalog =
        [
            ("Premium branding package", "Branding & promotion", 32),
            ("Social media", "Social media templates", 28),
            ("Web design", "Landing page and components", 1_200),
            ("Development", "Frontend build, 40 hours", 95),
            ("SEO audit", "Technical and content audit", 640),
            ("Hosting (12 months)", "Managed cloud hosting", 480),
            ("Support retainer", "Priority support, monthly", 350),
        ];
        var statuses = new[] { InvoiceStatus.Paid, InvoiceStatus.Paid, InvoiceStatus.Sent, InvoiceStatus.Draft, InvoiceStatus.PastDue, InvoiceStatus.PartialPayment, InvoiceStatus.Downloaded, InvoiceStatus.Paid };
        for (var i = 0; i < 48; i++)
        {
            var c = _customers[rng.Next(_customers.Count)];
            var issued = today.AddDays(-rng.Next(0, 200));
            var invoice = new Invoice
            {
                Id = 4_987 + i,
                ClientName = c.Name,
                ClientCompany = c.Company,
                ClientEmail = c.Email,
                ClientAddress = $"{rng.Next(3, 980)} {LastNames[rng.Next(LastNames.Length)]} Street, {c.City}",
                ClientCountry = Lookups.Countries[rng.Next(Lookups.Countries.Length)],
                IssuedAt = issued,
                DueAt = issued.AddDays(30),
                Status = statuses[rng.Next(statuses.Length)],
                DiscountRate = rng.Next(4) == 0 ? 0.05m : 0,
                Note = "It was a pleasure working with you. We hope you will keep us in mind for future projects.",
            };
            foreach (var index in Enumerable.Range(0, catalog.Length).OrderBy(_ => rng.Next()).Take(rng.Next(1, 4)))
            {
                var (item, description, cost) = catalog[index];
                invoice.Lines.Add(new InvoiceLine { Item = item, Description = description, Cost = cost, Qty = cost > 500 ? 1 : rng.Next(1, 12) });
            }
            invoice.Paid = invoice.Status switch
            {
                InvoiceStatus.Paid or InvoiceStatus.Downloaded => invoice.Total,
                InvoiceStatus.PartialPayment => Math.Round(invoice.Total * 0.4m, 2),
                _ => 0,
            };
            _invoices.Add(invoice);
        }
        _nextInvoiceId = 4_987 + _invoices.Count;

        // Mail -------------------------------------------------------------------------------
        (string From, string Email, string Subject, string[] Body, string[] Labels)[] mail =
        [
            ("Olivia Bennett", "olivia@northwind.com", "Q3 contract renewal", ["Hi Serena, thanks for the call yesterday. I've attached the redlined renewal with the changes we discussed: annual billing, the 5% volume discount and the new SLA appendix.", "Could you have legal look at clause 7.2 before Friday? We'd like to sign before the quarter closes.", "Best, Olivia"], ["Company", "Important"]),
            ("GitHub", "noreply@github.com", "[modulus/ui] Release v2.4.0 published", ["A new release was published by Noah Tanaka.", "Highlights: the Components gallery, responsive shell, htmx 2.0.11 and Alpine 3.17.4, and 13 new tag helper tests."], ["Company"]),
            ("Aisha Khan", "aisha.khan@fabrikam.com", "Onboarding checklist for the new region", ["Hi team, sharing the onboarding checklist for the Singapore office. The only open item is the payroll integration; everything else is ready for Monday.", "I'll set up a 30-minute walkthrough on Thursday."], ["Company"]),
            ("Mateo Reyes", "mateo@litware.com", "Lunch next week?", ["Hey! I'm in town next Tuesday and Wednesday. Want to grab lunch and catch up? The new ramen place near the office is supposed to be great."], ["Personal"]),
            ("Stripe", "receipts@stripe.com", "Your receipt from Modulus #2291-4471", ["Receipt for $12,400.00 paid on " + today.AddDays(-1).ToString("MMM d, yyyy") + ".", "Payment method: Visa ending 4242."], ["Private"]),
            ("Yuki Tanaka", "yuki@contoso.com", "Design review notes", ["Attached are my notes from today's review. Main themes: tighten the table density, keep one primary action per card, and add empty states to the reports.", "Happy to pair on the empty states tomorrow."], ["Company"]),
            ("Priya Silva", "priya.silva@tailspin.com", "Invoice INV-4990 is past due", ["Hi Serena, a quick reminder that invoice INV-4990 was due last week. Could you let me know when we can expect the payment?", "Thanks, Priya"], ["Important"]),
            ("Linear", "notifications@linear.app", "You were assigned 3 issues", ["MOD-412 Kanban: keyboard reordering", "MOD-415 Calendar: week view", "MOD-418 Invoice: PDF export"], ["Company"]),
            ("Elena Rossi", "elena@coho.com", "Photos from the offsite", ["Here's the album from the offsite, the sunset one came out really well! Feel free to use any of them for the blog post."], ["Personal"]),
            ("Chloe Dubois", "chloe@woodgrove.com", "Security review: action needed", ["Our annual security review is due at the end of the month. Please complete the vendor questionnaire and upload the latest SOC 2 report."], ["Important", "Private"]),
            ("Omar Hartmann", "omar@proseware.com", "Re: API rate limits", ["Thanks, raising the limit to 600 requests a minute fixed the batch job. We'll monitor over the weekend and report back."], ["Company"]),
            ("Nora Andersson", "nora@wingtip.com", "Newsletter draft for October", ["Draft attached. I kept it to three stories and one CTA. Let me know if you want to swap the customer story."], ["Company"]),
        ];
        for (var i = 0; i < mail.Length; i++)
        {
            var m = mail[i];
            _mail.Add(new MailMessage
            {
                Id = i + 1, FromName = m.From, FromEmail = m.Email, Subject = m.Subject, Body = [.. m.Body], Labels = [.. m.Labels],
                At = now.AddMinutes(-(i * i * 37 + 14)), Read = i > 3 && i % 3 != 0, Starred = i is 0 or 5 or 9,
                Attachments = i is 0 or 5 ? [new("contract-renewal.pdf", "1.4 MB"), new("sla-appendix.docx", "86 KB")] : [],
            });
        }
        (string Subject, string To, string[] Body)[] sent =
        [
            ("Re: Q3 contract renewal", "olivia@northwind.com", ["Thanks Olivia, forwarding to legal now. We should have comments by Thursday."]),
            ("Roadmap for Q4", "team@modulus.dev", ["Hi all, the Q4 roadmap is in the shared drive. Please add your estimates by Friday."]),
        ];
        foreach (var (subject, to, body) in sent)
            _mail.Add(new MailMessage { Id = _mail.Count + 1, FromName = CurrentUser.Name, FromEmail = CurrentUser.Email, To = to, Subject = subject, Body = [.. body], At = now.AddHours(-_mail.Count * 5), Folder = MailFolder.Sent, Read = true });
        _mail.Add(new MailMessage { Id = _mail.Count + 1, FromName = CurrentUser.Name, FromEmail = CurrentUser.Email, To = "board@modulus.dev", Subject = "Board update (draft)", Body = ["Revenue grew 12% quarter over quarter…"], At = now.AddDays(-2), Folder = MailFolder.Draft, Read = true });
        _mail.Add(new MailMessage { Id = _mail.Count + 1, FromName = "Prize Center", FromEmail = "winner@prize-center.biz", Subject = "You've won a $500 gift card!!!", Body = ["Click here to claim your prize."], At = now.AddDays(-3), Folder = MailFolder.Spam });
        _mail.Add(new MailMessage { Id = _mail.Count + 1, FromName = "Samuel Mensah", FromEmail = "samuel@lamna.com", Subject = "Old meeting notes", Body = ["Notes from the June planning session."], At = now.AddDays(-40), Folder = MailFolder.Trash, Read = true });
        _nextMailId = _mail.Count + 1;

        // Chat -------------------------------------------------------------------------------
        (string Name, string About, Presence Presence)[] contacts =
        [
            ("Olivia Bennett", "Account manager, Northwind", Presence.Online),
            ("Noah Tanaka", "Frontend engineer", Presence.Online),
            ("Aisha Khan", "Operations, Fabrikam", Presence.Away),
            ("Mateo Reyes", "Sales lead", Presence.Busy),
            ("Yuki Tanaka", "Product designer", Presence.Online),
            ("Kwame Mensah", "Customer success", Presence.Offline),
            ("Ingrid Lindqvist", "Finance", Presence.Offline),
        ];
        _contacts.AddRange(contacts.Select((c, i) => new ChatContact { Id = i + 1, Name = c.Name, About = c.About, Presence = c.Presence }));
        (int Contact, bool Me, string Text, int MinutesAgo)[] chat =
        [
            (1, false, "Hi Serena! Do you have a minute to go over the renewal?", 95),
            (1, true, "Sure, I'm free until 3. What's on your mind?", 93),
            (1, false, "Legal flagged clause 7.2. They want the liability cap tied to annual fees, not total fees.", 92),
            (1, true, "That's reasonable. I'll check with our counsel, but I don't expect pushback.", 90),
            (1, false, "Great, thanks! If we can sign by Friday we keep the Q3 pricing.", 88),
            (1, true, "Understood. I'll get back to you by tomorrow noon 👍", 86),
            (1, false, "Perfect. Talk soon!", 12),
            (2, false, "The release build is green. Want me to tag v2.4.0?", 240),
            (2, true, "Yes please, and post the changelog in #releases.", 236),
            (2, false, "Done ✅", 230),
            (3, false, "Payroll integration is the last open item for Singapore.", 600),
            (3, true, "Thanks Aisha, I'll ping finance today.", 590),
            (4, false, "Closed the Litware deal! 🎉", 1_500),
            (4, true, "Amazing work Mateo!", 1_490),
            (5, false, "Shared the new empty-state illustrations in Figma.", 2_900),
        ];
        _messages.AddRange(chat.Select(c => new ChatMessage(c.Contact, c.Me, c.Text, now.AddMinutes(-c.MinutesAgo))));

        // Calendar ---------------------------------------------------------------------------
        var month = new DateTime(today.Year, today.Month, 1);
        (int Day, int Hour, int Hours, string Title, EventCategory Category, string Location, bool AllDay)[] events =
        [
            (2, 9, 1, "Design review", EventCategory.Business, "Room 4B", false),
            (3, 0, 0, "Dart game?", EventCategory.Personal, "", true),
            (5, 14, 2, "Quarterly planning", EventCategory.Business, "Main hall", false),
            (7, 0, 0, "Family trip", EventCategory.Family, "Lake Tahoe", true),
            (8, 0, 0, "Family trip", EventCategory.Family, "Lake Tahoe", true),
            (10, 11, 1, "Doctor's appointment", EventCategory.Personal, "City clinic", false),
            (12, 16, 1, "Customer call: Northwind", EventCategory.Business, "Zoom", false),
            (14, 0, 0, "Company holiday", EventCategory.Holiday, "", true),
            (15, 10, 1, "Sprint demo", EventCategory.Business, "Room 2A", false),
            (15, 18, 3, "Book club", EventCategory.Other, "Library café", false),
            (18, 13, 1, "Lunch with Mateo", EventCategory.Personal, "Ramen Ichi", false),
            (20, 9, 8, "Product offsite", EventCategory.Business, "Sausalito", false),
            (22, 19, 2, "Mom's birthday dinner", EventCategory.Family, "Home", false),
            (24, 15, 1, "1:1 with Noah", EventCategory.Business, "Room 4B", false),
            (26, 0, 0, "Public holiday", EventCategory.Holiday, "", true),
            (28, 8, 1, "Monthly close", EventCategory.Business, "Finance", false),
            (today.Day, 10, 1, "Standup", EventCategory.Business, "Zoom", false),
            (today.Day, 15, 1, "Interview: senior designer", EventCategory.Business, "Room 1C", false),
        ];
        foreach (var e in events)
        {
            var day = Math.Min(e.Day, DateTime.DaysInMonth(month.Year, month.Month));
            var start = month.AddDays(day - 1).AddHours(e.Hour);
            _events.Add(new CalendarEvent
            {
                Id = _events.Count + 1, Title = e.Title, Start = start, End = e.AllDay ? start : start.AddHours(e.Hours),
                AllDay = e.AllDay, Category = e.Category, Location = e.Location,
            });
        }
        _nextEventId = _events.Count + 1;

        // Kanban -----------------------------------------------------------------------------
        (string Column, string Title, string Label, int DueDays, string[] Who, int Comments, int Files)[] tasks =
        [
            ("todo", "Research FAQ page UX", "UX", 6, ["Yuki Tanaka", "Noah Tanaka"], 4, 1),
            ("todo", "Review JavaScript code", "Code review", 9, ["Noah Tanaka"], 2, 0),
            ("todo", "Draft Q4 OKRs", "Dashboard", 12, ["Serena Park", "Mateo Reyes", "Aisha Khan"], 7, 2),
            ("progress", "Admin dashboard charts", "Charts & maps", 3, ["Noah Tanaka", "Yuki Tanaka"], 12, 3),
            ("progress", "Create product photos", "Images", 4, ["Elena Rossi"], 1, 8),
            ("progress", "Mobile app onboarding", "App", 7, ["Hiro Park", "Yuki Tanaka"], 5, 2),
            ("review", "Invoice PDF export", "Code review", 1, ["Noah Tanaka"], 9, 1),
            ("review", "Pricing page copy", "UX", 2, ["Nora Andersson"], 3, 0),
            ("done", "Set up CI pipeline", "App", -3, ["Omar Hartmann"], 6, 0),
            ("done", "Customer interviews", "UX", -8, ["Yuki Tanaka", "Serena Park"], 14, 4),
        ];
        foreach (var t in tasks)
        {
            _tasks.Add(new KanbanTask
            {
                Id = _tasks.Count + 1, Column = t.Column, Order = _tasks.Count(x => x.Column == t.Column), Title = t.Title, Label = t.Label,
                Due = today.AddDays(t.DueDays), Assignees = [.. t.Who], Comments = t.Comments, Attachments = t.Files,
            });
        }
        _nextTaskId = _tasks.Count + 1;

        // Orders -----------------------------------------------------------------------------
        string[] methods = ["Mastercard •••• 2412", "Visa •••• 4242", "PayPal", "Amex •••• 1005", "Apple Pay"];
        for (var i = 0; i < 60; i++)
        {
            var c = _customers[rng.Next(_customers.Count)];
            var status = (OrderStatus)(rng.Next(12) switch { < 5 => 3, < 7 => 2, < 9 => 1, < 11 => 0, _ => 4 });
            var order = new Order
            {
                Id = 6_979 - i,
                CustomerId = c.Id, CustomerName = c.Name, CustomerEmail = c.Email,
                PlacedAt = now.AddHours(-(i * 9.5 + rng.Next(0, 6))),
                Status = status,
                Payment = status == OrderStatus.Cancelled ? PaymentStatus.Refunded : (PaymentStatus)(rng.Next(10) switch { < 7 => 0, < 9 => 1, _ => 2 }),
                Method = methods[rng.Next(methods.Length)],
                ShippingAddress = $"{rng.Next(3, 980)} {LastNames[rng.Next(LastNames.Length)]} Avenue, {c.City}",
                Shipping = rng.Next(3) == 0 ? 0 : 12,
            };
            foreach (var p in _products.OrderBy(_ => rng.Next()).Take(rng.Next(1, 4)))
                order.Lines.Add(new OrderLine(p.Id, p.Name, p.Sku, rng.Next(1, 4), p.Price));
            _orders.Add(order);
        }
    }

    // Users ------------------------------------------------------------------------------------

    public ProTableResponse<AppUser> QueryUsers(UserQuery query, ProTableRequest request)
    {
        lock (_gate)
        {
            IEnumerable<AppUser> rows = _users;
            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                var k = query.Keyword.Trim();
                rows = rows.Where(u => u.Name.Contains(k, StringComparison.OrdinalIgnoreCase) || u.Email.Contains(k, StringComparison.OrdinalIgnoreCase) || u.Company.Contains(k, StringComparison.OrdinalIgnoreCase));
            }
            if (!string.IsNullOrEmpty(query.Role)) rows = rows.Where(u => u.Role == query.Role);
            if (!string.IsNullOrEmpty(query.Plan)) rows = rows.Where(u => u.Plan == query.Plan);
            if (query.Status is { } s) rows = rows.Where(u => u.Status == s);
            rows = request.SortField is null
                ? rows.OrderBy(u => u.Id)
                : rows.SortBy(request, nameof(AppUser.Name), u => u.Name)
                      .SortBy(request, nameof(AppUser.Role), u => u.Role)
                      .SortBy(request, nameof(AppUser.Plan), u => u.Plan)
                      .SortBy(request, nameof(AppUser.Billed), u => u.Billed)
                      .SortBy(request, nameof(AppUser.JoinedAt), u => u.JoinedAt);
            return rows.ToPage(request);
        }
    }

    public IReadOnlyList<AppUser> Users()
    {
        lock (_gate) return _users.ToList();
    }

    public AppUser? FindUser(int id)
    {
        lock (_gate) return _users.FirstOrDefault(u => u.Id == id);
    }

    public AppUser AddUser(UserInput input)
    {
        lock (_gate)
        {
            var user = new AppUser { Id = _nextUserId++, JoinedAt = DateTime.Today, LastActiveAt = DateTime.Now };
            ApplyUser(user, input);
            _users.Add(user);
            return user;
        }
    }

    public bool UpdateUser(int id, UserInput input)
    {
        lock (_gate)
        {
            var user = _users.FirstOrDefault(u => u.Id == id);
            if (user is null) return false;
            ApplyUser(user, input);
            return true;
        }
    }

    public bool SetUserStatus(int id, UserStatus status)
    {
        lock (_gate)
        {
            var user = _users.FirstOrDefault(u => u.Id == id);
            if (user is null) return false;
            user.Status = status;
            return true;
        }
    }

    public int DeleteUsers(IEnumerable<int> ids)
    {
        var set = ids.Where(id => id != CurrentUser.Id).ToHashSet();
        lock (_gate) return _users.RemoveAll(u => set.Contains(u.Id));
    }

    private static void ApplyUser(AppUser u, UserInput input)
    {
        u.Name = input.Name?.Trim() ?? "";
        u.Email = input.Email?.Trim() ?? "";
        u.Username = u.Email.Split('@')[0];
        u.Company = input.Company?.Trim() ?? "";
        u.Country = input.Country?.Trim() ?? "";
        u.Phone = input.Phone?.Trim() ?? "";
        u.Role = input.Role;
        u.Plan = input.Plan;
        u.Status = input.Status;
    }

    public UserStats UserStats()
    {
        lock (_gate)
        {
            return new UserStats(
                Total: _users.Count,
                Active: _users.Count(u => u.Status == UserStatus.Active),
                Pending: _users.Count(u => u.Status == UserStatus.Pending),
                Inactive: _users.Count(u => u.Status == UserStatus.Inactive),
                PaidPlans: _users.Count(u => u.Plan != "Basic"));
        }
    }

    // Roles & permissions --------------------------------------------------------------------

    public IReadOnlyList<AppRole> Roles()
    {
        lock (_gate) return _roles.Select(r => new AppRole { Id = r.Id, Name = r.Name, Description = r.Description, Permissions = new(r.Permissions) }).ToList();
    }

    public AppRole? FindRole(int id)
    {
        lock (_gate) return _roles.FirstOrDefault(r => r.Id == id) is { } r ? new AppRole { Id = r.Id, Name = r.Name, Description = r.Description, Permissions = new(r.Permissions) } : null;
    }

    public AppRole SaveRole(int? id, RoleInput input)
    {
        lock (_gate)
        {
            var role = id is { } existing ? _roles.First(r => r.Id == existing) : null;
            if (role is null)
            {
                role = new AppRole { Id = _nextRoleId++ };
                _roles.Add(role);
            }
            else if (role.Name != input.Name.Trim())
            {
                foreach (var user in _users.Where(u => u.Role == role.Name)) user.Role = input.Name.Trim();
            }
            role.Name = input.Name.Trim();
            role.Description = input.Description?.Trim() ?? "";
            role.Permissions = input.Permissions.Where(p => p.Value != Access.None).ToDictionary(p => p.Key, p => p.Value);
            return role;
        }
    }

    public IReadOnlyList<AppPermission> Permissions()
    {
        lock (_gate) return _permissions.Select(p => new AppPermission { Id = p.Id, Name = p.Name, Roles = [.. p.Roles], Core = p.Core, CreatedAt = p.CreatedAt }).ToList();
    }

    public AppPermission SavePermission(int? id, PermissionInput input)
    {
        lock (_gate)
        {
            var permission = id is { } existing ? _permissions.First(p => p.Id == existing) : null;
            if (permission is null)
            {
                permission = new AppPermission { Id = _nextPermissionId++, CreatedAt = DateTime.Now };
                _permissions.Add(permission);
            }
            permission.Name = input.Name.Trim();
            permission.Roles = [.. input.Roles];
            permission.Core = input.Core;
            return permission;
        }
    }

    public bool DeletePermission(int id)
    {
        lock (_gate) return _permissions.RemoveAll(p => p.Id == id && !p.Core) > 0;
    }

    // Invoices -----------------------------------------------------------------------------------

    public ProTableResponse<Invoice> QueryInvoices(InvoiceQuery query, ProTableRequest request)
    {
        lock (_gate)
        {
            IEnumerable<Invoice> rows = _invoices;
            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                var k = query.Keyword.Trim().TrimStart('#');
                rows = rows.Where(i => i.Id.ToString().Contains(k) || i.ClientName.Contains(k, StringComparison.OrdinalIgnoreCase) || i.ClientCompany.Contains(k, StringComparison.OrdinalIgnoreCase));
            }
            if (query.Status is { } s) rows = rows.Where(i => i.Status == s);
            rows = request.SortField is null
                ? rows.OrderByDescending(i => i.IssuedAt)
                : rows.SortBy(request, "Id", i => i.Id)
                      .SortBy(request, nameof(Invoice.ClientName), i => i.ClientName)
                      .SortBy(request, nameof(Invoice.Total), i => i.Total)
                      .SortBy(request, nameof(Invoice.IssuedAt), i => i.IssuedAt)
                      .SortBy(request, nameof(Invoice.Balance), i => i.Balance);
            return rows.ToPage(request);
        }
    }

    public Invoice? FindInvoice(int id)
    {
        lock (_gate) return _invoices.FirstOrDefault(i => i.Id == id);
    }

    public int NextInvoiceNumber
    {
        get { lock (_gate) return _nextInvoiceId; }
    }

    public Invoice SaveInvoice(int? id, InvoiceInput input)
    {
        lock (_gate)
        {
            var invoice = id is { } existing ? _invoices.FirstOrDefault(i => i.Id == existing) : null;
            if (invoice is null)
            {
                invoice = new Invoice { Id = _nextInvoiceId++ };
                _invoices.Add(invoice);
            }
            invoice.ClientName = input.ClientName.Trim();
            invoice.ClientCompany = input.ClientCompany?.Trim() ?? "";
            invoice.ClientEmail = input.ClientEmail.Trim();
            invoice.ClientAddress = input.ClientAddress?.Trim() ?? "";
            invoice.ClientCountry = input.ClientCountry?.Trim() ?? "";
            invoice.IssuedAt = input.IssuedAt ?? DateTime.Today;
            invoice.DueAt = input.DueAt ?? DateTime.Today.AddDays(30);
            invoice.Status = input.Status;
            invoice.DiscountRate = input.DiscountPercent / 100;
            invoice.TaxRate = input.TaxPercent / 100;
            invoice.Note = input.Note?.Trim() ?? "";
            invoice.Lines = input.Lines.Where(l => !string.IsNullOrWhiteSpace(l.Item))
                .Select(l => new InvoiceLine { Item = l.Item.Trim(), Description = l.Description?.Trim() ?? "", Cost = l.Cost, Qty = l.Qty }).ToList();
            if (invoice.Status is InvoiceStatus.Paid) invoice.Paid = invoice.Total;
            return invoice;
        }
    }

    public int DeleteInvoices(IEnumerable<int> ids)
    {
        var set = ids.ToHashSet();
        lock (_gate) return _invoices.RemoveAll(i => set.Contains(i.Id));
    }

    public bool MarkInvoiceSent(int id)
    {
        lock (_gate)
        {
            var invoice = _invoices.FirstOrDefault(i => i.Id == id);
            if (invoice is null) return false;
            if (invoice.Status == InvoiceStatus.Draft) invoice.Status = InvoiceStatus.Sent;
            return true;
        }
    }

    public bool RecordPayment(int id, decimal amount)
    {
        lock (_gate)
        {
            var invoice = _invoices.FirstOrDefault(i => i.Id == id);
            if (invoice is null || amount <= 0) return false;
            invoice.Paid = Math.Min(invoice.Total, invoice.Paid + amount);
            invoice.Status = invoice.Balance == 0 ? InvoiceStatus.Paid : InvoiceStatus.PartialPayment;
            return true;
        }
    }

    public InvoiceStats InvoiceStats()
    {
        lock (_gate)
        {
            return new InvoiceStats(
                Clients: _invoices.Select(i => i.ClientEmail).Distinct().Count(),
                Invoices: _invoices.Count,
                Paid: _invoices.Where(i => i.Status is InvoiceStatus.Paid or InvoiceStatus.Downloaded).Sum(i => i.Total),
                Unpaid: _invoices.Sum(i => i.Balance));
        }
    }

    // Mail ---------------------------------------------------------------------------------------

    public IReadOnlyList<MailMessage> Mail(MailFolder folder, string? label = null, string? keyword = null)
    {
        lock (_gate)
        {
            IEnumerable<MailMessage> rows = folder == MailFolder.Starred
                ? _mail.Where(m => m.Starred && m.Folder is not MailFolder.Trash and not MailFolder.Spam)
                : _mail.Where(m => m.Folder == folder);
            if (!string.IsNullOrEmpty(label)) rows = _mail.Where(m => m.Labels.Contains(label) && m.Folder is not MailFolder.Trash and not MailFolder.Spam);
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var k = keyword.Trim();
                rows = rows.Where(m => m.Subject.Contains(k, StringComparison.OrdinalIgnoreCase) || m.FromName.Contains(k, StringComparison.OrdinalIgnoreCase) || m.Preview.Contains(k, StringComparison.OrdinalIgnoreCase));
            }
            return rows.OrderByDescending(m => m.At).Select(CloneMail).ToList();
        }
    }

    public MailMessage? FindMail(int id)
    {
        lock (_gate) return _mail.FirstOrDefault(m => m.Id == id) is { } m ? CloneMail(m) : null;
    }

    public IReadOnlyDictionary<MailFolder, int> MailCounts()
    {
        lock (_gate)
        {
            return new Dictionary<MailFolder, int>
            {
                [MailFolder.Inbox] = _mail.Count(m => m.Folder == MailFolder.Inbox && !m.Read),
                [MailFolder.Draft] = _mail.Count(m => m.Folder == MailFolder.Draft),
                [MailFolder.Spam] = _mail.Count(m => m.Folder == MailFolder.Spam && !m.Read),
            };
        }
    }

    public void UpdateMail(int id, Action<MailMessage> change)
    {
        lock (_gate)
        {
            if (_mail.FirstOrDefault(m => m.Id == id) is { } m) change(m);
        }
    }

    public void MoveMail(IEnumerable<int> ids, MailFolder folder)
    {
        var set = ids.ToHashSet();
        lock (_gate)
        {
            // Deleting from the trash removes the message for good.
            if (folder == MailFolder.Trash) _mail.RemoveAll(m => set.Contains(m.Id) && m.Folder == MailFolder.Trash);
            foreach (var m in _mail.Where(m => set.Contains(m.Id))) m.Folder = folder;
        }
    }

    public MailMessage SendMail(ComposeInput input, bool draft = false)
    {
        lock (_gate)
        {
            var message = new MailMessage
            {
                Id = _nextMailId++, FromName = CurrentUser.Name, FromEmail = CurrentUser.Email, To = (input.To ?? "").Trim(), Subject = (input.Subject ?? "").Trim(),
                Body = (input.Body ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                At = DateTime.Now, Folder = draft ? MailFolder.Draft : MailFolder.Sent, Read = true,
            };
            _mail.Add(message);
            return message;
        }
    }

    private static MailMessage CloneMail(MailMessage m) => new()
    {
        Id = m.Id, FromName = m.FromName, FromEmail = m.FromEmail, To = m.To, Subject = m.Subject, Body = [.. m.Body], At = m.At,
        Folder = m.Folder, Labels = [.. m.Labels], Starred = m.Starred, Read = m.Read, Attachments = [.. m.Attachments],
    };

    // Chat ---------------------------------------------------------------------------------------

    public IReadOnlyList<ChatContact> ChatContacts()
    {
        lock (_gate) return _contacts.OrderByDescending(c => _messages.Where(m => m.ContactId == c.Id).Select(m => m.At).DefaultIfEmpty().Max()).ToList();
    }

    public ChatContact? FindContact(int id)
    {
        lock (_gate) return _contacts.FirstOrDefault(c => c.Id == id);
    }

    public IReadOnlyList<ChatMessage> Conversation(int contactId)
    {
        lock (_gate) return _messages.Where(m => m.ContactId == contactId).OrderBy(m => m.At).ToList();
    }

    public ChatMessage? LastMessage(int contactId)
    {
        lock (_gate) return _messages.Where(m => m.ContactId == contactId).MaxBy(m => m.At);
    }

    /// <summary>Sends a message and returns it plus an automatic reply (so the demo feels alive).</summary>
    public IReadOnlyList<ChatMessage> SendChat(int contactId, string text)
    {
        text = text.Trim();
        if (text.Length == 0) return [];
        lock (_gate)
        {
            var mine = new ChatMessage(contactId, true, text, DateTime.Now);
            _messages.Add(mine);
            var contact = _contacts.FirstOrDefault(c => c.Id == contactId);
            if (contact is null || contact.Presence == Presence.Offline) return [mine];
            string[] replies = ["Sounds good!", "Got it, thanks 👍", "Let me check and get back to you.", "Perfect, talk soon.", "Makes sense to me."];
            var reply = new ChatMessage(contactId, false, replies[_messages.Count % replies.Length], DateTime.Now.AddSeconds(1));
            _messages.Add(reply);
            return [mine, reply];
        }
    }

    // Calendar -----------------------------------------------------------------------------------

    public IReadOnlyList<CalendarEvent> Events(DateTime from, DateTime to)
    {
        lock (_gate) return _events.Where(e => e.End >= from && e.Start < to).OrderBy(e => e.Start).ThenBy(e => !e.AllDay).ToList();
    }

    public CalendarEvent? FindEvent(int id)
    {
        lock (_gate) return _events.FirstOrDefault(e => e.Id == id);
    }

    public CalendarEvent SaveEvent(EventInput input)
    {
        lock (_gate)
        {
            var e = input.Id is { } id ? _events.FirstOrDefault(x => x.Id == id) : null;
            if (e is null)
            {
                e = new CalendarEvent { Id = _nextEventId++ };
                _events.Add(e);
            }
            e.Title = input.Title.Trim();
            e.AllDay = input.AllDay;
            e.Start = input.AllDay ? input.Start!.Value.Date : input.Start!.Value;
            e.End = input.End is { } end && end >= e.Start ? (input.AllDay ? end.Date : end) : e.Start.AddHours(input.AllDay ? 0 : 1);
            e.Category = input.Category;
            e.Location = input.Location?.Trim() ?? "";
            e.Description = input.Description?.Trim() ?? "";
            return e;
        }
    }

    public bool DeleteEvent(int id)
    {
        lock (_gate) return _events.RemoveAll(e => e.Id == id) > 0;
    }

    // Kanban -------------------------------------------------------------------------------------

    public IReadOnlyList<KanbanTask> Tasks()
    {
        lock (_gate) return _tasks.OrderBy(t => t.Order).Select(t => new KanbanTask { Id = t.Id, Column = t.Column, Order = t.Order, Title = t.Title, Label = t.Label, Due = t.Due, Assignees = [.. t.Assignees], Comments = t.Comments, Attachments = t.Attachments }).ToList();
    }

    public KanbanTask AddTask(KanbanTaskInput input)
    {
        lock (_gate)
        {
            var task = new KanbanTask
            {
                Id = _nextTaskId++, Column = KanbanColumns.Any(c => c.Id == input.Column) ? input.Column : "todo",
                Title = input.Title.Trim(), Label = input.Label, Due = DateTime.Today.AddDays(7), Assignees = [CurrentUser.Name],
            };
            task.Order = _tasks.Count(t => t.Column == task.Column);
            _tasks.Add(task);
            return task;
        }
    }

    /// <summary>Moves a task to <paramref name="column"/> at <paramref name="index"/> and renumbers both columns.</summary>
    public bool MoveTask(int id, string column, int index)
    {
        lock (_gate)
        {
            var task = _tasks.FirstOrDefault(t => t.Id == id);
            if (task is null || KanbanColumns.All(c => c.Id != column)) return false;
            var from = task.Column;
            var target = _tasks.Where(t => t.Column == column && t.Id != id).OrderBy(t => t.Order).ToList();
            target.Insert(Math.Clamp(index, 0, target.Count), task);
            task.Column = column;
            for (var i = 0; i < target.Count; i++) target[i].Order = i;
            if (from != column)
            {
                var source = _tasks.Where(t => t.Column == from).OrderBy(t => t.Order).ToList();
                for (var i = 0; i < source.Count; i++) source[i].Order = i;
            }
            return true;
        }
    }

    public bool DeleteTask(int id)
    {
        lock (_gate) return _tasks.RemoveAll(t => t.Id == id) > 0;
    }

    // Orders -------------------------------------------------------------------------------------

    public ProTableResponse<Order> QueryOrders(OrderQuery query, ProTableRequest request)
    {
        lock (_gate)
        {
            IEnumerable<Order> rows = _orders;
            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                var k = query.Keyword.Trim().TrimStart('#');
                rows = rows.Where(o => o.Id.ToString().Contains(k) || o.CustomerName.Contains(k, StringComparison.OrdinalIgnoreCase));
            }
            if (query.Status is { } s) rows = rows.Where(o => o.Status == s);
            if (query.Payment is { } p) rows = rows.Where(o => o.Payment == p);
            rows = request.SortField is null
                ? rows.OrderByDescending(o => o.PlacedAt)
                : rows.SortBy(request, "Id", o => o.Id)
                      .SortBy(request, nameof(Order.PlacedAt), o => o.PlacedAt)
                      .SortBy(request, nameof(Order.CustomerName), o => o.CustomerName)
                      .SortBy(request, nameof(Order.Total), o => o.Total);
            return rows.ToPage(request);
        }
    }

    public Order? FindOrder(int id)
    {
        lock (_gate) return _orders.FirstOrDefault(o => o.Id == id);
    }

    public bool SetOrderStatus(int id, OrderStatus status)
    {
        lock (_gate)
        {
            var order = _orders.FirstOrDefault(o => o.Id == id);
            if (order is null) return false;
            order.Status = status;
            if (status == OrderStatus.Cancelled && order.Payment == PaymentStatus.Paid) order.Payment = PaymentStatus.Refunded;
            return true;
        }
    }

    public IReadOnlyDictionary<OrderStatus, int> OrderCounts()
    {
        lock (_gate) return Enum.GetValues<OrderStatus>().ToDictionary(s => s, s => _orders.Count(o => o.Status == s));
    }

    public OrderStats OrderStats()
    {
        lock (_gate)
            return new(_orders.Count(o => o.Payment == PaymentStatus.Pending), _orders.Count(o => o.Status == OrderStatus.Delivered),
                _orders.Count(o => o.Payment == PaymentStatus.Refunded), _orders.Count(o => o.Payment == PaymentStatus.Failed));
    }

    public IReadOnlyList<Order> CustomerOrders(int customerId)
    {
        lock (_gate) return _orders.Where(o => o.CustomerId == customerId).OrderByDescending(o => o.PlacedAt).ToList();
    }
}

public sealed record UserStats(int Total, int Active, int Pending, int Inactive, int PaidPlans);

public sealed record InvoiceStats(int Clients, int Invoices, decimal Paid, decimal Unpaid);
