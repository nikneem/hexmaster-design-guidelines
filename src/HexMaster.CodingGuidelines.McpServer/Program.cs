using HexMaster.CodingGuidelines.Docs.Abstractions;
using HexMaster.CodingGuidelines.Docs.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using ModelContextProtocol.Protocol;
using System.Net.Http;

// HexMaster Coding Guidelines MCP Server
// Chooses a document catalog: local path when HEXMASTER_DOCS_PATH is set, otherwise GitHub.

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(consoleLogOptions =>
{
    // route logs to stderr for MCP stdio compliance
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Register HttpClient factory manually to avoid optional extensions
builder.Services.AddSingleton(new HttpClient());

// Resolve docs provider based on environment settings
builder.Services.AddSingleton<IDocumentCatalog>(sp =>
{
    var localPath = Environment.GetEnvironmentVariable("HEXMASTER_DOCS_PATH");
    if (!string.IsNullOrWhiteSpace(localPath) && Directory.Exists(localPath))
    {
        return new FileSystemDocumentCatalog(localPath);
    }

    // Default to GitHub
    var client = sp.GetRequiredService<HttpClient>();
    return new GitHubDocumentCatalog(httpClient: client);
});

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new Implementation
        {
            Name = "HexMaster.CodingGuidelines.McpServer",
            Version = "0.1.0"
        };
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
