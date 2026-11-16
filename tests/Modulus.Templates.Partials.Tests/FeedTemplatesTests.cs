using System.IO.Compression;
using Xunit;

namespace Modulus.Templates.Partials.Tests;

/// <summary>
/// The CLI scaffolds from the Synvro.Modulus.Ui.Templates package in the local feed, not from this repository. This test fails
/// when that package's templates differ from the repository's, which happens after a template change that was not packed
/// (fix: tools/refresh-local-feed.sh). With no feed package on the machine, the test is skipped.
/// </summary>
public class FeedTemplatesTests
{
    [Fact]
    public void LocalFeedTemplates_MatchTheRepository()
    {
        var root = FindRepoRoot();
        var version = VersionPrefix(root);
        var feed = Environment.GetEnvironmentVariable("MODULUS_LOCAL_FEED");
        if (string.IsNullOrWhiteSpace(feed))
            feed = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".modulus", "feed");
        var nupkg = Path.Combine(feed, $"Synvro.Modulus.Ui.Templates.{version}.nupkg");
        if (!File.Exists(nupkg)) return;

        var templatesRoot = Path.Combine(root, "src", "Modulus.Ui.Templates");
        var stale = new List<string>();
        using (var zip = ZipFile.OpenRead(nupkg))
        {
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName == "ui-kit.json")
                    Compare(entry, Path.Combine(templatesRoot, "ui-kit.json"), stale);
                else if (entry.FullName.StartsWith("templates/", StringComparison.Ordinal))
                    Compare(entry, Path.Combine(templatesRoot, entry.FullName.Replace('/', Path.DirectorySeparatorChar)), stale);
            }
        }

        Assert.True(stale.Count == 0,
            $"The local feed holds a stale {nupkg}. Out of date: {string.Join(", ", stale.Take(8))}" +
            (stale.Count > 8 ? $" and {stale.Count - 8} more" : "") + ". Run tools/refresh-local-feed.sh.");
    }

    private static void Compare(ZipArchiveEntry entry, string repoFile, List<string> stale)
    {
        if (!File.Exists(repoFile))
        {
            stale.Add(entry.FullName + " (not in the repository)");
            return;
        }
        using var packed = entry.Open();
        using var ms = new MemoryStream();
        packed.CopyTo(ms);
        if (!ms.ToArray().AsSpan().SequenceEqual(File.ReadAllBytes(repoFile)))
            stale.Add(entry.FullName);
    }

    private static string VersionPrefix(string root)
    {
        var props = File.ReadAllText(Path.Combine(root, "src", "Directory.Build.props"));
        var m = System.Text.RegularExpressions.Regex.Match(props, "<VersionPrefix>([^<]+)</VersionPrefix>");
        return m.Success ? m.Groups[1].Value : throw new InvalidOperationException("No VersionPrefix in src/Directory.Build.props.");
    }

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Modulus.slnx"))) return dir.FullName;
        throw new InvalidOperationException("Run the tests from inside the Modulus repository.");
    }
}
