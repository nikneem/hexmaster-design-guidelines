---
title: "ADR 0007: Adopt Vertical Slice Architecture for Feature Organization"
date: 2025-11-24
status: Accepted
tags: [architecture, vertical-slice, feature-organization, adr, clean-architecture]
---
# ADR 0007: Adopt Vertical Slice Architecture for Feature Organization

## Context
Traditional layered architecture organizes code by technical concerns (Controllers, Services, Repositories, Models), leading to:

1. **High coupling across layers**: A single feature change requires modifying files in multiple folders (controller, service, repository, DTOs).
2. **Poor cohesion**: Related logic is scattered; understanding a feature requires navigating distant folders.
3. **Merge conflicts**: Multiple developers working on different features often touch the same service/repository files.
4. **Cognitive overhead**: Mental context-switching between technical layers slows development.
5. **Large files**: Service classes accumulate unrelated methods as features grow, violating Single Responsibility Principle.
6. **Difficult feature removal**: Deleting a feature requires careful hunting across layers to avoid leaving orphaned code.

Modern applications benefit from organizing code by business capability or feature (vertical slices) rather than technical layer (horizontal slices). Each slice contains everything needed for one use case: request model, handler, validation, domain logic, data access, and tests.

This aligns with Domain-Driven Design's bounded contexts and Hexagonal Architecture's focus on use case boundaries.

## Decision
We REQUIRE organizing application code using **Vertical Slice Architecture** for feature implementation within projects.

### Core Principles

1. **Feature-centric organization**: Group all code for a single feature/use case in one folder or namespace.
2. **Self-contained slices**: Each slice includes request models, handlers, validators, domain logic, data access, and tests.
3. **Minimal cross-slice dependencies**: Slices should be largely independent; shared concerns use abstractions (ports/interfaces).
4. **Thin adapters**: HTTP endpoints, message handlers, and CLI commands remain thin, delegating to slice handlers.
5. **Encapsulation**: Internal implementation details of a slice are private; only public contracts (request/response models, handler interfaces) are exposed.

### Project Structure

> **Note**: For modular-monolith projects, refer to **ADR 0009** for the authoritative physical project layout. The structure below illustrates the vertical-slice principle; actual folder names follow ADR 0009 conventions (`DomainModels/` not `Domain/`, `Features/` with `{Feature}CommandHandler` naming, tests in separate `.Tests` project).

#### Application Layer (Use Cases)
Organize by feature/capability, not by technical layer:

```
src/ProjectName.Orders/
├── DomainModels/
│   ├── Order.cs
│   └── OrderLine.cs
├── Features/
│   ├── CreateOrder/
│   │   ├── CreateOrderCommand.cs
│   │   ├── CreateOrderCommandHandler.cs
│   │   └── CreateOrderResult.cs
│   ├── GetOrderById/
│   │   ├── GetOrderByIdQuery.cs
│   │   └── GetOrderByIdQueryHandler.cs
│   └── CancelOrder/
│       ├── CancelOrderCommand.cs
│       └── CancelOrderCommandHandler.cs
├── IOrderRepository.cs                    (module-root, not in Features/)
├── Observability/
│   └── OrderMetrics.cs
└── OrdersModuleRegistration.cs

src/ProjectName.Orders.Abstractions/
└── DataTransferObjects/
    ├── OrderDetailDto.cs                  (shared via Abstractions; not inside feature folder)
    └── OrderLineDto.cs

src/ProjectName.Orders.Tests/
├── CreateOrder/
│   └── CreateOrderCommandHandlerTests.cs
├── GetOrderById/
│   └── GetOrderByIdQueryHandlerTests.cs
└── CancelOrder/
    └── CancelOrderCommandHandlerTests.cs
```

### Implementation Guidelines

#### 1. Handler Pattern (CQRS-aligned, per ADR 0004)
Each slice has a dedicated handler:

