---
title: "Minimal API Endpoint Organization"
date: 2025-11-12
status: Accepted
tags: [api, minimal-api, structure, web]
---
# Structure: Minimal API Endpoint Organization
Date: 2025-11-10
Type: Project Structure Template

## Purpose
Provide a consistent, scalable pattern for organizing Minimal API endpoints in ASP.NET projects following ADR 0005 (Minimal APIs over Controllers).

## Overview
Each base endpoint route (e.g., `/orders`, `/customers`, `/reports`) should be defined in a dedicated C# source file using a route group and a static extension method. This keeps routing logic modular, testable, and easy to navigate.

## File Organization Pattern

### Physical layout
```
src/
  YourProject.Api/
    Program.cs                       // Configures services & invokes extension methods
    Endpoints/
      OrderEndpoints.cs              // Extension method: MapOrderEndpoints()
      CustomerEndpoints.cs           // Extension method: MapCustomerEndpoints()
      ReportEndpoints.cs             // Extension method: MapReportEndpoints()
```

### Naming convention
- One file per base route or logical feature.
- File name: `{Feature}Endpoints.cs` (PascalCase, plural for resources, noun for features).
- Extension method: `Map{Feature}Endpoints(this IEndpointRouteBuilder app)` or `Map{Feature}Endpoints(this WebApplication app)`.
- Most often, features align with base routes (e.g., `/orders` → `OrderEndpoints.cs` → `MapOrderEndpoints()`).

## Template Example

### OrderEndpoints.cs
```csharp
namespace YourProject.Api.Endpoints;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders")
            .WithTags("Orders")
            .WithOpenApi();

        group.MapGet("/", async (IQueryHandler<ListOrdersQuery, IEnumerable<OrderDto>> handler, CancellationToken ct) =>
        {
            var orders = await handler.Handle(new ListOrdersQuery(), ct);
            return Results.Ok(orders);
        });

        group.MapGet("/{id:guid}", async (Guid id, IQueryHandler<GetOrderQuery, OrderDto?> handler, CancellationToken ct) =>
        {
            var order = await handler.Handle(new GetOrderQuery(id), ct);
            return order is not null ? Results.Ok(order) : Results.NotFound();
        });

        group.MapPost("/", async (CreateOrderRequest req, ICommandHandler<CreateOrderCommand, CreateOrderResult> handler, CancellationToken ct) =>
        {
            var result = await handler.Handle(new CreateOrderCommand(req.CustomerId, req.Lines), ct);
            return Results.Created($"/orders/{result.OrderId}", result);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateOrderRequest req, ICommandHandler<UpdateOrderCommand> handler, CancellationToken ct) =>
        {
            await handler.Handle(new UpdateOrderCommand(id, req.Lines), ct);
            return Results.NoContent();
        });

        group.MapDelete("/{id:guid}", async (Guid id, ICommandHandler<DeleteOrderCommand> handler, CancellationToken ct) =>
        {
            await handler.Handle(new DeleteOrderCommand(id), ct);
            return Results.NoContent();
        });

        return app;
    }
}
```

### Program.cs (wiring)
```csharp
var builder = WebApplication.CreateBuilder(args);

// Register services (handlers, repositories, etc.)
builder.Services.AddSingleton<IOrderService, OrderService>();
// ... DI registrations

var app = builder.Build();

// Map all endpoint groups
app.MapOrderEndpoints();
app.MapCustomerEndpoints();
app.MapReportEndpoints();

app.Run();
```

## Design principles

### One file per base route/feature
- Keeps endpoint definitions colocated with their logical grouping.
- Reduces merge conflicts when multiple features evolve in parallel.
- Simplifies navigation: `/orders` endpoints → `OrderEndpoints.cs`.

### Use route groups (`MapGroup`)
- Groups share a common prefix, metadata (tags, OpenAPI), and filters.
- Apply cross-cutting concerns (authorization, rate limiting, logging) at the group level when appropriate.

### Extension method pattern
- Keeps `Program.cs` thin and declarative.
- Makes endpoint registration testable (you can invoke `MapOrderEndpoints()` on a test `WebApplicationFactory`).
- Returns `IEndpointRouteBuilder` for chaining (optional but recommended).

### Endpoint lambdas stay thin
- Delegate to handlers (see ADR 0004: CQRS recommendation).
- Avoid business logic inside endpoint lambdas; keep them pure routing/binding/result mapping.

### Metadata and OpenAPI
- Use `.WithTags()` for grouping in Swagger UI.
- Use `.WithOpenApi()` or explicit `.WithName()` / `.WithDescription()` for documentation.
- Apply `.RequireAuthorization()` at group or individual endpoint level as needed.

## Variations

### Non-resource features
If the base route represents a non-resource action (e.g., `/health`, `/admin`, `/webhooks`):
- File: `HealthEndpoints.cs`, `AdminEndpoints.cs`, `WebhookEndpoints.cs`.
- Method: `MapHealthEndpoints()`, `MapAdminEndpoints()`, `MapWebhookEndpoints()`.

### Nested sub-resources
For nested routes (e.g., `/orders/{orderId}/items`):
- Define within the parent feature file (`OrderEndpoints.cs`) if tightly coupled.
- If sub-resource logic grows large, split into `OrderItemEndpoints.cs` and pass parent ID context.

### Filters and middleware
Apply endpoint filters (validation, telemetry, error handling) at the group level or per-endpoint:

```csharp
var group = app.MapGroup("/orders")
    .WithTags("Orders")
    .AddEndpointFilter<ValidationFilter>()
    .RequireAuthorization();
```

## Anti-patterns to avoid
- Defining all endpoints in `Program.cs` (gets unwieldy quickly).
- Mixing unrelated routes in a single extension method (violates single responsibility).
- Over-nesting groups without clear hierarchy (keep depth shallow).
- Forgetting to return `IEndpointRouteBuilder` from extension methods (breaks chaining).

## Testing recommendations
- Use `WebApplicationFactory<TProgram>` for integration tests.
- Call endpoint registration methods explicitly in test setups to verify routing.
- Mock handlers (see recommendation: Unit Testing with xUnit, Moq, Bogus) to test endpoint logic in isolation.

## References
- ADR 0001: Adopt .NET 10
- ADR 0004: CQRS Recommendation for ASP.NET API
- ADR 0005: Minimal APIs Over Controller-Based APIs
- Recommendation: Unit Testing with xUnit, Moq, Bogus
