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
        var d = New();
        var all = await d.SearchAsync(null);
        Assert.NotEmpty(all);

        await ((IUserDirectory)d).InviteAsync("new.person@example.com", "Admin");
        var found = await ((IUserDirectory)d).SearchAsync("new.person");
        var user = Assert.Single(found);
        Assert.Equal("Admin", user.Role);

        await ((IUserDirectory)d).RemoveAsync(user.Id);
        Assert.Empty(await ((IUserDirectory)d).SearchAsync("new.person"));
        Assert.Equal(all.Count, (await ((IUserDirectory)d).SearchAsync(null)).Count);
    }

    [Fact]
    public async Task Roles_CreateAndDelete()
    {
        IRoleDirectory d = New();
        var before = (await d.SearchAsync(null)).Count;

        await d.CreateAsync("Auditor", "Reads the audit log");
        Assert.Contains(await d.SearchAsync("audit"), r => r.Name == "Auditor");

        await d.DeleteAsync("Auditor");
        Assert.Equal(before, (await d.SearchAsync(null)).Count);
    }

    [Fact]
    public async Task Tenants_CreateSaveAndMembers()
    {
        ITenantDirectory d = New();
        await d.CreateAsync("Contoso", "contoso.example");
        var tenant = Assert.Single(await d.SearchAsync("contoso"));

        var details = await d.GetAsync(tenant.Id);
        Assert.NotNull(details);
        details!.Name = "Contoso Ltd";
        await d.SaveAsync(tenant.Id, details);
        Assert.Equal("Contoso Ltd", (await d.GetAsync(tenant.Id))!.Name);

        await d.InviteMemberAsync(tenant.Id, "a@contoso.example", "Member");
        Assert.Contains(await d.InvitationsAsync(tenant.Id), i => i.Email == "a@contoso.example");
        Assert.Null(await d.GetAsync("does-not-exist"));
    }

    [Fact]
    public async Task Notifications_MarkOneThenAllRead()
    {
        INotificationCenter d = New();
        var list = await d.ListAsync("someone", null);
        Assert.Contains(list, n => !n.Read);

        await d.MarkReadAsync("someone", list.First(n => !n.Read).Id);
        Assert.Equal(list.Count(n => !n.Read) - 1, (await d.ListAsync("someone", null)).Count(n => !n.Read));

        await d.MarkReadAsync("someone", null);
        Assert.All(await d.ListAsync("someone", null), n => Assert.True(n.Read));
    }

    [Fact]
    public async Task Settings_RoundTrip()
    {
        IAdminSettingsStore d = New();
        var s = await d.GetAsync();
        s.AppName = "Renamed";
        await d.SaveAsync(s);
        Assert.Equal("Renamed", (await d.GetAsync()).AppName);
    }

    [Fact]
    public async Task Metrics_AndAudit_ReturnData()
    {
        var d = New();
        Assert.NotEmpty((await ((IMetricsSource)d).DashboardAsync()).Stats);
        Assert.NotEmpty((await ((IMetricsSource)d).AnalyticsAsync()).Traffic);
        Assert.NotEmpty((await ((IMetricsSource)d).ReportsAsync()).Reports);
        Assert.NotEmpty((await ((IMetricsSource)d).HealthAsync()).Services);
        Assert.NotEmpty(await ((IAuditTrail)d).SearchAsync(null));
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
