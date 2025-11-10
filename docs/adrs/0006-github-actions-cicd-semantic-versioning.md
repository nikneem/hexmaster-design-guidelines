# ADR 0006: GitHub Actions CI/CD with Semantic Versioning and NuGet Publishing

**Date:** 2025-11-10  
**Status:** Accepted  

## Context

The MCP Server projects (`Hexmaster.DesignGuidelines.Core` and `Hexmaster.DesignGuidelines.Server`) need automated build, test, version management, and deployment to NuGet. The repository follows a test-first approach with an 80% code coverage requirement (per copilot-instructions.md).

Key requirements:
1. Automated versioning following semantic versioning principles
2. Build and test on every push to main
3. Coverage verification enforcing 80% threshold
4. Package creation for distribution
5. Automated publishing to NuGet
6. GitHub release creation with version tags

## Decision

We will use **GitHub Actions** for CI/CD with the following pipeline:

### Workflow Structure
- **Trigger:** Push to `main` branch and pull requests targeting `main`
- **Runner:** Ubuntu latest (linux-based for broader compatibility)
- **Target Framework:** .NET 9.0

### Versioning Strategy
- **Tool:** GitVersion 6.x
- **Mode:** ContinuousDelivery
- **Branch Strategy:**
  - `main`: Auto-increment patch version, no pre-release tag
  - `feature/*`: Pre-release with `alpha` tag, minor version increment
  - `release/*`: Pre-release with `beta` tag, patch version increment
  - `hotfix/*`: Patch version increment
- **Configuration:** GitVersion.yml at repository root

### Pipeline Stages

1. **Build**
   - Restore dependencies
   - Build in Release configuration
   - Inject version from GitVersion into assembly metadata

2. **Test & Coverage**
   - Run xUnit tests with XPlat Code Coverage
   - Generate HTML, JSON, and Markdown coverage reports using ReportGenerator
   - **Enforce 80% line coverage threshold** - fail build if not met
   - Upload coverage artifacts
   - Add coverage summary to PR comments

3. **Package** (main branch only)
   - Pack both Core and Server projects as NuGet packages
   - Include symbols (snupkg format)
   - Embed README.md in packages
   - Apply semantic version from GitVersion

4. **Publish** (main branch only)
   - Push packages to NuGet.org
   - Skip duplicates (idempotent)
   - Requires `NUGET_API_KEY` secret

5. **Release** (main branch only)
   - Create GitHub release with version tag
   - Document package versions in release notes
   - Link to coverage report

### Package Metadata
Both projects include:
- MIT License
- Repository URL and project URL
- Semantic tags (design-guidelines, architecture, adr, etc.)
- XML documentation files
- Symbol packages for debugging
- Embedded README.md

The **Server project** is additionally configured as a **.NET tool**:
- `PackAsTool=true` enables installation via `dotnet tool install`
- `ToolCommandName=hexmaster-design-guidelines-server` defines the command name
- Can be integrated with GitHub Copilot as an MCP server in VS Code and Visual Studio
- Provides design guidelines context to AI agents during code generation

## Consequences

### Positive
- **Automated quality gates:** Coverage threshold prevents regressions
- **Consistent versioning:** GitVersion eliminates manual version management
- **Transparency:** Coverage reports attached to PRs and releases
- **Distribution:** Automatic NuGet publishing enables consumption
- **Traceability:** GitHub releases link commits to published packages
- **Developer experience:** PR checks provide fast feedback
- **Idempotent:** Skip-duplicate prevents republish failures

### Negative
- **Secret management required:** NuGet API key must be stored as GitHub secret
- **Initial setup cost:** GitVersion configuration and workflow creation
- **Linux-only builds:** May miss Windows-specific issues (acceptable for .NET 9 cross-platform projects)
- **Coverage enforcement strictness:** 80% threshold could block legitimate refactoring (can be adjusted via environment variable if needed)

### Neutral
- **GitVersion learning curve:** Team must understand branch naming conventions
- **Build time:** Coverage analysis adds ~10-20 seconds per build
- **NuGet feed dependency:** Deployment requires NuGet.org availability

## Implementation Notes

### Required Secrets
Add to GitHub repository settings → Secrets and variables → Actions:
- `NUGET_API_KEY`: NuGet.org API key with push permissions

### Branch Naming Conventions
To leverage GitVersion effectively:
- Features: `feature/my-feature` or `features/my-feature`
- Releases: `release/v1.2.0` or `releases/v1.2.0`
- Hotfixes: `hotfix/critical-bug` or `hotfixes/critical-bug`

### Coverage Threshold Adjustment
To modify the 80% threshold, update the `COVERAGE_THRESHOLD` environment variable in `.github/workflows/ci-cd.yml`.

### Local Version Testing
Run GitVersion locally:
```bash
dotnet tool install --global GitVersion.Tool
dotnet-gitversion
```

## References

- GitVersion Documentation: https://gitversion.net/
- GitHub Actions Documentation: https://docs.github.com/en/actions
- NuGet Publishing Guide: https://learn.microsoft.com/en-us/nuget/nuget-org/publish-a-package
- ReportGenerator: https://github.com/danielpalme/ReportGenerator
- Copilot Instructions (80% coverage requirement): `.github/copilot-instructions.md`
