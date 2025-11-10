using Hexmaster.DesignGuidelines.Core.Models;
using Xunit;

namespace Hexmaster.DesignGuidelines.Tests;

public class AdrDocumentTests
{
    [Fact]
    public void Parse_ShouldExtractIdFromFileName()
    {
        var content = "# ADR 0001: Test Title\nDate: 2025-11-10\nStatus: Accepted";
        var result = AdrDocument.Parse("docs/adrs/0001-test-title.md", content);
        Assert.Equal("0001", result.Id);
    }

    [Fact]
    public void Parse_ShouldExtractTitleFromHeading()
    {
        var content = "# ADR 0001: Test Title\nDate: 2025-11-10\nStatus: Accepted";
        var result = AdrDocument.Parse("docs/adrs/0001-test-title.md", content);
        Assert.Equal("ADR 0001: Test Title", result.Title);
    }

    [Fact]
    public void Parse_ShouldUseFileNameWhenHeadingMissing()
    {
        var content = "Date: 2025-11-10\nStatus: Accepted\nNo heading here.";
        var result = AdrDocument.Parse("docs/adrs/0001-test-title.md", content);
        Assert.Equal("0001-test-title.md", result.Title);
    }

    [Fact]
    public void Parse_ShouldExtractStatus()
    {
        var content = "# ADR 0001: Test\nDate: 2025-11-10\nStatus: Proposed\nContext...";
        var result = AdrDocument.Parse("docs/adrs/0001-test.md", content);
        Assert.Equal("Proposed", result.Status);
    }

    [Fact]
    public void Parse_ShouldDefaultToUnknownStatusWhenMissing()
    {
        var content = "# ADR 0001: Test\nDate: 2025-11-10\nContext...";
        var result = AdrDocument.Parse("docs/adrs/0001-test.md", content);
        Assert.Equal("Unknown", result.Status);
    }

    [Fact]
    public void Parse_ShouldExtractDate()
    {
        var content = "# ADR 0001: Test\nDate: 2025-11-10\nStatus: Accepted";
        var result = AdrDocument.Parse("docs/adrs/0001-test.md", content);
        Assert.NotNull(result.Date);
        Assert.Equal(new DateTime(2025, 11, 10), result.Date!.Value.Date);
    }

    [Fact]
    public void Parse_ShouldReturnNullDateWhenInvalid()
    {
        var content = "# ADR 0001: Test\nDate: not-a-date\nStatus: Accepted";
        var result = AdrDocument.Parse("docs/adrs/0001-test.md", content);
        Assert.Null(result.Date);
    }

    [Fact]
    public void Parse_ShouldGenerateGuidIdWhenFileNamePatternInvalid()
    {
        var content = "# ADR Test\nDate: 2025-11-10\nStatus: Accepted";
        var result = AdrDocument.Parse("docs/adrs/invalid-name.md", content);
        Assert.NotEmpty(result.Id);
        Assert.NotEqual("0001", result.Id); // Should be a GUID
    }

    [Fact]
    public void Category_ShouldBeAdr()
    {
        var doc = new AdrDocument("0001", "Test", "path.md", "content", "Accepted", null);
        Assert.Equal(DocumentCategory.Adr, doc.Category);
    }
}
