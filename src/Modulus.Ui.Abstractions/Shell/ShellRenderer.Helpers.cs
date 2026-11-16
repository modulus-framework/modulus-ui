using System.Net;
using System.Text;
using Modulus.Ui.Abstractions.Navigation;
using Modulus.Ui.Abstractions.Theming;

namespace Modulus.Ui.Abstractions.Shell;
public static partial class ShellRenderer
{
    // Helpers ------------------------------------------------------------------------------------------------

    /// <summary>Brand mark and title linking to the home page.</summary>
    public static string Brand(ModulusThemeOptions options, string? cssClass = null)
    {
        var logo = options.LogoUrl is { } url
            ? $"""<img class="m-brand-logo" src="{E(url)}" alt="" />"""
            : """<svg class="m-brand-logo" viewBox="0 0 32 32" aria-hidden="true"><rect width="32" height="32" rx="8" fill="var(--m-primary)"/><path d="M8 22V10.5a.5.5 0 0 1 .85-.35L16 17.3l7.15-7.15a.5.5 0 0 1 .85.35V22" fill="none" stroke="#fff" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round"/></svg>""";
        return $"""<a class="m-brand{(cssClass is null ? "" : " " + cssClass)}" href="/">{logo}<span class="m-brand-text">{E(options.Title)}</span></a>""";
    }

    /// <summary>Square Tabler avatar: the image, or initials on a tinted background.</summary>
    public static string Avatar(ShellUser user, string size = "sm") =>
        user.AvatarUrl is { } src
            ? $"""<span class="m-avatar m-avatar-{size}" style="background-image:url('{E(src)}')"></span>"""
            : $"""<span class="m-avatar m-avatar-{size} m-av-1">{E(Initials(user.Name))}</span>""";

    public static string Initials(string name) =>
        string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpperInvariant(p[0])));

    private static bool IsActive(MenuItemDefinition item, string path) =>
        item.Matches(path) || item.Children.Any(c => IsActive(c, path));

    private static string Href(MenuItemDefinition item) => E("/" + (item.Href ?? "").TrimStart('/'));

    /// <summary>App-relative links ("settings") resolve from the site root; absolute URLs and anchors pass through.</summary>
    /// <summary>The chat menu of the header: the recent conversations with their unread messages, and a link to the chat page.</summary>
    private static void Chats(StringBuilder sb, IReadOnlyList<ShellChat> chats, string? allHref)
    {
        var unread = chats.Sum(ch => ch.Unread);
        sb.Append("""<details class="m-dropdown m-chats"><summary class="m-icon-btn" aria-label="Chat">""")
          .Append(Icon("message"))
          .Append(unread > 0 ? $"""<span class="m-notice-dot" aria-hidden="true"></span><span class="m-sr-only">{unread} unread</span>""" : "")
          .Append("""</summary><div class="m-dropdown-menu m-dropdown-end m-dropdown-card">""")
          .Append("""<div class="m-dropdown-card-header"><span class="m-dropdown-card-title">Chat</span>""")
          .Append(unread > 0 ? $"""<span class="m-badge">{unread}</span>""" : "")
          .Append("</div>");
        if (chats.Count == 0)
        {
            sb.Append("""<div class="m-dropdown-empty">No conversations yet.</div>""");
        }
        else
        {
            sb.Append("""<ul class="m-notice-list">""");
            foreach (var ch in chats)
            {
                var tag = ch.Href is null ? "div" : "a";
                sb.Append($"""<li class="m-notice{(ch.Unread > 0 ? " unread" : "")}"><{tag} class="m-notice-link"{(ch.Href is null ? "" : $" href=\"{Url(ch.Href)}\"")}>""")
                  .Append($"""<span class="m-icon-tile m-tile-{E(ch.Tone)}-lt">{E(Initials(ch.Title))}</span>""")
                  .Append($"""<span class="m-notice-body"><span class="m-notice-title">{E(ch.Title)}</span>""")
                  .Append(string.IsNullOrEmpty(ch.Preview) ? "" : $"""<span class="m-notice-desc">{E(ch.Preview)}</span>""")
                  .Append($"""<span class="m-notice-time">{E(ch.When)}</span></span>""")
                  .Append(ch.Unread > 0 ? $"""<span class="m-badge">{ch.Unread}</span>""" : "")
                  .Append($"</{tag}></li>");
            }
            sb.Append("</ul>");
        }

        if (allHref is { } all)
            sb.Append($"""<a class="m-dropdown-card-footer" href="{Url(all)}">Open chat</a>""");
        sb.Append("</div></details>");
    }

    /// <summary>
    /// "Mark all as read": a post to the app when it names where to post (the server then marks the notices read), otherwise the
    /// client-only button that modulus-shell.js uses to clear the page's markers.
    /// </summary>
    private static string MarkAllRead(ShellContent c) =>
        c.NoticesMarkAllAction is { } action
            ? $"""<form method="post" action="{Url(action)}"><input type="hidden" name="__RequestVerificationToken" value="{E(c.AntiForgeryToken)}" /><button type="submit" class="m-link-btn">Mark all as read</button></form>"""
            : """<button type="button" class="m-link-btn" data-m-mark-read>Mark all as read</button>""";

    private static string Url(string href) =>
        E(href.StartsWith('/') || href.StartsWith('#') || href.Contains("://", StringComparison.Ordinal) ? href : "/" + href);

    private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? "");
}
