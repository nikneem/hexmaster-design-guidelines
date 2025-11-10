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
}
