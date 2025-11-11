# HexMaster Design Guidelines

Design, architecture, style and structure guidelines for modern .NET (C#) projects, organized as ADRs, designs, recommendations and structures under `docs/`. An MCP Server in `src/` exposes these documents for tools/agents.

## MCP Server (C#, .NET 9)

An MCP (Model Context Protocol) server implementing the official Microsoft MCP SDK. Exposes design guideline documents as tools that AI assistants can call.

### Requirements
- .NET 9 SDK

### MCP Protocol

The server implements the Model Context Protocol using the official `ModelContextProtocol` NuGet package. It exposes tools for:

1. **ListDocuments** - Lists all available design guideline documents (ADRs, recommendations, structures)
2. **GetDocument** - Retrieves the content of a specific document by its ID
3. **SearchDocuments** - Searches documents by keyword or phrase

Documents are served from the local filesystem when available, with automatic fallback to GitHub repository content.

### Run Standalone (for testing)

From the repository root:

```powershell
dotnet run --project .\src\HexMaster.CodingGuidelines.McpServer\HexMaster.CodingGuidelines.McpServer.csproj
```

The server uses stdio transport for MCP communication. Logs are written to stderr, JSON-RPC messages to stdout.

### Install as GitHub Copilot MCP Tool

The MCP Server can be integrated with GitHub Copilot to provide AI agents with access to design guidelines during code generation.

There are two usage scenarios:

1. **Standard Installation** (Recommended) - Install from NuGet, documents fetched from GitHub
2. **Local Development** - Run from source with local documents for testing changes

---

#### Scenario 1: Standard Installation (NuGet Global Tool)

This is the recommended approach for general use. Documents are automatically fetched from the GitHub repository, so no local clone is needed.

**VS Code Setup**

1. **Install the package**:
   ```bash
   dotnet tool install --global HexMaster.CodingGuidelines.McpServer
   ```

2. **Configure VS Code MCP settings**:
   
   Create or edit `.vscode/mcp.json` in your user profile or workspace:
   
   ```json
   {
     "inputs": [],
     "servers": {
       "hexmaster-design-guidelines": {
         "type": "stdio",
         "command": "hexmaster-codingguidelines-mcpserver",
         "args": []
       }
     }
   }
   ```

   **Location options:**
   - **User-level** (all workspaces): `%USERPROFILE%\.vscode\mcp.json` (Windows) or `~/.vscode/mcp.json` (Mac/Linux)
   - **Workspace-level** (specific project): `.vscode/mcp.json` in your project root

3. **Restart VS Code** to apply changes

4. **Verify the connection**:
   - Open the Output panel: View → Output
   - Select "MCP" from the dropdown
   - You should see server startup logs
   - Open GitHub Copilot Chat
   - Ask Copilot: "What ADRs are available in the design guidelines?"

**Visual Studio Setup**

1. **Install the package**:
   ```powershell
   dotnet tool install --global HexMaster.CodingGuidelines.McpServer
   ```

2. **Configure Copilot MCP settings**:
   - Go to `Tools` → `Options`
   - Navigate to `GitHub` → `Copilot` → `MCP Servers`
   - Click "Add Server"
   - Configure:
     - **Name:** `hexmaster-design-guidelines`
     - **Command:** `hexmaster-codingguidelines-mcpserver`

3. **Restart Visual Studio** to apply changes

4. **Verify the connection**:
   - Open GitHub Copilot Chat window
   - The MCP server should be listed as an active tool
   - Ask Copilot: "Show me the ADR for .NET version adoption"

**How it works**: When installed as a global tool, the server automatically fetches documents from the GitHub repository (`https://github.com/nikneem/hexmaster-design-guidelines`). No local clone is required, and you'll always get the latest published content from the `main` branch.

**Uninstall**:
```bash
dotnet tool uninstall --global HexMaster.CodingGuidelines.McpServer
```

---

#### Scenario 2: Local Development (Run from Source)

For contributors testing local changes before publishing to NuGet. This allows you to work with unpublished ADRs, recommendations, or structural changes.

**VS Code Setup**

1. **Clone the repository**:
   ```bash
   git clone https://github.com/nikneem/hexmaster-design-guidelines.git
   cd hexmaster-design-guidelines
   ```

2. **Create or edit `.vscode/mcp.json`** in the repository root with your actual path:
   ```json
   {
     "inputs": [],
     "servers": {
       "hexmaster-design-guidelines-local": {
         "type": "stdio",
         "command": "dotnet",
         "args": [
           "run",
           "--project",
           "D:/projects/github.com/nikneem/hexmaster-design-guidelines/src/HexMaster.CodingGuidelines.McpServer/HexMaster.CodingGuidelines.McpServer.csproj"
         ]
       }
     }
   }
   ```

4. **Restart VS Code** - The MCP server will run directly from your local source code

**How it works**: When running from source with `dotnet run`, the server automatically discovers and reads documents from your local `docs/` folder. This allows you to test changes immediately without publishing.

**Testing Local NuGet Packages (Advanced)**

If you want to test the packaged tool locally before publishing to NuGet.org:

```powershell
# Pack the project
dotnet pack src/HexMaster.CodingGuidelines.McpServer/HexMaster.CodingGuidelines.McpServer.csproj -o ./local-packages

# Install from local package
dotnet tool install --global --add-source ./local-packages HexMaster.CodingGuidelines.McpServer
```

---

#### Troubleshooting

**Server doesn't appear in Copilot**
- Check Output panel (View → Output) and select "MCP" from dropdown
- Verify the command path is correct (use full path if needed)
- Ensure .NET 9 SDK is installed: `dotnet --version`
- Try restarting VS Code

**Documents not loading**
- For NuGet installation: Check internet connectivity (docs fetched from GitHub)
- For local development: Verify `HEXMASTER_REPO_ROOT` points to repository root
- Check server logs in MCP Output panel

**Global tool not found**
- Verify installation: `dotnet tool list --global`
- Check PATH includes .NET tools directory
  - Windows: `%USERPROFILE%\.dotnet\tools`
  - Mac/Linux: `~/.dotnet/tools`

---

### Registering new documents
This repository intentionally keeps an explicit registry. When you add a file anywhere under `docs/`, register it in:

`src/Hexmaster.DesignGuidelines.Core/Services/DocumentRegistry.cs`

Add a new entry with an `Id`, `Title`, `RelativePath` (from repo root) and the correct category type. This ensures the MCP Server “knows” about the document and it will appear in `/docs` and be retrievable via `/docs/{id}`.

## Repo structure

```
docs/
	adrs/
	designs/
	recommendations/
	structures/
src/
	Hexmaster.DesignGuidelines.Core/
	Hexmaster.DesignGuidelines.Server/
	hexmaster-design-guidelines.sln
.github/
	copilot-instructions.md
```

## ADRs
 - 0001: Adopt .NET 9 as Target Framework (Accepted)
 - 0002: Modular Monolith Project Structure (Proposed)
 - 0003: .NET Aspire Recommendation for ASP.NET Services (Proposed)
 - 0004: CQRS Recommendation for ASP.NET API (Proposed)
 - 0005: Minimal APIs Over Controller-Based APIs (Proposed)

## Recommendations
- Unit Testing with xUnit, Moq, Bogus (`docs/recommendations/unit-testing-xunit-moq-bogus.md`)

## Structures
- Minimal API Endpoint Organization (`docs/structures/minimal-api-endpoint-organization.md`)

---

## Development

### Building and Testing

```bash
# Build the solution
dotnet build src/

# Run all tests
dotnet test src/

# Run tests with coverage
dotnet test src/ --collect:"XPlat Code Coverage" --results-directory ./coverage --settings coverlet.runsettings

# Generate coverage report
reportgenerator -reports:"coverage/**/coverage.cobertura.xml" -targetdir:"coverage/report" -reporttypes:"Html"
```

### Code Coverage Requirements
- **Core Library** (`HexMaster.CodingGuidelines.Docs`): ≥80% line coverage
- **Tests**: All tests must pass
- Coverage reports are automatically generated in CI/CD

---

## CI/CD Workflows

### Build and Publish Workflow
**Workflow**: `.github/workflows/publish-nuget.yml`

Triggers on push to `main` branch when files in `src/` change.

Steps:
1. **Versioning** – GitVersion generates semantic version
2. **Build** – Compiles solution in Release configuration with version info
3. **Test** – Runs all unit tests with 80% coverage enforcement
4. **Coverage Report** – Generates coverage summary
5. **Package** – Creates NuGet package for the MCP Server
6. **Publish** – Pushes package to NuGet.org
7. **Release** – Creates GitHub release with version tag and artifacts

Semantic Versioning Strategy (GitHubFlow):
- **Main branch**: 1.0.0, 1.0.1, 1.0.2... (patch increments)
- **Feature branches** (`feature/*`): 1.1.0-alpha.1, 1.1.0-alpha.2... (minor with alpha pre-release)
- **Release branches** (`release/*`): 1.0.0-beta.1, 1.0.0-beta.2... (beta pre-release)

Configuration: `GitVersion.yml` at repository root.

### NuGet Package
Published to NuGet.org:
- **HexMaster.CodingGuidelines.McpServer** – MCP Server .NET global tool

Package features:
- .NET 9 global tool
- Automatic document discovery from filesystem or GitHub
- ModelContextProtocol SDK integration
- MIT license

### Setup Requirements
To enable automated publishing, add the following GitHub secret:
- `NUGET_API_KEY` – NuGet.org API key with push permissions

Navigate to: Repository Settings → Secrets and variables → Actions → New repository secret

### Local Version Testing
To check what version GitVersion would generate locally:

```bash
# Install GitVersion tool
dotnet tool install --global GitVersion.Tool

# Run in repository root
dotnet-gitversion
```

## Notes
- All code and examples target `.NET 9`.
- The MCP Server uses the `FileSystemDocumentCatalog` for local development and `GitHubDocumentCatalog` for published scenarios.
- Coverage threshold is enforced at 80% for core library code.
- CI/CD pipeline only triggers on changes to `src/` folder when pushed to `main` branch.