# ADR 0003: Strong Recommendation to Adopt .NET Aspire for ASP.NET Web Services
Date: 2025-11-10
Status: Proposed

## Context
Modern ASP.NET (APIs, Blazor, minimal APIs, background workers) routinely depend on multiple distributed resources: databases, caches, message brokers, AI services, and external HTTP endpoints. Teams repeatedly implement cross-cutting concerns (service discovery, telemetry, structured logging, health checks, resilience policies, configuration wiring) manually, leading to inconsistency, missed observability, and slower onboarding.

Microsoft's .NET Aspire stack (introduced in 2024 and evolving through 2025) provides opinionated defaults, orchestration (AppHost), integrated service discovery via environment variables, pluggable resilience (retries, timeouts), automatic OpenTelemetry instrumentation (logs, metrics, traces), and a unified dashboard for the developer inner loop. Aspire lowers cognitive load and accelerates distributed ASP.NET development without forcing architectural lock-in. Adoption is incremental (use some or all features) and does not require wholesale CI/CD replacement.

## Decision
We strongly RECOMMEND (not mandate) adopting .NET Aspire for all ASP.NET-hosted, web-enabled services (HTTP APIs, Web Apps, Blazor frontends, gRPC services) in solutions governed by these guidelines. For non-web modules (pure domain libraries, isolated batch utilities, internal algorithmic components), Aspire integration remains optional.

When a project qualifies (web-facing or service boundary), create and maintain an Aspire AppHost project to orchestrate dependent services and apply Aspire Service Defaults. Integrate telemetry, health checks, resilience, and service discovery via Aspire packages before implementing custom equivalents.

### Applicability Levels
- Strong Recommendation: ASP.NET API / Blazor / gRPC / web-facing service projects.
- Optional: Console tools, domain-only libraries, test harnesses, small single-process prototypes.

### Minimum Adoption Set
1. AppHost project with orchestration & service defaults.
2. Telemetry (logs, metrics, traces) via Aspire + OpenTelemetry exporters.
3. Health checks surface (per service + dependencies) accessible locally.
4. Resilience defaults (retry, timeout, circuit breaker) applied to outbound calls.
5. Service discovery variables used instead of hardcoded endpoints.

### Incremental Path
Start with AppHost + service defaults; add integrations (Redis, PostgreSQL, AI services) as needed; extend to production telemetry/export only when stable; evaluate deployment to Azure Container Apps or similar later.

## Consequences
Positive:
- Faster onboarding; consistent baseline configuration and observability.
- Reduced bespoke setup for telemetry, health checks, service discovery.
- More resilient defaults applied uniformly across services.
- Simplifies local multi-service orchestration (containers + projects) improving inner loop productivity.
- Facilitates future scaling or partial extraction without losing cross-cutting features.

Negative / Trade-offs:
- Additional project (AppHost) increases initial solution complexity.
- Opinionated defaults may mask underlying configuration details; requires education to fine-tune.
- Some features may exceed needs of extremely small prototypes.

Risk Mitigation:
- Adoption is incremental; unused features can remain dormant.
- Defaults are overridable; document any deviations via ADRs referencing 0003.

## Alternatives Considered
- Manual custom wiring (rejected: increases duplication and inconsistency).
- Full microservice orchestrators (e.g., Kubernetes locally) (rejected for early stages: complexity overhead).
- Minimal single-project setups (rejected for web services with multiple integrations: insufficient observability/resilience).

## Implementation Notes
- Create `YourSolution.AppHost` using Aspire template.
- Reference web projects from AppHost to apply service defaults.
- Use generated environment variables for service endpoints.
- Enable OpenTelemetry exporters (OTLP / console) early for diagnostics.
- Add health checks endpoints to each web service; surface aggregated view in dashboard.
- Tag Aspire integration commit messages with `feat: adopt aspire (ADR-0003)`.

## Evaluation Criteria
Review adoption annually or when significant Aspire version changes occur. Measure:
- Mean time to debug production incidents (expect decrease).
- Consistency of resilience policies across outbound dependencies.
- Observability completeness (trace spans covering end-to-end requests).

## References
- External Article: "Introduction to .NET Aspire" (Visual Studio Magazine, Feb 19 2025)
- Official Docs: https://docs.microsoft.com/dotnet/aspire
- eShop reference app (Aspire sample)
- ADR 0001 (.NET 9 baseline)

## Status Rationale
Marked Proposed pending validation in at least one multi-project ASP.NET solution. Upgrade to Accepted after initial integration demonstrates improved developer inner-loop productivity.
