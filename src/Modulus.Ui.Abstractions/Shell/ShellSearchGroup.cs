namespace Modulus.Ui.Abstractions.Shell;

/// <summary>A titled group of header search results, such as "Users" or "Documents".</summary>
public sealed record ShellSearchGroup(string Title, IReadOnlyList<ShellSearchItem> Items);
