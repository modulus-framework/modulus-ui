using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Modulus.AspNetCore.Fluid;

/// <summary>
/// Reads Liquid files for <c>{% render %}</c> and <c>{% include %}</c> from one folder. A name resolves with or without the
/// <c>.liquid</c> extension, so <c>{% render 'Shared/Components/badge' %}</c> finds <c>Shared/Components/badge.liquid</c>.
/// A name that climbs out of the folder (<c>..</c>) or is rooted resolves to nothing.
/// </summary>
internal sealed class FolderFileProvider : IFileProvider
{
    private readonly string _root;

    public FolderFileProvider(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        _root = Path.GetFullPath(root);
    }

    public IDirectoryContents GetDirectoryContents(string subpath) => NotFoundDirectoryContents.Singleton;

    public IFileInfo GetFileInfo(string subpath)
    {
        var resolved = Resolve(subpath);
        return resolved is null ? new NotFoundFileInfo(subpath) : new Info(resolved);
    }

    public IChangeToken Watch(string filter) => NullChangeToken.Singleton;

    private string? Resolve(string subpath)
    {
        if (string.IsNullOrWhiteSpace(subpath) || Path.IsPathRooted(subpath)) return null;
        var name = subpath.Replace('\\', '/').TrimStart('/');
        foreach (var candidate in new[] { name, name + ".liquid" })
        {
            var full = Path.GetFullPath(Path.Combine(_root, candidate));
            if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
            if (File.Exists(full)) return full;
        }
        return null;
    }

    private sealed class Info(string path) : IFileInfo
    {
        private readonly FileInfo _file = new(path);

        public bool Exists => true;
        public long Length => _file.Length;
        public string? PhysicalPath => _file.FullName;
        public string Name => _file.Name;
        public DateTimeOffset LastModified => _file.LastWriteTimeUtc;
        public bool IsDirectory => false;
        public Stream CreateReadStream() => _file.OpenRead();
    }
}
