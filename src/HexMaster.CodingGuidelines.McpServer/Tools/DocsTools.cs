using System.ComponentModel;
using System.Text.Json;
using HexMaster.CodingGuidelines.Docs.Abstractions;
using ModelContextProtocol.Server;

namespace HexMaster.CodingGuidelines.McpServer.Tools;

[McpServerToolType]
public static class DocsTools
{
    [McpServerTool(Name = "list_docs"), Description("Lists all documentation with id, title and category.")]
    public static string ListDocs(IDocumentCatalog catalog)
    {
        var docs = catalog.ListDocuments().Select(d => new { d.Id, d.Title, d.Category, d.RelativePath, d.Tags });
        return JsonSerializer.Serialize(docs);
    }

    [McpServerTool(Name = "list_docs_by_type"), Description("Lists documentation filtered by category (e.g. 'adrs', 'guidelines').")]
    public static string ListDocsByType(IDocumentCatalog catalog, [Description("Category to filter by")] string category)
    {
        var docs = catalog.ListDocuments()
            .Where(d => string.Equals(d.Category.Split('/')?.FirstOrDefault() ?? string.Empty, category, StringComparison.OrdinalIgnoreCase))
            .Select(d => new { d.Id, d.Title, d.Category, d.RelativePath, d.Tags });
        return JsonSerializer.Serialize(docs);
    }

    [McpServerTool(Name = "search_docs"), Description("Searches documents by title/id/path (case-insensitive).")]
    public static string SearchDocs(IDocumentCatalog catalog, [Description("Search query")] string query)
    {
        var docs = catalog.Search(query).Select(d => new { d.Id, d.Title, d.Category, d.RelativePath, d.Tags });
        return JsonSerializer.Serialize(docs);
    }

    [McpServerTool(Name = "search_docs_by_tag"), Description("Search documents by tag.")]
    public static string SearchDocsByTag(IDocumentCatalog catalog, [Description("Tag to filter by")] string tag)
    {
        var docs = catalog.SearchByTag(tag).Select(d => new { d.Id, d.Title, d.Category, d.RelativePath, d.Tags });
        return JsonSerializer.Serialize(docs);
    }

    [McpServerTool(Name = "get_doc"), Description("Returns the full Markdown content of a document by id.")]
    public static async Task<string> GetDocAsync(IDocumentCatalog catalog, [Description("Document id")] string id, CancellationToken ct)
        => await catalog.GetContentAsync(id, ct);
}
