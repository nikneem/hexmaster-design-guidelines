# Pragmatic Domain-Driven Design Approach

**Status**: Proposed  
**Date**: 2025-11-12  
**Author**: Design Guidelines Team

## Overview

This document provides guidance on applying Domain-Driven Design (DDD) principles pragmatically in .NET applications. The focus is on maintaining domain validity through rich domain models while avoiding over-engineering with excessive value objects for primitive types.

## Problem Statement

Teams face challenges when applying DDD:
- **Under-application**: Anemic domain models with all logic in services, leading to scattered business rules and validation
- **Over-application**: Wrapping every primitive in a value object (e.g., `CustomerId`, `FirstName`, `Age`), resulting in verbose, hard-to-maintain code
- **Inconsistent state**: Direct entity manipulation allowing invalid domain states to be persisted
- **Unclear boundaries**: Business logic spread across controllers, services, and repositories

We need a balanced approach that maintains domain integrity without excessive ceremony.

## Core Principles

### 1. Domain Models Are the Source of Truth

**All business operations must go through the domain model.**

Domain entities are responsible for:
- Enforcing invariants
- Maintaining consistency
- Expressing business rules
- Protecting their internal state

```csharp
// ❌ BAD: Anemic model - no behavior
public class Order
{
    public Guid Id { get; set; }
    public OrderStatus Status { get; set; }
    public List<OrderLine> Lines { get; set; }
    public decimal Total { get; set; }
}

// ✅ GOOD: Rich domain model
public class Order
{
    public Guid Id { get; private set; }
    public OrderStatus Status { get; private set; }
    private readonly List<OrderLine> _lines = new();
    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();
    
    private Order() { } // For EF Core
    
    public static Order Create(Customer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);
        return new Order 
        { 
            Id = Guid.NewGuid(),
            Status = OrderStatus.Draft 
        };
    }
    
    public void AddLine(Product product, int quantity)
    {
        if (Status != OrderStatus.Draft)
            throw new DomainException("Cannot modify order after it has been submitted");
            
        if (quantity <= 0)
            throw new DomainException("Quantity must be positive");
            
        var existingLine = _lines.FirstOrDefault(l => l.ProductId == product.Id);
        if (existingLine is not null)
        {
            existingLine.IncreaseQuantity(quantity);
        }
        else
        {
            _lines.Add(OrderLine.Create(product, quantity));
        }
    }
    
    public void Submit()
    {
        if (!_lines.Any())
            throw new DomainException("Cannot submit empty order");
            
        if (Status != OrderStatus.Draft)
            throw new DomainException("Order has already been submitted");
            
        Status = OrderStatus.Submitted;
    }
    
    public decimal CalculateTotal() => _lines.Sum(l => l.LineTotal);
}
```

### 2. Command Handlers Orchestrate Domain Operations

**Command handlers coordinate domain model interactions but don't contain business logic.**

```csharp
// ✅ GOOD: Handler orchestrates, domain model enforces rules
public class SubmitOrderCommandHandler : IRequestHandler<SubmitOrderCommand, Result>
{
    private readonly IOrderRepository _orders;
    private readonly IUnitOfWork _unitOfWork;
    
    public SubmitOrderCommandHandler(IOrderRepository orders, IUnitOfWork unitOfWork)
    {
        _orders = orders;
        _unitOfWork = unitOfWork;
    }
    
    public async Task<Result> Handle(SubmitOrderCommand command, CancellationToken cancellationToken)
    {
        // Load aggregate root
        var order = await _orders.GetByIdAsync(command.OrderId, cancellationToken);
        if (order is null)
            return Result.NotFound("Order not found");
        
        // Domain model enforces business rules
        try
        {
            order.Submit(); // All validation happens here
        }
        catch (DomainException ex)
        {
            return Result.ValidationError(ex.Message);
        }
        
        // Persist valid state
        await _orders.UpdateAsync(order, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);
        
        return Result.Success();
    }
}
```

**Key responsibilities**:
- ✅ Load aggregates from repositories
- ✅ Call domain model methods
- ✅ Handle transaction boundaries
- ✅ Translate domain exceptions to command results
- ❌ **NO** business logic or validation
- ❌ **NO** direct property manipulation

### 3. Pragmatic Value Objects

**Use value objects when they add value, not for every primitive.**

#### When to Create Value Objects

✅ **Create value objects for**:

