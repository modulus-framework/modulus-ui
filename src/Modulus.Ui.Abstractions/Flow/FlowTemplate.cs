using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Modulus.Ui.Abstractions.Flow;

/// <summary>One piece of data travelling between nodes (a message, a row, a document): a bag of named values, nested as needed.</summary>
public sealed record FlowItem(IReadOnlyDictionary<string, object?> Json)
{
    public static FlowItem Empty { get; } = new(new Dictionary<string, object?>());

    /// <summary>An item from name/value pairs.</summary>
    public static FlowItem Of(params (string Key, object? Value)[] pairs) => new(pairs.ToDictionary(p => p.Key, p => p.Value));

    /// <summary>A copy with one value set (a dotted name sets inside nested values: <c>customer.email</c>).</summary>
    public FlowItem With(string key, object? value)
    {
        var copy = Copy(Json);
        var parts = key.Split('.');
        var cursor = copy;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            cursor[parts[i]] = cursor.TryGetValue(parts[i], out var next) && next is IReadOnlyDictionary<string, object?> d ? Copy(d) : new Dictionary<string, object?>();
            cursor = (Dictionary<string, object?>)cursor[parts[i]]!;
        }
        cursor[parts[^1]] = value;
        return new FlowItem(copy);
    }

    /// <summary>The value at a dotted path (<c>customer.email</c>, <c>articles.0.title</c>), or null.</summary>
    public object? Get(string path)
    {
        object? current = Json;
        foreach (var part in path.Trim().Split('.'))
        {
            current = current switch
            {
                IReadOnlyDictionary<string, object?> d => d.TryGetValue(part, out var v) ? v : null,
                IDictionary<string, object?> d => d.TryGetValue(part, out var v) ? v : null,
                IList l when int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var i) && i < l.Count => l[i],
                _ => null,
            };
            if (current is null) return null;
        }
        return current;
    }

    /// <summary>The value at <paramref name="path"/> as text ("" when missing).</summary>
    public string Text(string path) => FlowTemplate.Stringify(Get(path));

    private static Dictionary<string, object?> Copy(IReadOnlyDictionary<string, object?> source) => source.ToDictionary(p => p.Key, p => p.Value);
}

/// <summary>
/// The small language the built-in nodes and the settings use: <c>{{path}}</c> in a text is replaced by that value of the item, and a
/// condition is <c>path</c> (true when it has a value), <c>!path</c>, or <c>path op value</c> with <c>== != &gt; &gt;= &lt; &lt;= contains
/// startsWith endsWith</c>. A value is a number, <c>true</c>, <c>false</c>, a quoted text, or another <c>{{path}}</c>. Comparison is numeric when
/// both sides are numbers and otherwise ignores case.
/// </summary>
public static partial class FlowTemplate
{
    [GeneratedRegex(@"\{\{\s*([^{}]+?)\s*\}\}")]
    private static partial Regex Slot();

    /// <summary>Replaces every <c>{{path}}</c> by that value of the item; a missing value is an empty text.</summary>
    public static string Render(string? template, FlowItem item, Func<string, string?>? secrets = null) =>
        string.IsNullOrEmpty(template) ? "" : Slot().Replace(template, m => Value(m.Groups[1].Value, item, secrets));

    /// <summary>Replaces only the <c>{{secret.name}}</c> slots of a text, leaving the others as written.</summary>
    public static string RenderSecrets(string? template, Func<string, string?>? secrets) =>
        string.IsNullOrEmpty(template) ? "" : Slot().Replace(template, m => m.Groups[1].Value.StartsWith(SecretPrefix, StringComparison.Ordinal) ? SecretValue(m.Groups[1].Value, secrets) : m.Value);

    private const string SecretPrefix = "secret.";

    // One pass: a value inserted for a slot is never scanned again, so text a customer typed cannot pull a secret in.
    private static string Value(string path, FlowItem item, Func<string, string?>? secrets) =>
        path.StartsWith(SecretPrefix, StringComparison.Ordinal) ? SecretValue(path, secrets) : item.Text(path);

