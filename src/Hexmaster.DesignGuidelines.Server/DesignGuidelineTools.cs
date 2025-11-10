using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Hexmaster.DesignGuidelines.Core.Services;

namespace Hexmaster.DesignGuidelines.Server;

/// <summary>
/// MCP tools for querying design guideline documents.
/// </summary>
[McpServerToolType]
public static class DesignGuidelineTools
{
    /// <summary>
    /// Lists all available design guideline documents.
    /// </summary>
    [McpServerTool, Description("Lists all available design guideline documents including ADRs, recommendations, and structures.")]
    public static string ListDocuments(DocumentService documentService)
    {
        var documents = documentService.ListDocuments();
        return JsonSerializer.Serialize(documents, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Retrieves the full content of a specific document.
    /// </summary>
    [McpServerTool, Description("Retrieves the full content of a specific design guideline document by its ID.")]
    public static async Task<string> GetDocument(
        DocumentService documentService,
        [Description("The document ID (e.g., '0001' for ADR-0001, 'rec-unit-testing' for recommendations)")] string id)
    {
        var (doc, content) = await documentService.GetDocumentAsync(id, CancellationToken.None);
        
        if (doc == null)
        {
            return $"Error: Document '{id}' not found. Use ListDocuments to see available document IDs.";
        }

        if (content == null)
        {
            return $"Error: Content for document '{id}' ('{doc.Title}') could not be loaded.";
        }

        return content;
    }
}
