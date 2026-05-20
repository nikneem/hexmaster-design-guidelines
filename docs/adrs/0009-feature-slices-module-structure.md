---
title: "ADR 0009: Feature Slices Within Module Projects"
date: 2026-04-07
status: Accepted
tags: [architecture, feature-slices, modules, cqrs, minimal-api, adr, modular-monolith]
---
# ADR 0009: Feature Slices Within Module Projects

## Context

ADR 0002 establishes the modular monolith structure: each domain module lives in its own set of projects (`Namespace.XYZ.{ModuleName}` and `Namespace.XYZ.{ModuleName}.Abstractions`). ADR 0007 establishes that application code is organized as vertical slices, and ADR 0004 mandates CQRS for request handling.

However, there is no authoritative guidance on **how features are physically added to a module** — specifically:

1. How feature namespaces and folders are structured inside the module project.
2. Where API payload types (Data Transfer Objects) live and how they are named.
3. How a Minimal API endpoint maps an incoming request payload (a C# record) onto a command or query and dispatches it to a handler.
4. What the boundary between the module project and its Abstractions project is with respect to feature slices.

Without explicit guidance, developers introduce inconsistencies: DTOs scattered across namespaces, logic leaking into endpoint lambdas, and request/command models conflated with public API contracts.

## Decision

We REQUIRE the following structure when adding features to a module.

### 1. Module Projects

Every module consists of exactly two core projects (additional data adapter projects are optional per ADR 0002):

| Project | Purpose |
|---|---|
| `Namespace.XYZ.{ModuleName}` | Domain logic, application (use-case) handlers, feature slice namespaces |
| `Namespace.XYZ.{ModuleName}.Abstractions` | Public contracts: DTOs, port interfaces, domain event declarations |

The Abstractions project defines the module's public surface. External modules and the host/API project depend only on Abstractions; never on the module implementation project directly.

### 2. Feature Namespace Convention

Features are organized under a `Features` sub-namespace inside the module project, with each feature in its own nested namespace:

```
Namespace.XYZ.{ModuleName}.Features.{FeatureName}
```

Examples:
- `Namespace.XYZ.Orders.Features.CreateOrder`
- `Namespace.XYZ.Orders.Features.DeleteOrder`
- `Namespace.XYZ.Orders.Features.GetOrderById`

Each feature namespace contains:
- The **command or query** record.
- The **handler** class that implements `ICommandHandler<TCommand, TResult>` or `IQueryHandler<TQuery, TResult>` (interfaces defined in the shared `Core/` project per ADR 0004).

**Handler naming convention**: Use `{FeatureName}CommandHandler` for command handlers and `{FeatureName}QueryHandler` for query handlers. The shorter `{FeatureName}Handler` is also acceptable but the full suffix makes the handler role unambiguous.

### 3. Data Transfer Objects in the Abstractions Project

All types that cross the module boundary — including API request payloads and response envelopes — are placed in the `DataTransferObjects` namespace of the Abstractions project:

```
Namespace.XYZ.{ModuleName}.Abstractions.DataTransferObjects
```

Examples:
- `Namespace.XYZ.Orders.Abstractions.DataTransferObjects.CreateOrderRequest`
- `Namespace.XYZ.Orders.Abstractions.DataTransferObjects.OrderDto`

> **Note**: Some legacy projects use `Dtos` as the folder/namespace name; `DataTransferObjects` is the standard for new projects.

**Rules for DTOs:**

- DTOs are always C# `record` types (immutable by default, structural equality, clean serialization).
- DTOs carry only data — no behavior, no domain logic, no annotations tied to a specific ORM or serializer framework.
- A DTO name ends with `Request` (incoming API payloads), `Response` or `Dto` (outgoing results), or `Event` (domain/integration event contracts).
- DTOs may be shared by multiple features in the same module; cross-module DTOs must live in a shared Abstractions project (e.g., `Namespace.XYZ.Shared.Abstractions`).

### 4. Endpoint → Handler Flow

The API host maps an incoming HTTP request to a DTO, then the endpoint lambda translates the DTO into the internal command/query and dispatches to the handler:

```
HTTP Request
  └─> Minimal API endpoint receives DTO record (from Abstractions.DataTransferObjects)
        └─> Endpoint maps DTO → Command/Query (defined in module Features namespace)
              └─> Handler executes use case, returns result DTO
                    └─> Endpoint translates result to HTTP response
```

This keeps handlers free of any HTTP or web-framework concerns.

### 5. Implementation Guidelines

#### Feature folder structure

```
src/
  Namespace.XYZ.Orders/
    DomainModels/
      Order.cs
      OrderLine.cs
    IOrderRepository.cs                 ← Repository port — internal to the module
    Features/
      CreateOrder/
        CreateOrderCommand.cs           (Command record)
        CreateOrderCommandHandler.cs    (ICommandHandler implementation)
      DeleteOrder/
        DeleteOrderCommand.cs
        DeleteOrderCommandHandler.cs
      GetOrderById/
        GetOrderByIdQuery.cs            (Query record)
        GetOrderByIdQueryHandler.cs     (IQueryHandler implementation)
    Observability/
      OrderMetrics.cs                   ← OpenTelemetry metrics (ActivitySource, counters)
    Services/                           ← Domain/application services
    OrdersModuleRegistration.cs
  Namespace.XYZ.Orders.Abstractions/
    DataTransferObjects/
      CreateOrderRequest.cs             (API payload — received from client)
      OrderDto.cs                       (API response — returned to client)
    Services/
      IOrderService.cs                  ← Cross-module service port (if consumed externally)
```

> **Repository interfaces** (`IOrderRepository`) belong in the **module implementation project** (alongside the domain logic that consumes them), not in the Abstractions project. The data adapter project references the module project to implement these internal ports. Only cross-module service contracts (`IOrderService`) belong in Abstractions.

#### Command / Query records

Commands and queries are internal to the module; they are not exposed through Abstractions:

```csharp
namespace Namespace.XYZ.Orders.Features.CreateOrder;

public sealed record CreateOrderCommand(Guid CustomerId, IReadOnlyList<OrderLineDto> Lines);
public sealed record CreateOrderResult(Guid OrderId, decimal Total, DateTimeOffset CreatedAt);
```

#### Handler

```csharp
namespace Namespace.XYZ.Orders.Features.CreateOrder;

public sealed class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, CreateOrderResult>
{
    private readonly IOrderRepository _orders;
    private readonly ILogger<CreateOrderCommandHandler> _logger;

    public CreateOrderCommandHandler(IOrderRepository orders, ILogger<CreateOrderCommandHandler> logger)
        => (_orders, _logger) = (orders, logger);

    public async Task<CreateOrderResult> Handle(CreateOrderCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Lines.Count == 0)
            throw new DomainException("Order must contain at least one line.");

        var order = Order.Create(command.CustomerId, command.Lines);
        await _orders.AddAsync(order, ct);

        _logger.LogInformation("Order {OrderId} created for customer {CustomerId}", order.Id, command.CustomerId);

        return new CreateOrderResult(order.Id, order.Total, order.CreatedAt);
    }
}
```

#### DTO in Abstractions project

```csharp
namespace Namespace.XYZ.Orders.Abstractions.DataTransferObjects;

public sealed record CreateOrderRequest(Guid CustomerId, IReadOnlyList<OrderLineRequest> Lines);
public sealed record OrderLineRequest(Guid ProductId, int Quantity, decimal UnitPrice);
```

#### Minimal API endpoint (in the host/API project)

The endpoint receives the DTO and maps it into the internal command, then calls the handler:

```csharp
namespace Namespace.XYZ.Api.Endpoints;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders")
            .WithTags("Orders")
            .WithOpenApi();

        group.MapPost("/", CreateOrder)
            .WithName("CreateOrder")
            .Produces<CreateOrderResult>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapDelete("/{id:guid}", DeleteOrder)
            .WithName("DeleteOrder")
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}", GetOrderById)
            .WithName("GetOrderById")
            .Produces<OrderDto>()
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CreateOrder(
        CreateOrderRequest request,
        ICommandHandler<CreateOrderCommand, CreateOrderResult> handler,
        CancellationToken ct)
    {
        var command = new CreateOrderCommand(
            request.CustomerId,
            request.Lines.Select(l => new OrderLineDto(l.ProductId, l.Quantity, l.UnitPrice)).ToList());

        var result = await handler.Handle(command, ct);
        return Results.Created($"/orders/{result.OrderId}", result);
    }

    private static async Task<IResult> DeleteOrder(
        Guid id,
        ICommandHandler<DeleteOrderCommand> handler,
        CancellationToken ct)
    {
        await handler.Handle(new DeleteOrderCommand(id), ct);
        return Results.Accepted();
    }

    private static async Task<IResult> GetOrderById(
        Guid id,
        IQueryHandler<GetOrderByIdQuery, OrderDto?> handler,
        CancellationToken ct)
    {
        var result = await handler.Handle(new GetOrderByIdQuery(id), ct);
        return result is not null ? Results.Ok(result) : Results.NotFound();
    }
}
```

### 6. Dependency Registration

Each module exposes a registration extension method in its module project (not Abstractions) to wire its handlers and infrastructure:

```csharp
namespace Namespace.XYZ.Orders;

public static class OrdersModuleRegistration
{
    public static IServiceCollection AddOrdersModule(this IServiceCollection services)
    {
        services.AddScoped<ICommandHandler<CreateOrderCommand, CreateOrderResult>, CreateOrderCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteOrderCommand>, DeleteOrderCommandHandler>();
        services.AddScoped<IQueryHandler<GetOrderByIdQuery, OrderDto?>, GetOrderByIdQueryHandler>();
        // Register infrastructure adapters (repositories, etc.)
        return services;
    }
}
```

The host project calls `services.AddOrdersModule()` in `Program.cs`.

## Consequences

### Positive

1. **Clear, predictable structure**: Any developer can locate the handler for a feature by navigating `{Module}/Features/{FeatureName}/{FeatureName}CommandHandler.cs`.
2. **Clean module boundary**: Abstractions project defines the module's public API; internal types (commands, queries, repository interfaces) stay private.
3. **DTO discipline**: Centralizing DTOs in `Abstractions/DataTransferObjects` prevents payload types from leaking into domain or handler namespaces.
4. **Thin endpoints**: Endpoint lambdas only map DTOs to commands/queries; no business logic.
5. **Testability**: Handlers are isolated from HTTP concerns and can be unit tested with simple mocks (xUnit + Moq + Bogus).
6. **Composability**: New features are new slice namespaces; no existing files are modified.
7. **Aligns with prior ADRs**: Natural extension of ADR 0002 (Modular Monolith), ADR 0004 (CQRS), ADR 0005 (Minimal APIs), and ADR 0007 (Vertical Slices).

### Negative

1. **Namespace verbosity**: Fully-qualified type names are long (e.g., `Namespace.XYZ.Orders.Features.CreateOrder.CreateOrderCommandHandler`). Mitigate with `using` aliases and file-scoped namespaces.
2. **Boilerplate per feature**: Each feature requires at least two files (command/query + handler). Mitigate with project templates or scaffolding scripts.
3. **Mapping overhead**: Each endpoint must explicitly map DTO → command and result → response. This is intentional (avoids coupling), but adds code. Mitigate with a thin mapping helper where the mapping is trivial.

### Mitigation Strategies

- Provide a project-item template (`.NET` item template or a Rider/VS live template) for the `Features/{FeatureName}` slice scaffold.
- Use `dotnet new` custom templates to scaffold a full module skeleton.
- Document and enforce via code review that Abstractions projects must not reference the module implementation project (enforced via project reference direction).

## Alignment with Other ADRs

| ADR | Relationship |
|---|---|
| ADR 0002: Modular Monolith Structure | This ADR refines the internal project structure of each module |
| ADR 0004: CQRS Recommendation | Feature slices use command/query handler interfaces defined there |
| ADR 0005: Minimal APIs | Endpoint pattern delegates to feature slice handlers |
| ADR 0007: Vertical Slice Architecture | This ADR applies vertical slices specifically to the module/Abstractions boundary |

## References

- ADR 0002: Modular Monolith Project Structure
- ADR 0004: CQRS Recommendation for ASP.NET API Projects
- ADR 0005: Minimal APIs Over Controller-Based APIs
- ADR 0007: Vertical Slice Architecture for Feature Organization
- Structure: Feature Slices Module Structure (see `structures/feature-slices-module-structure.md`)
