# HexMaster Design Guidelines

Design, architecture, style and structure guidelines for modern .NET (C#) projects, organized as ADRs, designs, recommendations and structures under `docs/`. An MCP Server in `src/` exposes these documents for tools/agents.

## MCP Server (C#, .NET 9)

An MCP (Model Context Protocol) server implementing the official Microsoft MCP SDK. Exposes design guideline documents as tools that AI assistants can call.

### Requirements
- .NET 9 SDK

### MCP Protocol

The server implements the Model Context Protocol using the official `ModelContextProtocol` NuGet package. It exposes two tools:

1. **ListDocuments** - Lists all available design guideline documents (ADRs, recommendations, structures)
2. **GetDocument** - Retrieves the content of a specific document by its ID

Documents are served from the local repository first, with automatic fallback to GitHub raw content if not found locally.

### Run Standalone (for testing)

From the repository root:

```powershell
dotnet run --project .\src\Hexmaster.DesignGuidelines.Server\Hexmaster.DesignGuidelines.Server.csproj
```

The server uses stdio transport for MCP communication. Logs are written to stderr, JSON-RPC messages to stdout.

You can override the repository root with `HEXMASTER_REPO_ROOT` environment variable if needed.

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
   dotnet tool install --global HexMaster.DesignGuidelines.Server
   ```

2. **Configure VS Code MCP settings**:
   
   Create or edit `.vscode/mcp.json` in your user profile or workspace:
   
   ```json
   {
     "inputs": [],
     "servers": {
       "hexmaster-design-guidelines": {
         "type": "stdio",
         "command": "Hexmaster.DesignGuidelines.Server",
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
   dotnet tool install --global HexMaster.DesignGuidelines.Server
   ```

2. **Configure Copilot MCP settings**:
   - Go to `Tools` → `Options`
   - Navigate to `GitHub` → `Copilot` → `MCP Servers`
   - Click "Add Server"
   - Configure:
     - **Name:** `hexmaster-design-guidelines`
     - **Command:** `HexMaster.DesignGuidelines.Server`

3. **Restart Visual Studio** to apply changes

4. **Verify the connection**:
   - Open GitHub Copilot Chat window
   - The MCP server should be listed as an active tool
   - Ask Copilot: "Show me the ADR for .NET version adoption"

**How it works**: When installed as a global tool, the server automatically fetches documents from the GitHub repository (`https://github.com/nikneem/hexmaster-design-guidelines`). No local clone is required, and you'll always get the latest published content from the `main` branch.

**Uninstall**:
```bash
dotnet tool uninstall --global HexMaster.DesignGuidelines.Server
```

---

#### Scenario 2: Local Development (Run from Source)

For contributors testing local changes before publishing:
---


For contributors testing local changes before publishing to NuGet. This allows you to work with unpublished ADRs, recommendations, or structural changes.

**VS Code Setup**

1. **Clone the repository**:
   ```bash
   git clone https://github.com/nikneem/hexmaster-design-guidelines.git
   cd hexmaster-design-guidelines
   ```

2. **Create `.vscode/mcp.json`** in the repository root:
   ```bash
   cp .vscode/mcp.json.template .vscode/mcp.json
   ```

3. **Edit `.vscode/mcp.json`** and replace `<ABSOLUTE_PATH_TO_REPO>` with your actual path:
   ```json
   {
     "inputs": [],
     "servers": {
       "hexmaster-design-guidelines": {
         "type": "stdio",
         "command": "dotnet",
         "args": [
           "run",
           "--project",
           "D:/projects/github.com/nikneem/hexmaster-design-guidelines/src/Hexmaster.DesignGuidelines.Server/Hexmaster.DesignGuidelines.Server.csproj"
         ],
         "env": {
           "HEXMASTER_REPO_ROOT": "D:/projects/github.com/nikneem/hexmaster-design-guidelines"
         }
       }
     }
   }
   ```

4. **Restart VS Code** - The MCP server will run directly from your local source code

**How it works**: When running from source with `dotnet run`, the `HEXMASTER_REPO_ROOT` environment variable tells the server to read documents from your local `docs/` folder instead of fetching from GitHub. This allows you to test changes immediately without publishing.

**Testing Local NuGet Packages (Advanced)**

If you want to test the packaged tool locally before publishing to NuGet.org:

```powershell
# Pack the project
dotnet pack src/Hexmaster.DesignGuidelines.Server/Hexmaster.DesignGuidelines.Server.csproj -o ./local-packages

# Install from local package
dotnet tool install --global --add-source ./local-packages Hexmaster.DesignGuidelines.Server
```

Configure VS Code to use the installed tool (NO HEXMASTER_REPO_ROOT - fetches from GitHub). Edit `.vscode/mcp.json` or `%USERPROFILE%\.vscode\mcp.json`:

```json
{
  "inputs": [],
  "servers": {
    "hexmaster-design-guidelines": {
      "type": "stdio",
      "command": "HexMaster.DesignGuidelines.Server",
      "args": []
    }
  }
}
```

ONLY if you want to test with LOCAL documents (not typical):

```json
{
  "inputs": [],
  "servers": {
    "hexmaster-design-guidelines": {
      "type": "stdio",
      "command": "HexMaster.DesignGuidelines.Server",
      "args": [],
      "env": {
        "HEXMASTER_REPO_ROOT": "D:/projects/github.com/nikneem/hexmaster-design-guidelines"
      }
    }
  }
}
```

**When to use `HEXMASTER_REPO_ROOT`**:
- ✅ Running from source with `dotnet run` (always required)
- ✅ Testing unpublished document changes with installed tool
- ❌ Standard NuGet installation (documents come from GitHub automatically)

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

## CI/CD Workflows

### Pull Request Validation
**Workflow**: `.github/workflows/pr-validation.yml`

Triggers on pull requests to `main` or `develop` branches when files in `src/` or `tests/` change.

Steps:
1. **Build** – Compiles the solution in Release configuration
2. **Test** – Runs all unit tests
3. **Coverage** – Collects code coverage using coverlet
4. **Report** – Generates HTML and markdown coverage reports
5. **Threshold Check** – Enforces 80% coverage on Core library
6. **PR Comment** – Posts coverage summary to the pull request
7. **Artifact** – Uploads full coverage report (retained for 30 days)

Coverage Requirements:
- **Core Library**: Must maintain ≥80% line coverage
- **Server**: Console app with limited unit test coverage (informational only)

### CI/CD Pipeline
**Workflow**: `.github/workflows/ci-cd.yml`

Triggers on push to `main` branch.

Steps:
1. **Versioning** – GitVersion generates semantic version
2. **Build** – Compiles solution in Release configuration
3. **Test** – Runs all unit tests with 80% coverage enforcement
4. **Package** – Creates NuGet packages for Core and Server
5. **Publish** – Pushes packages to NuGet.org
6. **Release** – Creates GitHub release with version tag

Semantic Versioning Strategy:
- **Main branch**: 1.0.0, 1.0.1, 1.0.2... (patch increments)
- **Feature branches** (`feature/*`): 1.1.0-alpha.1, 1.1.0-alpha.2... (minor with pre-release)
- **Release branches** (`release/*`): 1.0.0-beta.1, 1.0.0-beta.2... (patch with pre-release)
- **Hotfix branches** (`hotfix/*`): Patch increment

Configuration: `GitVersion.yml` at repository root.

### NuGet Packages
Both projects are published to NuGet.org:
- **Hexmaster.DesignGuidelines.Core** – Core domain models and document services
- **HexMaster.DesignGuidelines.Server** – MCP Server .NET tool implementing JSON-RPC protocol

Package features:
- XML documentation included
- Symbol packages (`.snupkg`) for debugging
- MIT license
- Embedded README.md

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
- The MCP Server prefers local files but can fall back to GitHub raw for missing content.
- Coverage reports are generated for every build and attached as artifacts
- Pull requests include coverage summaries in comments automatically