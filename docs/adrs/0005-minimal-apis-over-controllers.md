# ADR 0005: Recommend Minimal APIs Over Controller-Based APIs
Date: 2025-11-10
Status: Proposed

## Context
ASP.NET has historically supported two approaches for HTTP endpoint definition: Controller-based APIs (using `[ApiController]` and routing attributes) and Minimal APIs (introduced in .NET 6, matured in .NET 7-9). Controller-based APIs carry framework conventions (base classes, action filters, model binding attributes) that add overhead and ceremony. Minimal APIs offer a lighter, more explicit, and generally faster alternative.

With .NET 9, Minimal APIs have feature parity with controller-based APIs (OpenAPI integration, parameter binding customization, filters, endpoint grouping). The ecosystem tooling and templates have converged around Minimal APIs as the default for new projects.

## Decision
We RECOMMEND using Minimal APIs for all NEW ASP.NET API projects (HTTP APIs, REST services, gRPC-JSON transcoding endpoints).

- Minimal APIs are lightweight, more performant, and align with modern .NET idioms (top-level statements, DI-resolved lambdas).
- They reduce indirection (no base controller class inheritance) and improve startup time.
- OpenAPI/Swagger generation, validation, binding, filters, and authentication/authorization are fully supported as of .NET 9.

### Legacy projects
Projects historically created as controller-based APIs are ALLOWED to remain as-is. No mandatory migration is required. Incremental conversion to Minimal APIs is encouraged if refactoring occurs, but stability and team familiarity take priority over forced rewrites.

### When to prefer controllers
Rare cases where controller-based APIs may still be justified:
- Very large existing codebase with heavy investment in custom action filters and controller conventions (evaluate cost/benefit of migration).
- Third-party tooling explicitly requiring MVC controllers (verify if Minimal API alternatives exist).

In all other scenarios, default to Minimal APIs.

## Minimal API style guidelines
- Group related endpoints using `RouteGroupBuilder` (`app.MapGroup("/orders")`).
- Keep endpoint lambdas thin; delegate to handlers (see ADR 0004 CQRS recommendation).
- Use C# records for request/response models.
- Apply validation at the edge (validator middleware or manual checks before invoking handlers).
- Register endpoint filters (logging, telemetry, error handling) via `.AddEndpointFilter<T>()`.
- Use `Results` static helper (`Results.Ok()`, `Results.NotFound()`, `Results.Accepted()`) for consistent HTTP responses.

### Example (net9.0)

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IOrderService, OrderService>();

var app = builder.Build();

var orders = app.MapGroup("/orders");

orders.MapPost("/", async (CreateOrderRequest req, ICommandHandler<CreateOrderCommand, CreateOrderResult> handler, CancellationToken ct) =>
{
    var result = await handler.Handle(new CreateOrderCommand(req.CustomerId, req.Lines), ct);
    return Results.Ok(result);
});

orders.MapGet("/{id:guid}", async (Guid id, IQueryHandler<GetOrderQuery, OrderDto> handler, CancellationToken ct) =>
{
    var order = await handler.Handle(new GetOrderQuery(id), ct);
    return order is not null ? Results.Ok(order) : Results.NotFound();
});

app.Run();
```

## Consequences
Positive:
- Faster startup, lower memory footprint, reduced ceremony.
- Explicit DI resolution; easier to reason about dependencies.
- Aligns with .NET 9 defaults and Microsoft guidance.
- Simplified testing (test handlers directly without controller instantiation).

Negative/Trade-offs:
- Developers accustomed to MVC controllers need onboarding.
- Endpoint organization requires discipline (use `MapGroup` and separate extension methods for clarity).

Neutral:
- Legacy controller-based projects remain supported; no forced migration.

## Migration considerations
If converting a controller-based API to Minimal APIs:
- Extract business logic from controller actions into handlers (see ADR 0004).
- Convert action methods to `app.MapXxx()` calls grouped by resource.
- Replace attribute-based routing with route patterns.
- Migrate custom action filters to endpoint filters or middleware.
- Test incrementally; consider running both patterns side-by-side during transition (use `/api/v1` controller prefix vs `/api/v2` minimal).

## References
- ADR 0001: Adopt .NET 9
- ADR 0004: CQRS Recommendation for ASP.NET API
- Microsoft Docs: Minimal APIs overview (.NET 9)
- Performance benchmarks (Microsoft TechEmpower results show Minimal APIs outperform MVC controllers)
