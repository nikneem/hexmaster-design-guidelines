# Copilot Repository Instructions: hexmaster-design-guidelines

> Purpose: This repo defines authoritative design, architecture, style and structural guidance for modern .NET C# projects using Hexagonal and Clean Architecture, supported by Architecture Decision Records (ADRs), design notes, recommendations and structure templates. An MCP Server (under `src/`) will expose these documents so AI agents/tools can consult them when making technical decisions.

---
## 1. Scope & Mission
Use this repo as the single source of truth for:
- Architectural principles (Hexagonal, DDD, Clean Architecture)
- Coding standards for modern C# (.NET 9 or newest stable)
- Project structuring & layering conventions
- Decision history (ADRs) and rationale
- Patterns to embrace and anti-patterns to avoid
- Design recommendations for cross-cutting concerns (logging, config, resilience, security, testing)
- MCP Server interface guidelines for retrieving and applying this knowledge programmatically

The MCP Server should enable agents to query: "What is the recommended way to structure a persistence adapter?" or "Fetch ADR for logging strategy".

---
## 2. Repository Structure (Authoritative)
```
/README.md                     – High-level name; expand later.
/docs/
  adrs/                        – Each ADR: `NNNN-title.md` (sequential 0001, 0002 ...)
  designs/                     – Deeper design explorations & diagrams
  recommendations/             – Prescriptive guidance & best practices
  structures/                  – Example folder/file scaffolds & templates
/src/                          – MCP Server solution & supporting code
  hexmaster-design-guidelines.sln
/.github/copilot-instructions.md – This file (guides Copilot behaviors)
```
Future expansions may add: `scripts/`, `examples/`, `reference-implementations/`.

---
## 3. Document Taxonomy & Conventions
### ADRs (`docs/adrs`)
Format (MADR-ish simplified):
```
# ADR NNNN: Concise Title
Date: YYYY-MM-DD
Status: {Proposed|Accepted|Deprecated|Superseded by NNNN}
Context
Decision
Consequences (Positive/Negative)
References
```
Rules:
- Immutable once accepted except for status; use superseding ADRs for changes.
- Use present tense in decision section.
- Provide justification, not restatement of context.

### Designs (`docs/designs`)
- Exploratory or future-facing. Can evolve freely.
- Use diagrams (Mermaid preferred) and clearly labeled sections: Problem, Forces, Proposed Solution, Variants, Risks.

### Recommendations (`docs/recommendations`)
- Prescriptive, stable guidance (e.g., "Error handling approach").
- Keep narrowly scoped; link back to ADR if origin traced.

### Structures (`docs/structures`)
- Provide canonical directory/file scaffolds (e.g., API service, background worker, library pack).
- Show minimal code shells with comments for where domain logic goes.

---
## 4. Architectural Principles
Adopt Hexagonal Architecture layering:
- Domain: Entities, Value Objects, Domain Services, Aggregates, Domain Events.
- Application (Use Cases): Orchestrates domain operations; defines command/query handlers; pure, framework-agnostic.
- Ports (Interfaces): Define required/produced interactions (`IEmailSender`, `IRepository<T>`).
- Adapters (Infrastructure): Implement ports (DB, message bus, external APIs). Keep side-effects here.
- Interface / Delivery: HTTP controllers, gRPC services, CLI, workers.

Guidelines:
- Dependency direction: Outer -> Inner only. Domain never depends outward.
- Keep business rules framework-agnostic; avoid EF Core attributes inside domain entities (map externally).
- Use constructor injection; avoid service locators & static singletons.
- Prefer vertical slice organization for application use cases over large service classes.

---
## 5. Domain-Driven Design (DDD) Notes
- Use ubiquitous language from ADRs & Recommendations.
- Value Objects: immutable `record struct` or `record` with validation in factory/static Create method.
- Aggregates: enforce invariants; expose only behavior, not setters.
- Domain Events: simple messages; publish via application layer (e.g., MediatR or custom dispatcher) – keep infrastructure concerns decoupled.

---
## 6. C# Coding Standards
Style:
- Target `.NET 9` (update via ADR when changed).
- Enable nullable reference types; treat warnings as errors where practical.
- File-scoped namespaces.
- `var` only when RHS type is obvious.
- Expression-bodied members for trivial properties/methods.
- Use `async`/`await` pervasively; avoid blocking (`.Result`, `.Wait()`).
- Guard clauses at top of public methods using `ArgumentException`, `ArgumentNullException`.
- Use `ILogger<T>` for logging; no static loggers.
- Avoid prematurely exposing internal implementation types; lean on interfaces for ports.
- Favor composition over inheritance; use `sealed` where extension not intended.
- Use analyzers: Roslyn, StyleCop (if adopted via ADR), Security analyzers.

