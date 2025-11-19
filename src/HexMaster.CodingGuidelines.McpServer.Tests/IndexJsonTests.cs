using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HexMaster.CodingGuidelines.Docs.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace HexMaster.CodingGuidelines.McpServer.Tests;

public class IndexJsonTests
{
    [Fact]
    public void FileSystemCatalog_LoadsFromIndexJson_WhenAvailable()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        try
        {
            var indexPath = Path.Combine(tempDir, "index.json");
            var indexContent = @"{
                ""version"": ""1.0"",
                ""generated"": ""2025-11-19T00:00:00Z"",
                ""documents"": [
                    {
                        ""id"": ""test-doc"",
                        ""title"": ""Test Document"",
                        ""category"": ""test"",
                        ""relativePath"": ""test/test-doc.md"",
                        ""tags"": [""testing"", ""sample""]
                    }
                ]
            }";
            File.WriteAllText(indexPath, indexContent);

            // Act
            var catalog = new FileSystemDocumentCatalog(tempDir);
            var docs = catalog.ListDocuments();

            // Assert
            Assert.Single(docs);
            Assert.Equal("test-doc", docs[0].Id);
            Assert.Equal("Test Document", docs[0].Title);
            Assert.Contains("testing", docs[0].Tags);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void FileSystemCatalog_FallsBackToScanning_WhenIndexMissing()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        var testDir = Path.Combine(tempDir, "test");
        Directory.CreateDirectory(testDir);

        try
        {
            var testFile = Path.Combine(testDir, "test-doc.md");
            File.WriteAllText(testFile, @"---
title: ""Fallback Test Document""
tags: [fallback, test]
---
# Fallback Test Document

Content here.
");

            // Act
            var catalog = new FileSystemDocumentCatalog(tempDir);
            var docs = catalog.ListDocuments();

            // Assert
            Assert.Single(docs);
            Assert.Equal("test-doc", docs[0].Id);
            Assert.Equal("Fallback Test Document", docs[0].Title);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact(Skip = "Requires GitHub network access")]
    public void GitHubCatalog_LoadsFromIndexJson_WhenAvailable()
    {
        // Arrange
        var cache = new MemoryCache(new MemoryCacheOptions());

        // Act - This will attempt to fetch from GitHub
        var catalog = new GitHubDocumentCatalog(cache);
        var docs = catalog.ListDocuments();

        // Assert - Should have loaded from index.json in the repo
        // ID format doesn't include leading zero padding
        Assert.NotEmpty(docs);
        Assert.Contains(docs, d => d.Id.Contains("use-index-json"));
    }

    [Fact(Skip = "Requires GitHub network access")]
    public void GitHubCatalog_CachesResults_WithSlidingExpiration()
    {
        // Arrange
        var cache = new MemoryCache(new MemoryCacheOptions());
        var catalog = new GitHubDocumentCatalog(cache);

        // Act - First call
        var docs1 = catalog.ListDocuments();

        // Act - Second call (should hit cache)
        var docs2 = catalog.ListDocuments();

        // Assert
        Assert.NotEmpty(docs1);
        Assert.NotEmpty(docs2);
        Assert.Equal(docs1.Count, docs2.Count);
    }

    [Fact]
    public void RealIndex_ExistsAndIsValid()
    {
        // Arrange
        var repoRoot = FindRepoRoot();
        Assert.NotNull(repoRoot);

        var indexPath = Path.Combine(repoRoot, "docs", "index.json");

        // Assert index exists
        Assert.True(File.Exists(indexPath), "docs/index.json should exist");

        // Assert index is valid JSON
        var content = File.ReadAllText(indexPath);
        var indexData = JsonSerializer.Deserialize<JsonElement>(content);

        Assert.True(indexData.TryGetProperty("version", out _));
        Assert.True(indexData.TryGetProperty("generated", out _));
        Assert.True(indexData.TryGetProperty("documents", out var docs));
        Assert.True(docs.GetArrayLength() > 0);
    }

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
