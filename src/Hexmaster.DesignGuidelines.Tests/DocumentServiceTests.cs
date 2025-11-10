using System.Net;
using Hexmaster.DesignGuidelines.Core.Services;
using Xunit;

namespace Hexmaster.DesignGuidelines.Tests;

public class DocumentServiceTests
{
    [Fact]
    public async Task ListDocuments_ShouldReturnRegisteredIds()
    {
        using var http = new HttpClient(new FakeHandler());
        var svc = new DocumentService(http, GetRepoRoot());
        var list = svc.ListDocuments().ToList();
        Assert.Contains(list, d => string.Equals(d.GetType().GetProperty("Id")?.GetValue(d)?.ToString(), "0001"));
        Assert.Contains(list, d => string.Equals(d.GetType().GetProperty("Id")?.GetValue(d)?.ToString(), "0002"));
    }

    [Fact]
    public async Task GetDocumentAsync_ShouldReturnContent_ForExistingAdr()
    {
        using var http = new HttpClient(new FakeHandler());
        var svc = new DocumentService(http, GetRepoRoot());
        var (doc, content) = await svc.GetDocumentAsync("0001", CancellationToken.None);
        Assert.NotNull(doc);
        Assert.NotNull(content);
        Assert.Contains("Adopt .NET 9", content);
    }

    [Fact]
    public async Task GetDocumentAsync_ShouldReturnNull_ForNonExistentId()
    {
        using var http = new HttpClient(new FakeHandler());
        var svc = new DocumentService(http, GetRepoRoot());
        var (doc, content) = await svc.GetDocumentAsync("9999", CancellationToken.None);
        Assert.Null(doc);
        Assert.Null(content);
    }

    [Fact]
    public async Task GetDocumentAsync_ShouldReturnDocWithNullContent_WhenFileNotFoundLocallyOrRemote()
    {
        using var http = new HttpClient(new FakeHandler());
        var svc = new DocumentService(http, "C:\\NonExistentPath");
        var (doc, content) = await svc.GetDocumentAsync("0001", CancellationToken.None);
        Assert.NotNull(doc); // Doc is registered
        Assert.Null(content); // But file not found locally, and remote returns 404
    }

    [Fact]
    public async Task GetDocumentAsync_ShouldFallbackToGitHub_WhenLocalFileMissing()
    {
        using var http = new HttpClient(new FakeHandlerWithSuccessfulGitHub());
        var svc = new DocumentService(http, "C:\\NonExistentPath");
        var (doc, content) = await svc.GetDocumentAsync("0001", CancellationToken.None);
        Assert.NotNull(doc);
        Assert.NotNull(content);
        Assert.Contains("Mocked GitHub content", content);
    }

    [Fact]
    public void ListDocuments_ShouldIncludeAllCategories()
    {
        using var http = new HttpClient(new FakeHandler());
        var svc = new DocumentService(http, GetRepoRoot());
        var list = svc.ListDocuments().ToList();

        // Should have ADRs, Recommendations, and Structures
        Assert.Contains(list, d => d.GetType().GetProperty("Category")?.GetValue(d)?.ToString() == "Adr");
        Assert.Contains(list, d => d.GetType().GetProperty("Category")?.GetValue(d)?.ToString() == "Recommendation");
        Assert.Contains(list, d => d.GetType().GetProperty("Category")?.GetValue(d)?.ToString() == "Structure");
    }

    private static string GetRepoRoot()
    {
        // Assume tests run from repo root parent of src
        var current = Directory.GetCurrentDirectory();
        // Search upward for README.md sentinel
        var dir = new DirectoryInfo(current);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "README.md")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? current;
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Returns 404 for remote fallback; tests focus on local reads.
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private sealed class FakeHandlerWithSuccessfulGitHub : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Simulate successful GitHub response
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("Mocked GitHub content for testing fallback")
            };
            return Task.FromResult(response);
        }
    }
}
