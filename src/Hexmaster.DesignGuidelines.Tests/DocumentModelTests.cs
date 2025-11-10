using Hexmaster.DesignGuidelines.Core.Models;
using Xunit;

namespace Hexmaster.DesignGuidelines.Tests;

public class DocumentCategoryTests
{
    [Fact]
    public void DocumentCategory_ShouldHaveExpectedValues()
    {
        Assert.Equal(DocumentCategory.Adr, DocumentCategory.Adr);
        Assert.Equal(DocumentCategory.Design, DocumentCategory.Design);
        Assert.Equal(DocumentCategory.Recommendation, DocumentCategory.Recommendation);
        Assert.Equal(DocumentCategory.Structure, DocumentCategory.Structure);
    }

    [Fact]
    public void DesignDocument_ShouldHaveCorrectCategory()
    {
        var doc = new DesignDocument("id", "title", "path.md", "content");
        Assert.Equal(DocumentCategory.Design, doc.Category);
    }

    [Fact]
    public void RecommendationDocument_ShouldHaveCorrectCategory()
    {
        var doc = new RecommendationDocument("id", "title", "path.md", "content");
        Assert.Equal(DocumentCategory.Recommendation, doc.Category);
    }

    [Fact]
    public void StructureDocument_ShouldHaveCorrectCategory()
    {
        var doc = new StructureDocument("id", "title", "path.md", "content");
        Assert.Equal(DocumentCategory.Structure, doc.Category);
    }

    [Fact]
    public void GuidelineDocument_ShouldStoreAllProperties()
    {
        var doc = new DesignDocument("test-id", "Test Title", "docs/test.md", "Test content");
        Assert.Equal("test-id", doc.Id);
        Assert.Equal("Test Title", doc.Title);
        Assert.Equal("docs/test.md", doc.RelativePath);
        Assert.Equal("Test content", doc.RawContent);
    }
}
