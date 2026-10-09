using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions.Dashboard;
using Modulus.Ui.Abstractions.State;

namespace Modulus.AdminTemplate.RazorPages.Pages.Dashboards;

/// <summary>The dashboard layout: the signed-in user's saved tiles (the forms post to /_modulus/dashboard).</summary>
public class TilesModel(IUiStateStore state) : SamplePageModel
{
    public DashboardView View { get; private set; } = DashboardModel.View(DashboardPage.Id, DashboardPage.Tiles, DashboardModel.Default(DashboardPage.Tiles));

    public async Task OnGetAsync()
    {
        var layout = await DashboardModel.LoadAsync(state, UiUser.Key(User), DashboardPage.Id, DashboardPage.Tiles, HttpContext.RequestAborted);
        View = DashboardModel.View(DashboardPage.Id, DashboardPage.Tiles, layout);
    }
}
