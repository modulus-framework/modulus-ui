using Modulus.Samples.Shared;

namespace Modulus.AdminTemplate.RazorPages.Pages.Tables;

/// <summary>Printing overview: same URL as the MVC sample (/tables/print).</summary>
public class PrintModel : SamplePageModel
{
    public PrintOverview Overview => PrintSamples.Overview;
}
