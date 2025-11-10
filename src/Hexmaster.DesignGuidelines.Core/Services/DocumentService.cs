using System.Net.Http.Headers;
using Hexmaster.DesignGuidelines.Core.Models;

namespace Hexmaster.DesignGuidelines.Core.Services;

public sealed class DocumentService
{
    private readonly HttpClient _http;
    private readonly string _repoRoot;
    private const string GitHubRawBase = "https://raw.githubusercontent.com/nikneem/hexmaster-design-guidelines/main/";

    public DocumentService(HttpClient httpClient, string repositoryRoot)
    {
        _http = httpClient;
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("HexmasterDesignGuidelines", "1.0"));
        _repoRoot = repositoryRoot;
    }

    public IEnumerable<object> ListDocuments()
    {
        return DocumentRegistry.All.Select(d => new
        {
            d.Id,
            d.Title,
            d.Category,
            d.RelativePath
        });
    }

    public async Task<(GuidelineDocument? Doc, string? Content)> GetDocumentAsync(string id, CancellationToken ct)
    {
        var doc = DocumentRegistry.All.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));
        if (doc is null) return (null, null);

        // Try local filesystem first
        var localPath = Path.Combine(_repoRoot, doc.RelativePath.Replace('/', '\\'));
        if (File.Exists(localPath))
        {
            var content = await File.ReadAllTextAsync(localPath, ct);
            return (doc, content);
        }

        // Fallback: GitHub raw
        var url = GitHubRawBase + doc.RelativePath.Replace("\\", "/");
        using var resp = await _http.GetAsync(url, ct);
        if (resp.IsSuccessStatusCode)
        {
            var content = await resp.Content.ReadAsStringAsync(ct);
            return (doc, content);
        }

        return (doc, null);
    }
}
