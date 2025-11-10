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
        new AdrDocument("0003", ".NET Aspire Recommendation for ASP.NET Services", "docs/adrs/0003-recommend-aspire-for-aspnet-projects.md", string.Empty, "Proposed", new DateTime(2025, 11, 10)),
        new AdrDocument("0004", "CQRS Recommendation for ASP.NET API", "docs/adrs/0004-cqrs-recommendation-for-aspnet-api.md", string.Empty, "Proposed", new DateTime(2025, 11, 10)),
        new AdrDocument("0005", "Minimal APIs Over Controller-Based APIs", "docs/adrs/0005-minimal-apis-over-controllers.md", string.Empty, "Proposed", new DateTime(2025, 11, 10)),
        new AdrDocument("0006", "GitHub Actions CI/CD with Semantic Versioning and NuGet Publishing", "docs/adrs/0006-github-actions-cicd-semantic-versioning.md", string.Empty, "Accepted", new DateTime(2025, 11, 10)),
        // Recommendations
        new RecommendationDocument("rec-unit-testing", "Unit Testing with xUnit, Moq, Bogus", "docs/recommendations/unit-testing-xunit-moq-bogus.md", string.Empty),
        // Structures
        new StructureDocument("structure-minimal-api-endpoints", "Minimal API Endpoint Organization", "docs/structures/minimal-api-endpoint-organization.md", string.Empty),
    };

    /// <summary>
    /// Gets all registered guideline documents.
    /// </summary>
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
