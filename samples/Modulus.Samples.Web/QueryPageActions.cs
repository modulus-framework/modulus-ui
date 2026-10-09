using Microsoft.AspNetCore.Http;
using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions.Query;

namespace Modulus.Samples.Web;

/// <summary>
/// The visual filter page behind MVC <c>TablesController</c> and the Razor Pages <c>Tables/Query</c> page: the filter is
/// read from the URL's <c>f.*</c> fields (<see cref="QueryBuilderForm"/>), and a button's operation (<c>f.op</c>) is
/// applied to it before the rows are loaded.
/// </summary>
public static class QueryPageActions
{
    public static QueryPage Load(IQueryCollection query)
    {
        // Each value is its own pair: a multi-select posts one per option, and the form reads them as one list.
        var fields = query.SelectMany(q => q.Value.Select(v => new KeyValuePair<string, string?>(q.Key, v)));
        var filter = QueryBuilderForm.Read(fields);
        if (query[QueryBuilderForm.DefaultName + ".op"].ToString() is { Length: > 0 } op)
            filter = QueryBuilderForm.Edit(QueryPage.Builder.Schema, filter, op);
        return QueryPage.Load(filter);
    }
}
