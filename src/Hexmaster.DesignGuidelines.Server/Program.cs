using Hexmaster.DesignGuidelines.Core.Services;

var builder = WebApplication.CreateBuilder(args);

// Determine repository root: assume server is run from repo root; otherwise allow override via env var.
var repoRoot = Environment.GetEnvironmentVariable("HEXMASTER_REPO_ROOT")
               ?? Directory.GetCurrentDirectory();

builder.Services.AddHttpClient();
builder.Services.AddSingleton(sp =>
{
    var http = sp.GetRequiredService<HttpClient>();
    return new DocumentService(http, repoRoot);
});

var app = builder.Build();

app.MapGet("/docs", (DocumentService svc) => Results.Json(svc.ListDocuments()));

app.MapGet("/docs/{id}", async (string id, DocumentService svc, CancellationToken ct) =>
{
    var (doc, content) = await svc.GetDocumentAsync(id, ct);
    if (doc is null) return Results.NotFound(new { error = $"Document '{id}' is not registered. Update DocumentRegistry to add it." });
    if (content is null) return Results.NotFound(new { error = $"Document '{id}' registered but not found locally or on GitHub." });
    return Results.Text(content, "text/markdown");
});

app.MapGet("/health", () => Results.Ok(new { status = "ok", runtime = ".NET 9" }));

app.Run();
