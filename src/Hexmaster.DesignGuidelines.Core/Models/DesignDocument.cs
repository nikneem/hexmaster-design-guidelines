using System.Text.RegularExpressions;

namespace Hexmaster.DesignGuidelines.Core.Models;

/// <summary>
/// Represents a design document from the designs/ folder.
/// </summary>
public sealed class DesignDocument : GuidelineDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DesignDocument"/> class.
    /// </summary>
    public DesignDocument(string id, string title, string relativePath, string rawContent)
        : base(id, title, relativePath, rawContent, DocumentCategory.Design) { }
}

/// <summary>
/// Represents an Architecture Decision Record (ADR) document.
/// </summary>
public sealed class AdrDocument : GuidelineDocument
{
    /// <summary>
    /// Gets the status of the ADR (Proposed, Accepted, Deprecated, etc.).
    /// </summary>
    public string Status { get; }

    /// <summary>
    /// Gets the decision date of the ADR.
    /// </summary>
    public DateTime? Date { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AdrDocument"/> class.
    /// </summary>
    public AdrDocument(string id, string title, string relativePath, string rawContent, string status, DateTime? date)
        : base(id, title, relativePath, rawContent, DocumentCategory.Adr)
    {
        Status = status;
        Date = date;
    }

    /// <summary>
    /// Parses an ADR document from raw markdown content.
    /// </summary>
    /// <param name="relativePath">The relative path to the ADR file.</param>
    /// <param name="rawContent">The raw markdown content.</param>
    /// <returns>A parsed <see cref="AdrDocument"/> instance.</returns>
    public static AdrDocument Parse(string relativePath, string rawContent)
    {
        // Expect file name NNNN-title.md
        var fileName = Path.GetFileName(relativePath);
        var match = Regex.Match(fileName, "^(?<num>\\d{4})-(?<slug>.+)\\.md$", RegexOptions.IgnoreCase);
        var id = match.Success ? match.Groups["num"].Value : Guid.NewGuid().ToString("N");
        string title = ExtractHeading(rawContent) ?? fileName;
        string status = ExtractField(rawContent, "Status") ?? "Unknown";
        DateTime? date = DateTime.TryParse(ExtractField(rawContent, "Date"), out var d) ? d : null;
        return new AdrDocument(id, title, relativePath, rawContent, status, date);
    }

    private static string? ExtractHeading(string content)
    {
        using var reader = new StringReader(content);
        while (reader.ReadLine() is string line)
        {
            if (line.StartsWith("# ")) return line[2..].Trim();
        }
        return null;
    }

    private static string? ExtractField(string content, string fieldName)
    {
        var pattern = $"^{fieldName}\\s*:\\s*(?<val>.+)$";
        foreach (var line in content.Split('\n'))
        {
            var m = Regex.Match(line, pattern, RegexOptions.IgnoreCase);
            if (m.Success) return m.Groups["val"].Value.Trim();
        }
        return null;
    }
}

/// <summary>
/// Represents a recommendation document.
/// </summary>
public sealed class RecommendationDocument : GuidelineDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RecommendationDocument"/> class.
    /// </summary>
    public RecommendationDocument(string id, string title, string relativePath, string rawContent)
        : base(id, title, relativePath, rawContent, DocumentCategory.Recommendation) { }
}

/// <summary>
/// Represents a structure document providing project templates and scaffolds.
/// </summary>
public sealed class StructureDocument : GuidelineDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StructureDocument"/> class.
    /// </summary>
    public StructureDocument(string id, string title, string relativePath, string rawContent)
        : base(id, title, relativePath, rawContent, DocumentCategory.Structure) { }
}

/// <summary>
/// Base class for all guideline documents.
/// </summary>
public abstract class GuidelineDocument
{
    /// <summary>
    /// Gets the unique identifier of the document.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the title of the document.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the relative path to the document from the repository root.
    /// </summary>
    public string RelativePath { get; }

    /// <summary>
    /// Gets the raw markdown content of the document.
    /// </summary>
    public string RawContent { get; }

    /// <summary>
    /// Gets the category of the document.
    /// </summary>
    public DocumentCategory Category { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GuidelineDocument"/> class.
    /// </summary>
    protected GuidelineDocument(string id, string title, string relativePath, string rawContent, DocumentCategory category)
    {
        Id = id;
        Title = title;
        RelativePath = relativePath;
        RawContent = rawContent;
        Category = category;
    }
}
