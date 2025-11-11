using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HexMaster.CodingGuidelines.Docs.Abstractions;

namespace HexMaster.CodingGuidelines.Docs.Infrastructure;

/// <summary>
/// Loads Markdown documents directly from the GitHub repository's docs/ folder using the GitHub API/raw content.
/// Default repo: nikneem/hexmaster-design-guidelines (branch: main).
/// </summary>
public sealed class GitHubDocumentCatalog : IDocumentCatalog, IAsyncDisposable
{
    private readonly string _owner;
    private readonly string _repo;
    private readonly string _branch;
    private readonly HttpClient _http;
    private readonly Lazy<Task<IReadOnlyList<DocumentInfo>>> _documents;

    public GitHubDocumentCatalog(string owner = "nikneem", string repo = "hexmaster-design-guidelines", string branch = "main", HttpClient? httpClient = null)
    {
        _owner = owner;
        _repo = repo;
        _branch = branch;
        _http = httpClient ?? new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("HexMaster.McpServer", "1.0"));
        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (!string.IsNullOrWhiteSpace(token))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        _documents = new Lazy<Task<IReadOnlyList<DocumentInfo>>>(LoadIndexAsync);
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

    private async Task<IReadOnlyList<DocumentInfo>> LoadIndexAsync()
    {
        var list = new List<DocumentInfo>();
        await foreach (var file in EnumerateDocsAsync("docs").ConfigureAwait(false))
        {
            // Titles are derived from file name here; content fetch is deferred to GetContentAsync
            var title = file.name.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                ? file.name[..^3].Replace('-', ' ')
                : file.name;
            var relative = file.path.Length > 5 ? file.path.Substring(5) : file.path; // strip leading "docs/"
            var category = relative.Contains('/') ? relative[..relative.LastIndexOf('/')]: string.Empty;
            var id = GenerateId(relative);
            list.Add(new DocumentInfo(id, title, category, relative.Replace('/', System.IO.Path.DirectorySeparatorChar)));
        }
        // Stable ordering
        list.Sort((a,b) => string.Compare(a.Category, b.Category, StringComparison.OrdinalIgnoreCase) switch
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
}