    private static string SecretValue(string path, Func<string, string?>? secrets)
    {
        var name = path[SecretPrefix.Length..];
        return secrets?.Invoke(name) ?? throw new InvalidOperationException($"The workflow secret \"{name}\" is not configured.");
    }

    private static readonly string[] Operators = ["==", "!=", ">=", "<=", ">", "<", " contains ", " startsWith ", " endsWith "];

    /// <summary>Evaluates a condition against an item. An empty condition is true; an unreadable one is false.</summary>
    public static bool Test(string? condition, FlowItem item)
    {
        var text = condition?.Trim();
        if (string.IsNullOrEmpty(text)) return true;
        foreach (var op in Operators)
        {
            var at = IndexOutsideQuotes(text, op);
            if (at < 0) continue;
            var left = Operand(text[..at].Trim(), item);
            var right = Operand(text[(at + op.Length)..].Trim(), item);
            return Compare(left, right, op.Trim());
        }
        if (text.StartsWith('!')) return !Truthy(Operand(text[1..].Trim(), item));
        return Truthy(Operand(text, item));
    }

    private static int IndexOutsideQuotes(string text, string op)
    {
        var quote = '\0';
        for (var i = 0; i <= text.Length - op.Length; i++)
        {
            var c = text[i];
            if (quote == '\0' && (c == '"' || c == '\'')) quote = c;
            else if (c == quote) quote = '\0';
            else if (quote == '\0' && string.CompareOrdinal(text, i, op, 0, op.Length) == 0) return i;
        }
        return -1;
    }

    private static object? Operand(string token, FlowItem item)
    {
        if (token.Length >= 2 && ((token[0] == '"' && token[^1] == '"') || (token[0] == '\'' && token[^1] == '\''))) return Render(token[1..^1], item);
        if (token.StartsWith("{{", StringComparison.Ordinal) && token.EndsWith("}}", StringComparison.Ordinal)) return item.Get(token[2..^2]);
        if (token == "true") return true;
        if (token == "false") return false;
        if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)) return n;
        return item.Get(token);
    }

    private static bool Truthy(object? v) => v switch
    {
        null => false,
        bool b => b,
        string s => s.Length > 0 && !s.Equals("false", StringComparison.OrdinalIgnoreCase) && s != "0",
        double d => d != 0,
        int i => i != 0,
        long l => l != 0,
        decimal m => m != 0,
        ICollection c => c.Count > 0,
        _ => true,
    };

    private static bool Compare(object? left, object? right, string op)
    {
        var l = Stringify(left);
        var r = Stringify(right);
        switch (op)
        {
            case "contains": return l.Contains(r, StringComparison.OrdinalIgnoreCase);
            case "startsWith": return l.StartsWith(r, StringComparison.OrdinalIgnoreCase);
            case "endsWith": return l.EndsWith(r, StringComparison.OrdinalIgnoreCase);
        }
        int cmp;
        if (TryNumber(left, out var a) && TryNumber(right, out var b)) cmp = a.CompareTo(b);
        else if (left is bool lb && right is bool rb) cmp = lb.CompareTo(rb);
        else cmp = string.Compare(l, r, StringComparison.OrdinalIgnoreCase);
        return op switch { "==" => cmp == 0, "!=" => cmp != 0, ">" => cmp > 0, ">=" => cmp >= 0, "<" => cmp < 0, "<=" => cmp <= 0, _ => false };
    }

    private static bool TryNumber(object? v, out double n)
    {
        switch (v)
        {
            case double d: n = d; return true;
            case int i: n = i; return true;
            case long l: n = l; return true;
            case decimal m: n = (double)m; return true;
            case string s: return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out n);
            default: n = 0; return false;
        }
    }

    /// <summary>A value as text: numbers and booleans invariant, lists and nested values as JSON-like text, null as "".</summary>
    public static string Stringify(object? v) => v switch
    {
        null => "",
        string s => s,
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        IReadOnlyDictionary<string, object?> d => "{" + string.Join(", ", d.Select(p => $"{p.Key}: {Stringify(p.Value)}")) + "}",
        IEnumerable e when v is not string => "[" + string.Join(", ", e.Cast<object?>().Select(Stringify)) + "]",
        _ => v.ToString() ?? "",
    };
}
