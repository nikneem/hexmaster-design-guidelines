using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HexMaster.CodingGuidelines.Docs.Abstractions;

/// <summary>
/// Provides access to coding guidelines, standards, recommendations and ADRs.
/// </summary>
public interface IDocumentCatalog
{
    /// <summary>
    /// List all available documents with metadata.
    /// </summary>
    IReadOnlyList<DocumentInfo> ListDocuments();

    /// <summary>
    /// Get the raw Markdown content of a document by id.
    /// </summary>
    Task<string> GetContentAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Simple case-insensitive search over title and content.
    /// </summary>
    IReadOnlyList<DocumentInfo> Search(string query);
}

/// <summary>
/// Lightweight document metadata.
/// </summary>
public sealed record DocumentInfo(
    string Id,
    string Title,
    string Category,
    string RelativePath
);
