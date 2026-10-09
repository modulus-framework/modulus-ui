using System.Globalization;
using System.Net;
using System.Text;

namespace Modulus.Ui.Abstractions.Printing;

/// <summary>
/// A sheet of labels: how many across and down, the size of one label, the gaps between labels, and the margin from the
/// sheet's top and left edge. Measures are millimetres.
/// </summary>
public sealed record LabelLayout(int Columns, int Rows, double WidthMm, double HeightMm, double GapXMm = 0, double GapYMm = 0, double LeftMm = 0, double TopMm = 0)
{
    /// <summary>The labels on one sheet.</summary>
    public int PerSheet => Columns * Rows;

    /// <summary>The common A4 sheet of 21 labels: three across, seven down, 63.5 × 38.1 mm each.</summary>
    public static LabelLayout A4ThreeBySeven { get; } = new(3, 7, 63.5, 38.1, GapXMm: 2.5, LeftMm: 7, TopMm: 15);

}

/// <summary>One label: its lines of text. The first line prints bold, the rest as they are.</summary>
public sealed record LabelItem(IReadOnlyList<string> Lines);

/// <summary>
/// The labels as sheets: the items fill a sheet left to right, top to bottom, then the next sheet starts (each sheet breaks
/// onto a new page when printed). The Blazor <c>LabelSheet</c> and the MVC <c>app-label-sheet</c> both render this.
/// </summary>
public static class LabelHtml
{
    /// <summary>Renders the labels. Every line is HTML-encoded.</summary>
    public static string Render(IReadOnlyList<LabelItem> items, LabelLayout layout)
    {
        Check(layout);
        var sb = new StringBuilder();
        sb.Append("""<div class="m-labels" data-m-labels>""");
        for (var start = 0; start < items.Count; start += layout.PerSheet)
        {
            sb.Append($"""<div class="m-label-sheet" style="{Vars(layout)}">""");
            foreach (var item in items.Skip(start).Take(layout.PerSheet))
            {
                sb.Append("""<div class="m-label">""");
                for (var i = 0; i < item.Lines.Count; i++)
                    sb.Append(i == 0
                        ? $"""<p class="m-label-title">{E(item.Lines[i])}</p>"""
                        : $"""<p class="m-label-line">{E(item.Lines[i])}</p>""");
                sb.Append("</div>");
            }
            sb.Append("</div>");
        }
        return sb.Append("</div>").ToString();
    }

    private static void Check(LabelLayout l)
    {
        if (l.Columns < 1 || l.Columns > 20 || l.Rows < 1 || l.Rows > 40) throw new ArgumentOutOfRangeException(nameof(l), "A sheet has 1 to 20 columns and 1 to 40 rows.");
        if (l.WidthMm <= 0 || l.HeightMm <= 0) throw new ArgumentOutOfRangeException(nameof(l), "A label has a size.");
        if (l.GapXMm < 0 || l.GapYMm < 0 || l.LeftMm < 0 || l.TopMm < 0) throw new ArgumentOutOfRangeException(nameof(l), "Gaps and margins are not negative.");
    }

    /// <summary>How many sheets the items take.</summary>
    public static int SheetCount(int items, LabelLayout layout) => (items + layout.PerSheet - 1) / layout.PerSheet;

    private static string Vars(LabelLayout l) => string.Join(';',
        $"--m-label-cols:{l.Columns.ToString(CultureInfo.InvariantCulture)}",
        $"--m-label-rows:{l.Rows.ToString(CultureInfo.InvariantCulture)}",
        $"--m-label-w:{Mm(l.WidthMm)}",
        $"--m-label-h:{Mm(l.HeightMm)}",
        $"--m-label-gap-x:{Mm(l.GapXMm)}",
        $"--m-label-gap-y:{Mm(l.GapYMm)}",
        $"--m-label-left:{Mm(l.LeftMm)}",
        $"--m-label-top:{Mm(l.TopMm)}");

    private static string Mm(double value) => value.ToString("0.###", CultureInfo.InvariantCulture) + "mm";

    private static string E(string? text) => WebUtility.HtmlEncode(text ?? "");
}
