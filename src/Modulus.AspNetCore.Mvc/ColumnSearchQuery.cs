using Microsoft.AspNetCore.Http;
using Modulus.Ui.Abstractions;

namespace Modulus.AspNetCore.Mvc;

/// <summary>Reads the column searches of a table from the query string (<c>s.{key}=query</c>).</summary>
public static class ColumnSearchQuery
{
    /// <summary>The well-formed column searches in the query (see <see cref="ColumnSearch.FromQuery"/>); null when none.</summary>
    public static IReadOnlyDictionary<string, string?>? ColumnSearches(this IQueryCollection query) =>
        ColumnSearch.FromQuery(query.Select(kv => new KeyValuePair<string, string?>(kv.Key, kv.Value.ToString())));
}
