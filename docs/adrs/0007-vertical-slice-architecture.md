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

#### Application Layer (Use Cases)
Organize by feature/capability, not by technical layer:

```
src/ProjectName.Application/
├── Orders/
│   ├── CreateOrder/
│   │   ├── CreateOrderCommand.cs          (Request model)
│   │   ├── CreateOrderHandler.cs          (Handler/orchestrator)
│   │   ├── CreateOrderValidator.cs        (Input validation)
│   │   ├── CreateOrderResult.cs           (Response DTO)
│   │   └── CreateOrderHandler.Tests.cs    (Unit tests)
│   ├── GetOrderById/
│   │   ├── GetOrderByIdQuery.cs
│   │   ├── GetOrderByIdHandler.cs
│   │   ├── OrderDetailDto.cs
│   │   └── GetOrderByIdHandler.Tests.cs
│   ├── CancelOrder/
│   │   ├── CancelOrderCommand.cs
│   │   ├── CancelOrderHandler.cs
│   │   └── CancelOrderHandler.Tests.cs
│   └── _Shared/                           (Slice-specific shared code)
│       ├── IOrderRepository.cs
│       └── OrderLineDto.cs
├── Customers/
│   ├── RegisterCustomer/
│   ├── UpdateCustomerProfile/
│   └── GetCustomerHistory/
└── _Common/                               (Cross-slice shared abstractions)
    ├── IUnitOfWork.cs
    ├── IClock.cs
    └── DomainException.cs
```

#### Domain Layer
May be organized by aggregate or subdomain (DDD-style), but handlers reference domain via interfaces:

```
src/ProjectName.Domain/
├── Orders/
│   ├── Order.cs
│   ├── OrderLine.cs
│   ├── OrderStatus.cs
│   └── IOrderRepository.cs
├── Customers/
│   ├── Customer.cs
│   └── ICustomerRepository.cs
└── Shared/
    ├── Entity.cs
    └── ValueObject.cs
```

#### Infrastructure/Adapters Layer
Implement repository contracts per aggregate, not per slice (repositories are shared infrastructure):

```
src/ProjectName.Infrastructure/
├── Persistence/
│   ├── OrderRepository.cs
│   ├── CustomerRepository.cs
│   └── DatabaseContext.cs
├── Messaging/
│   └── ServiceBusPublisher.cs
└── External/
    └── PaymentGatewayAdapter.cs
```

#### HTTP/Interface Layer
Endpoints map directly to slices:

```
src/ProjectName.Api/
├── Endpoints/
│   ├── OrderEndpoints.cs                  (All order-related endpoints)
│   ├── CustomerEndpoints.cs
│   └── HealthEndpoints.cs
├── Middleware/
│   └── ExceptionHandlerMiddleware.cs
└── Program.cs
```

### Implementation Guidelines

#### 1. Handler Pattern (CQRS-aligned, per ADR 0004)
Each slice has a dedicated handler:

```csharp
namespace ProjectName.Application.Orders.CreateOrder;

public sealed record CreateOrderCommand(Guid CustomerId, IReadOnlyList<OrderLineDto> Lines);
public sealed record CreateOrderResult(Guid OrderId, decimal Total, DateTimeOffset CreatedAt);

public sealed class CreateOrderHandler : ICommandHandler<CreateOrderCommand, CreateOrderResult>
{
    private readonly IOrderRepository _orders;
    private readonly ICustomerRepository _customers;
    private readonly IClock _clock;
    private readonly ILogger<CreateOrderHandler> _logger;

    public CreateOrderHandler(
        IOrderRepository orders,
        ICustomerRepository customers,
        IClock clock,
        ILogger<CreateOrderHandler> logger)
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
Use C# records for immutability and expressiveness:

```csharp
namespace ProjectName.Application.Orders.CreateOrder;

// Input DTO (from HTTP body)
public sealed record OrderLineDto(Guid ProductId, int Quantity, decimal UnitPrice);

// Command (internal application model)
public sealed record CreateOrderCommand(Guid CustomerId, IReadOnlyList<OrderLineDto> Lines);

// Result DTO (serialized to HTTP response)
public sealed record CreateOrderResult(Guid OrderId, decimal Total, DateTimeOffset CreatedAt);
```

#### 3. Validation
Keep validation close to the slice; use FluentValidation or manual guards:

```csharp
namespace ProjectName.Application.Orders.CreateOrder;

public sealed class CreateOrderValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Order must have at least one line");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.Quantity).GreaterThan(0);
            line.RuleFor(l => l.UnitPrice).GreaterThanOrEqualTo(0);
        });
    }
}
```

#### 4. Endpoint Mapping
Thin endpoints delegate to handlers:

```csharp
namespace ProjectName.Api.Endpoints;

