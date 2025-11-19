---
title: "ADR 0002: Modular Monolith Project Structure"
date: 2025-11-10
status: Accepted
tags: [architecture, modular-monolith, adr, design, modularity, hexagonal]
---
# ADR 0002: Modular Monolith Project Structure

## Context
The repository aims to provide guidance for .NET 10 C# solutions employing Hexagonal / Clean Architecture while remaining maintainable and evolvable. Many teams start with a single project or a coarse set of layered projects (Domain, Application, Infrastructure). As functionality grows, unrelated domains/features can become tightly coupled, increasing build times, merge conflicts, and cognitive load. A full microservice split may be premature, introducing deployment, observability, and transactional complexity. We require a structure that enforces boundaries, supports independent evolution of domain modules, enables targeted testing, and keeps operational simplicity.

## Decision
Adopt a Modular Monolith physical project structure. Each domain/feature/module resides in its own folder and set of projects forming an internal boundary:

1. One "root" solution aggregates all module projects plus cross-cutting shared abstractions.
2. Each module gets its own directory at the repository root or under `src/`, e.g.:
   - `src/HexMaster.TicTacToe/`
   - `src/HexMaster.Inventory/`
3. A module may contain multiple projects:
   - `HexMaster.TicTacToe` (domain + application logic for the module) – may be split further into `HexMaster.TicTacToe.Domain` / `HexMaster.TicTacToe.Application` if complexity warrants.
   - `HexMaster.TicTacToe.Abstractions` (optional): DTO contracts, port interfaces (repositories, service abstractions), event contracts. No implementation code.
   - `HexMaster.TicTacToe.Data.<DataStore>` (e.g., `HexMaster.TicTacToe.Data.CosmosDb`, `HexMaster.TicTacToe.Data.Postgres`): persistence adapter implementations, mappings, EF Core or SDK-specific code. Contains infrastructure details only.
   - `HexMaster.TicTacToe.Tests` (unit + integration tests for the module).
4. Cross-cutting shared abstractions (if truly generic) live in a dedicated project: `HexMaster.Shared.Abstractions` (avoid dumping everything—only stable, widely reused contracts). Prefer duplication until contracts proven.
5. Internal dependencies follow clean architecture direction: Data / Adapters depend inward on domain/application abstractions; domain never references data implementations.
6. A module's public surface is defined by its Abstractions project; other modules depend only on abstractions, never on Data or internal implementation projects.
7. Module registration/composition occurs at the host/startup boundary (e.g., web/API host project) wiring adapters to ports.
8. Each module may evolve into its own deployable unit later (potential service extraction) with minimal refactoring since boundaries are explicit.
9. Build pipelines can target individual modules for faster feedback (future optimization).

Naming Conventions:
- Module root: `HexMaster.<Module>`
- Optional abstractions: `HexMaster.<Module>.Abstractions`
- Data projects: `HexMaster.<Module>.Data.<Store>` (Store examples: `CosmosDb`, `Postgres`, `Mongo`, `InMemory`)
- Host/adapters: `HexMaster.<Module>.Adapters.Http`, `HexMaster.<Module>.Adapters.Worker` (if delivery mechanisms are module-specific)

Physical Layout Example:
```
src/
  HexMaster.TicTacToe/
    HexMaster.TicTacToe/                (Domain + Application or split further)
    HexMaster.TicTacToe.Abstractions/   (Ports, DTOs, Events)
    HexMaster.TicTacToe.Data.CosmosDb/  (Cosmos persistence implementation)
    HexMaster.TicTacToe.Tests/          (Unit + integration tests)
  HexMaster.Inventory/
    HexMaster.Inventory.Domain/         (Domain entities/value objects)
    HexMaster.Inventory.Application/    (Use cases/handlers)
    HexMaster.Inventory.Abstractions/   (Interfaces)
    HexMaster.Inventory.Data.Postgres/  (EF Core / Dapper implementation)
    HexMaster.Inventory.Tests/
```

Module Boundary Rules:
- No cross-module domain entity sharing—use DTO contracts from abstractions.
- Avoid leaking infrastructure types (e.g., EF DbContext) outside data project.
- Domain projects remain persistence-agnostic (no ORM attributes, no SDK references).
- All inter-module communication via abstractions interfaces or domain events (if using an in-memory dispatcher).
- Keep tests colocated with module to encourage high cohesion.

Refactoring Guidance:
- Start simple: domain + data in one module directory; introduce Abstractions only when another module requires a stable contract.
- Split Domain/Application if use case orchestration logic grows large or distinct from core domain invariants.
- Introduce multiple data provider projects only when necessary (e.g., read vs write stores) and tag with ADR referencing rationale.

Versioning & Ownership:
- Each module can have an OWNER file (future enhancement) for stewardship.
- ADR changes affecting module boundaries must reference impacted modules explicitly.

## Consequences
Positive:
- Clear internal boundaries without distributed system overhead.
- Simplifies future extraction into microservices if needed.
- Improves build ergonomics (selective testing, potential project filtering).
- Encourages explicit contracts and reduces accidental coupling.
- Aligns with Hexagonal & Clean Architecture principles (ports/adapters localized per module).

Negative:
- More projects to manage; initial overhead in solution maintenance.
- Risk of premature abstraction if modules over-split early.
- Requires discipline to prevent a "shared dumping ground" project.

Neutral / Trade-offs:
- Some duplication of simple DTOs until stability proven (preferred over premature centralization).

## References
- ADR 0001 (Adopt .NET 10)
- Chris Richardson: Modular Monolith guidance (external)
- Udi Dahan: Service boundaries (conceptual alignment)
- ThoughtWorks Tech Radar: Evolutionary architecture principles
