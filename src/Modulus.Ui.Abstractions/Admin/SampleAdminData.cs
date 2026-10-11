namespace Modulus.Ui.Abstractions.Admin;

/// <summary>
/// In-memory sample data behind every scaffolded admin page, so a freshly generated app shows working pages. It is shared by
/// everyone who uses the app and lost on restart. Replace it per interface by registering your own implementation; the
/// pages then stop saying "Sample data".
/// </summary>
public sealed class SampleAdminData :
    IUserDirectory, IRoleDirectory, ITenantDirectory, INotificationCenter, IAuditTrail, IAdminSettingsStore, IMetricsSource
{
    private readonly object _gate = new();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private readonly List<AdminUser> _users =
    [
        new("u1", "Ada Lovelace", "ada@example.com", "Admin", "Active", Now.AddMinutes(-12)),
        new("u2", "Grace Hopper", "grace@example.com", "Manager", "Active", Now.AddHours(-3)),
        new("u3", "Alan Turing", "alan@example.com", "User", "Active", Now.AddDays(-1)),
        new("u4", "Katherine Johnson", "katherine@example.com", "User", "Invited", null),
    ];

    private readonly List<AdminRole> _roles =
    [
        new("Admin", "Full access to everything.", 1),
        new("Manager", "Manages records and reports.", 1),
        new("User", "Works with their own records.", 2),
    ];

    private readonly List<(AdminTenant Tenant, TenantDetails Details, List<TenantMember> Members, List<TenantInvitation> Invitations)> _tenants =
    [
        (new("t1", "Acme Corp", "acme.example.com", "Business", "Active", 2), new TenantDetails { Name = "Acme Corp", Domain = "acme.example.com", Description = "Headquarters." },
            [new("u1", "Ada Lovelace", "ada@example.com", "Owner"), new("u2", "Grace Hopper", "grace@example.com", "Member")],
            [new("katherine@example.com", "Member", Now.AddDays(-2))]),
        (new("t2", "Globex", "globex.example.com", "Starter", "Trial", 1), new TenantDetails { Name = "Globex", Domain = "globex.example.com" },
            [new("u3", "Alan Turing", "alan@example.com", "Owner")], []),
    ];

    private readonly List<UserNotification> _notifications =
    [
        new("n1", "Welcome", "Your workspace is ready.", Now.AddDays(-2), true),
        new("n2", "Report finished", "The monthly sales report is ready to download.", Now.AddHours(-5), false),
        new("n3", "New sign-in", "A new device signed in to your account.", Now.AddMinutes(-40), false),
    ];

    private readonly List<AuditEntry> _audit =
    [
        new("a1", Now.AddMinutes(-5), "ada@example.com", "Signed in", "Session", "From 203.0.113.7"),
        new("a2", Now.AddHours(-2), "grace@example.com", "Updated role", "Role: Manager", "Added reports:view"),
        new("a3", Now.AddHours(-26), "ada@example.com", "Invited user", "katherine@example.com", "Role: User"),
    ];

    private NotificationSettings _prefs = new()
    {
        EmailActivitySummary = true, EmailSecurityAlerts = true, NotifySystemUpdates = true, NotifyReports = true,
    };

    private SystemSettings _settings = new() { AppName = "My Modulus App", SupportEmail = "support@example.com", TimeZone = "UTC" };

    private readonly Dictionary<string, ProfileInfo> _profiles = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public bool IsSample => true;

    private static bool Matches(string? query, params string?[] fields) =>
        string.IsNullOrWhiteSpace(query) || fields.Any(f => f?.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase) == true);

    private void Audit(string actor, string action, string target, string detail) =>
        _audit.Insert(0, new(Guid.NewGuid().ToString("n")[..8], DateTimeOffset.UtcNow, actor, action, target, detail));

    // Users

    public Task<IReadOnlyList<AdminUser>> SearchAsync(string? query, CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult<IReadOnlyList<AdminUser>>([.. _users.Where(u => Matches(query, u.Name, u.Email, u.Role))]);
    }

    public Task InviteAsync(string email, string role, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (_users.All(u => !u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
                _users.Add(new(Guid.NewGuid().ToString("n")[..8], email.Split('@')[0], email, role, "Invited", null));
            Audit("you", "Invited user", email, $"Role: {role}");
        }
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string id, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (_users.FirstOrDefault(u => u.Id == id) is { } user)
            {
                _users.Remove(user);
                Audit("you", "Removed user", user.Email, "");
            }
        }
        return Task.CompletedTask;
    }

    public Task<ProfileInfo> GetProfileAsync(string? userName, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var key = userName ?? "";
            if (!_profiles.TryGetValue(key, out var profile))
                _profiles[key] = profile = new ProfileInfo
                {
                    FirstName = userName ?? "Sample", LastName = "User", Email = $"{(userName ?? "sample").ToLowerInvariant()}@example.com",
                    MemberSince = Now.AddMonths(-8), AccountType = "Standard", LastLogin = Now.AddMinutes(-3), Status = "Active",
                };
            return Task.FromResult(Copy(profile));
        }
    }

    public Task SaveProfileAsync(string? userName, ProfileInfo profile, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var existing = _profiles.GetValueOrDefault(userName ?? "");
            profile.MemberSince = existing?.MemberSince ?? Now;
            profile.AccountType = existing?.AccountType ?? "Standard";
            profile.LastLogin = existing?.LastLogin;
            profile.Status = existing?.Status ?? "Active";
            _profiles[userName ?? ""] = Copy(profile);
        }
        return Task.CompletedTask;
    }

    private static ProfileInfo Copy(ProfileInfo p) => new()
    {
        FirstName = p.FirstName, LastName = p.LastName, Email = p.Email, Phone = p.Phone, JobTitle = p.JobTitle, Bio = p.Bio,
        MemberSince = p.MemberSince, AccountType = p.AccountType, LastLogin = p.LastLogin, Status = p.Status,
    };

    // Roles

    Task<IReadOnlyList<AdminRole>> IRoleDirectory.SearchAsync(string? query, CancellationToken ct)
    {
        lock (_gate)
            return Task.FromResult<IReadOnlyList<AdminRole>>([.. _roles.Where(r => Matches(query, r.Name, r.Description))]);
    }

    public Task CreateAsync(string name, string description, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (_roles.All(r => !r.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                _roles.Add(new(name, description, 0));
                Audit("you", "Created role", name, "");
            }
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string name, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (_roles.RemoveAll(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) > 0)
                Audit("you", "Deleted role", name, "");
        }
        return Task.CompletedTask;
    }

    // Tenants

    Task<IReadOnlyList<AdminTenant>> ITenantDirectory.SearchAsync(string? query, CancellationToken ct)
    {
        lock (_gate)
            return Task.FromResult<IReadOnlyList<AdminTenant>>(
                [.. _tenants.Where(t => Matches(query, t.Tenant.Name, t.Tenant.Domain)).Select(t => t.Tenant with { Members = t.Members.Count })]);
    }

    Task ITenantDirectory.CreateAsync(string name, string domain, CancellationToken ct)
    {
        lock (_gate)
        {
            var id = "t" + (_tenants.Count + 1) + Guid.NewGuid().ToString("n")[..3];
            _tenants.Add((new(id, name, domain, "Starter", "Trial", 0), new TenantDetails { Name = name, Domain = domain }, [], []));
            Audit("you", "Created tenant", name, domain);
        }
        return Task.CompletedTask;
    }

    public Task<TenantDetails?> GetAsync(string id, CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult(_tenants.Where(t => t.Tenant.Id == id)
                .Select(t => new TenantDetails { Name = t.Details.Name, Domain = t.Details.Domain, Description = t.Details.Description })
                .FirstOrDefault());
    }

    public Task SaveAsync(string id, TenantDetails details, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var i = _tenants.FindIndex(t => t.Tenant.Id == id);
            if (i >= 0)
            {
                var t = _tenants[i];
                t.Details.Name = details.Name;
                t.Details.Description = details.Description;
                _tenants[i] = (t.Tenant with { Name = details.Name }, t.Details, t.Members, t.Invitations);
                Audit("you", "Updated tenant", details.Name, "");
            }
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TenantMember>> MembersAsync(string id, string? query, CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult<IReadOnlyList<TenantMember>>(
                [.. _tenants.Where(t => t.Tenant.Id == id).SelectMany(t => t.Members).Where(m => Matches(query, m.Name, m.Email, m.Role))]);
    }

    public Task<IReadOnlyList<TenantInvitation>> InvitationsAsync(string id, CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult<IReadOnlyList<TenantInvitation>>([.. _tenants.Where(t => t.Tenant.Id == id).SelectMany(t => t.Invitations)]);
    }

    public Task InviteMemberAsync(string id, string email, string role, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (_tenants.FirstOrDefault(t => t.Tenant.Id == id) is { Invitations: { } invitations } tenant && tenant.Tenant is not null)
            {
                invitations.Add(new(email, role, DateTimeOffset.UtcNow));
                Audit("you", "Invited tenant member", email, tenant.Tenant.Name);
            }
        }
        return Task.CompletedTask;
    }

    public Task RemoveMemberAsync(string id, string userId, CancellationToken ct = default)
    {
        lock (_gate)
            _tenants.FirstOrDefault(t => t.Tenant.Id == id).Members?.RemoveAll(m => m.UserId == userId);
        return Task.CompletedTask;
    }

    // Notifications

    public Task<IReadOnlyList<UserNotification>> ListAsync(string? user, string? query, CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult<IReadOnlyList<UserNotification>>(
                [.. _notifications.Where(n => Matches(query, n.Title, n.Message)).OrderByDescending(n => n.At)]);
    }

    public Task MarkReadAsync(string? user, string? id, CancellationToken ct = default)
    {
        lock (_gate)
            for (var i = 0; i < _notifications.Count; i++)
                if (id is null || _notifications[i].Id == id)
                    _notifications[i] = _notifications[i] with { Read = true };
        return Task.CompletedTask;
    }

    public Task<NotificationSettings> GetPreferencesAsync(string? user, CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult(CopyPrefs(_prefs));
    }

    public Task SavePreferencesAsync(string? user, NotificationSettings preferences, CancellationToken ct = default)
    {
        lock (_gate)
            _prefs = CopyPrefs(preferences);
        return Task.CompletedTask;
    }

    private static NotificationSettings CopyPrefs(NotificationSettings p) => new()
    {
        EmailActivitySummary = p.EmailActivitySummary, EmailSecurityAlerts = p.EmailSecurityAlerts, EmailProductUpdates = p.EmailProductUpdates,
        EmailMarketing = p.EmailMarketing, NotifySystemUpdates = p.NotifySystemUpdates, NotifyReports = p.NotifyReports,
        NotifyCollaboration = p.NotifyCollaboration, NotifyScheduled = p.NotifyScheduled, DigestFrequency = p.DigestFrequency, QuietHours = p.QuietHours,
    };

    // Audit

    Task<IReadOnlyList<AuditEntry>> IAuditTrail.SearchAsync(string? query, CancellationToken ct)
    {
        lock (_gate)
            return Task.FromResult<IReadOnlyList<AuditEntry>>([.. _audit.Where(a => Matches(query, a.Actor, a.Action, a.Target, a.Detail))]);
    }

    // Settings

    Task<SystemSettings> IAdminSettingsStore.GetAsync(CancellationToken ct)
    {
        lock (_gate)
            return Task.FromResult(new SystemSettings { AppName = _settings.AppName, SupportEmail = _settings.SupportEmail, TimeZone = _settings.TimeZone });
    }

    public Task SaveAsync(SystemSettings settings, CancellationToken ct = default)
    {
        lock (_gate)
        {
            _settings = new SystemSettings { AppName = settings.AppName, SupportEmail = settings.SupportEmail, TimeZone = settings.TimeZone };
            Audit("you", "Changed settings", "System settings", "");
        }
        return Task.CompletedTask;
    }

    // Metrics

    public Task<DashboardData> DashboardAsync(CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult(new DashboardData(
                [new("Total users", _users.Count.ToString()), new("Active sessions", "3"), new("Tenants", _tenants.Count.ToString()), new("System health", "Healthy")],
                [.. _users.OrderByDescending(u => u.LastActive ?? DateTimeOffset.MinValue).Take(4)],
                Services));
    }

    public Task<AnalyticsData> AnalyticsAsync(CancellationToken ct = default) => Task.FromResult(new AnalyticsData(
        [new("Page views", "12,480"), new("Unique users", "1,942"), new("Avg. session", "4m 12s"), new("Conversion rate", "3.4%")],
        [new("Mon", 1420), new("Tue", 1810), new("Wed", 1675), new("Thu", 2110), new("Fri", 2390), new("Sat", 980), new("Sun", 870)],
        [new("/dashboard", 3120), new("/admin/users", 1840), new("/reports", 1330), new("/notifications", 910)]));

    public Task<ReportsData> ReportsAsync(CancellationToken ct = default) => Task.FromResult(new ReportsData(
        [new("Total reports", "3"), new("Scheduled", "2"), new("This month", "14"), new("Avg. generation", "2.3s")],
        [new("Monthly sales", "Monthly", Now.AddDays(-3)), new("User activity", "Weekly", Now.AddDays(-1)), new("Audit summary", "On demand", null)]));

    public Task<HealthData> HealthAsync(CancellationToken ct = default) => Task.FromResult(new HealthData(
        [new("Uptime", "99.98%"), new("Requests / min", "412"), new("Error rate", "0.2%"), new("Avg. response", "84 ms")],
        Services,
        [new("CPU", 34), new("Memory", 61), new("Disk", 47)]));

    private static readonly IReadOnlyList<ServiceStatus> Services =
    [
        new("Web", "Healthy", "All instances responding"),
        new("Database", "Healthy", "Latency 6 ms"),
        new("Background jobs", "Degraded", "2 jobs retrying"),
    ];
}