Project Layout Example (API):
```
src/ProjectName/
  ProjectName.Domain/
  ProjectName.Application/
  ProjectName.Infrastructure/
  ProjectName.Adapters.Http/ (controllers, DTO mappers)
  ProjectName.Tests/ (unit + integration)
```

---
## 7. Patterns to Prefer
- CQRS (lightweight) for separating queries from commands.
- Vertical slices (each use case: request model, handler, validator, tests).
- Mediator for decoupling application requests (optional; evaluate complexity).
- Resilience: Polly for retries/circuit breakers in adapter layer only.
- Options pattern for configuration (`IOptions<T>` / `IOptionsSnapshot<T>`).
- Cancellation tokens for all async operations crossing boundaries.

## 8. Anti-Patterns to Avoid
- God services accumulating unrelated methods.
- Fat controllers with domain logic.
- Active Record pattern mixing persistence logic with domain entities.
- Static helpers maintaining hidden state.
- Circular dependencies between application and infrastructure.
- Leaking EF Core tracked entities outside repository boundary.

---
## 9. Error & Exception Handling
- Domain validations throw domain-specific exceptions (derive from a base `DomainException`). Translate to HTTP/problem details in delivery layer only.
- Use `Result<T>` or exceptions consistently (decide via ADR); don’t mix patterns haphazardly.
- Log exceptions once at boundary; avoid duplicate logging.

---
## 10. Testing Strategy
- Domain: pure unit tests (no mocks) validating invariants.
- Application: test handlers with lightweight fakes for ports.
- Infrastructure: integration tests (containerized DB or testcontainers).
- Contract tests for external APIs if adapters parse/transform.
- Use naming: `Method_ShouldExpected_WhenCondition`.
- Keep one assertion per conceptual rule (or use FluentAssertions grouping).
 - Maintain at least 80% code coverage for unit tests across Core and Server projects; enforce via CI using coverlet or equivalent reporter.

---
## 11. Observability & Logging
- Structured logging (Serilog or Microsoft.Extensions.Logging with JSON sinks) – to be confirmed by ADR.
- Correlation IDs propagated across adapters.
- Include latency metrics for outbound calls (Prometheus/OpenTelemetry; instrumentation ADR required).

---
## 12. Security Considerations
- Never store secrets in source; use user-secrets or environment variables.
- Input validation before persistence or external calls.
- Threat modeling notes stored in `docs/designs/` if complex.
- Prefer least-privilege for infrastructure services.

---
## 13. Performance & Resilience
- Async all the way; avoid synchronous wrappers.
- Caching decisions documented via ADR (in-memory vs distributed vs no cache).
- Use bulkheads & circuit breakers only at adapter boundaries.

---
## 14. MCP Server Guidance
The MCP Server acts as a knowledge provider:
- Index categories: `adrs`, `designs`, `recommendations`, `structures`.
- Provide search endpoints: keyword, semantic (later) – list matched docs with metadata (title, status, path, tags).
- Fetch raw content: prefer local filesystem; fallback to GitHub raw (cache results with ETag).
- Normalization: strip front-matter, return structured JSON including sections (Context, Decision, Consequences).
- Provide helper endpoint: `suggestDecision({ query })` -> returns relevant ADRs + recommendations summary.
- Rate limits & caching defined in future ADR.

Agent Prompt Examples:
1. "Given we need a persistence adapter with retry logic, what do guidelines recommend?" -> Fetch recommendations + ADRs tagged `persistence`, `resilience`.
2. "Summarize domain layer rules" -> Return Domain architecture section + anti-pattern list.
3. "List deprecated ADRs" -> Filter status `Deprecated`.

Integration Principles:
- Do not embed business logic; serve guidance only.
- Clearly label confidence when summarizing multiple docs.
- Provide list of source doc IDs for traceability.

---
## 15. Contribution Workflow
- Branch naming: `feature/<short-phrase>`, `fix/<issue-id>`, `docs/<topic>`, `adr/<NNNN-title>`.
- ADR process: Open PR with ADR in `adrs/` as `NNNN-title.md` (reserve number). Review ensures clarity of consequences.
- Commit messages: Conventional Commits (`feat:`, `fix:`, `docs:`, `refactor:` etc.).
- Merge requires: passing build (if MCP code exists) + review from at least 1 maintainer.

