using System.Text;
using System.Text.RegularExpressions;

namespace Modulus.Ui.Abstractions.Assistant;

/// <summary>Reads uploaded files into text for the knowledge base. Plain text formats only; an app adds its own readers (PDF, Word) the same way.</summary>
public static class DocumentText
{
    /// <summary>The largest file read.</summary>
    public const int MaxDocumentBytes = 2_000_000;

    /// <summary>The longest passage a chunk holds, in characters.</summary>
    public const int ChunkLength = 700;

    /// <summary>The types the samples accept.</summary>
    public const string Accept = ".txt,.md,.csv,.json,.log";

    private static readonly string[] Supported = [".txt", ".md", ".csv", ".json", ".log"];

    public static bool IsSupported(string? fileName) =>
        !string.IsNullOrEmpty(fileName) && Supported.Contains(Path.GetExtension(fileName).ToLowerInvariant());

    /// <summary>The text of a file, or null when the type is not supported, the file is empty or too large, or it is not UTF-8 text.</summary>
    public static string? Read(string? fileName, byte[] bytes)
    {
        if (!IsSupported(fileName) || bytes.Length == 0 || bytes.Length > MaxDocumentBytes) return null;
        try
        {
            return new UTF8Encoding(false, true).GetString(bytes).Replace("\r\n", "\n").Trim();
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    /// <summary>Passages of about <see cref="ChunkLength"/> characters, split at paragraphs (and at words for a very long one).</summary>
    public static IReadOnlyList<string> Chunk(string text)
    {
        var chunks = new List<string>();
        var current = new StringBuilder();
        void Flush()
        {
            if (current.Length == 0) return;
            chunks.Add(current.ToString());
            current.Clear();
        }

        foreach (var paragraph in text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var part = paragraph;
            while (part.Length > ChunkLength)
            {
                var cut = part.LastIndexOf(' ', ChunkLength);
                if (cut <= 0) cut = ChunkLength;
                Flush();
                chunks.Add(part[..cut]);
                part = part[cut..].TrimStart();
            }
            if (current.Length > 0 && current.Length + part.Length + 2 > ChunkLength) Flush();
            if (current.Length > 0) current.Append("\n\n");
            current.Append(part);
        }
        Flush();
        return chunks;
    }
}

/// <summary>A document in the knowledge base: its name and how many passages it became.</summary>
public sealed record KnowledgeDocument(string Name, int Chunks);

/// <summary>
/// The knowledge base the assistant searches: the documents people uploaded, as passages. Adding a document with the name of
/// one already there replaces it. The search is by shared words (a stand-in for a vector index); an app can put its index behind
/// <see cref="IKnowledgeSearch"/> instead.
/// </summary>
public sealed class KnowledgeStore : IKnowledgeSearch
{
    private readonly object _lock = new();
    private readonly Dictionary<string, List<KnowledgeChunk>> _documents = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _order = [];

    /// <summary>The documents, in the order they were added.</summary>
    public IReadOnlyList<KnowledgeDocument> Documents
    {
        get
        {
            lock (_lock) return _order.Select(n => new KnowledgeDocument(n, _documents[n].Count)).ToList();
        }
    }

    /// <summary>Adds a document from its text and returns how many passages it became.</summary>
    public int Add(string name, string text, string? url = null)
    {
        var chunks = DocumentText.Chunk(text).Select((c, i) => new KnowledgeChunk($"{name}#{i + 1}", name, c, url)).ToList();
        lock (_lock)
        {
            if (!_documents.ContainsKey(name)) _order.Add(name);
            _documents[name] = chunks;
        }
        return chunks.Count;
    }

    public void Remove(string name)
    {
        lock (_lock)
        {
            _documents.Remove(name);
            _order.Remove(name);
        }
    }

    public Task<IReadOnlyList<KnowledgeChunk>> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        List<KnowledgeChunk> all;
        lock (_lock) all = _documents.Values.SelectMany(c => c).ToList();
        return Task.FromResult(KeywordRanking.Rank(all, query, limit));
    }
}

/// <summary>Ranks passages by the words they share with a question (words of three letters or more).</summary>
public static class KeywordRanking
{
    public static IReadOnlyList<KnowledgeChunk> Rank(IEnumerable<KnowledgeChunk> chunks, string query, int limit)
    {
        var words = Words(query);
        if (words.Count == 0) return [];
        return chunks
            .Select(c => (Chunk: c, Score: words.Intersect(Words(c.Title + " " + c.Text)).Count()))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(Math.Clamp(limit, 1, 10))
            .Select(x => x.Chunk)
            .ToList();
    }

    public static HashSet<string> Words(string text) =>
        Regex.Matches(text.ToLowerInvariant(), @"[a-z0-9]{3,}").Select(m => m.Value).ToHashSet();
}
