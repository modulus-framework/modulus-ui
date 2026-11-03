namespace Modulus.Ui.Abstractions.Shell;

/// <summary>One header search result: where it leads, and a short detail shown beside it.</summary>
public sealed record ShellSearchItem(string Label, string Href, string? Detail = null);