1. **Multi-field concepts**
   ```csharp
   public record Address(
       string Street,
       string City,
       string PostalCode,
       string Country)
   {
       public static Address Create(string street, string city, string postalCode, string country)
       {
           ArgumentException.ThrowIfNullOrWhiteSpace(street);
           ArgumentException.ThrowIfNullOrWhiteSpace(city);
           ArgumentException.ThrowIfNullOrWhiteSpace(postalCode);
           ArgumentException.ThrowIfNullOrWhiteSpace(country);
           
           if (!IsValidPostalCode(postalCode, country))
               throw new DomainException("Invalid postal code for country");
           
           return new Address(street, city, postalCode, country);
       }
       
       private static bool IsValidPostalCode(string postalCode, string country)
       {
           // Country-specific validation
           return country switch
           {
               "NL" => Regex.IsMatch(postalCode, @"^\d{4}\s?[A-Z]{2}$"),
               "US" => Regex.IsMatch(postalCode, @"^\d{5}(-\d{4})?$"),
               _ => true
           };
       }
   }
   ```

2. **Types with interdependent fields**
   ```csharp
   public record DateRange
   {
       public DateOnly StartDate { get; init; }
       public DateOnly EndDate { get; init; }
       
       private DateRange(DateOnly start, DateOnly end)
       {
           StartDate = start;
           EndDate = end;
       }
       
       public static DateRange Create(DateOnly start, DateOnly end)
       {
           if (end < start)
               throw new DomainException("End date must be after start date");
               
           return new DateRange(start, end);
       }
       
       public int DurationInDays => EndDate.DayNumber - StartDate.DayNumber;
       public bool Contains(DateOnly date) => date >= StartDate && date <= EndDate;
   }
   ```

3. **Types requiring format validation**
   ```csharp
   public record EmailAddress
   {
       public string Value { get; init; }
       
       private EmailAddress(string value) => Value = value;
       
       public static EmailAddress Create(string email)
       {
           ArgumentException.ThrowIfNullOrWhiteSpace(email);
           
           if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
               throw new DomainException("Invalid email format");
               
           return new EmailAddress(email.ToLowerInvariant());
       }
       
       public string Domain => Value.Split('@')[1];
   }
   ```

4. **Types with domain-specific behavior**
   ```csharp
   public record Money
   {
       public decimal Amount { get; init; }
       public string Currency { get; init; }
       
       private Money(decimal amount, string currency)
       {
           Amount = amount;
           Currency = currency;
       }
       
       public static Money Create(decimal amount, string currency)
       {
           if (amount < 0)
               throw new DomainException("Amount cannot be negative");
               
           if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
               throw new DomainException("Currency must be 3-letter ISO code");
               
           return new Money(Math.Round(amount, 2), currency.ToUpperInvariant());
       }
       
       public Money Add(Money other)
       {
           if (Currency != other.Currency)
               throw new DomainException("Cannot add money with different currencies");
               
           return Create(Amount + other.Amount, Currency);
       }
       
       public Money Multiply(decimal factor) => Create(Amount * factor, Currency);
   }
   ```

#### When NOT to Create Value Objects

❌ **Don't create value objects for**:

1. **Simple primitives without complex rules**
   ```csharp
   // ❌ OVERKILL
   public record FirstName(string Value);
   public record Age(int Value);
   public record CustomerId(Guid Value);
   
   // ✅ BETTER - Use primitives directly
   public class Person
   {
       public string FirstName { get; private set; }
       public int Age { get; private set; }
       public Guid CustomerId { get; private set; }
       
       public void UpdateName(string firstName)
       {
           ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
           FirstName = firstName;
       }
   }
   ```

2. **IDs and technical identifiers** (unless they encode business meaning)
   ```csharp
   // ❌ OVERKILL - IDs are just GUIDs
   public record OrderId(Guid Value);
   
   // ✅ BETTER
   public class Order
   {
       public Guid Id { get; private set; }
   }
   ```

3. **Simple validation without relationships**
   ```csharp
   // ❌ OVERKILL
   public record Quantity(int Value)
   {
       public static Quantity Create(int value)
       {
           if (value <= 0) throw new DomainException("Must be positive");
           return new Quantity(value);
       }
   }
   
   // ✅ BETTER - Validate in the entity
   public class OrderLine
   {
       public int Quantity { get; private set; }
       
       public void IncreaseQuantity(int amount)
       {
           if (amount <= 0)
               throw new DomainException("Amount must be positive");
           Quantity += amount;
       }
   }
   ```

