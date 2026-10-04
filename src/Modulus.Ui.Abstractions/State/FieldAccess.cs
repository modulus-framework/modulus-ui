using System.Security.Claims;

namespace Modulus.Ui.Abstractions.State;

/// <summary>
/// Which fields (columns, pivot group-bys and measures, search and sort keys) a user may see, by field key. A field
/// without a rule is open to everyone. Build the rules once and ask <see cref="For"/> per request, then hand the result
/// to the queries before they run (<c>TableQuery.Restrict</c>, <c>PivotQuery.Restrict</c>) and to the columns before
/// they render (<see cref="GridSchema.Allowed"/>) — never trust a field the query string names.
/// <code>
/// static readonly FieldAccess Access = FieldAccess.Create().Require("cost", "Finance", "Admin");
/// var allow = Access.For(User);
/// </code>
/// </summary>
public sealed class FieldAccess
{
    private readonly Dictionary<string, Func<ClaimsPrincipal, bool>> _rules = new(StringComparer.OrdinalIgnoreCase);

    private FieldAccess() { }

    public static FieldAccess Create() => new();

    /// <summary>Everyone sees every field.</summary>
    public static Func<string, bool> All { get; } = _ => true;

    /// <summary><paramref name="field"/> needs one of <paramref name="roles"/>.</summary>
    public FieldAccess Require(string field, params string[] roles)
    {
        if (roles.Length == 0) throw new ArgumentException("Name at least one role.", nameof(roles));
        _rules[field] = user => roles.Any(user.IsInRole);
        return this;
    }

    /// <summary><paramref name="field"/> needs <paramref name="test"/> to pass (a claim, a policy result the app computed).</summary>
    public FieldAccess Require(string field, Func<ClaimsPrincipal, bool> test)
    {
        _rules[field] = test;
        return this;
    }

    /// <summary>Whether a field has a rule (a field without one is open).</summary>
    public bool IsRestricted(string field) => _rules.ContainsKey(Base(field));

    /// <summary>
    /// The fields <paramref name="user"/> may see; an anonymous or null user passes only the open ones. A date group-by
    /// (<c>date:month</c>) follows its field (<c>date</c>).
    /// </summary>
    public Func<string, bool> For(ClaimsPrincipal? user)
    {
        var principal = user ?? new ClaimsPrincipal(new ClaimsIdentity());
        var allowed = _rules.Where(r => r.Value(principal)).Select(r => r.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return field => !_rules.ContainsKey(Base(field)) || allowed.Contains(Base(field));
    }

    private static string Base(string field) => field.IndexOf(':') is var i and >= 0 ? field[..i] : field;
}
