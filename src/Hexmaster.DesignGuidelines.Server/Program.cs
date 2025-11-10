using System.Text.Json;
using Hexmaster.DesignGuidelines.Core.Services;

// MCP Server - communicates via JSON-RPC over stdin/stdout
var repoRoot = Environment.GetEnvironmentVariable("HEXMASTER_REPO_ROOT")
               ?? Directory.GetCurrentDirectory();

using var httpClient = new HttpClient();
var documentService = new DocumentService(httpClient, repoRoot);

// Read JSON-RPC messages from stdin
using var reader = new StreamReader(Console.OpenStandardInput());
using var writer = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };

var jsonOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};

while (true)
{
    var line = await reader.ReadLineAsync();
    if (string.IsNullOrEmpty(line)) break;

    try
    {
        var request = JsonSerializer.Deserialize<JsonRpcRequest>(line, jsonOptions);
        if (request == null) continue;

        object? result = request.Method switch
        {
            "tools/list" => HandleToolsList(),
            "tools/call" => await HandleToolsCallAsync(request, documentService),
            "initialize" => HandleInitialize(),
            _ => throw new JsonRpcException(-32601, $"Method not found: {request.Method}")
        };

        var response = new JsonRpcResponse
        {
            Jsonrpc = "2.0",
            Id = request.Id,
            Result = result
        };

        await writer.WriteLineAsync(JsonSerializer.Serialize(response, jsonOptions));
    }
    catch (JsonRpcException ex)
    {
        var errorResponse = new JsonRpcResponse
        {
            Jsonrpc = "2.0",
            Id = null,
            Error = new JsonRpcError { Code = ex.Code, Message = ex.Message }
        };
        await writer.WriteLineAsync(JsonSerializer.Serialize(errorResponse, jsonOptions));
    }
    catch (Exception ex)
    {
        var errorResponse = new JsonRpcResponse
        {
            Jsonrpc = "2.0",
            Id = null,
            Error = new JsonRpcError { Code = -32603, Message = $"Internal error: {ex.Message}" }
        };
        await writer.WriteLineAsync(JsonSerializer.Serialize(errorResponse, jsonOptions));
    }
}

static object HandleInitialize()
{
    return new
    {
        protocolVersion = "2024-11-05",
        capabilities = new
        {
            tools = new { }
        },
        serverInfo = new
        {
            name = "hexmaster-design-guidelines",
            version = "1.0.0"
        }
    };
}

static object HandleToolsList()
{
    return new
    {
        tools = new object[]
        {
            new
            {
                name = "list_documents",
                description = "Lists all available design guideline documents (ADRs, recommendations, structures)",
                inputSchema = new
                {
                    type = "object",
                    properties = new { }
                }
            },
            new
            {
                name = "get_document",
                description = "Retrieves the content of a specific document by its ID",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        id = new
                        {
                            type = "string",
                            description = "The document ID (e.g., '0001' for ADRs, 'rec-unit-testing' for recommendations)"
                        }
                    },
                    required = new[] { "id" }
                }
            }
        }
    };
}

static async Task<object> HandleToolsCallAsync(JsonRpcRequest request, DocumentService documentService)
{
    var args = request.Params?.GetProperty("arguments");
    var toolName = request.Params?.GetProperty("name").GetString();

    return toolName switch
    {
        "list_documents" => new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = JsonSerializer.Serialize(documentService.ListDocuments())
                }
            }
        },
        "get_document" => await HandleGetDocumentAsync(args, documentService),
        _ => throw new JsonRpcException(-32602, "Invalid tool name")
    };
}

static async Task<object> HandleGetDocumentAsync(JsonElement? args, DocumentService documentService)
{
    if (args == null || !args.Value.TryGetProperty("id", out var idElement))
    {
        throw new JsonRpcException(-32602, "Missing required parameter: id");
    }

    var id = idElement.GetString();
    if (string.IsNullOrEmpty(id))
    {
        throw new JsonRpcException(-32602, "Invalid parameter: id cannot be empty");
    }

    var (doc, content) = await documentService.GetDocumentAsync(id, CancellationToken.None);

    if (doc == null)
    {
        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = $"Error: Document '{id}' not found"
                }
            },
            isError = true
        };
    }

    return new
    {
        content = new[]
        {
            new
            {
                type = "text",
                text = content ?? "Error: Document content not available"
            }
        }
    };
}

// JSON-RPC types
record JsonRpcRequest
{
    public string Jsonrpc { get; init; } = "2.0";
    public object? Id { get; init; }
    public string Method { get; init; } = "";
    public JsonElement? Params { get; init; }
}

record JsonRpcResponse
{
    public string Jsonrpc { get; init; } = "2.0";
    public object? Id { get; init; }
    public object? Result { get; init; }
    public JsonRpcError? Error { get; init; }
}

record JsonRpcError
{
    public int Code { get; init; }
    public string Message { get; init; } = "";
}

class JsonRpcException : Exception
{
    public int Code { get; }
    public JsonRpcException(int code, string message) : base(message)
    {
        Code = code;
    }
}

/// <summary>
/// Program class for testing access.
/// </summary>
public partial class Program { }