---
## 16. Dependency Management
- Pin versions for critical libraries (logging, DI extras, resilience) – update via dedicated `chore:` PR.
- Avoid unnecessary packages - prefer BCL first.

---
## 17. Formatting & Analysis Tooling (To establish via ADR)
Candidates:
- `dotnet format` for style enforcement.
- Roslyn analyzers: `Microsoft.CodeAnalysis.NetAnalyzers`, `StyleCop.Analyzers`.
- Security: `DevSkim`, `GitHub code scanning`.

---
## 18. Prompt Crafting Guidance (For Copilot / Agents)
When asking for guidance:
- Specify layer (Domain/Application/Adapter).
- Provide intent & constraints (e.g., performance-critical, low-latency, high-write volume).
- Ask for rationale referencing ADR numbers (e.g., "Explain per ADR 0003").
If implementing new code:
- Request scaffolds from `structures` folder; adapt rather than invent.
- Cite decisions with inline comments `// ADR-0005: Using circuit breaker at adapter layer`.

---
## 19. Example Snippets
Value Object pattern:
```csharp
public sealed record struct Money(decimal Amount, string Currency)
{
    public static Money Create(decimal amount, string currency)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (string.IsNullOrWhiteSpace(currency)) throw new ArgumentException("Currency required", nameof(currency));
        return new Money(decimal.Round(amount, 2, MidpointRounding.AwayFromZero), currency.ToUpperInvariant());
    }
}
```
Vertical slice (command handler skeleton):
```csharp
public sealed record CreateOrderCommand(Guid CustomerId, IReadOnlyList<OrderLineDto> Lines) : IRequest<CreateOrderResult>;

public sealed class CreateOrderHandler : IRequestHandler<CreateOrderCommand, CreateOrderResult>
{
    private readonly IOrderRepository _orders;
    private readonly IClock _clock; // Port for time abstraction
    public CreateOrderHandler(IOrderRepository orders, IClock clock) => (_orders, _clock) = (orders, clock);

    public async Task<CreateOrderResult> Handle(CreateOrderCommand command, CancellationToken ct)
    {
        // Guard clauses
        if (command.Lines.Count == 0) throw new DomainException("Order must have at least one line");

        var order = Order.Create(command.CustomerId, command.Lines.Select(l => new OrderLine(l.Sku, l.Quantity)));
        order.StampCreated(_clock.UtcNow);
        await _orders.SaveAsync(order, ct);
        return new CreateOrderResult(order.Id, order.Total);
    }
}
```

---
## 20. Do / Don't Quick Reference
| Do | Don't |
|----|-------|
| Keep domain pure | Add EF Core attributes to domain entities |
| Use ports/interfaces for boundaries | Call external APIs directly from domain |
| Document decisions via ADRs | Edit accepted ADR content silently |
| Leverage vertical slices | Build giant service classes |
| Centralize resilience in adapters | Scatter retry logic across layers |
| Use immutable value objects | Use primitive obsession without encapsulation |
| Log structured context | Swallow exceptions silently |

---
## 21. Future Enhancements (Track via ADRs)
- Semantic search for documents
- OpenTelemetry full instrumentation pattern
- Code generation templates for slices
- Decision tagging taxonomy

---
## 22. How Copilot Should Behave Here
When generating code or guidance:
1. Prefer examples aligned with structures & principles above.
2. Reference ADR numbers when the user asks for rationale (invent number only if placeholder, mark clearly e.g., `ADR-TBD`).
3. Avoid suggesting frameworks not justified (e.g., heavy ORMs if not in ADR).
4. Encourage test-first for domain logic; provide sample test outlines.
5. Suggest adding/editing docs when you detect an unstated decision.
6. Never expose secrets; remind user to externalize configuration.

If a user request conflicts with established guidance, respond by highlighting the conflict and offering the compliant alternative.

---
## 23. Maintenance Notes
Review this instructions file quarterly or after major ADR shifts. Update version history below.

Version History:
- 2025-11-10: Initial creation.

---
## 24. Fallback Assumptions (If Docs Missing)
Until directories are populated, treat this file as canonical baseline. Populate ADRs starting from `0001` for foundational stack choices (e.g., .NET version, logging strategy, testing approach).

---
## 25. Contact / Stewardship
Primary maintainer: GitHub user `nikneem`. Community contributions welcome via Issues + PRs.

---
End of Copilot repository instructions.
