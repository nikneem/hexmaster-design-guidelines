using System.Text.RegularExpressions;

namespace Hexmaster.DesignGuidelines.Core.Models;

public sealed class DesignDocument : GuidelineDocument
{
    public DesignDocument(string id, string title, string relativePath, string rawContent)
        : base(id, title, relativePath, rawContent, DocumentCategory.Design) { }
}

public sealed class AdrDocument : GuidelineDocument
{
    public string Status { get; }
    public DateTime? Date { get; }

    public AdrDocument(string id, string title, string relativePath, string rawContent, string status, DateTime? date)
        : base(id, title, relativePath, rawContent, DocumentCategory.Adr)
    {
        Status = status;
        Date = date;
    }

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

public sealed class RecommendationDocument : GuidelineDocument
{
    public RecommendationDocument(string id, string title, string relativePath, string rawContent)
        : base(id, title, relativePath, rawContent, DocumentCategory.Recommendation) { }
}

public sealed class StructureDocument : GuidelineDocument
{
    public StructureDocument(string id, string title, string relativePath, string rawContent)
        : base(id, title, relativePath, rawContent, DocumentCategory.Structure) { }
}

public abstract class GuidelineDocument
{
    public string Id { get; }
    public string Title { get; }
    public string RelativePath { get; }
    public string RawContent { get; }
    public DocumentCategory Category { get; }

    protected GuidelineDocument(string id, string title, string relativePath, string rawContent, DocumentCategory category)
    {
        Id = id;
        Title = title;
        RelativePath = relativePath;
        RawContent = rawContent;
        Category = category;
    }
}
