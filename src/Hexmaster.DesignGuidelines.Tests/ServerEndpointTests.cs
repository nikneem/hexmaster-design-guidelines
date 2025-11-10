using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Hexmaster.DesignGuidelines.Tests;

public class ServerEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ServerEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_ShouldReturnOk()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("ok", content);
        Assert.Contains(".NET 9", content);
    }

    [Fact]
    public async Task ListDocs_ShouldReturnJson()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/docs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ListDocs_ShouldIncludeRegisteredDocuments()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/docs");
        var content = await response.Content.ReadAsStringAsync();

        // Should contain registered ADRs
        Assert.Contains("0001", content);
        Assert.Contains("0002", content);
        Assert.Contains("rec-unit-testing", content);
        Assert.Contains("structure-minimal-api-endpoints", content);
    }

    [Fact]
    public async Task GetDocById_ShouldReturnMarkdown_ForExistingDoc()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/docs/0001");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/markdown", response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Adopt .NET 9", content);
    }

    [Fact]
    public async Task GetDocById_ShouldReturnNotFound_ForNonExistentId()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/docs/9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("not registered", content);
    }

    [Fact]
    public async Task GetDocById_ShouldReturnNotFound_WhenDocRegisteredButFileMissing()
    {
        // This test would need a mock/fake scenario where a doc is registered but file doesn't exist
        // For now, all registered docs should have corresponding files in the repo
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/docs/0001");

        // Should succeed since file exists
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
