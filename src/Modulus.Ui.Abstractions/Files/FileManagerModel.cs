using System.Globalization;

namespace Modulus.Ui.Abstractions.Files;

/// <summary>An entry in the tree: a folder or a file, under <paramref name="ParentId"/> (null for the top level).</summary>
public sealed record FileEntry(int Id, int? ParentId, string Name, bool IsFolder, long Size = 0, DateTime? Modified = null);

/// <summary>A step of the breadcrumb: a folder's id (null for the top level) and its name.</summary>
public sealed record FileCrumb(int? Id, string Name);

/// <summary>A row of the listing: the entry, its size and date as text, its kind (Folder, PDF, Image...) and its icon name.</summary>
public sealed record FileItemView(FileEntry Entry, string Kind, string Icon, string SizeText, string ModifiedText);

/// <summary>A folder ready to draw: where it is, the way back up, the items in it (folders first), and the search it was filtered by.</summary>
public sealed record FileManagerView(int? FolderId, IReadOnlyList<FileCrumb> Crumbs, IReadOnlyList<FileItemView> Items, string Search);

/// <summary>Where the file manager is: the folder to show and the search text (empty for none).</summary>
public sealed record FileNavigation(int? Folder, string Search = "");

/// <summary>A folder or file to create under a folder (null for the top level), with its name.</summary>
public sealed record FileCreate(int? ParentId, string Name, bool IsFolder = true);

/// <summary>An entry renamed: its id and its new name.</summary>
public sealed record FileRename(int Id, string Name);

/// <summary>
/// The file manager's model, shared by the Blazor <c>FileManager</c> and the MVC <c>app-file-manager</c>: the listing of one
/// folder with its breadcrumb, the search within it, the name rules, and the size, date and kind of each entry.
/// </summary>
public static class FileManagerModel
{
    /// <summary>The longest name an entry may have.</summary>
    public const int MaxNameLength = 120;

    private const int MaxDepth = 64;
    private const string InvalidChars = "\\/:*?\"<>|";

    /// <summary>
    /// Lists a folder. An unknown <paramref name="folderId"/> shows the top level. The search keeps the items whose name
    /// contains it (ignoring case); folders come first, then names in order.
    /// </summary>
    public static FileManagerView Build(IEnumerable<FileEntry> entries, int? folderId, string? search, DateTime now)
    {
        var all = entries.ToList();
        var byId = all.ToDictionary(e => e.Id);
        var folder = folderId is { } id && byId.TryGetValue(id, out var f) && f.IsFolder ? f.Id : (int?)null;

        var crumbs = new List<FileCrumb> { new(null, "Files") };
        var chain = new List<FileCrumb>();
        var current = folder;
        for (var depth = 0; current is { } cid && byId.TryGetValue(cid, out var node) && depth < MaxDepth; depth++)
        {
            chain.Add(new FileCrumb(node.Id, node.Name));
            current = node.ParentId;
        }
        chain.Reverse();
        crumbs.AddRange(chain);

        var term = (search ?? "").Trim();
        var items = all.Where(e => e.ParentId == folder)
            .Where(e => term.Length == 0 || e.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.IsFolder)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .Select(e => new FileItemView(e, KindOf(e), IconOf(e), e.IsFolder ? "" : SizeText(e.Size), ModifiedText(e.Modified, now)))
            .ToList();

        return new FileManagerView(folder, crumbs, items, term);
    }

    /// <summary>
    /// Why a name cannot be used under a folder, or null when it can. A name is required, at most <see cref="MaxNameLength"/>
    /// characters, not <c>.</c> or <c>..</c>, free of <c>\ / : * ? " &lt; &gt; |</c> and control characters, and unique among its
    /// siblings (ignoring case). <paramref name="exceptId"/> is the entry being renamed, which does not count as its own sibling.
    /// </summary>
    public static string? NameError(string? name, int? parentId, IEnumerable<FileEntry> entries, int? exceptId = null)
    {
        var text = (name ?? "").Trim();
        if (text.Length == 0) return "Give it a name.";
        if (text.Length > MaxNameLength) return $"Use {MaxNameLength} characters or fewer.";
        if (text is "." or "..") return "That name is reserved.";
        if (text.Any(c => InvalidChars.Contains(c) || char.IsControl(c))) return "Names can't contain \\ / : * ? \" < > | or control characters.";
        if (entries.Any(e => e.ParentId == parentId && e.Id != exceptId && string.Equals(e.Name, text, StringComparison.OrdinalIgnoreCase)))
            return "A file or folder with that name is already here.";
        return null;
    }

    /// <summary>A size as text: bytes below 1 KB, then KB, MB or GB to one decimal place (1024 bytes to a unit).</summary>
    public static string SizeText(long bytes)
    {
        if (bytes < 1024) return bytes.ToString(CultureInfo.InvariantCulture) + " B";
        string[] units = ["KB", "MB", "GB", "TB"];
        var value = bytes / 1024.0;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return value.ToString("0.#", CultureInfo.InvariantCulture) + " " + units[unit];
    }

    /// <summary>A date as text: the time for today, "Yesterday", the month and day this year, or the full date. A dash when unknown.</summary>
    public static string ModifiedText(DateTime? modified, DateTime now)
    {
        if (modified is not { } m) return "–";
        var day = m.Date;
        if (day == now.Date) return "Today " + m.ToString("HH:mm", CultureInfo.InvariantCulture);
        if (day == now.Date.AddDays(-1)) return "Yesterday";
        return day.Year == now.Year
            ? m.ToString("MMM d", CultureInfo.InvariantCulture)
            : m.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
    }

    /// <summary>The kind of an entry, from its extension: Folder, PDF, Document, Spreadsheet, Image, Archive, Text, or File.</summary>
    public static string KindOf(FileEntry e)
    {
        if (e.IsFolder) return "Folder";
        return Path.GetExtension(e.Name).ToLowerInvariant() switch
        {
            ".pdf" => "PDF",
            ".doc" or ".docx" or ".odt" => "Document",
            ".xls" or ".xlsx" or ".csv" or ".ods" => "Spreadsheet",
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".svg" => "Image",
            ".zip" or ".7z" or ".rar" or ".tar" or ".gz" => "Archive",
            ".txt" or ".md" => "Text",
            _ => "File",
        };
    }

    private static string IconOf(FileEntry e) => KindOf(e) switch
    {
        "Folder" => "folder",
        "PDF" => "file-pdf",
        "Document" => "file-word",
        "Spreadsheet" => "file-excel",
        "Image" => "picture",
        "Archive" => "tb:file-zip",
        "Text" => "file-text",
        _ => "file",
    };
}
