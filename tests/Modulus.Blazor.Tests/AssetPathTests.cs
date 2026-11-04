using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>The scripts load their own libraries, styles and the flow editor by URL; a path that names the wrong package 404s every one of them.</summary>
public class AssetPathTests
{
    private static string? Wwwroot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", "Modulus.Ui.Assets", "wwwroot");
            if (Directory.Exists(candidate)) return candidate;
        }
        return null;
    }

    [Fact]
    public void NoScriptHardCodesTheOldPackageName()
    {
        var root = Wwwroot();
        if (root is null) return; // run from a packed test output, not the repository

        // The package is Synvro.Modulus.Ui.Assets (StaticWebAssetBasePath). "/_content/Modulus.Ui.Assets/" was its old name and returns 404.
        var offenders = Directory.EnumerateFiles(Path.Combine(root, "js"), "*.js")
            .Where(f => File.ReadAllText(f).Contains("/_content/Modulus.Ui.Assets/", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(offenders.Count == 0, "Stale asset path in: " + string.Join(", ", offenders));
    }

    [Fact]
    public void ThePluginScriptTakesItsBaseFromItsOwnUrl_AndLoadsTheFlowEditorFromThere()
    {
        var root = Wwwroot();
        if (root is null) return;

        var plugins = File.ReadAllText(Path.Combine(root, "js", "modulus-plugins.js"));

        Assert.Contains("modulus-plugins\\.js", plugins); // the regular expression that finds its own URL
        Assert.Contains("ASSETS + 'js/modulus-flow.js'", plugins);
        Assert.Contains("ASSETS + 'css/modulus-flow.css'", plugins);
        Assert.True(File.Exists(Path.Combine(root, "js", "modulus-flow.js")));
        Assert.True(File.Exists(Path.Combine(root, "css", "modulus-flow.css")));
    }
}
