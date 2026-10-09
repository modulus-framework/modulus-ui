using Modulus.Ui.Abstractions.Files;

namespace Modulus.Samples.Shared;

/// <summary>
/// The file manager sample's tree, kept in memory for the life of the app (register as a singleton). Creates and renames
/// check the name with <see cref="FileManagerModel.NameError"/>; a folder's delete takes everything under it.
/// </summary>
public sealed class FileStore
{
    private readonly Lock _gate = new();
    private readonly List<FileEntry> _entries;
    private int _nextId;

    public FileStore(TimeProvider? clock = null)
    {
        var now = (clock ?? TimeProvider.System).GetLocalNow().DateTime;
        _entries = Seed(now);
        _nextId = _entries.Max(e => e.Id) + 1;
    }

    /// <summary>A copy of every entry.</summary>
    public IReadOnlyList<FileEntry> Entries()
    {
        lock (_gate) return _entries.ToList();
    }

    /// <summary>Creates a folder or a file under a folder. Returns the name's error, or null when it was created.</summary>
    public string? Create(FileCreate input, DateTime now)
    {
        lock (_gate)
        {
            if (input.ParentId is { } parent && !_entries.Any(e => e.Id == parent && e.IsFolder)) return "That folder is gone.";
            var error = FileManagerModel.NameError(input.Name, input.ParentId, _entries);
            if (error is not null) return error;
            _entries.Add(new FileEntry(_nextId++, input.ParentId, input.Name.Trim(), input.IsFolder, 0, now));
            return null;
        }
    }

    /// <summary>Renames an entry. Returns the name's error, or null when it was renamed.</summary>
    public string? Rename(FileRename input, DateTime now)
    {
        lock (_gate)
        {
            var index = _entries.FindIndex(e => e.Id == input.Id);
            if (index < 0) return "That file is gone.";
            var entry = _entries[index];
            var error = FileManagerModel.NameError(input.Name, entry.ParentId, _entries, entry.Id);
            if (error is not null) return error;
            _entries[index] = entry with { Name = input.Name.Trim(), Modified = now };
            return null;
        }
    }

    /// <summary>Deletes an entry, and everything under it when it is a folder. Returns false when there was no such entry.</summary>
    public bool Delete(int id)
    {
        lock (_gate)
        {
            if (!_entries.Any(e => e.Id == id)) return false;
            var doomed = new HashSet<int> { id };
            var grew = true;
            while (grew)
            {
                grew = false;
                foreach (var e in _entries.Where(e => e.ParentId is { } p && doomed.Contains(p) && !doomed.Contains(e.Id)).ToList())
                {
                    doomed.Add(e.Id);
                    grew = true;
                }
            }
            _entries.RemoveAll(e => doomed.Contains(e.Id));
            return true;
        }
    }

    private static List<FileEntry> Seed(DateTime now)
    {
        var docs = new FileEntry(1, null, "Documents", true, 0, now.AddDays(-2));
        var images = new FileEntry(2, null, "Images", true, 0, now.AddDays(-9));
        var projects = new FileEntry(3, null, "Projects", true, 0, now.AddDays(-1));
        var website = new FileEntry(4, 3, "Website relaunch", true, 0, now.AddDays(-3));
        return
        [
            docs, images, projects, website,
            new FileEntry(5, 1, "Handbook.pdf", false, 2_411_520, now.AddDays(-2)),
            new FileEntry(6, 1, "Meeting notes.docx", false, 48_128, now.AddHours(-5)),
            new FileEntry(7, 1, "Budget 2026.xlsx", false, 186_368, now.AddDays(-4)),
            new FileEntry(8, 1, "README.md", false, 3_072, now.AddDays(-20)),
            new FileEntry(9, 2, "Office.jpg", false, 1_835_008, now.AddDays(-9)),
            new FileEntry(10, 2, "Logo.svg", false, 14_336, now.AddDays(-40)),
            new FileEntry(11, 4, "Launch plan.pdf", false, 912_384, now.AddDays(-3)),
            new FileEntry(12, 4, "Assets.zip", false, 27_262_976, now.AddDays(-6)),
            new FileEntry(13, 3, "Roadmap.docx", false, 96_256, now.AddDays(-1)),
        ];
    }
}
