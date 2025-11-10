using Hexmaster.DesignGuidelines.Core.Models;

namespace Hexmaster.DesignGuidelines.Core.Services;

/// <summary>
/// Explicit registry of guideline documents.
/// IMPORTANT: When adding a file under the docs/ folder, register it here as well.
/// This constraint is intentional to keep the MCP index explicit and versioned.
/// </summary>
public static class DocumentRegistry
{
    // Relative paths are from repository root
    private static readonly List<GuidelineDocument> _documents = new()
    {
        // Registered ADRs
        new AdrDocument("0001", "Adopt .NET 9 as Target Framework", "docs/adrs/0001-adopt-dotnet-9.md", string.Empty, "Accepted", new DateTime(2025, 11, 10)),
        new AdrDocument("0002", "Modular Monolith Project Structure", "docs/adrs/0002-modular-monolith-structure.md", string.Empty, "Proposed", new DateTime(2025, 11, 10)),
    };

    public static IReadOnlyList<GuidelineDocument> All => _documents;

    /// <summary>
    /// Register or replace a document definition.
    /// </summary>
    public static void Upsert(GuidelineDocument doc)
    {
        var idx = _documents.FindIndex(d => d.Id == doc.Id);
        if (idx >= 0) _documents[idx] = doc; else _documents.Add(doc);
    }
}
