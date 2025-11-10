# ADR 0004: Recommendation to Implement CQRS for ASP.NET API Projects
Date: 2025-11-10
Status: Proposed

## Context
ASP.NET API projects frequently mix request handling, validation, orchestration and domain logic inside controllers/endpoint lambdas, leading to low cohesion, hidden coupling, and hard-to-test code. We want a simple, consistent pattern that aligns with Hexagonal/Clean Architecture: HTTP endpoints are thin, application logic is encapsulated, and cross-module reuse is possible.

## Decision
We RECOMMEND (not mandate) using a lightweight CQRS style in ASP.NET API projects:

- Requests translate to Commands or Queries handled by dedicated handlers.
- Payloads and DTOs are C# records for immutability and clarity.
- Handlers are injected via dependency injection and kept free of HTTP concerns.
- Handlers can return a DTO record result; when work succeeds without a return value, endpoints should return HTTP 202 Accepted.

This pattern improves testability, boundaries, and vertical slice organization.

### Contract rules
- HTTP payloads (body or parameters) should be mapped into C# records (request models).
- Commands/Queries should be C# records (immutable by default) representing intent.
- Handler outputs are DTO records suitable for serialization; do not expose domain entities directly.
- For fire-and-forget or background-triggered work, use a Command with no return type and return 202 Accepted from the endpoint on success.

### Minimal example (net9.0)

```csharp
// Request payload (HTTP)
public sealed record CreateOrderRequest(Guid CustomerId, IReadOnlyList<OrderLineDto> Lines);

// Command & Result (Application layer)
public sealed record CreateOrderCommand(Guid CustomerId, IReadOnlyList<OrderLineDto> Lines);
public sealed record CreateOrderResult(Guid OrderId, decimal Total);

public interface ICommandHandler<TCommand, TResult>
{
    Task<TResult> Handle(TCommand command, CancellationToken ct);
}

public sealed class CreateOrderHandler : ICommandHandler<CreateOrderCommand, CreateOrderResult>
{
    private readonly IOrderService _service;
    public CreateOrderHandler(IOrderService service) => _service = service;

    public async Task<CreateOrderResult> Handle(CreateOrderCommand command, CancellationToken ct)
    {
        var order = await _service.CreateAsync(command.CustomerId, command.Lines, ct);
        return new CreateOrderResult(order.Id, order.Total);
    }
}

// Endpoint (thin) using DI-resolved handler
app.MapPost("/orders", async (CreateOrderRequest req, ICommandHandler<CreateOrderCommand, CreateOrderResult> handler, CancellationToken ct) =>
{
    var result = await handler.Handle(new CreateOrderCommand(req.CustomerId, req.Lines), ct);
    return Results.Ok(result);
});

// Fire-and-forget (no return DTO): return 202 Accepted
public sealed record RebuildProjectionsCommand();
public interface ICommandHandler<TCommand> { Task Handle(TCommand command, CancellationToken ct); }

app.MapPost("/projections/rebuild", async (ICommandHandler<RebuildProjectionsCommand> handler, CancellationToken ct) =>
{
    await handler.Handle(new RebuildProjectionsCommand(), ct);
    return Results.Accepted();
});
```

### Organizing slices
- Keep request/command/result/handler together per feature (vertical slice).
- Validate at the edge (endpoint or dedicated validator) before invoking handler.
- Keep handlers framework-agnostic (no HttpContext access).

## Shared CQRS base types across modules
When a solution contains multiple modules/domains/projects using this CQRS pattern, move the shared base abstractions to a central Core project accessible to all modules:

- Define `ICommandHandler<TCommand, TResult>`, `ICommandHandler<TCommand>`, `IQueryHandler<TQuery, TResult>` and optional marker interfaces (`ICommand`, `IQuery`).
- Keep these in `YourCompany.YourSolution.Core` (name may vary), separate from any specific module’s implementation.
- All modules reference this Core project to avoid duplication and ensure consistent signatures.
- Do not let Core depend on web frameworks or infrastructure.

This prevents repeated type definitions and keeps signatures uniform across modules.

## Consequences
Positive:
- Thin endpoints, testable handlers, clear separation of concerns.
- Encourages vertical slices; simplifies onboarding and maintenance.
- DTOs as records reduce accidental mutability and serialization surprises.
- Consistent Core abstractions facilitate reuse across modules.

Negative/Trade-offs:
- Slight increase in types (request, command/query, result, handler).
- Requires discipline to avoid putting HTTP logic inside handlers.

## References
- ADR 0001: Adopt .NET 9
- ADR 0002: Modular Monolith Project Structure
- Hexagonal/Clean Architecture principles (ports/handlers decouple delivery from application logic)
