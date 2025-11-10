using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Hexmaster.DesignGuidelines.Core.Services;

var repoRoot = Environment.GetEnvironmentVariable("HEXMASTER_REPO_ROOT")
               ?? Directory.GetCurrentDirectory();

var builder = Host.CreateApplicationBuilder(args);

// Configure logging to stderr (MCP requirement)
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Register services
builder.Services.AddHttpClient();
builder.Services.AddSingleton(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    return new DocumentService(httpClient, repoRoot);
});

// Configure MCP Server
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();

/// <summary>
/// Program class for testing access.
/// </summary>
public partial class Program { }
