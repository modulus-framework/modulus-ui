using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Modulus.Templates.Partials.Tests;

/// <summary>
/// Renders the Razor Pages partials that the CLI scaffolds (<c>templates/razor-pages/*.cshtml.sbn</c>, copied into
/// <c>tests/fixtures/PartialsHost/Pages/Partials</c>) with sample data, and checks that each shows its data. The
/// Modulus tag helpers and the page layout run as they do in the samples, so a partial that throws, or that shows a
/// variable name as text, fails here.
/// </summary>
public class RazorPagesPartialsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RazorPagesPartialsTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task EveryPartial_RendersWithItsSampleData()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        // approval-timeline
        Assert.Contains("Manager review", html);
        Assert.Contains("Approval steps", html);
        // audit-trail (no filter: every row shows)
        Assert.Contains("Within budget", html);
        Assert.Contains("Quantity", html);
        // radio-group
        Assert.Contains("Sets the queue order", html);
        Assert.Contains("Normal", html);
        // chat (the transcript and the placeholder)
        Assert.Contains("Three orders are waiting for approval.", html);
        Assert.Contains("Ask about orders", html);
        // image-cropper
        Assert.Contains("alt=\"Hero\"", html);
        // card
        Assert.Contains("Body text", html);
        Assert.Contains("Updated today", html);
    }

    [Fact]
    public async Task EveryPartial_ShowsNoVariableNameAsText()
    {
        using var client = _factory.CreateClient();

        var html = await client.GetStringAsync("/");

        // A bare name passed to a tag helper (placeholder="placeholder") shows the name, so no ViewBag key may appear as text.
        foreach (var name in new[] { "Steps", "AriaLabel", "Entries", "Placeholder", "Turns", "Items", "Src", "HeaderAction" })
            Assert.DoesNotContain($">{name}<", html);
    }
}

/// <summary>
/// Every plain Razor Pages partial renders with empty data, which is the state a page is in before its data arrives. The
/// partials are read from the templates at run time (<c>tests/fixtures/PartialsHost/PartialTemplateFileProvider.cs</c>), so a
/// template that throws on empty input fails here. Templates with Scriban markers ({{ }}) are rendered by the CLI, not here.
/// </summary>
public class EmptyPartialsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public EmptyPartialsTests(WebApplicationFactory<Program> factory) => _factory = factory;

    public static IEnumerable<object[]> PlainPartials() =>
        File.ReadAllLines(PlainListPath())
            .Where(l => l.Length > 0)
            .Select(n => (object[])[n])
            .OrderBy(a => (string)a[0], StringComparer.Ordinal);

    /// <summary>plain-partials.txt is the list the fixture copies. It must name every template with no Scriban markers and no @page.</summary>
    [Fact]
    public void PlainPartialList_MatchesTheTemplatesOnDisk()
    {
        var onDisk = Directory.EnumerateFiles(TemplatesDirectory(), "*.cshtml.sbn")
            .Where(f => { var t = File.ReadAllText(f); return !t.Contains("{{") && !t.Contains("@page"); })
            .Select(f => Path.GetFileName(f)[..^".cshtml.sbn".Length])
            .Order(StringComparer.Ordinal)
            .ToList();
        var listed = File.ReadAllLines(PlainListPath()).Where(l => l.Length > 0).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(onDisk, listed);
    }

    private static string PlainListPath() => Path.Combine(FindRepoRoot(), "tests", "fixtures", "PartialsHost", "plain-partials.txt");

    private static string TemplatesDirectory() => Path.Combine(FindRepoRoot(), "src", "Modulus.Ui.Templates", "templates", "razor-pages");

    [Theory]
    [MemberData(nameof(PlainPartials))]
    public async Task Partial_RendersWithEmptyData(string name)
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/partial/" + name);
        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, $"{name}: HTTP {(int)response.StatusCode}");
        Assert.DoesNotContain("Unhandled exception", html);
        Assert.DoesNotContain("An error occurred", html);
    }

    /// <summary>
    /// Every plain partial renders with its sample values (fixture PartialSamples.cs): the state a page gives it with data.
    /// A typed value of the wrong type, or a dynamic item missing a property, fails here.
    /// </summary>
    [Theory]
    [MemberData(nameof(PlainPartials))]
    public async Task Partial_RendersWithSampleData(string name)
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/partial/" + name + "?sample=true");
        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, $"{name}: HTTP {(int)response.StatusCode}");
        Assert.DoesNotContain("Unhandled exception", html);
    }

    /// <summary>Every plain partial has sample values, so the sample render covers the whole list. A new partial without one fails here.</summary>
    [Theory]
    [MemberData(nameof(PlainPartials))]
    public void Partial_HasSampleData(string name) =>
        Assert.NotEmpty(PartialSamples.For(name));

    /// <summary>The sample values reach the partial: its title and its message show, so a passing render is not an empty one.</summary>
    [Fact]
    public async Task SampleData_ReachesThePartial()
    {
        using var client = _factory.CreateClient();

        var html = await client.GetStringAsync("/partial/empty-state?sample=true");

        Assert.Contains("No orders", html);
        Assert.Contains("Create one to start.", html);
        Assert.Contains("New order", html);
    }

    /// <summary>With no form yet, the PDF button shows as a disabled button with its label (open task 7, decided 2026-11-15).</summary>
    [Fact]
    public async Task PdfButton_WithNoForm_ShowsADisabledPlaceholder()
    {
        using var client = _factory.CreateClient();

        var html = await client.GetStringAsync("/partial/pdf-button");

        Assert.Contains("disabled", html);
        Assert.Contains("Download PDF", html);
        Assert.DoesNotContain("formaction", html);
    }

    /// <summary>The MVC and Razor Pages templates of a partial are the same file, so the fixture can render either.</summary>
    [Theory]
    [InlineData("approval-timeline")]
    [InlineData("audit-trail")]
    [InlineData("radio-group")]
    [InlineData("chat")]
    [InlineData("image-cropper")]
    [InlineData("card")]
    public void MvcAndRazorPagesTemplates_AreTheSame(string name)
    {
        var root = FindRepoRoot();
        var rp = File.ReadAllText(Path.Combine(root, "src", "Modulus.Ui.Templates", "templates", "razor-pages", name + ".cshtml.sbn"));
        var mvc = File.ReadAllText(Path.Combine(root, "src", "Modulus.Ui.Templates", "templates", "mvc", name + ".cshtml.sbn"));
        Assert.Equal(rp.ReplaceLineEndings("\n"), mvc.ReplaceLineEndings("\n"));
    }

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Modulus.slnx"))) return dir.FullName;
        throw new InvalidOperationException("Run the tests from inside the Modulus repository.");
    }
}