### Decision Matrix: Value Object vs Primitive

| Scenario | Use Value Object? | Rationale |
|----------|-------------------|-----------|
| Email address | ✅ Yes | Format validation + domain behavior (extract domain) |
| First name | ❌ No | Simple string, basic null check sufficient |
| Money (amount + currency) | ✅ Yes | Multiple fields, interdependent validation, arithmetic operations |
| Age | ❌ No | Single number, simple range check |
| Address | ✅ Yes | Multiple related fields, country-specific validation |
| Order ID | ❌ No | Just a GUID, no special behavior |
| Date range | ✅ Yes | Two fields with relationship (end > start) |
| Product SKU | ⚠️ Maybe | If format rules exist (e.g., "ABC-123-XYZ") then yes |
| Quantity | ❌ No | Simple positive check, better in entity |
| Phone number | ⚠️ Maybe | If you need parsing/formatting per country, yes; otherwise no |

## Domain Model Patterns

### Aggregate Roots

**Aggregates enforce consistency boundaries.**

```csharp
public class Order // Aggregate Root
{
    public Guid Id { get; private set; }
    private readonly List<OrderLine> _lines = new();
    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();
    
    // Order controls its lines - no external manipulation
    public void AddLine(Product product, int quantity)
    {
        // Validation and business rules here
        _lines.Add(OrderLine.Create(this, product, quantity));
    }
    
    public void RemoveLine(Guid lineId)
    {
        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line is not null)
            _lines.Remove(line);
    }
}

public class OrderLine // Entity, part of Order aggregate
{
    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    
    internal static OrderLine Create(Order order, Product product, int quantity)
    {
        ArgumentNullException.ThrowIfNull(product);
        if (quantity <= 0)
            throw new DomainException("Quantity must be positive");
            
        return new OrderLine
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            UnitPrice = product.Price,
            Quantity = quantity
        };
    }
    
    public decimal LineTotal => UnitPrice * Quantity;
}
```

**Rules**:
- ✅ External code only references aggregate roots
- ✅ Child entities created through aggregate root methods
- ✅ Child entities use `internal` or `private` constructors
- ❌ Never expose `List<T>` directly - use `IReadOnlyList<T>`

### Domain Events

**Emit events for significant domain occurrences.**

```csharp
public abstract class DomainEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

public record OrderSubmittedEvent(Guid OrderId, Guid CustomerId, decimal Total) : DomainEvent;

public class Order
{
    private readonly List<DomainEvent> _domainEvents = new();
    public IReadOnlyList<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    
    public void Submit()
    {
        // ... validation ...
        
        Status = OrderStatus.Submitted;
        _domainEvents.Add(new OrderSubmittedEvent(Id, CustomerId, CalculateTotal()));
    }
    
    public void ClearDomainEvents() => _domainEvents.Clear();
}
```

### Specifications

**Use specifications for complex queries.**

```csharp
public abstract class Specification<T>
{
    public abstract Expression<Func<T, bool>> ToExpression();
    
    public bool IsSatisfiedBy(T entity) => ToExpression().Compile()(entity);
}

public class ActiveOrdersForCustomerSpec : Specification<Order>
{
    private readonly Guid _customerId;
    
    public ActiveOrdersForCustomerSpec(Guid customerId) => _customerId = customerId;
    
    public override Expression<Func<Order, bool>> ToExpression()
    {
        return order => order.CustomerId == _customerId 
                     && order.Status != OrderStatus.Cancelled 
                     && order.Status != OrderStatus.Completed;
    }
}

// Usage
var spec = new ActiveOrdersForCustomerSpec(customerId);
var orders = await _repository.FindAsync(spec, cancellationToken);
```

## Repository Pattern

**Repositories work with aggregate roots only.**

```csharp
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Order>> FindAsync(Specification<Order> spec, CancellationToken cancellationToken = default);
    Task<Guid> AddAsync(Order order, CancellationToken cancellationToken = default);
    Task UpdateAsync(Order order, CancellationToken cancellationToken = default);
}

// ❌ BAD - Don't create repositories for child entities
public interface IOrderLineRepository { } // NO!

// ✅ GOOD - Access children through aggregate root
var order = await _orderRepository.GetByIdAsync(orderId);
var line = order.Lines.FirstOrDefault(l => l.Id == lineId);
```

## Command Handler Pattern

**Handlers ensure valid state transitions.**

