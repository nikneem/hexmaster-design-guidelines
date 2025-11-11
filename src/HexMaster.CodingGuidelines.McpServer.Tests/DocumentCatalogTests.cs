using System;
using System.Linq;
using System.Threading.Tasks;
using HexMaster.CodingGuidelines.Docs.Abstractions;
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

    [Fact]
    public void Search_WithEmptyQuery_ReturnsEmpty()
    {
        var catalog = new FileSystemDocumentCatalog();
        var results = catalog.Search("");
        Assert.Empty(results);
    }

    [Fact]
    public void Search_WithWhitespaceQuery_ReturnsEmpty()
    {
        var catalog = new FileSystemDocumentCatalog();
        var results = catalog.Search("   ");
        Assert.Empty(results);
    }

    [Fact]
    public void Search_WithNullQuery_ReturnsEmpty()
    {
        var catalog = new FileSystemDocumentCatalog();
        var results = catalog.Search(null!);
        Assert.Empty(results);
    }

    [Fact]
    public void Search_CaseInsensitive_FindsResults()
    {
        var catalog = new FileSystemDocumentCatalog();
        var results = catalog.Search("TARGET FRAMEWORK");
        Assert.NotEmpty(results);
    }

    [Fact]
    public void Search_ByTitle_FindsDocument()
    {
        var catalog = new FileSystemDocumentCatalog();
        var docs = catalog.ListDocuments();
        var firstDoc = docs.First();
        var results = catalog.Search(firstDoc.Title.Substring(0, 5));
        Assert.NotEmpty(results);
    }

    [Fact]
    public async Task GetContent_WithInvalidId_ThrowsException()
    {
        var catalog = new FileSystemDocumentCatalog();
        await Assert.ThrowsAsync<System.IO.FileNotFoundException>(
            async () => await catalog.GetContentAsync("non-existent-id"));
    }

    [Fact]
    public void ListDocuments_ReturnsSortedByCategory()
    {
        var catalog = new FileSystemDocumentCatalog();
        var docs = catalog.ListDocuments();
        var categories = docs.Select(d => d.Category).ToList();
        var sortedCategories = categories.OrderBy(c => c).ToList();
        Assert.Equal(sortedCategories, categories);
    }

    [Fact]
    public void ListDocuments_AllDocumentsHaveValidIds()
    {
        var catalog = new FileSystemDocumentCatalog();
        var docs = catalog.ListDocuments();
        foreach (var doc in docs)
        {
            Assert.False(string.IsNullOrWhiteSpace(doc.Id));
            Assert.False(string.IsNullOrWhiteSpace(doc.Title));
            Assert.False(string.IsNullOrWhiteSpace(doc.RelativePath));
        }
    }

    [Fact]
    public void DocumentInfo_RecordEquality_Works()
    {
        var doc1 = new DocumentInfo("test-id", "Test Title", "category", "path/to/file.md");
        var doc2 = new DocumentInfo("test-id", "Test Title", "category", "path/to/file.md");
        var doc3 = new DocumentInfo("other-id", "Test Title", "category", "path/to/file.md");

        Assert.Equal(doc1, doc2);
        Assert.NotEqual(doc1, doc3);
    }

    [Fact]
    public void DocumentInfo_ToString_ContainsId()
    {
        var doc = new DocumentInfo("test-id", "Test Title", "category", "path/to/file.md");
        var str = doc.ToString();
        Assert.Contains("test-id", str);
    }

    [Fact]
    public async Task GetContent_MultipleDocuments_AllReadable()
    {
        var catalog = new FileSystemDocumentCatalog();
        var docs = catalog.ListDocuments().Take(3);

        foreach (var doc in docs)
        {
            var content = await catalog.GetContentAsync(doc.Id);
            Assert.False(string.IsNullOrWhiteSpace(content));
        }
    }

    [Fact]
    public void Search_FindsDocumentByRelativePath()
    {
        var catalog = new FileSystemDocumentCatalog();
        var results = catalog.Search("adrs");
        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Contains("adrs", r.Category));
    }

    [Fact]
    public void FileSystemCatalog_WithCustomRoot_Works()
    {
        var docsPath = FindDocsFolder();
        Assert.NotNull(docsPath);

        var catalog = new FileSystemDocumentCatalog(docsPath);
        var docs = catalog.ListDocuments();
        Assert.NotEmpty(docs);
    }

    [Fact]
    public void FileSystemCatalog_WithNonExistentRoot_ReturnsEmpty()
    {
        var catalog = new FileSystemDocumentCatalog("C:\\NonExistentPath\\Docs");
        var docs = catalog.ListDocuments();
        Assert.Empty(docs);
    }

    private static string? FindDocsFolder()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var docsPath = System.IO.Path.Combine(current, "docs");
            if (System.IO.Directory.Exists(docsPath))
                return docsPath;
            current = System.IO.Directory.GetParent(current)?.FullName;
        }
        return null;
    }
}
