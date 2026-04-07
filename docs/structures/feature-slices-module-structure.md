---
title: "Feature Slices Module Structure"
date: 2026-04-07
status: Accepted
tags: [feature-slices, modules, cqrs, minimal-api, structure, modular-monolith, abstractions]
---
# Structure: Feature Slices Module Structure

Date: 2026-04-07
Type: Project Structure Template

## Purpose

Provide a concrete, copy-paste scaffold for implementing feature slices inside a module, following the conventions established in ADR 0009. Use this template when creating a new module or adding a new feature to an existing module.

## Overview

Each module consists of two core projects:

- **`Namespace.XYZ.{ModuleName}`** — implementation: domain entities, application handlers, feature slices.
- **`Namespace.XYZ.{ModuleName}.Abstractions`** — public contracts: DTOs (in `DataTransferObjects`), port interfaces.

Features are added as sub-namespaces under `Features`:

```
Namespace.XYZ.{ModuleName}.Features.{FeatureName}
```

---

## Physical Project Layout

```
src/
  Namespace.XYZ.Orders/
    Namespace.XYZ.Orders/                         ← Module implementation project
      Domain/
        Order.cs
        OrderLine.cs
        IOrderRepository.cs
      Features/
        CreateOrder/
          CreateOrderCommand.cs
          CreateOrderHandler.cs
        DeleteOrder/
          DeleteOrderCommand.cs
          DeleteOrderHandler.cs
        GetOrderById/
          GetOrderByIdQuery.cs
          GetOrderByIdHandler.cs
      OrdersModuleRegistration.cs                 ← DI extension method
      Namespace.XYZ.Orders.csproj

    Namespace.XYZ.Orders.Abstractions/            ← Module Abstractions project
      DataTransferObjects/
        CreateOrderRequest.cs
        OrderDto.cs
        OrderLineRequest.cs
      IOrderService.cs                            ← Optional: cross-module service port
      Namespace.XYZ.Orders.Abstractions.csproj
```

---

## File Templates

### Abstractions Project: DTO records

All types exchanged with the outside world (API payloads, responses, integration events) are C# `record` types under `DataTransferObjects`:

**`DataTransferObjects/CreateOrderRequest.cs`**
```csharp
namespace Namespace.XYZ.Orders.Abstractions.DataTransferObjects;

public sealed record CreateOrderRequest(Guid CustomerId, IReadOnlyList<OrderLineRequest> Lines);

public sealed record OrderLineRequest(Guid ProductId, int Quantity, decimal UnitPrice);
```

**`DataTransferObjects/OrderDto.cs`**
```csharp
namespace Namespace.XYZ.Orders.Abstractions.DataTransferObjects;

public sealed record OrderDto(
    Guid Id,
    Guid CustomerId,
    decimal Total,
    DateTimeOffset CreatedAt,
    IReadOnlyList<OrderLineDto> Lines);

public sealed record OrderLineDto(Guid ProductId, int Quantity, decimal UnitPrice);
```

---

### Module Project: Feature Slices

Each feature lives in its own namespace under `Features`:

**`Features/CreateOrder/CreateOrderCommand.cs`**
```csharp
namespace Namespace.XYZ.Orders.Features.CreateOrder;

using Namespace.XYZ.Orders.Abstractions.DataTransferObjects;

public sealed record CreateOrderCommand(Guid CustomerId, IReadOnlyList<OrderLineDto> Lines);

public sealed record CreateOrderResult(Guid OrderId, decimal Total, DateTimeOffset CreatedAt);
```

**`Features/CreateOrder/CreateOrderHandler.cs`**
```csharp
namespace Namespace.XYZ.Orders.Features.CreateOrder;

public sealed class CreateOrderHandler : ICommandHandler<CreateOrderCommand, CreateOrderResult>
{
    private readonly IOrderRepository _orders;
    private readonly ILogger<CreateOrderHandler> _logger;

    public CreateOrderHandler(IOrderRepository orders, ILogger<CreateOrderHandler> logger)
        => (_orders, _logger) = (orders, logger);

    public async Task<CreateOrderResult> Handle(CreateOrderCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Lines.Count == 0)
            throw new DomainException("Order must contain at least one line.");

        var order = Order.Create(command.CustomerId, command.Lines);
        await _orders.AddAsync(order, ct);

        _logger.LogInformation("Order {OrderId} created for customer {CustomerId}",
            order.Id, command.CustomerId);

        return new CreateOrderResult(order.Id, order.Total, order.CreatedAt);
    }
}
```

