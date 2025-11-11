using System.Linq;
using System.Threading.Tasks;
using HexMaster.CodingGuidelines.Docs.Infrastructure;
using Xunit;

namespace HexMaster.CodingGuidelines.McpServer.Tests;

public class DocumentCatalogTests
{
    [Fact]
    public void ListDocuments_ReturnsItems()
    {
        var catalog = new FileSystemDocumentCatalog();
        var docs = catalog.ListDocuments();
        Assert.NotEmpty(docs);
        Assert.Contains(docs, d => d.Id.Contains("adopt-dotnet"));
    }

    [Fact]
    public async Task GetContent_ReturnsMarkdown()
    {
        var catalog = new FileSystemDocumentCatalog();
        var id = catalog.ListDocuments().First().Id;
        var content = await catalog.GetContentAsync(id);
        Assert.False(string.IsNullOrWhiteSpace(content));
        Assert.Contains("#", content);
    }

    [Fact]
    public void Search_FindsExpected()
    {
        var catalog = new FileSystemDocumentCatalog();
        var results = catalog.Search("target framework");
        Assert.Contains(results, r => r.Id.Contains("0001"));
    }
}