```csharp
namespace ProjectName.Orders.Features.CreateOrder;

public sealed record CreateOrderCommand(Guid CustomerId, IReadOnlyList<OrderLineDto> Lines);
public sealed record CreateOrderResult(Guid OrderId, decimal Total, DateTimeOffset CreatedAt);

public sealed class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, CreateOrderResult>
{
    private readonly IOrderRepository _orders;
    private readonly ICustomerRepository _customers;
    private readonly IClock _clock;
    private readonly ILogger<CreateOrderCommandHandler> _logger;

    public CreateOrderCommandHandler(
        IOrderRepository orders,
        ICustomerRepository customers,
        IClock clock,
        ILogger<CreateOrderCommandHandler> logger)
    {
        (_orders, _customers, _clock, _logger) = (orders, customers, clock, logger);
    }

    public async Task<CreateOrderResult> Handle(CreateOrderCommand command, CancellationToken ct)
    {
        // Guard clauses
        if (command.Lines.Count == 0)
            throw new ValidationException("Order must contain at least one line");

        // Verify customer exists
        var customer = await _customers.GetByIdAsync(command.CustomerId, ct)
            ?? throw new NotFoundException($"Customer {command.CustomerId} not found");

        // Create domain entity (business rules enforced here)
        var order = Order.Create(
            customer.Id,
            command.Lines.Select(l => new OrderLine(l.ProductId, l.Quantity, l.UnitPrice)),
            _clock.UtcNow);

        // Persist
        await _orders.AddAsync(order, ct);

        _logger.LogInformation("Order {OrderId} created for customer {CustomerId}", order.Id, customer.Id);

        return new CreateOrderResult(order.Id, order.Total, order.CreatedAt);
    }
}
```

#### 2. Request/Response Models
Use C# records for immutability and expressiveness.

**DTOs belong in the `.Abstractions` project** (`DataTransferObjects/` folder). This keeps contracts consumable by other modules and the API project without creating coupling to the module implementation. Commands, queries, and result types that are internal to a single handler may live inside the feature slice.

```csharp
// ProjectName.Orders.Abstractions / DataTransferObjects /
namespace ProjectName.Orders.Abstractions.DataTransferObjects;

public sealed record OrderLineDto(Guid ProductId, int Quantity, decimal UnitPrice);
public sealed record OrderDto(Guid OrderId, decimal Total, DateTimeOffset CreatedAt);
```

```csharp
// ProjectName.Orders / Features / CreateOrder /
namespace ProjectName.Orders.Features.CreateOrder;

// Command — internal to the module
public sealed record CreateOrderCommand(Guid CustomerId, IReadOnlyList<OrderLineDto> Lines);

// Result — returned to the API layer; use an Abstractions DTO if shared across modules
public sealed record CreateOrderResult(Guid OrderId, decimal Total, DateTimeOffset CreatedAt);
```

#### 3. Validation
Validation operates at two levels:

**Level 1 — Endpoint filter (shallow):** Validates the incoming DTO for required fields, format, and range constraints. Catches malformed requests early and returns `400 Bad Request`. Can use FluentValidation or manual checks. Cannot enforce business invariants.

**Level 2 — Domain model (authoritative):** The handler creates or loads the domain model via a factory (`Order.Create(...)`) or a behavior method (`customer.UpdateProfile(...)`). The domain model enforces invariants and throws `DomainException` on violations. This is the definitive validation layer.

```csharp
// Level 1 — Endpoint filter (shallow: checks format and presence only)
public sealed class CreateOrderRequestFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var req = context.GetArgument<CreateOrderRequest>(0);
        if (req.Lines is null || req.Lines.Count == 0)
            return Results.ValidationProblem(new Dictionary<string, string[]>
                { ["lines"] = ["At least one line is required."] });
        return await next(context);
    }
}

// Level 2 — Domain model (business rule enforcement inside handler)
var order = Order.Create(command.CustomerId, command.Lines); // throws DomainException if invariant violated
```

#### 4. Endpoint Mapping
Thin endpoints delegate to handlers:

```csharp
namespace ProjectName.Api.Endpoints;

public static class OrderEndpoints
{
    public static RouteGroupBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders").WithTags("Orders").WithOpenApi();

        group.MapPost("/", CreateOrder)
            .WithName("CreateOrder")
            .Produces<CreateOrderResult>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", GetOrderById)
            .WithName("GetOrderById")
            .Produces<OrderDetailDto>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}/cancel", CancelOrder)
            .WithName("CancelOrder")
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> CreateOrder(
        CreateOrderCommand command,
        ICommandHandler<CreateOrderCommand, CreateOrderResult> handler,
        CancellationToken ct)
    {
        var result = await handler.Handle(command, ct);
        return Results.Created($"/orders/{result.OrderId}", result);
    }

    private static async Task<IResult> GetOrderById(
        Guid id,
        IQueryHandler<GetOrderByIdQuery, OrderDetailDto> handler,
        CancellationToken ct)
    {
        var result = await handler.Handle(new GetOrderByIdQuery(id), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> CancelOrder(
        Guid id,
        ICommandHandler<CancelOrderCommand> handler,
        CancellationToken ct)
    {
        await handler.Handle(new CancelOrderCommand(id), ct);
        return Results.Accepted();
    }
}
```

#### 5. Testing Slices
Tests live in a dedicated `.Tests` project that mirrors the feature structure of the module (see ADR 0009). Use **xUnit**, **Moq**, and **Bogus**:

```csharp
namespace ProjectName.Orders.Tests.CreateOrder;

public sealed class CreateOrderCommandHandlerTests
{
    private readonly Mock<IOrderRepository> _mockOrderRepo;
    private readonly Mock<ICustomerRepository> _mockCustomerRepo;
    private readonly Mock<IClock> _mockClock;
    private readonly CreateOrderCommandHandler _handler;

    public CreateOrderCommandHandlerTests()
    {
        _mockOrderRepo = new Mock<IOrderRepository>();
        _mockCustomerRepo = new Mock<ICustomerRepository>();
        _mockClock = new Mock<IClock>();
        _handler = new CreateOrderCommandHandler(
            _mockOrderRepo.Object,
            _mockCustomerRepo.Object,
            _mockClock.Object,
            NullLogger<CreateOrderCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ShouldCreateOrder_WhenValidCommand()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        _mockCustomerRepo.Setup(x => x.GetByIdAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Customer(customerId, "John Doe"));
        _mockClock.Setup(x => x.UtcNow).Returns(DateTimeOffset.UtcNow);

        var command = new CreateOrderCommand(customerId, new[]
        {
            new OrderLineDto(Guid.NewGuid(), 2, 10.50m)
        });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(21.00m, result.Total);
        _mockOrderRepo.Verify(x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenCustomerDoesNotExist()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        _mockCustomerRepo.Setup(x => x.GetByIdAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Customer?)null);

        var command = new CreateOrderCommand(customerId, new[]
        {
            new OrderLineDto(Guid.NewGuid(), 1, 5.00m)
        });

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => _handler.Handle(command, CancellationToken.None));
    }
}
```

### Shared Code Guidelines

#### When to Share Across Slices
- **Abstractions/Ports**: `IRepository<T>`, `IUnitOfWork`, `IClock`, `IEmailSender` → module root or `Core/` project.
- **DTOs (Data Transfer Objects)**: All DTOs go in the `.Abstractions` project under `DataTransferObjects/`. This makes them available to API projects and other modules without coupling to the implementation.
- **Domain entities**: Shared across slices via `DomainModels/` in the module project.
- **Cross-cutting concerns**: Logging, exception handling, authorization → middleware or base classes in `Core/`.

#### When NOT to Share
- **Handler logic**: Each slice has its own handler; avoid "helper" handlers.
- **Validation rules**: Slice-specific validation stays within the slice.
- **Slice-specific result types**: `CreateOrderResult` used only by its handler can stay in the feature folder; promote to `Abstractions/DataTransferObjects/` once consumed by another module or the API project.

### Mediator Libraries (Optional)
Vertical slices work with or without mediator libraries (e.g., MediatR):

**Without Mediator** (Preferred for simplicity):
```csharp
// Direct DI registration per handler
builder.Services.AddScoped<ICommandHandler<CreateOrderCommand, CreateOrderResult>, CreateOrderCommandHandler>();
```

**With Mediator** (For larger projects with cross-cutting behaviors like logging, transactions):
```csharp
// MediatR discovers handlers automatically
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateOrderCommand).Assembly));

// Usage in endpoint
var result = await mediator.Send(new CreateOrderCommand(customerId, lines), ct);
```

Use mediator only if you need pipeline behaviors (logging, validation, transactions) applied uniformly. For most projects, direct handler registration is simpler and more explicit.

## Consequences

### Positive
1. **High cohesion**: All code for a feature lives together, improving readability and maintainability.
2. **Low coupling**: Slices are independent; changes to one feature rarely affect others.
3. **Faster development**: Developers work on isolated slices without stepping on each other's toes.
4. **Easy feature removal**: Delete the slice folder; no orphaned code in distant layers.
5. **Simplified testing**: Slice tests are co-located and test a complete vertical flow.
6. **Scalability**: New features are added as new slices without growing monolithic service classes.
7. **Clearer boundaries**: Aligns with DDD bounded contexts and Hexagonal Architecture use cases.
8. **Reduced merge conflicts**: Multiple developers can work on different slices simultaneously.

### Negative
1. **Initial learning curve**: Developers accustomed to layered architecture need adjustment.
2. **Potential duplication**: Similar logic (e.g., validation patterns) may appear in multiple slices; refactor to `Core/` when genuinely shared.
3. **Namespace proliferation**: Deeply nested namespaces (e.g., `ProjectName.Orders.Features.CreateOrder`) can feel verbose.
4. **Tooling challenges**: Some IDEs default to layered folder structures; teams must configure templates.
5. **Shared code ambiguity**: Developers may struggle to decide when code belongs in `Core/` vs. slice-specific.