**`Features/DeleteOrder/DeleteOrderCommand.cs`**
```csharp
namespace Namespace.XYZ.Orders.Features.DeleteOrder;

public sealed record DeleteOrderCommand(Guid OrderId);
```

**`Features/DeleteOrder/DeleteOrderHandler.cs`**
```csharp
namespace Namespace.XYZ.Orders.Features.DeleteOrder;

public sealed class DeleteOrderHandler : ICommandHandler<DeleteOrderCommand>
{
    private readonly IOrderRepository _orders;
    private readonly ILogger<DeleteOrderHandler> _logger;

    public DeleteOrderHandler(IOrderRepository orders, ILogger<DeleteOrderHandler> logger)
        => (_orders, _logger) = (orders, logger);

    public async Task Handle(DeleteOrderCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var order = await _orders.GetByIdAsync(command.OrderId, ct)
            ?? throw new NotFoundException($"Order {command.OrderId} not found.");

        order.MarkDeleted();
        await _orders.UpdateAsync(order, ct);

        _logger.LogInformation("Order {OrderId} deleted", command.OrderId);
    }
}
```

**`Features/GetOrderById/GetOrderByIdQuery.cs`**
```csharp
namespace Namespace.XYZ.Orders.Features.GetOrderById;

using Namespace.XYZ.Orders.Abstractions.DataTransferObjects;

public sealed record GetOrderByIdQuery(Guid OrderId);
```

**`Features/GetOrderById/GetOrderByIdHandler.cs`**
```csharp
namespace Namespace.XYZ.Orders.Features.GetOrderById;

using Namespace.XYZ.Orders.Abstractions.DataTransferObjects;

public sealed class GetOrderByIdHandler : IQueryHandler<GetOrderByIdQuery, OrderDto?>
{
    private readonly IOrderRepository _orders;

    public GetOrderByIdHandler(IOrderRepository orders) => _orders = orders;

    public async Task<OrderDto?> Handle(GetOrderByIdQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var order = await _orders.GetByIdAsync(query.OrderId, ct);
        if (order is null)
            return null;

        return new OrderDto(
            order.Id,
            order.CustomerId,
            order.Total,
            order.CreatedAt,
            order.Lines.Select(l => new OrderLineDto(l.ProductId, l.Quantity, l.UnitPrice)).ToList());
    }
}
```

---

### Module Registration

**`OrdersModuleRegistration.cs`**
```csharp
namespace Namespace.XYZ.Orders;

using Namespace.XYZ.Orders.Features.CreateOrder;
using Namespace.XYZ.Orders.Features.DeleteOrder;
using Namespace.XYZ.Orders.Features.GetOrderById;
using Namespace.XYZ.Orders.Abstractions.DataTransferObjects;

public static class OrdersModuleRegistration
{
    public static IServiceCollection AddOrdersModule(this IServiceCollection services)
    {
        // Feature handlers
        services.AddScoped<ICommandHandler<CreateOrderCommand, CreateOrderResult>, CreateOrderHandler>();
        services.AddScoped<ICommandHandler<DeleteOrderCommand>, DeleteOrderHandler>();
        services.AddScoped<IQueryHandler<GetOrderByIdQuery, OrderDto?>, GetOrderByIdHandler>();

        // Infrastructure (register repository adapters here or in a dedicated extension)
        return services;
    }
}
```

---

### API Host: Endpoint Mapping

The host project references only the Abstractions project for DTOs and the module project (or a shared Core project) for handler interfaces. Endpoint lambdas map DTO → command/query:

**`Endpoints/OrderEndpoints.cs`** (in the API host project)
```csharp
namespace Namespace.XYZ.Api.Endpoints;

using Namespace.XYZ.Orders.Abstractions.DataTransferObjects;
using Namespace.XYZ.Orders.Features.CreateOrder;
using Namespace.XYZ.Orders.Features.DeleteOrder;
using Namespace.XYZ.Orders.Features.GetOrderById;

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

**`Program.cs`** (host wiring)
```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOrdersModule();
// ... other modules

var app = builder.Build();

app.MapOrderEndpoints();
// ... other endpoint groups

