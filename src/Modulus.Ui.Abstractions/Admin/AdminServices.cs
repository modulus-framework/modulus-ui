namespace Modulus.Ui.Abstractions.Admin;

// The seams of the scaffolded admin pages. Each page injects one of these. Until you register your own, the sample
// implementation (SampleAdminData) answers, and the page says so. To connect a page to real data, implement the interface
// and register it after AddModulusMvc / AddModulusBlazor:
//
//     builder.Services.AddScoped<IUserDirectory, MyUserDirectory>();
//
// The generated pages are yours: you can also edit a page to call your own services directly and drop the interface.

/// <summary>Marks a service as sample data, so its pages can say "Sample data" until a real one is registered.</summary>
public interface ISampleAware
{
    /// <summary>True for the built-in in-memory sample; leave false (the default) in your own implementation.</summary>
    bool IsSample => false;
}

/// <summary>Users: the list, invitations, and the signed-in user's profile.</summary>
public interface IUserDirectory : ISampleAware
{
    Task<IReadOnlyList<AdminUser>> SearchAsync(string? query, CancellationToken ct = default);
    Task InviteAsync(string email, string role, CancellationToken ct = default);
    Task RemoveAsync(string id, CancellationToken ct = default);
    Task<ProfileInfo> GetProfileAsync(string? userName, CancellationToken ct = default);
    Task SaveProfileAsync(string? userName, ProfileInfo profile, CancellationToken ct = default);
}

/// <summary>Roles.</summary>
public interface IRoleDirectory : ISampleAware
{
    Task<IReadOnlyList<AdminRole>> SearchAsync(string? query, CancellationToken ct = default);
    Task CreateAsync(string name, string description, CancellationToken ct = default);
    Task DeleteAsync(string name, CancellationToken ct = default);
}

/// <summary>Tenants, their settings and their members.</summary>
public interface ITenantDirectory : ISampleAware
{
    Task<IReadOnlyList<AdminTenant>> SearchAsync(string? query, CancellationToken ct = default);
    Task CreateAsync(string name, string domain, CancellationToken ct = default);
    Task<TenantDetails?> GetAsync(string id, CancellationToken ct = default);
    Task SaveAsync(string id, TenantDetails details, CancellationToken ct = default);
    Task<IReadOnlyList<TenantMember>> MembersAsync(string id, string? query, CancellationToken ct = default);
    Task<IReadOnlyList<TenantInvitation>> InvitationsAsync(string id, CancellationToken ct = default);
    Task InviteMemberAsync(string id, string email, string role, CancellationToken ct = default);
    Task RemoveMemberAsync(string id, string userId, CancellationToken ct = default);
}

/// <summary>A user's notifications and notification preferences. <c>user</c> is the signed-in user's name.</summary>
public interface INotificationCenter : ISampleAware
{
    Task<IReadOnlyList<UserNotification>> ListAsync(string? user, string? query, CancellationToken ct = default);
    /// <summary>Marks one notification read, or all of them when <paramref name="id"/> is null.</summary>
    Task MarkReadAsync(string? user, string? id, CancellationToken ct = default);
    Task<NotificationSettings> GetPreferencesAsync(string? user, CancellationToken ct = default);
    Task SavePreferencesAsync(string? user, NotificationSettings preferences, CancellationToken ct = default);
}

/// <summary>The audit log.</summary>
public interface IAuditTrail : ISampleAware
{
    Task<IReadOnlyList<AuditEntry>> SearchAsync(string? query, CancellationToken ct = default);
}

/// <summary>Application settings.</summary>
public interface IAdminSettingsStore : ISampleAware
{
    Task<SystemSettings> GetAsync(CancellationToken ct = default);
    Task SaveAsync(SystemSettings settings, CancellationToken ct = default);
}

/// <summary>The figures of the dashboard, analytics, reports and system health pages.</summary>
public interface IMetricsSource : ISampleAware
{
    Task<DashboardData> DashboardAsync(CancellationToken ct = default);
    Task<AnalyticsData> AnalyticsAsync(CancellationToken ct = default);
    Task<ReportsData> ReportsAsync(CancellationToken ct = default);
    Task<HealthData> HealthAsync(CancellationToken ct = default);
}