public static class OrderEndpoints
{
    public static RouteGroupBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders").WithTags("Orders");

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
Each slice has co-located tests:

```csharp
namespace ProjectName.Application.Orders.CreateOrder;

public sealed class CreateOrderHandlerTests
{
    [Fact]
    public async Task Handle_ShouldCreateOrder_WhenValidCommand()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var orderRepo = Substitute.For<IOrderRepository>();
        var customerRepo = Substitute.For<ICustomerRepository>();
        customerRepo.GetByIdAsync(customerId, Arg.Any<CancellationToken>())
            .Returns(new Customer(customerId, "John Doe"));
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);

        var handler = new CreateOrderHandler(orderRepo, customerRepo, clock, NullLogger<CreateOrderHandler>.Instance);
        var command = new CreateOrderCommand(customerId, new[]
        {
            new OrderLineDto(Guid.NewGuid(), 2, 10.50m)
        });

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(21.00m);
        await orderRepo.Received(1).AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenCustomerDoesNotExist()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var customerRepo = Substitute.For<ICustomerRepository>();
        customerRepo.GetByIdAsync(customerId, Arg.Any<CancellationToken>())
            .Returns((Customer?)null);

        var handler = new CreateOrderHandler(
            Substitute.For<IOrderRepository>(),
            customerRepo,
            Substitute.For<IClock>(),
            NullLogger<CreateOrderHandler>.Instance);

        var command = new CreateOrderCommand(customerId, new[]
        {
            new OrderLineDto(Guid.NewGuid(), 1, 5.00m)
        });

        // Act & Assert
        await handler.Invoking(h => h.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>()
            .WithMessage($"Customer {customerId} not found");
    }
}
```

### Shared Code Guidelines

#### When to Share Across Slices
- **Abstractions/Ports**: `IRepository<T>`, `IUnitOfWork`, `IClock`, `IEmailSender` → `_Common/` folder.
- **Domain entities**: Shared across slices via domain layer (e.g., `Order`, `Customer`).
- **DTOs used by multiple slices**: Place in `_Shared/` subfolder within the relevant module (e.g., `Orders/_Shared/OrderLineDto.cs`).
- **Cross-cutting concerns**: Logging, exception handling, authorization → middleware or base classes in `_Common/`.

#### When NOT to Share
- **Handler logic**: Each slice has its own handler; avoid "helper" handlers.
- **Validation rules**: Slice-specific validation stays within the slice.
- **Slice-specific DTOs**: `CreateOrderResult` is not shared; `GetOrderByIdQuery` defines its own `OrderDetailDto` if structure differs.

### Mediator Libraries (Optional)
Vertical slices work with or without mediator libraries (e.g., MediatR):

**Without Mediator** (Preferred for simplicity):
```csharp
// Direct DI registration per handler
builder.Services.AddScoped<ICommandHandler<CreateOrderCommand, CreateOrderResult>, CreateOrderHandler>();
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
2. **Potential duplication**: Similar logic (e.g., validation patterns) may appear in multiple slices; refactor to `_Common/` when genuinely shared.
3. **Namespace proliferation**: Deeply nested namespaces (e.g., `ProjectName.Application.Orders.CreateOrder`) can feel verbose.
4. **Tooling challenges**: Some IDEs default to layered folder structures; teams must configure templates.
5. **Shared code ambiguity**: Developers may struggle to decide when code belongs in `_Common/` vs. slice-specific.

### Mitigation Strategies
1. **Code reviews**: Ensure slices remain independent; flag unnecessary coupling.
2. **Refactoring cadence**: Periodically review slices for duplicated logic; extract to `_Common/` when 3+ slices use the same pattern.
3. **Templates/scaffolding**: Provide Visual Studio/Rider templates for creating new slices (command, handler, validator, tests).
4. **Team training**: Conduct workshops on vertical slice principles and CQRS patterns.
5. **Documentation**: Maintain examples in this ADR and the `/structures/` folder.

## Alignment with Other ADRs
- **ADR 0002 (Modular Monolith)**: Vertical slices can be organized into modules (bounded contexts) at a higher level.
- **ADR 0004 (CQRS)**: Vertical slices naturally align with command/query handlers.
- **ADR 0005 (Minimal APIs)**: Thin endpoint mapping delegates to slice handlers.

## Examples

### Complete Slice: UpdateCustomerProfile

```
src/ProjectName.Application/Customers/UpdateCustomerProfile/
├── UpdateCustomerProfileCommand.cs
├── UpdateCustomerProfileHandler.cs
├── UpdateCustomerProfileValidator.cs
└── UpdateCustomerProfileHandler.Tests.cs
```

**UpdateCustomerProfileCommand.cs:**
```csharp
namespace ProjectName.Application.Customers.UpdateCustomerProfile;

public sealed record UpdateCustomerProfileCommand(
    Guid CustomerId,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber);
```

**UpdateCustomerProfileHandler.cs:**
```csharp
namespace ProjectName.Application.Customers.UpdateCustomerProfile;

public sealed class UpdateCustomerProfileHandler : ICommandHandler<UpdateCustomerProfileCommand>
{
    private readonly ICustomerRepository _customers;
    private readonly ILogger<UpdateCustomerProfileHandler> _logger;

    public UpdateCustomerProfileHandler(ICustomerRepository customers, ILogger<UpdateCustomerProfileHandler> logger)
        => (_customers, _logger) = (customers, logger);

    public async Task Handle(UpdateCustomerProfileCommand command, CancellationToken ct)
    {
        var customer = await _customers.GetByIdAsync(command.CustomerId, ct)
            ?? throw new NotFoundException($"Customer {command.CustomerId} not found");

        customer.UpdateProfile(command.FirstName, command.LastName, command.Email, command.PhoneNumber);

        await _customers.UpdateAsync(customer, ct);

        _logger.LogInformation("Customer {CustomerId} profile updated", customer.Id);
    }
}
```

**UpdateCustomerProfileValidator.cs:**
```csharp
namespace ProjectName.Application.Customers.UpdateCustomerProfile;

public sealed class UpdateCustomerProfileValidator : AbstractValidator<UpdateCustomerProfileCommand>
{
    public UpdateCustomerProfileValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.PhoneNumber).Matches(@"^\+?[1-9]\d{1,14}$").When(x => !string.IsNullOrEmpty(x.PhoneNumber));
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