app.Run();
```

---

## Naming Reference

| Artifact | Pattern | Example |
|---|---|---|
| Module implementation project | `Namespace.XYZ.{ModuleName}` | `HexMaster.Orders` |
| Module abstractions project | `Namespace.XYZ.{ModuleName}.Abstractions` | `HexMaster.Orders.Abstractions` |
| DTO namespace | `Namespace.XYZ.{ModuleName}.Abstractions.DataTransferObjects` | `HexMaster.Orders.Abstractions.DataTransferObjects` |
| Feature namespace | `Namespace.XYZ.{ModuleName}.Features.{FeatureName}` | `HexMaster.Orders.Features.CreateOrder` |
| Command record | `{FeatureName}Command` | `CreateOrderCommand` |
| Query record | `{FeatureName}Query` | `GetOrderByIdQuery` |
| Handler class | `{FeatureName}Handler` | `CreateOrderHandler` |
| API request DTO | `{FeatureName}Request` | `CreateOrderRequest` |
| API response DTO | `{Entity}Dto` or `{FeatureName}Result` | `OrderDto`, `CreateOrderResult` |
| Module registration method | `Add{ModuleName}Module` | `AddOrdersModule` |
| Endpoint mapping method | `Map{ModuleName}Endpoints` | `MapOrderEndpoints` |

---

## Rules Summary

1. **Features folder**: All feature namespaces live under `{ModuleName}.Features.{FeatureName}`.
2. **Two files per feature** (minimum): `{FeatureName}Command.cs` / `{FeatureName}Query.cs` and `{FeatureName}Handler.cs`.
3. **DTOs in Abstractions**: All types that cross the module boundary go in `{ModuleName}.Abstractions.DataTransferObjects`.
4. **Records everywhere**: Commands, queries, DTOs, and results are `sealed record` types.
5. **Thin endpoints**: Endpoint methods only map DTO → command/query; no domain logic.
6. **No domain types in DTOs**: DTOs must not reference domain entities or EF Core types.
7. **One registration method per module**: `Add{ModuleName}Module` wires all handlers and infrastructure.

---

## Anti-Patterns to Avoid

| Anti-pattern | Why it's wrong | Correct approach |
|---|---|---|
| Putting DTOs in the module implementation project | Couples API consumers to internal types | DTOs belong in `Abstractions/DataTransferObjects` |
| Putting commands/queries in Abstractions | Leaks internal design to consumers | Commands/queries are private to the module |
| Business logic in endpoint lambdas | Bypasses the handler, untestable | Delegate everything to the handler |
| Sharing a handler across features | Violates single-responsibility | One handler per feature |
| Referencing the module implementation project from other modules | Breaks the module boundary | Reference only the Abstractions project |
| Using `class` for commands/queries/DTOs | Mutable, non-structural equality | Use `sealed record` |

---

## Testing

Co-locate handler tests with the feature slice:

```csharp
namespace Namespace.XYZ.Orders.Features.CreateOrder;

public sealed class CreateOrderHandlerTests
{
    [Fact]
    public async Task Handle_ShouldCreateOrder_WhenCommandIsValid()
    {
        // Arrange
        var ordersRepo = Substitute.For<IOrderRepository>();
        var handler = new CreateOrderHandler(ordersRepo, NullLogger<CreateOrderHandler>.Instance);
        var command = new CreateOrderCommand(
            Guid.NewGuid(),
            [new OrderLineDto(Guid.NewGuid(), 2, 10.00m)]);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(20.00m);
        await ordersRepo.Received(1).AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowDomainException_WhenNoLines()
    {
        // Arrange
        var handler = new CreateOrderHandler(
            Substitute.For<IOrderRepository>(),
            NullLogger<CreateOrderHandler>.Instance);
        var command = new CreateOrderCommand(Guid.NewGuid(), []);

        // Act & Assert
        await handler.Invoking(h => h.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<DomainException>()
            .WithMessage("*at least one line*");
    }
}
```

---

## References

- ADR 0002: Modular Monolith Project Structure
- ADR 0004: CQRS Recommendation for ASP.NET API Projects
- ADR 0005: Minimal APIs Over Controller-Based APIs
- ADR 0007: Vertical Slice Architecture for Feature Organization
- ADR 0009: Feature Slices Within Module Projects
- Structure: Minimal API Endpoint Organization
