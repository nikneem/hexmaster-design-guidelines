using System.ComponentModel;
using System.Text.Json;
using HexMaster.CodingGuidelines.Docs.Abstractions;
using ModelContextProtocol.Server;

namespace HexMaster.CodingGuidelines.McpServer.Tools;

[McpServerToolType]
public static class DocsTools
{
    [McpServerTool(Name = "list_docs"), Description("Retrieves the complete catalog of all available documentation, including ADRs, designs, recommendations, and structures. Returns id, title, category, description, relative path, and tags for each document. Use this to get a full overview of available guidance before deciding which documents to read in detail.")]
    public static string ListDocs(IDocumentCatalog catalog)
    {
        var docs = catalog.ListDocuments().Select(d => new { d.Id, d.Title, d.Description, d.Category, d.RelativePath, d.Tags });
        return JsonSerializer.Serialize(docs);
    }

    [McpServerTool(Name = "list_docs_by_type"), Description("Retrieves all documents belonging to a specific category. Valid categories are: 'adrs' (Architecture Decision Records with status, context, and consequences), 'designs' (exploratory design documents and diagrams), 'recommendations' (prescriptive best-practice guidance), and 'structures' (canonical project scaffolds and templates). Use this when you know the type of guidance you need rather than searching by keyword.")]
    public static string ListDocsByType(IDocumentCatalog catalog, [Description("Category to filter by")] string category)
    {
        var docs = catalog.ListDocuments()
            .Where(d => string.Equals(d.Category.Split('/')?.FirstOrDefault() ?? string.Empty, category, StringComparison.OrdinalIgnoreCase))
            .Select(d => new { d.Id, d.Title, d.Description, d.Category, d.RelativePath, d.Tags });
        return JsonSerializer.Serialize(docs);
    }

    [McpServerTool(Name = "search_docs"), Description("Searches the document catalog by matching a query against document titles, IDs, paths, and descriptions (case-insensitive). Returns all matching documents with full metadata. Use this when you have a keyword or concept in mind (e.g. 'logging', 'persistence', 'value object') and want to discover all relevant guidance without knowing exact document IDs or categories.")]
    public static string SearchDocs(IDocumentCatalog catalog, [Description("Search query")] string query)
    {
        var docs = catalog.Search(query).Select(d => new { d.Id, d.Title, d.Description, d.Category, d.RelativePath, d.Tags });
        return JsonSerializer.Serialize(docs);
    }

    [McpServerTool(Name = "search_docs_by_tag"), Description("Finds all documents annotated with a specific tag (e.g. 'persistence', 'resilience', 'domain', 'testing', 'cqrs'). Tag-based search is more precise than text search and maps directly to architectural concerns and cross-cutting topics. Use this when you need all guidance related to a specific architectural area or concern rather than a free-text keyword.")]
    public static string SearchDocsByTag(IDocumentCatalog catalog, [Description("Tag to filter by")] string tag)
    {
        var docs = catalog.SearchByTag(tag).Select(d => new { d.Id, d.Title, d.Description, d.Category, d.RelativePath, d.Tags });
        return JsonSerializer.Serialize(docs);
    }

    [McpServerTool(Name = "get_doc"), Description("Fetches the complete Markdown content of a specific document by its ID. Use this after listing or searching to read the full text of an ADR (including decision rationale and consequences), a recommendation, a design document, or a project structure template. Always call this before implementing anything that may be governed by an existing ADR or recommendation.")]
    public static async Task<string> GetDocAsync(IDocumentCatalog catalog, [Description("Document id")] string id, CancellationToken ct)
        => await catalog.GetContentAsync(id, ct);
}
