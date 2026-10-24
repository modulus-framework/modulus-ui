using AntDesign;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Modulus.Blazor.Components.Charts;
using Modulus.Blazor.Components.DataDisplay;
using Modulus.Blazor.Components.General;
using Modulus.Blazor.Components.Pro;
using Modulus.Ui.Abstractions.Charts;
using Xunit;

namespace Modulus.Blazor.Tests;

public class ComponentTests : AsyncBunitContext
{
    public ComponentTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddAntDesign();
    }

    [Fact]
    public void AppButton_IsAntDesignButton()
    {
        var cut = Render<AppButton>(p => p
            .Add(b => b.Type, ButtonType.Primary)
            .AddChildContent("Save"));

        var button = cut.Find("button");
        Assert.Contains("ant-btn", button.ClassList);
        Assert.Contains("ant-btn-primary", button.ClassList);
        Assert.Contains("Save", button.TextContent);
    }

    [Fact]
    public void AppCard_IsAntDesignCard()
    {
        var cut = Render<AppCard>(p => p
            .Add(c => c.Title, "Revenue")
            .AddChildContent("body"));

        Assert.NotNull(cut.Find(".ant-card .ant-card-head"));
        Assert.Contains("Revenue", cut.Find(".ant-card-head-title").TextContent);
    }

    [Theory]
    [InlineData(TrendDirection.Up, "m-trend-up", "increase")]
    [InlineData(TrendDirection.Down, "m-trend-down", "decrease")]
    public void Trend_ExposesDirectionAsText(TrendDirection direction, string cssClass, string srText)
    {
        var cut = Render<Trend>(p => p
            .Add(t => t.Direction, direction)
            .AddChildContent("12%"));

        Assert.Contains(cssClass, cut.Find(".m-trend").ClassList);
        // Direction must not rely on colour or the caret alone.
        Assert.Equal(srText, cut.Find(".m-sr-only").TextContent);
    }

    [Fact]
    public void StatisticCard_RendersTitleValueAndFooter()
    {
        var cut = Render<StatisticCard>(p => p
            .Add(s => s.Title, "Total sales")
            .Add(s => s.Value, "$126,560")
            .Add(s => s.Footer, "Daily sales $12,423"));

        Assert.Contains("Total sales", cut.Find(".m-stat-card-head").TextContent);
        Assert.Equal("$126,560", cut.Find(".m-stat-card-value").TextContent.Trim());
        Assert.Contains("Daily sales", cut.Find(".m-stat-card-footer").TextContent);
        Assert.Empty(cut.FindAll(".m-stat-card-chart"));
    }

    [Fact]
    public void BarChart_HasAccessibleDataTable()
    {
        ChartPoint[] data = [new("Jan", 120), new("Feb", 480), new("Mar", 300)];

        var cut = Render<BarChart>(p => p.Add(b => b.Data, data));

        Assert.Equal(3, cut.FindAll(".m-bar").Count);
        var rows = cut.FindAll("table tbody tr");
        Assert.Equal(3, rows.Count);
        Assert.Contains("Feb", rows[1].TextContent);
    }
}
