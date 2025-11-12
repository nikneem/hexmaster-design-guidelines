# ADR 0001: Adopt .NET 10 as Target Framework
Date: 2025-11-12
Status: Accepted

## Context
The project requires a modern, stable and forward-looking runtime for implementing the MCP server and offering guidance for consumer projects. .NET 10, released on November 11, 2025, is the latest LTS (Long-Term Support) release providing performance improvements, language enhancements (C# 14 features), and updated base libraries with 3 years of support.

## Decision
We adopt .NET 10 as the baseline target framework for all code in this repository (server, examples, templates). New projects must specify `<TargetFramework>net10.0</TargetFramework>`. As an LTS release, this provides stability and extended support. When .NET 12 LTS becomes available, reassess via a superseding ADR.

## Consequences
Positive:
- Access to latest performance, GC and library improvements.
- LTS release with 3 years of support and security updates.
- Enables use of modern C# 14 language features improving clarity and safety.
- Future-proof foundation for upcoming ecosystem tooling.
- Production-ready stability for enterprise deployments.

Negative:
- Consumers limited to environments supporting .NET 10.
- Migration required from .NET 9 for existing codebases.

## References
- .NET 10 release notes (Microsoft docs)
- .NET 10 LTS support policy
- C# 14 language features documentation
