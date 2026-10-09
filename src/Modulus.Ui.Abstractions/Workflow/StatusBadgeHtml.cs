namespace Modulus.Ui.Abstractions.Workflow;

/// <summary>
/// A record's status as a tinted pill: the markup of the Blazor <c>StatusBadge</c> and the MVC <c>app-status-badge</c>, for
/// templates and other server-side markup. The tone picks the colour; an unknown tone is drawn as <c>default</c>, so it can never
/// add a class of its own.
/// </summary>
public static class StatusBadgeHtml
{
    /// <summary>The tones a badge can take.</summary>
    public static IReadOnlyList<string> Tones { get; } = ["success", "processing", "error", "warning", "default"];

    /// <summary>The badge around <paramref name="innerHtml"/>, which the caller has already encoded.</summary>
    public static string Render(string innerHtml, string tone = "default")
    {
        ArgumentNullException.ThrowIfNull(innerHtml);
        var safe = Tones.Contains(tone) ? tone : "default";
        return $"<span class=\"m-status-badge m-status-{safe}\"><span class=\"m-status-dot\" aria-hidden=\"true\"></span>{innerHtml}</span>";
    }
}