### Mitigation Strategies
1. **Code reviews**: Ensure slices remain independent; flag unnecessary coupling.
2. **Refactoring cadence**: Periodically review slices for duplicated logic; extract to `Core/` when 3+ slices use the same pattern.
3. **Templates/scaffolding**: Provide Visual Studio/Rider templates for creating new slices (command, handler, validator, tests).
4. **Team training**: Conduct workshops on vertical slice principles and CQRS patterns.
5. **Documentation**: Maintain examples in this ADR and the `/structures/` folder.

## Alignment with Other ADRs
- **ADR 0002 (Modular Monolith)**: Vertical slices can be organized into modules (bounded contexts) at a higher level.
- **ADR 0004 (CQRS)**: Vertical slices naturally align with command/query handlers.
- **ADR 0005 (Minimal APIs)**: Thin endpoint mapping delegates to slice handlers.
- **ADR 0009 (Feature Slices Module Structure)**: For modular-monolith projects, ADR 0009 **supersedes** the physical project layout described in this ADR. ADR 0009 defines the authoritative folder structure within a module, including `Features/`, `DomainModels/`, `Observability/`, `Services/`, and the handler naming convention (`{Feature}CommandHandler` / `{Feature}QueryHandler`). Tests are in a **separate** `.Tests` project, not co-located.

## Examples

### Complete Slice: UpdateCustomerProfile

```
src/ProjectName.Customers/Features/UpdateCustomerProfile/
├── UpdateCustomerProfileCommand.cs
├── UpdateCustomerProfileCommandHandler.cs
└── UpdateCustomerProfileRequestFilter.cs

src/ProjectName.Customers.Tests/UpdateCustomerProfile/
└── UpdateCustomerProfileCommandHandlerTests.cs
```

**UpdateCustomerProfileCommand.cs:**
```csharp
namespace ProjectName.Customers.Features.UpdateCustomerProfile;

public sealed record UpdateCustomerProfileCommand(
    Guid CustomerId,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber);
```

**UpdateCustomerProfileCommandHandler.cs:**
```csharp
namespace ProjectName.Customers.Features.UpdateCustomerProfile;

public sealed class UpdateCustomerProfileCommandHandler : ICommandHandler<UpdateCustomerProfileCommand>
{
    private readonly ICustomerRepository _customers;
    private readonly ILogger<UpdateCustomerProfileCommandHandler> _logger;

    public UpdateCustomerProfileCommandHandler(ICustomerRepository customers, ILogger<UpdateCustomerProfileCommandHandler> logger)
        => (_customers, _logger) = (customers, logger);

    public async Task Handle(UpdateCustomerProfileCommand command, CancellationToken ct)
    {
        var customer = await _customers.GetByIdAsync(command.CustomerId, ct)
            ?? throw new NotFoundException($"Customer {command.CustomerId} not found");

        // Domain model enforces business rules — throws DomainException on invariant violations
        customer.UpdateProfile(command.FirstName, command.LastName, command.Email, command.PhoneNumber);

        await _customers.UpdateAsync(customer, ct);

        _logger.LogInformation("Customer {CustomerId} profile updated", customer.Id);
    }
}
```

**UpdateCustomerProfileRequestFilter.cs** (shallow endpoint-level validation):
```csharp
namespace ProjectName.Customers.Features.UpdateCustomerProfile;

public sealed class UpdateCustomerProfileRequestFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var req = context.GetArgument<UpdateCustomerProfileRequest>(0);
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(req.FirstName))
            errors["firstName"] = ["First name is required."];
        if (string.IsNullOrWhiteSpace(req.LastName))
            errors["lastName"] = ["Last name is required."];
        if (string.IsNullOrWhiteSpace(req.Email))
            errors["email"] = ["Email is required."];

        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        return await next(context);
    }
}
```

## Compliance & Review
- New features MUST be implemented as vertical slices, not added to existing service classes.
- Code reviews MUST verify slices are self-contained and do not introduce unnecessary cross-slice dependencies.
- Refactoring PRs that convert layered code to slices are encouraged but not mandatory for stable legacy code.
- Architecture reviews SHOULD periodically audit slice boundaries and shared code usage.

## References
- Vertical Slice Architecture: https://jimmybogard.com/vertical-slice-architecture/
- Feature Slices for ASP.NET Core: https://www.youtube.com/watch?v=SUiWfhAhgQw (Jimmy Bogard talk)
- CQRS and Vertical Slices: https://event-driven.io/en/slim_your_aggregates_with_event_sourcing/
- Clean Architecture (Robert C. Martin): Screaming Architecture chapter
- Domain-Driven Design (Eric Evans): Bounded Contexts
- ADR 0004: CQRS Recommendation for ASP.NET API Projects
- ADR 0005: Minimal APIs Over Controller-Based APIs
