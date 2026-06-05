---
title: "ADR 0010: Resolve External Subject ID to Internal User ID at the API Boundary"
date: 2026-06-05
status: Accepted
tags: [identity, authentication, adr, aspnet, security, users]
---
# ADR 0010: Resolve External Subject ID to Internal User ID at the API Boundary

## Context

When a system authenticates users via an external identity provider (Auth0, Azure AD, Entra ID, Cognito, etc.), the JWT token contains a `sub` claim — the identity provider's own opaque identifier for the user (the "Subject ID" or `SubjectId`). This value is controlled by the external provider and has no meaning inside the domain model.

If domain entities, commands, queries, or data records store the `SubjectId` directly, the system becomes tightly coupled to the identity provider:

- Migrating to a different provider requires updating every table, event, and projection that stores the `SubjectId`.
- Domain logic ends up referencing infrastructure-level identifiers.
- Cross-module references become implicit contracts on the `SubjectId` format.

## Decision

When a project registers users from an external identity provider using a `SubjectId` from a JWT token, the following rules apply:

### 1. Resolve early — at the API boundary

The `SubjectId` MUST be resolved to an internal `UserId` as early as possible in the ASP.NET Core execution lifecycle — ideally inside the endpoint handler, before any command or query is dispatched. The resolution is a lookup in the Users module: given a `SubjectId`, return the corresponding internal `UserId`. If no user exists for that `SubjectId`, the endpoint returns `401 Unauthorized` or creates a new user record, depending on the feature's registration flow.

```csharp
// Endpoint (thin) — resolves SubjectId to internal UserId before dispatching
app.MapPost("/playfields", async (
    ClaimsPrincipal principal,
    CreatePlayFieldRequest req,
    IUserResolver userResolver,
    ICommandHandler<CreatePlayFieldCommand, CreatePlayFieldResult> handler,
    CancellationToken ct) =>
{
    var subjectId = principal.FindFirstValue("sub")
        ?? throw new UnauthorizedAccessException();

    var userId = await userResolver.ResolveAsync(subjectId, ct);

    var result = await handler.Handle(
        new CreatePlayFieldCommand(userId, req.Name, req.Coordinates), ct);

    return Results.Created($"/playfields/{result.Id}", result);
});
```

### 2. Internal UserId type

The preferred type for `UserId` is `Guid`. In cases where an existing schema or external system requires it, `int` or `long` are permitted, but `Guid` MUST be used for all new modules.

### 3. Domain entities and DTOs use UserId — never SubjectId

All domain models, commands, queries, and DTOs that reference a user MUST carry the internal `UserId`. The `SubjectId` MUST NOT appear in:

- Domain model properties
- Command or query records
- DTO records in `Abstractions/DataTransferObjects/`
- Stored data records (Table Storage entities, EF Core models, etc.)
- Integration events or domain events

The `SubjectId` is allowed only at the boundary layer (endpoint code, `IUserResolver` implementation) where it is immediately traded for a `UserId`.

### 4. IUserResolver contract

The resolution contract lives in a shared port or in the Users module's public interface. A minimal example:

```csharp
public interface IUserResolver
{
    /// <summary>
    /// Returns the internal UserId for the given external SubjectId.
    /// Throws UnauthorizedAccessException when no user is registered for the SubjectId.
    /// </summary>
    Task<Guid> ResolveAsync(string subjectId, CancellationToken ct = default);
}
```

The implementation queries the Users module's data store. It MUST NOT be implemented inline in individual domain modules.

### 5. The Users module is the single authority

Only the Users module stores the mapping between `SubjectId` and `UserId`. No other module duplicates this mapping. Cross-module queries that need to filter or join on a user reference use `UserId` exclusively.

## Consequences

**Positive:**

- The domain model is completely decoupled from the identity provider. Swapping providers (or running multiple in parallel during a migration) only requires updating the Users module's registration and lookup logic.
- `SubjectId` leakage into domain events, projections, and stored data is prevented by rule.
- All modules share a stable, typed `UserId` (Guid) as the user reference, making cross-module joins and events well-defined.

**Negative / Trade-offs:**

- Every authenticated endpoint that needs a user reference must call `IUserResolver` before dispatching its handler, adding one async lookup per request. This should be mitigated by caching the resolved `UserId` in the request context or via a distributed cache keyed on `SubjectId`.
- If the Users module is unavailable, endpoints that require `UserId` resolution will fail. This is acceptable — a system cannot safely operate on behalf of an unresolvable user.

## References

- ADR 0004: CQRS Recommendation — commands and queries carry `UserId`, not `SubjectId`
- ADR 0005: Minimal APIs Over Controllers — resolution happens in the thin endpoint layer
- ADR 0009: Feature Slices Module Structure — Users module owns the `SubjectId` → `UserId` mapping
- CLAUDE.md: "The caller's identity is available in handlers via the `sub` claim (`principal.FindFirstValue("sub")`)" — this value MUST be resolved before reaching a handler
