using Microsoft.AspNetCore.Components;
using Modulus.Ui.Abstractions.Charts;

namespace Modulus.Blazor.Components.Charts;

/// <summary>Parameters shared by the multi-series charts; markup comes from <see cref="ChartHtml"/>.</summary>
public abstract class ChartBase : ComponentBase
{
    [Parameter] public int Height { get; set; } = 260;

    /// <summary>.NET numeric format for values ("N0", "N1", "P0" …).</summary>
    [Parameter] public string ValueFormat { get; set; } = "N0";

    [Parameter] public string Prefix { get; set; } = "";

    [Parameter] public string Suffix { get; set; } = "";

    /// <summary>Accessible name; also the caption of the screen-reader table.</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>First categorical colour slot (1–8).</summary>
    [Parameter] public int FirstSlot { get; set; } = 1;

    [Parameter] public int MaxXLabels { get; set; } = 12;

    /// <summary>Legend entries hide and show their series (line, column and combo charts).</summary>
    [Parameter] public bool LegendToggle { get; set; }

    /// <summary>Drill-down: one link per x-axis label, null for none (line, column and combo charts).</summary>
    [Parameter] public IReadOnlyList<string?>? Links { get; set; }

    protected ChartOptions Options(bool area = false, bool stacked = false, string? centerLabel = null) => new()
    {
        Height = Height, ValueFormat = ValueFormat, Prefix = Prefix, Suffix = Suffix, Title = Title,
        FirstSlot = FirstSlot, MaxXLabels = MaxXLabels, Area = area, Stacked = stacked, CenterLabel = centerLabel,
        LegendToggle = LegendToggle, Links = Links,
    };
}
