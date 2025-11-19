using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HexMaster.CodingGuidelines.Docs.Abstractions;
using Microsoft.Extensions.Caching.Memory;

namespace HexMaster.CodingGuidelines.Docs.Infrastructure;

/// <summary>
/// Loads Markdown documents from the GitHub repository's docs/ folder.
/// Prefers docs/index.json with 10-minute cache; falls back to directory traversal if index is missing.
/// Default repo: nikneem/hexmaster-design-guidelines (branch: main).
/// </summary>
public sealed class GitHubDocumentCatalog : IDocumentCatalog, IAsyncDisposable
{
    private readonly string _owner;
    private readonly string _repo;
    private readonly string _branch;
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly Lazy<Task<IReadOnlyList<DocumentInfo>>> _documents;

    public GitHubDocumentCatalog(
        IMemoryCache cache,
        string owner = "nikneem",
        string repo = "hexmaster-design-guidelines",
        string branch = "main",
        HttpClient? httpClient = null)
    {
        _owner = owner;
        _repo = repo;
        _branch = branch;
        _http = httpClient ?? new HttpClient();
        _cache = cache;
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("HexMaster.McpServer", "1.0"));
        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (!string.IsNullOrWhiteSpace(token))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        _documents = new Lazy<Task<IReadOnlyList<DocumentInfo>>>(LoadDocumentsAsync);
    }

    public IReadOnlyList<DocumentInfo> ListDocuments() => _documents.Value.GetAwaiter().GetResult();

    public IReadOnlyList<DocumentInfo> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<DocumentInfo>();
        var q = query.Trim();
        var all = ListDocuments();
        var results = new List<DocumentInfo>();
        foreach (var d in all)
        {
            if (d.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                d.Id.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                d.RelativePath.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(d);
            }
        }
        return results;
    }

    public IReadOnlyList<DocumentInfo> SearchByTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return Array.Empty<DocumentInfo>();
        var all = _documents.Value.GetAwaiter().GetResult();
        return all.Where(d => d.Tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase))).ToList();
    }

    public async Task<string> GetContentAsync(string id, CancellationToken cancellationToken = default)
    {
        var all = await _documents.Value.ConfigureAwait(false);
        foreach (var d in all)
        {
            if (string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                var rawUrl = $"https://raw.githubusercontent.com/{_owner}/{_repo}/{_branch}/docs/{d.RelativePath.Replace("\\", "/")}";
                return await _http.GetStringAsync(rawUrl, cancellationToken).ConfigureAwait(false);
            }
        }
        throw new InvalidOperationException($"Document '{id}' not found in GitHub index");
    }

    private async Task<IReadOnlyList<DocumentInfo>> LoadDocumentsAsync()
    {
        var cacheKey = $"docs-index:{_owner}/{_repo}/{_branch}";

        // Try cache first
        if (_cache.TryGetValue<IReadOnlyList<DocumentInfo>>(cacheKey, out var cached))
        {
            return cached!;
        }

        // Try loading from index.json
        try
        {
            var indexUrl = $"https://raw.githubusercontent.com/{_owner}/{_repo}/{_branch}/docs/index.json";
            var indexJson = await _http.GetStringAsync(indexUrl).ConfigureAwait(false);
            var indexData = JsonSerializer.Deserialize<DocumentIndexFile>(indexJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (indexData?.Documents != null && indexData.Documents.Count > 0)
            {
                var documents = indexData.Documents
                    .Select(d => new DocumentInfo(
                        d.Id,
                        d.Title,
                        d.Category,
                        d.RelativePath,
                        (IReadOnlyList<string>)(d.Tags ?? new List<string>())))
                    .ToList();

                // Cache with 10-minute sliding expiration
                _cache.Set(cacheKey, documents, new MemoryCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromMinutes(10)
                });

                return documents;
            }
        }
        catch
        {
            // Index.json missing or invalid, fall back to traversal
        }

        // Fallback: traverse directory structure (existing logic)
        var list = await LoadIndexViaDirTraversalAsync().ConfigureAwait(false);

        // Cache the fallback result too
        _cache.Set(cacheKey, list, new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(10)
        });

        return list;
    }

    private async Task<IReadOnlyList<DocumentInfo>> LoadIndexViaDirTraversalAsync()
    {
        var list = new List<DocumentInfo>();
        await foreach (var file in EnumerateDocsAsync("docs").ConfigureAwait(false))
        {
            // Titles are derived from file name here; content fetch is deferred to GetContentAsync
            var title = file.name.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                ? file.name[..^3].Replace('-', ' ')
                : file.name;
            var relative = file.path.Length > 5 ? file.path.Substring(5) : file.path; // strip leading "docs/"
            var category = relative.Contains('/') ? relative[..relative.LastIndexOf('/')] : string.Empty;
            var id = GenerateId(relative);
            // Try to fetch the raw content to parse front matter for title and tags
            string raw = string.Empty;
            try
            {
                var rawUrl = $"https://raw.githubusercontent.com/{_owner}/{_repo}/{_branch}/docs/{relative}";
                raw = await _http.GetStringAsync(rawUrl).ConfigureAwait(false);
            }
            catch
            {
                // ignore, fallback to title only
            }
            var (frontTitle, tags) = ParseFrontMatterForTitleAndTags(raw);
            var finalTitle = frontTitle ?? title;
            list.Add(new DocumentInfo(id, finalTitle, category, relative.Replace('/', System.IO.Path.DirectorySeparatorChar), tags));
        }
        // Stable ordering
        list.Sort((a, b) => string.Compare(a.Category, b.Category, StringComparison.OrdinalIgnoreCase) switch
        {
            0 => string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase),
            var x => x
        });
        return list;
    }

    private static string GenerateId(string relativePath)
        => System.IO.Path.GetFileNameWithoutExtension(relativePath)
            .Replace(' ', '-')
            .Replace('_', '-')
            .ToLowerInvariant();

    private static (string? Title, IReadOnlyList<string> Tags) ParseFrontMatterForTitleAndTags(string content)
    {
        if (string.IsNullOrWhiteSpace(content) || !content.StartsWith("---")) return (null, Array.Empty<string>());
        var end = content.IndexOf("\n---", StringComparison.Ordinal);
        if (end < 0) return (null, Array.Empty<string>());
        var block = content.Substring(3, end - 3).Trim();
        string? title = null;
        var tags = new List<string>();
        bool inTagList = false;
        foreach (var ln in block.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = ln.Trim();
            if (line.StartsWith("title:", StringComparison.OrdinalIgnoreCase))
            {
                title = line.Substring(line.IndexOf(':') + 1).Trim().Trim('"');
                continue;
            }
            if (line.StartsWith("tags:", StringComparison.OrdinalIgnoreCase))
            {
                var rest = line.Substring(line.IndexOf(':') + 1).Trim();
                if (rest.StartsWith("["))
                {
                    rest = rest.Trim('[', ']');
                    tags.AddRange(rest.Split(',').Select(s => s.Trim().Trim('"')).Where(s => s.Length > 0));
                }
                else
                {
                    inTagList = true;
                }
                continue;
            }
            if (inTagList)
            {
                if (line.StartsWith("- "))
                {
                    tags.Add(line.Substring(2).Trim().Trim('"'));
                    continue;
                }
                inTagList = false;
            }
        }
        return (title, tags);
    }

    private async IAsyncEnumerable<(string name, string path)> EnumerateDocsAsync(string path)
    {
        var url = $"https://api.github.com/repos/{_owner}/{_repo}/contents/{path}?ref={_branch}";
        using var resp = await _http.GetAsync(url).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
        var items = await JsonSerializer.DeserializeAsync<List<GitHubContentItem>>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }).ConfigureAwait(false) ?? new();
        foreach (var item in items)
        {
            if (string.Equals(item.Type, "dir", StringComparison.OrdinalIgnoreCase))
            {
                await foreach (var inner in EnumerateDocsAsync(item.Path))
                {
                    yield return inner;
                }
            }
            else if (string.Equals(item.Type, "file", StringComparison.OrdinalIgnoreCase) && item.Name.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                yield return (item.Name, item.Path);
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        _http.Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed class GitHubContentItem
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "file" or "dir"
    }

    private sealed class DocumentIndexFile
    {
        public string Version { get; set; } = string.Empty;
        public string Generated { get; set; } = string.Empty;
        public List<DocumentIndexEntry> Documents { get; set; } = new();
    }

    private sealed class DocumentIndexEntry
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public List<string>? Tags { get; set; }
    }
}
