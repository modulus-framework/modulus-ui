using System.Security.Claims;

namespace Modulus.Ui.Abstractions.Shell;

/// <summary>The signed-in user shown in the header.</summary>
public sealed record ShellUser(string Name, string? Subtitle = null, string? AvatarUrl = null);

/// <summary>A header notification. <paramref name="Tone"/> is primary, success, warning or danger.</summary>
public sealed record ShellNotice(string Title, string? Description, string When, bool Unread, string Icon = "bell", string Tone = "primary", string? Href = null);

/// <summary>A link in the user menu or footer. Set <see cref="DividerBefore"/> to separate groups.</summary>
public sealed record ShellLink(string Label, string Href, string? Icon = null)
{
    public bool DividerBefore { get; init; }
}

/// <summary>Per-request header and footer content: user, notifications, menus.</summary>
public sealed class ShellContent
{
    public ShellUser User { get; set; } = new("Admin");

    /// <summary>Null hides the notification bell.</summary>
    public IReadOnlyList<ShellNotice>? Notices { get; set; }

    /// <summary>"View all" link under the notifications. Null hides it.</summary>
    public string? NoticesHref { get; set; }

    public IReadOnlyList<ShellLink> UserLinks { get; set; } =
    [
        new("Profile", "profile", "user"),
        new("Settings", "settings", "settings"),
    ];

    /// <summary>Sign-out link at the bottom of the user menu. Null hides it.</summary>
    public string? SignOutHref { get; set; } = "logout";

    public IReadOnlyList<ShellLink> FooterLinks { get; set; } = [];

    /// <summary>Placeholder of the header search box, which searches the menu. Null hides the search box.</summary>
    public string? SearchPlaceholder { get; set; } = "Search…";
}

/// <summary>
/// Supplies <see cref="ShellContent"/> for the current user. Register your own to show real
/// notifications and account links; the default shows the user's name and a sign-out link.
/// </summary>
public interface IShellContentProvider
{
    Task<ShellContent> GetContentAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);
}

public sealed class DefaultShellContentProvider : IShellContentProvider
{
    public Task<ShellContent> GetContentAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ShellContent { User = new ShellUser(user.Identity?.Name ?? "Admin") });
}
