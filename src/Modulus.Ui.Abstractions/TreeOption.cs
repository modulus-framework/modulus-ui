namespace Modulus.Ui.Abstractions;

/// <summary>
/// A node of hierarchical options, for cascaders, tree selects, trees and transfer lists. Values must be
/// unique within one tree: the posted value of a cascader or tree select is the chosen node's value.
/// </summary>
public sealed record TreeOption(string Value, string Title, IReadOnlyList<TreeOption>? Children = null, bool Disabled = false)
{
    public bool IsLeaf => Children is not { Count: > 0 };

    /// <summary>The nodes from a root down to the node with <paramref name="value"/>, or empty when it is not found.</summary>
    public static IReadOnlyList<TreeOption> PathTo(IEnumerable<TreeOption> roots, string? value)
    {
        if (value is null) return [];
        foreach (var node in roots)
        {
            if (node.Value == value) return [node];
            if (node.Children is { } children && PathTo(children, value) is { Count: > 0 } path) return [node, .. path];
        }
        return [];
    }

    /// <summary>Every node, depth first.</summary>
    public static IEnumerable<TreeOption> Flatten(IEnumerable<TreeOption> roots) =>
        roots.SelectMany(n => (IEnumerable<TreeOption>)[n, .. Flatten(n.Children ?? [])]);
}
