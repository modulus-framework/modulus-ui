namespace Modulus.Ui.Abstractions.Admin;

// Data shapes the scaffolded admin pages show. They are deliberately small: map your own entities onto them in your
// implementation of the service interfaces, or change the generated page to show your own types.

/// <summary>A user row in the user list.</summary>
public sealed record AdminUser(string Id, string Name, string Email, string Role, string Status, DateTimeOffset? LastActive);

/// <summary>The signed-in user's profile, as edited on the profile page.</summary>
public sealed class ProfileInfo
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string? JobTitle { get; set; }
    public string? Bio { get; set; }
    public DateTimeOffset? MemberSince { get; set; }
    public string? AccountType { get; set; }
    public DateTimeOffset? LastLogin { get; set; }
    public string? Status { get; set; }
}

/// <summary>A role in the role list.</summary>
public sealed record AdminRole(string Name, string Description, int Members);

/// <summary>A tenant in the tenant list.</summary>
public sealed record AdminTenant(string Id, string Name, string Domain, string Plan, string Status, int Members);

/// <summary>A tenant's editable details.</summary>
public sealed class TenantDetails
{
    public string Name { get; set; } = "";
    public string Domain { get; set; } = "";
    public string? Description { get; set; }
}

/// <summary>A member of a tenant.</summary>
public sealed record TenantMember(string UserId, string Name, string Email, string Role);

/// <summary>An invitation that has not been accepted yet.</summary>
public sealed record TenantInvitation(string Email, string Role, DateTimeOffset SentAt);

/// <summary>One notification in the notification center.</summary>
public sealed record UserNotification(string Id, string Title, string Message, DateTimeOffset At, bool Read);

/// <summary>A user's notification choices.</summary>
public sealed class NotificationSettings
{
    public bool EmailActivitySummary { get; set; }
    public bool EmailSecurityAlerts { get; set; }
    public bool EmailProductUpdates { get; set; }
    public bool EmailMarketing { get; set; }
    public bool NotifySystemUpdates { get; set; }
    public bool NotifyReports { get; set; }
    public bool NotifyCollaboration { get; set; }
    public bool NotifyScheduled { get; set; }
    public string DigestFrequency { get; set; } = "Real-time";
    public string? QuietHours { get; set; }

    public static readonly IReadOnlyList<string> DigestFrequencies = ["Real-time", "Daily digest", "Weekly digest", "Never"];
}

/// <summary>One event in the audit log.</summary>
public sealed record AuditEntry(string Id, DateTimeOffset At, string Actor, string Action, string Target, string Detail);

/// <summary>Application-wide settings edited on the system settings page.</summary>
public sealed class SystemSettings
{
    public string AppName { get; set; } = "";
    public string SupportEmail { get; set; } = "";
    public string TimeZone { get; set; } = "UTC";

    public static readonly IReadOnlyList<string> TimeZones = ["UTC", "America/New_York", "Europe/London", "Asia/Dhaka"];
}

/// <summary>A labelled figure on a stat card.</summary>
public sealed record Metric(string Label, string Value);

/// <summary>The state of one service on the health page and dashboard.</summary>
public sealed record ServiceStatus(string Name, string Status, string Detail);

/// <summary>A page and its view count.</summary>
public sealed record TopPage(string Path, int Views);

/// <summary>One bar of a simple chart.</summary>
public sealed record TrafficPoint(string Label, int Value);

/// <summary>A report on the reports page.</summary>
public sealed record ReportInfo(string Name, string Schedule, DateTimeOffset? LastRun);

/// <summary>A resource gauge (CPU, memory ...) as a percentage.</summary>
public sealed record UsageGauge(string Label, int Percent);

public sealed record DashboardData(IReadOnlyList<Metric> Stats, IReadOnlyList<AdminUser> RecentUsers, IReadOnlyList<ServiceStatus> Services);

public sealed record AnalyticsData(IReadOnlyList<Metric> Stats, IReadOnlyList<TrafficPoint> Traffic, IReadOnlyList<TopPage> TopPages);

public sealed record ReportsData(IReadOnlyList<Metric> Stats, IReadOnlyList<ReportInfo> Reports);

public sealed record HealthData(IReadOnlyList<Metric> Stats, IReadOnlyList<ServiceStatus> Services, IReadOnlyList<UsageGauge> Usage);

/// <summary>What the MVC user list view shows: the users and the roles to offer when inviting.</summary>
public sealed record UsersPage(IReadOnlyList<AdminUser> Users, IReadOnlyList<AdminRole> Roles);

/// <summary>What the MVC tenant members view shows.</summary>
public sealed record TenantMembersPage(string TenantId, IReadOnlyList<TenantMember> Members, IReadOnlyList<TenantInvitation> Invitations);