```csharp
public record AddOrderLineCommand(Guid OrderId, Guid ProductId, int Quantity) : IRequest<Result<Guid>>;

public class AddOrderLineCommandHandler : IRequestHandler<AddOrderLineCommand, Result<Guid>>
{
    private readonly IOrderRepository _orders;
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _unitOfWork;
    
    public async Task<Result<Guid>> Handle(AddOrderLineCommand command, CancellationToken ct)
    {
        // 1. Load aggregates
        var order = await _orders.GetByIdAsync(command.OrderId, ct);
        if (order is null)
            return Result.NotFound<Guid>("Order not found");
            
        var product = await _products.GetByIdAsync(command.ProductId, ct);
        if (product is null)
            return Result.NotFound<Guid>("Product not found");
        
        // 2. Execute domain logic (all validation inside domain model)
        try
        {
            var lineId = order.AddLine(product, command.Quantity);
        }
        catch (DomainException ex)
        {
            return Result.ValidationError<Guid>(ex.Message);
        }
        
        // 3. Persist valid state
        await _orders.UpdateAsync(order, ct);
        await _unitOfWork.CommitAsync(ct);
        
        // 4. Return result
        return Result.Success(lineId);
    }
}
```

**Handler checklist**:
- ✅ Loads entities via repositories
- ✅ Calls domain model methods
- ✅ Catches and translates `DomainException`
- ✅ Manages transaction via `IUnitOfWork`
- ✅ Returns strongly-typed results
- ❌ Contains NO business logic
- ❌ Doesn't manipulate entity properties directly

## Validation Strategy

### Domain Validation (Required)

**Always validate in the domain model.**

```csharp
public class Order
{
    public void Submit()
    {
        if (!_lines.Any())
            throw new DomainException("Cannot submit order without lines");
            
        if (Status != OrderStatus.Draft)
            throw new DomainException($"Cannot submit order in {Status} status");
            
        Status = OrderStatus.Submitted;
    }
}
```

### Input Validation (Recommended)

**Validate DTOs/commands before reaching the domain.**

```csharp
public record AddOrderLineCommand(Guid OrderId, Guid ProductId, int Quantity) : IRequest<Result>;

public class AddOrderLineValidator : AbstractValidator<AddOrderLineCommand>
{
    public AddOrderLineValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(1000);
    }
}
```

**Two-layer validation**:
1. **Input validation** (FluentValidation): Basic format, required fields, ranges
2. **Domain validation** (Domain model): Business rules, state consistency, invariants

## EF Core Integration

**Map value objects correctly.**

```csharp
public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.Id);
        
        // Owned entity for value object
        builder.OwnsOne(o => o.ShippingAddress, address =>
        {
            address.Property(a => a.Street).HasColumnName("ShippingStreet").IsRequired();
            address.Property(a => a.City).HasColumnName("ShippingCity").IsRequired();
            address.Property(a => a.PostalCode).HasColumnName("ShippingPostalCode").IsRequired();
            address.Property(a => a.Country).HasColumnName("ShippingCountry").IsRequired();
        });
        
        // Money value object
        builder.OwnsOne(o => o.TotalAmount, money =>
        {
            money.Property(m => m.Amount).HasColumnName("TotalAmount").HasPrecision(18, 2);
            money.Property(m => m.Currency).HasColumnName("Currency").HasMaxLength(3);
        });
        
        // Collection
        builder.HasMany(o => o.Lines)
            .WithOne()
            .HasForeignKey("OrderId")
            .OnDelete(DeleteBehavior.Cascade);
            
        // Ignore domain events
        builder.Ignore(o => o.DomainEvents);
    }
}
```

## Anti-Patterns to Avoid

### 1. Anemic Domain Model

```csharp
// ❌ BAD - All logic in service
public class OrderService
{
    public async Task SubmitOrder(Guid orderId)
    {
        var order = await _repo.GetByIdAsync(orderId);
        
        if (order.Lines.Count == 0)
            throw new Exception("Empty order");
            
        if (order.Status != OrderStatus.Draft)
            throw new Exception("Already submitted");
            
        order.Status = OrderStatus.Submitted; // Direct manipulation
        await _repo.UpdateAsync(order);
    }
}

// ✅ GOOD - Logic in domain
public class Order
{
    public void Submit()
    {
        if (!_lines.Any())
            throw new DomainException("Cannot submit empty order");
            
        if (Status != OrderStatus.Draft)
            throw new DomainException("Order already submitted");
            
        Status = OrderStatus.Submitted;
    }
}
```

