# HexMaster Design Guidelines

Design, architecture, style and structure guidelines for modern .NET (C#) projects, organized as ADRs, designs, recommendations and structures under `docs/`. An MCP Server in `src/` exposes these documents for tools/agents.

## MCP Server (C#, .NET 9)

A minimal HTTP server exposes endpoints to list and fetch registered guideline documents.

### Requirements
- .NET 9 SDK

### Run
From the repository root:

```powershell
dotnet run --project .\src\Hexmaster.DesignGuidelines.Server\Hexmaster.DesignGuidelines.Server.csproj
```

Endpoints:
- `GET /health` – health probe
- `GET /docs` – list registered docs (id, title, category, relativePath)
- `GET /docs/{id}` – fetch markdown content (local first, fallback to GitHub raw)

You can override the repository root with `HEXMASTER_REPO_ROOT` environment variable if needed.

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
 - 0006: GitHub Actions CI/CD with Semantic Versioning and NuGet Publishing (Accepted)

## Recommendations
- Unit Testing with xUnit, Moq, Bogus (`docs/recommendations/unit-testing-xunit-moq-bogus.md`)

## Structures
- Minimal API Endpoint Organization (`docs/structures/minimal-api-endpoint-organization.md`)

## CI/CD & Publishing

### Automated Pipeline
The repository uses **GitHub Actions** for continuous integration and deployment. The workflow (`.github/workflows/ci-cd.yml`) runs on every push to `main` and on pull requests.

**Pipeline stages:**
1. **Build** – Compile all projects in Release configuration
2. **Test** – Run xUnit tests with code coverage collection
3. **Coverage Verification** – Enforce 80% line coverage threshold (build fails if not met)
4. **Package** – Create NuGet packages for Core and Server projects (main branch only)
5. **Publish** – Push packages to NuGet.org (main branch only)
6. **Release** – Create GitHub release with version tag (main branch only)

### Semantic Versioning
Versions are managed automatically using **GitVersion**:
- **Main branch:** Patch increment (e.g., 1.0.0 → 1.0.1)
- **Feature branches** (`feature/*`): Pre-release with `alpha` tag, minor increment (e.g., 1.1.0-alpha.1)
- **Release branches** (`release/*`): Pre-release with `beta` tag (e.g., 1.2.0-beta.1)
- **Hotfix branches** (`hotfix/*`): Patch increment

Configuration: `GitVersion.yml` at repository root.

### NuGet Packages
Both projects are published to NuGet.org:
- **Hexmaster.DesignGuidelines.Core** – Core domain models and document services
- **Hexmaster.DesignGuidelines.Server** – MCP Server host with minimal API endpoints

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
- Code coverage reports are generated for every build and attached as artifacts
- Pull requests include coverage summaries in comments