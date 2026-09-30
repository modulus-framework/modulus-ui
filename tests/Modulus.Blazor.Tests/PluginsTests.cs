using Modulus.Ui.Abstractions.Charts;
using Modulus.Ui.Abstractions.Forms;
using Xunit;

namespace Modulus.Blazor.Tests;

public class ChartInteractionTests
{
    private static readonly string[] Labels = ["Q1", "Q2", "Q3"];
    private static readonly ChartSeries[] Series = [new("Online", [10, 20, 30]), new("Retail", [5, 6, 7])];

    [Fact]
    public void LegendToggle_TurnsEntriesIntoButtons_AndMarksEverySeriesElement()
    {
        var html = ChartHtml.Line(Labels, Series, new ChartOptions { LegendToggle = true, Area = true });

        Assert.Contains("<ul class=\"m-legend m-legend-toggle\" data-m-legend>", html);
        Assert.Contains("<button type=\"button\" class=\"m-legend-btn\" data-series=\"1\" aria-pressed=\"true\">", html);
        // Area, line, end dot, three hover dots and three tooltip rows for the first series.
        Assert.Equal(9, System.Text.RegularExpressions.Regex.Matches(html, "data-s=\"0\"").Count);
        Assert.Contains("class=\"m-xy-tip-row\" data-s=\"1\"", html);
    }

    [Fact]
    public void LegendToggle_IsOffByDefault_AndLeavesTheMarkupAsItWas()
    {
        var html = ChartHtml.Columns(Labels, Series);

        Assert.Contains("<ul class=\"m-legend\"><li><span class=\"m-key m-key-rect\"", html);
        Assert.DoesNotContain("data-s=", html);
        Assert.DoesNotContain("data-m-legend", html);
        Assert.DoesNotContain("m-xy-link", html);
    }

    [Fact]
    public void LegendToggle_MarksComboLinesAfterTheColumns()
    {
        var html = ChartHtml.Combo(Labels, [Series[0]], [new ChartSeries("Budget", [12, 18, 25])], new ChartOptions { LegendToggle = true });

        Assert.Contains("class=\"m-col-bar\"", html);
        Assert.Contains("<path class=\"m-xy-line\" d=\"", html);
        Assert.Matches("<path class=\"m-xy-line\" d=\"[^\"]+\" style=\"[^\"]+\" data-s=\"1\"/>", html);
        Assert.Contains("data-series=\"1\"", html);
    }

    [Fact]
    public void Links_MakeLinkedSlotsAndAxisLabelsAnchors()
    {
        var html = ChartHtml.Columns(Labels, Series, new ChartOptions { Stacked = true, Links = ["/q/1", null, "/q/3?a=1&b=2"] });

        Assert.Contains("<a class=\"m-xy-slot m-col-slot m-xy-link\" href=\"/q/1\" tabindex=\"-1\">", html);
        Assert.Contains("<div class=\"m-xy-slot m-col-slot\">", html); // Q2 has no link
        Assert.Contains("<div class=\"m-xy-x m-xy-x-links\">", html);
        Assert.Contains("<a href=\"/q/1\">Q1</a><span>Q2</span><a href=\"/q/3?a=1&amp;b=2\">Q3</a>", html);
        Assert.Contains("<a class=\"m-xy-slot m-col-slot m-xy-link\" href=\"/q/3?a=1&amp;b=2\" tabindex=\"-1\">", html);
    }
}

public class DatePeriodsTests
{
    [Fact]
    public void Presets_EndToday_AndCoverTheUsualRanges()
    {
        var p = DatePeriods.Presets(new DateOnly(2026, 2, 10)).ToDictionary(x => x.Label);

        Assert.Equal((new DateOnly(2026, 2, 10), new DateOnly(2026, 2, 10)), (p["Today"].From, p["Today"].To));
        Assert.Equal(new DateOnly(2026, 2, 4), p["Last 7 days"].From);
        Assert.Equal(new DateOnly(2026, 1, 12), p["Last 30 days"].From);
        Assert.Equal((new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28)), (p["This month"].From, p["This month"].To));
        Assert.Equal((new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)), (p["Last month"].From, p["Last month"].To));
        Assert.Equal((new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31)), (p["This quarter"].From, p["This quarter"].To));
        Assert.Equal(new DateOnly(2026, 12, 31), p["This year"].To);
    }

    [Theory]
    [InlineData(1, "2026-Q1")]
    [InlineData(3, "2026-Q1")]
    [InlineData(4, "2026-Q2")]
    [InlineData(12, "2026-Q4")]
    public void Quarter_FormatsTheMonthsQuarter(int month, string expected) =>
        Assert.Equal(expected, DatePeriods.Quarter(new DateOnly(2026, month, 15)));

    [Theory]
    [InlineData("2026-Q3", true, 7)]
    [InlineData("2026-q1", true, 1)]
    [InlineData(" 2026 - Q4 ", true, 10)]
    [InlineData("2026-Q5", false, 0)]
    [InlineData("2026-3", false, 0)]
    [InlineData("Q3", false, 0)]
    [InlineData(null, false, 0)]
    public void TryParseQuarter_ReadsTheFirstDay(string? text, bool ok, int month)
    {
        Assert.Equal(ok, DatePeriods.TryParseQuarter(text, out var start));
        if (ok) Assert.Equal(new DateOnly(2026, month, 1), start);
    }

    [Fact]
    public void Quarters_ListsEveryQuarterOfTheYears()
    {
        var q = DatePeriods.Quarters(2025, 2026);

        Assert.Equal(8, q.Count);
        Assert.Equal(("2025-Q1", "Q1 2025"), q[0]);
        Assert.Equal(("2026-Q4", "Q4 2026"), q[^1]);
        Assert.Empty(DatePeriods.Quarters(2027, 2026));
    }
}
