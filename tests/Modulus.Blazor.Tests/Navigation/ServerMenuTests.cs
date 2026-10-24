using System.Text.Json;
using Modulus.Ui.Abstractions.Navigation;
using Xunit;

namespace Modulus.Blazor.Tests.Navigation;

public class ServerMenuTests
{
    private static ServerMenuNode Node(string key, string title, string? url, params ServerMenuNode[] children)
        => new(key, title, url, null, children);

    [Fact]
    public void A_ServerUrl_BecomesAnAppRelativePath_AndAGroupHasNone()
    {
        var page = ServerMenu.ToDefinition(Node("customers", "Customers", "/customers/list"));
        var group = ServerMenu.ToDefinition(Node("sales", "Sales", null));

        Assert.Equal("customers/list", page.Href);
        Assert.Equal("Customers", page.Label);
        Assert.Null(group.Href);
        Assert.False(page.ExactMatch);
    }

    [Fact]
    public void TheRootUrl_IsMatchedExactly()
    {
        var root = ServerMenu.ToDefinition(Node("home", "Home", "/"));

        Assert.Equal(string.Empty, root.Href);
        Assert.True(root.ExactMatch);
    }

    [Fact]
    public void SiblingsKeepTheServersOrder_AtEveryLevel()
    {
        var menu = ServerMenu.ToDefinitions([
            Node("b", "B", "/b", Node("b2", "Second", "/b/2"), Node("b1", "First", "/b/1")),
            Node("a", "A", "/a"),
        ]);

        Assert.Equal(["b", "a"], menu.Select(i => i.Key));
        Assert.Equal(["b2", "b1"], menu[0].Children.Select(i => i.Key));
        Assert.Equal(0, menu[0].Order);
        Assert.Equal(1, menu[1].Order);
    }

    [Fact]
    public async Task TheMenuProvider_ShowsTheServersTree_InTheServersOrder()
    {
        var served = new[]
        {
            Node("reports", "Reports", "/reports", Node("aging", "Aging", "/reports/aging")),
            Node("orders", "Orders", "/orders"),
        };
        var provider = new MenuProvider([new ServerMenuContributor(_ => Task.FromResult<IReadOnlyList<ServerMenuNode>>(served))], services: null!);

        var menu = await provider.GetMainMenuAsync();

        Assert.Equal(["reports", "orders"], menu.Select(i => i.Key));
        Assert.Equal("reports/aging", menu[0].Children[0].Href);
    }

    [Fact]
    public async Task AFailedFetch_IsNotSwallowed()
    {
        var contributor = new ServerMenuContributor(_ => throw new HttpRequestException("API down"));
        var context = new MenuConfigurationContext(services: null!);

        await Assert.ThrowsAsync<HttpRequestException>(() => contributor.ConfigureMenuAsync(context));
        Assert.Empty(context.Items);
    }

    [Fact]
    public void TheServersJson_DeserializesIntoTheNodeShape()
    {
        const string json = """
            [{"key":"orders","title":"Orders","url":"/orders","icon":"shopping","children":[
                {"key":"open","title":"Open","url":"/orders/open","icon":null,"children":[]}]}]
            """;

        var nodes = JsonSerializer.Deserialize<List<ServerMenuNode>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.Equal("shopping", nodes[0].Icon);
        Assert.Equal("orders/open", ServerMenu.ToDefinition(nodes[0]).Children[0].Href);
    }
}
