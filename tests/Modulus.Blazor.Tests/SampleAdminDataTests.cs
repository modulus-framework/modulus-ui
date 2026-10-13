using Microsoft.Extensions.DependencyInjection;
using Modulus.Ui.Abstractions.Admin;
using Xunit;

namespace Modulus.Blazor.Tests;

public class SampleAdminDataTests
{
    private static SampleAdminData New() => new();

    [Fact]
    public void IsSample_IsTrue() => Assert.True(New().IsSample);

    [Fact]
    public async Task Users_SearchInviteRemove()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        var d = New();
        var all = await d.SearchAsync(null, ct);
        Assert.NotEmpty(all);

        await ((IUserDirectory)d).InviteAsync("new.person@example.com", "Admin", ct);
        var found = await ((IUserDirectory)d).SearchAsync("new.person", ct);
        var user = Assert.Single(found);
        Assert.Equal("Admin", user.Role);

        await ((IUserDirectory)d).RemoveAsync(user.Id, ct);
        Assert.Empty(await ((IUserDirectory)d).SearchAsync("new.person", ct));
        Assert.Equal(all.Count, (await ((IUserDirectory)d).SearchAsync(null, ct)).Count);
    }

    [Fact]
    public async Task Roles_CreateAndDelete()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        IRoleDirectory d = New();
        var before = (await d.SearchAsync(null, ct)).Count;

        await d.CreateAsync("Auditor", "Reads the audit log", ct);
        Assert.Contains(await d.SearchAsync("audit", ct), r => r.Name == "Auditor");

        await d.DeleteAsync("Auditor", ct);
        Assert.Equal(before, (await d.SearchAsync(null, ct)).Count);
    }

    [Fact]
    public async Task Tenants_CreateSaveAndMembers()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        ITenantDirectory d = New();
        await d.CreateAsync("Contoso", "contoso.example", ct);
        var tenant = Assert.Single(await d.SearchAsync("contoso", ct));

        var details = await d.GetAsync(tenant.Id, ct);
        Assert.NotNull(details);
        details!.Name = "Contoso Ltd";
        await d.SaveAsync(tenant.Id, details, ct);
        Assert.Equal("Contoso Ltd", (await d.GetAsync(tenant.Id, ct))!.Name);

        await d.InviteMemberAsync(tenant.Id, "a@contoso.example", "Member", ct);
        Assert.Contains(await d.InvitationsAsync(tenant.Id, ct), i => i.Email == "a@contoso.example");
        Assert.Null(await d.GetAsync("does-not-exist", ct));
    }

    [Fact]
    public async Task Notifications_MarkOneThenAllRead()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        INotificationCenter d = New();
        var list = await d.ListAsync("someone", null, ct);
        Assert.Contains(list, n => !n.Read);

        await d.MarkReadAsync("someone", list.First(n => !n.Read).Id, ct);
        Assert.Equal(list.Count(n => !n.Read) - 1, (await d.ListAsync("someone", null, ct)).Count(n => !n.Read));

        await d.MarkReadAsync("someone", null, ct);
        Assert.All(await d.ListAsync("someone", null, ct), n => Assert.True(n.Read));
    }

    [Fact]
    public async Task Settings_RoundTrip()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        IAdminSettingsStore d = New();
        var s = await d.GetAsync(ct);
        s.AppName = "Renamed";
        await d.SaveAsync(s, ct);
        Assert.Equal("Renamed", (await d.GetAsync(ct)).AppName);
    }

    [Fact]
    public async Task Metrics_AndAudit_ReturnData()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        var d = New();
        Assert.NotEmpty((await ((IMetricsSource)d).DashboardAsync(ct)).Stats);
        Assert.NotEmpty((await ((IMetricsSource)d).AnalyticsAsync(ct)).Traffic);
        Assert.NotEmpty((await ((IMetricsSource)d).ReportsAsync(ct)).Reports);
        Assert.NotEmpty((await ((IMetricsSource)d).HealthAsync(ct)).Services);
        Assert.NotEmpty(await ((IAuditTrail)d).SearchAsync(null, ct));
    }

    [Fact]
    public void Registration_SharesOneSample()
    {
        using var sp = new ServiceCollection().AddLogging().AddModulusBlazor().BuildServiceProvider();

        Assert.Same(sp.GetRequiredService<IUserDirectory>(), sp.GetRequiredService<IRoleDirectory>());
        Assert.True(sp.GetRequiredService<IUserDirectory>().IsSample);
    }

    [Fact]
    public void Registration_YourOwnImplementationWins()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<IRoleDirectory, Mine>();
        using var sp = services.AddModulusBlazor().BuildServiceProvider();

        Assert.IsType<Mine>(sp.GetRequiredService<IRoleDirectory>());
        Assert.False(sp.GetRequiredService<IRoleDirectory>().IsSample);
        Assert.True(sp.GetRequiredService<IUserDirectory>().IsSample);
    }

    private sealed class Mine : IRoleDirectory
    {
        public Task<IReadOnlyList<AdminRole>> SearchAsync(string? query, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<AdminRole>>([]);
        public Task CreateAsync(string name, string description, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(string name, CancellationToken ct = default) => Task.CompletedTask;
    }
}
