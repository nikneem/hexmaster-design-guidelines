# ADR 0001: Adopt .NET 9 as Target Framework
Date: 2025-11-10
Status: Accepted

## Context
The project requires a modern, stable and forward-looking runtime for implementing the MCP server and offering guidance for consumer projects. .NET 9 provides performance improvements, language enhancements (C# 13 preview features) and updated base libraries while remaining aligned with future LTS planning.

## Decision
We adopt .NET 9 as the baseline target framework for all code in this repository (server, examples, templates). New projects must specify `<TargetFramework>net9.0</TargetFramework>`. When a later LTS becomes available, reassess via a superseding ADR.

## Consequences
Positive:
- Access to latest performance, GC and library improvements.
- Enables use of modern C# language features improving clarity and safety.
- Future-proof foundation for upcoming ecosystem tooling.

Negative:
- Consumers limited to environments supporting .NET 9.
- Potential minor churn when upgrading to next LTS.

## References
- .NET 9 release notes (Microsoft docs)
- Internal performance benchmarks (to be added)