### 2. Primitive Obsession (Over-correction)

```csharp
// ❌ TOO MUCH - Value objects for everything
public record CustomerId(Guid Value);
public record OrderId(Guid Value);
public record FirstName(string Value);
public record LastName(string Value);
public record Age(int Value);

// Usage becomes verbose
var customer = new Customer(
    new CustomerId(Guid.NewGuid()),
    new FirstName("John"),
    new LastName("Doe"),
    new Age(30)
);

// ✅ BALANCED - Primitives where appropriate
public class Customer
{
    public Guid Id { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public int Age { get; private set; }
    public EmailAddress Email { get; private set; } // Value object where it adds value
}
```

### 3. Public Setters

```csharp
// ❌ BAD - Public setters allow invalid state
public class Order
{
    public Guid Id { get; set; }
    public OrderStatus Status { get; set; }
    public List<OrderLine> Lines { get; set; }
}

// Anyone can do this:
order.Status = OrderStatus.Completed; // Bypasses business rules!
order.Lines.Add(invalidLine); // No validation!

// ✅ GOOD - Encapsulation
public class Order
{
    public Guid Id { get; private set; }
    public OrderStatus Status { get; private set; }
    private readonly List<OrderLine> _lines = new();
    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();
    
    public void Complete()
    {
        // Business rules enforced
        if (Status != OrderStatus.Submitted)
            throw new DomainException("Can only complete submitted orders");
        Status = OrderStatus.Completed;
    }
}
```

### 4. Leaky Persistence

```csharp
// ❌ BAD - EF Core attributes in domain model
[Table("Orders")]
public class Order
{
    [Key]
    public Guid Id { get; set; }
    
    [Column("OrderStatus")]
    public OrderStatus Status { get; set; }
}

// ✅ GOOD - Fluent configuration separate
public class Order
{
    public Guid Id { get; private set; }
    public OrderStatus Status { get; private set; }
}

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Status).HasColumnName("OrderStatus");
    }
}
```

## Testing Domain Models

```csharp
public class OrderTests
{
    [Fact]
    public void Submit_WithEmptyLines_ThrowsDomainException()
    {
        // Arrange
        var customer = Customer.Create("John", "Doe");
        var order = Order.Create(customer);
        
        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => order.Submit());
        Assert.Contains("empty", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
    
    [Fact]
    public void Submit_ValidOrder_ChangesStatus()
    {
        // Arrange
        var customer = Customer.Create("John", "Doe");
        var order = Order.Create(customer);
        var product = Product.Create("Widget", 10.0m);
        order.AddLine(product, 2);
        
        // Act
        order.Submit();
        
        // Assert
        Assert.Equal(OrderStatus.Submitted, order.Status);
    }
}
```

## Guidelines Summary

### ✅ DO

- Create rich domain models with behavior
- Use private setters and readonly collections
- Enforce invariants in domain entities
- Create value objects for multi-field concepts or interdependent validation
- Use command handlers to orchestrate domain operations
- Validate in both input layer and domain layer
- Emit domain events for significant occurrences
- Use repositories for aggregate roots only
- Test domain logic thoroughly

### ❌ DON'T

- Create anemic models with only getters/setters
- Use public setters on domain entities
- Create value objects for every primitive type
- Put business logic in command handlers or services
- Manipulate entity properties directly from handlers
- Create repositories for child entities
- Use EF Core attributes in domain models
- Allow invalid domain states to exist

## Migration Strategy

### From Anemic to Rich Domain Model

1. **Identify business rules** scattered in services
2. **Move validation** into entity methods
3. **Add factory methods** for entity creation
4. **Protect state** with private setters
5. **Encapsulate collections** with readonly wrappers
6. **Create methods** for state transitions
7. **Update handlers** to call domain methods
8. **Add tests** for domain logic

## Related Documents

- **ADR 0002**: Modular Monolith Project Structure
- **ADR 0004**: CQRS Recommendation for ASP.NET API
- **Design**: Modular Solution Structure
- **Recommendation**: Unit Testing with xUnit, Moq, Bogus

## Conclusion

Pragmatic DDD means:
- ✅ Rich domain models that protect their invariants
- ✅ Command handlers that orchestrate but don't contain logic
- ✅ Value objects where they provide real value
- ❌ Not wrapping every primitive in a value object
- ❌ Not creating artificial complexity

**Goal**: Maintain domain validity through expressive, testable domain models without ceremony.
