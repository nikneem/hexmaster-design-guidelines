# GitHub Actions Workflow Setup Guide

This document provides step-by-step instructions for setting up the CI/CD pipeline for the Hexmaster Design Guidelines MCP Server.

## Overview

The workflow (`.github/workflows/ci-cd.yml`) automates:
- Building and testing the solution
- Enforcing 80% code coverage threshold
- Semantic versioning with GitVersion
- Publishing NuGet packages
- Creating GitHub releases

## Prerequisites

1. **GitHub Repository:** nikneem/hexmaster-design-guidelines
2. **NuGet.org Account:** For publishing packages
3. **GitHub Repository Permissions:** Admin access to add secrets

## Setup Steps

### 1. Create NuGet API Key

1. Log in to [NuGet.org](https://www.nuget.org/)
2. Navigate to: Account → API Keys
3. Click "Create"
4. Configure the key:
   - **Key Name:** GitHub Actions Hexmaster Design Guidelines
   - **Select Scopes:** Push
   - **Glob Pattern:** Hexmaster.DesignGuidelines.*
   - **Expiration:** 365 days (or custom)
5. Click "Create"
6. **Copy the API key immediately** (it won't be shown again)

### 2. Add GitHub Secret

1. Navigate to repository: https://github.com/nikneem/hexmaster-design-guidelines
2. Go to: Settings → Secrets and variables → Actions
3. Click "New repository secret"
4. Add secret:
   - **Name:** `NUGET_API_KEY`
   - **Secret:** Paste the NuGet API key from step 1
5. Click "Add secret"

### 3. Verify Workflow Files

Ensure these files exist in the repository:

- `.github/workflows/ci-cd.yml` – The workflow definition
- `GitVersion.yml` – Versioning configuration

### 4. Push to Main Branch

The workflow triggers on push to `main`:

```bash
git add .
git commit -m "feat: Add GitHub Actions CI/CD workflow"
git push origin main
```

### 5. Monitor First Run

1. Navigate to: Actions tab in GitHub
2. Select the "CI/CD Pipeline" workflow
3. Watch the build progress
4. Verify all steps complete successfully:
   - ✅ Checkout code
   - ✅ Setup .NET
   - ✅ Determine Version
   - ✅ Build
   - ✅ Run tests with coverage
   - ✅ Verify coverage threshold (≥80%)
   - ✅ Pack Core library
   - ✅ Pack Server
   - ✅ Push to NuGet
   - ✅ Create GitHub Release

## Versioning Strategy

GitVersion automatically calculates versions based on branch names and commit history:

| Branch Pattern | Tag | Increment | Example |
|---------------|-----|-----------|---------|
| `main` | None | Patch | 1.0.1 |
| `feature/*` | alpha | Minor | 1.1.0-alpha.1 |
| `release/*` | beta | Patch | 1.0.1-beta.1 |
| `hotfix/*` | None | Patch | 1.0.2 |

### Manual Version Bumps

To bump the major or minor version, add a tag:

```bash
# Bump to 2.0.0
git tag 2.0.0
git push --tags

# Bump to 1.5.0
git tag 1.5.0
git push --tags
```

## Coverage Threshold

The workflow enforces **80% line coverage**. Builds fail if coverage drops below this threshold.

To adjust the threshold, edit `.github/workflows/ci-cd.yml`:

```yaml
env:
  COVERAGE_THRESHOLD: 80  # Change this value
```

## Pull Request Workflow

For pull requests, the workflow:
1. Runs build and tests
2. Verifies coverage (no packaging/publishing)
3. Adds coverage summary as a comment
4. Uploads coverage report as an artifact

## Troubleshooting

### Build Fails: "Missing XML comment"

All public types and members require XML documentation comments since `GenerateDocumentationFile` is enabled.

**Solution:** Add XML doc comments:
```csharp
/// <summary>
/// Description of the type or member.
/// </summary>
public class MyClass { }
```

### Coverage Below Threshold

**Error:** `Coverage 75% is below threshold of 80%`

**Solution:** Add more unit tests to increase coverage, or adjust threshold if justified.

### NuGet Push Fails: "API key invalid"

**Solution:** 
1. Verify `NUGET_API_KEY` secret exists
2. Check API key hasn't expired on NuGet.org
3. Regenerate key and update secret if needed

### Duplicate Package Version

**Error:** `Package version already exists`

**Solution:** The workflow uses `--skip-duplicate` flag, so this should be non-fatal. Ensure version is being incremented properly by GitVersion.

## Local Testing

### Test GitVersion Locally

```bash
# Install tool
dotnet tool install --global GitVersion.Tool

# Run from repository root
cd D:\projects\github.com\nikneem\hexmaster-design-guidelines
dotnet-gitversion
```

### Test Coverage Locally

```bash
cd src

# Run tests with coverage
dotnet test Hexmaster.DesignGuidelines.Tests/Hexmaster.DesignGuidelines.Tests.csproj `
  --collect:"XPlat Code Coverage" `
  --results-directory ./coverage

# Generate report
reportgenerator `
  -reports:"./coverage/**/coverage.cobertura.xml" `
  -targetdir:"./coverage/report" `
  -reporttypes:"Html"

# Open report
start ./coverage/report/index.html
```

### Test Package Creation Locally

```bash
cd src

# Pack projects
dotnet pack Hexmaster.DesignGuidelines.Core/Hexmaster.DesignGuidelines.Core.csproj `
  --configuration Release `
  --output ./packages

dotnet pack Hexmaster.DesignGuidelines.Server/Hexmaster.DesignGuidelines.Server.csproj `
  --configuration Release `
  --output ./packages

# Inspect packages
dir ./packages/*.nupkg
```

## Maintenance

### Update .NET Version

When upgrading to a new .NET version:

1. Update `TargetFramework` in all `.csproj` files
2. Update `DOTNET_VERSION` in `.github/workflows/ci-cd.yml`
3. Create ADR documenting the upgrade decision
4. Update README.md notes section

### Update Dependencies

Review and update NuGet packages quarterly:

```bash
cd src
dotnet list package --outdated
dotnet add package <PackageName>
```

## Security Considerations

- **Never commit NuGet API keys** to source control
- Rotate API keys annually
- Use GitHub's secret scanning protection
- Limit API key scope to specific packages
- Review workflow permissions regularly

## Additional Resources

- [GitVersion Documentation](https://gitversion.net/)
- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [NuGet Package Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/publish-a-package)
- [Coverage Verification](https://github.com/danielpalme/ReportGenerator)
- [ADR 0006](../docs/adrs/0006-github-actions-cicd-semantic-versioning.md) – Full context and decision rationale
