using Modulus.AspNetCore.Mvc.TagHelpers;
using Modulus.Ui.Abstractions;
using Xunit;

namespace Modulus.AspNetCore.Mvc.Tests;

/// <summary>The MVC header's checkbox list (the Excel-style filter menu) that modulus-widgets.js drives.</summary>
public class ColumnHeaderValuesTests
{
    private static readonly ColumnValue[] Values = [new("Active", 1200), new("Closed", 3), new("", 2)];

    [Fact]
    public void Values_RenderACheckboxList_WithTheSearchChecked()
    {
        var html = ColumnHeader.Content("Status", "Status", search: "!=Closed", values: Values);

        Assert.Contains("data-values data-search=\"!=Closed\"", html);
        Assert.Contains("aria-label=\"Search Status: Not Closed\"", html);
        Assert.Contains("x-ref=\"values\"", html);
        Assert.DoesNotContain("m-col-search-hint", html);
        Assert.Contains("<span class=\"m-col-value-text\">Active</span><span class=\"m-col-value-count\">1,200</span>", html);
        Assert.Contains("value=\"\" data-value data-text=\"(Blanks)\"", html);
        // Active and the blanks are checked, Closed isn't; Select all isn't (not every value is).
        Assert.Contains("ant-checkbox-input\" checked value=\"Active\"", html);
        Assert.Contains("ant-checkbox-input\" value=\"Closed\"", html);
        Assert.Contains("ant-checkbox-input\" checked value=\"\"", html);
        Assert.Contains("ant-checkbox-input\" x-ref=\"all\"", html);
        Assert.Contains("x-ref=\"ok\" @click=\"apply()\"><span>OK</span>", html);
    }

    [Fact]
    public void NoValues_KeepTheSearchBox()
    {
        var html = ColumnHeader.Content("Status", "Status", search: "act", values: []);

        Assert.DoesNotContain("data-values", html);
        Assert.Contains("m-col-search-hint", html);
        Assert.Contains("value=\"act\"", html);
    }
}
