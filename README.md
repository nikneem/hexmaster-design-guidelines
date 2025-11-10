# hexmaster-design-guidelines

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

## Notes
- All code and examples target `.NET 9`.
- The MCP Server prefers local files but can fall back to GitHub raw for missing content.