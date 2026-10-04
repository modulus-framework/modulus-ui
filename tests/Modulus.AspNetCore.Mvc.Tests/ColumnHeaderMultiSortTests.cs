using Modulus.AspNetCore.Mvc.TagHelpers;
using Modulus.Ui.Abstractions;
using Xunit;

namespace Modulus.AspNetCore.Mvc.Tests;

/// <summary>The MVC header's multi-sort: the Shift+click URL for modulus-shell.js and the sort's place by the carets.</summary>
public class ColumnHeaderMultiSortTests
{
    [Fact]
    public void Content_CarriesTheAddUrl_AndThePlace()
    {
        var html = ColumnHeader.Content("Salary", "Salary", sort: SortDirection.Descending, sortHref: "/t?sortField=Salary",
            searchable: false, sortAddHref: "/t?sortField=Dept&sortOrder=Ascending&thenBy=Salary", sortRank: 2);

        Assert.Contains("data-sort-add-href=\"/t?sortField=Dept&amp;sortOrder=Ascending&amp;thenBy=Salary\"", html);
        Assert.Contains("<span class=\"m-th-sort-rank\" aria-hidden=\"true\">2</span>", html);
        Assert.Contains("aria-label=\"Sort by Salary (descending, sort 2)\"", html);
        Assert.Contains("title=\"Sort by Salary (Shift+click to add)\"", html);
    }

    [Fact]
    public void Content_WithoutMultiSort_IsUnchanged()
    {
        var html = ColumnHeader.Content("Name", "Name", sort: SortDirection.Ascending, sortHref: "/t", searchable: false);

        Assert.DoesNotContain("data-sort-add-href", html);
        Assert.DoesNotContain("m-th-sort-rank", html);
        Assert.Contains("title=\"Sort by Name\" aria-label=\"Sort by Name (ascending)\"", html);
    }
}
