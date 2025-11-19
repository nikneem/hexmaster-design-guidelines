---
title: "Modular Solution Structure Design"
date: 2025-11-12
status: Accepted
tags: [design, modular, architecture, aspire]
---
# Modular Solution Structure Design

**Status**: Proposed  
**Date**: 2025-11-12  
**Author**: Design Guidelines Team

## Overview

This document defines the recommended solution and filesystem structure for .NET projects, particularly for web-enabled applications (Web APIs, Web Apps). The structure emphasizes modularity, domain-driven organization, and clear separation of concerns.

## Problem Statement

Teams need consistent guidance on:
- How to organize solutions with multiple modules or domains
- Where to place Aspire orchestration projects
- How to structure domain-specific code and dependencies
- Naming conventions for projects across different layers
- When and how to split modules into independent services

Without clear structure, solutions become difficult to navigate, maintain, and scale as the codebase grows.

## Proposed Solution Structure

### High-Level Organization

```
solution-root/
├── src/
│   ├── Aspire/                                    # Orchestration projects (web-enabled only)
│   │   ├── Company.Product.AppHost/
│   │   └── Company.Product.ServiceDefaults/
│   ├── ModuleName/                                # Domain/Module folders
│   │   ├── Company.Product.ModuleName/
│   │   ├── Company.Product.ModuleName.Abstractions/
│   │   ├── Company.Product.ModuleName.Data.{StorageType}/
│   │   ├── Company.Product.ModuleName.Api/        # Optional: Independent API
│   │   └── Company.Product.ModuleName.Tests/
│   └── SharedKernel/                              # Optional: Cross-cutting concerns
│       ├── Company.Product.SharedKernel/
│       └── Company.Product.SharedKernel.Abstractions/
├── tests/                                         # Optional: Integration/E2E tests
│   └── Company.Product.IntegrationTests/
└── Company.Product.sln
```

### Example: Multi-Module Solution

For a product called "Webshop" by company "HexMaster" with Inventory, Users, and Catalog modules:

```
HexMaster.Webshop/
├── src/
│   ├── Aspire/
│   │   ├── HexMaster.Webshop.AppHost/
│   │   └── HexMaster.Webshop.ServiceDefaults/
│   ├── Inventory/
│   │   ├── HexMaster.Webshop.Inventory/
│   │   ├── HexMaster.Webshop.Inventory.Abstractions/
│   │   ├── HexMaster.Webshop.Inventory.Data.SqlServer/
│   │   └── HexMaster.Webshop.Inventory.Tests/
│   ├── Users/
│   │   ├── HexMaster.Webshop.Users/
│   │   ├── HexMaster.Webshop.Users.Abstractions/
│   │   ├── HexMaster.Webshop.Users.Data.CosmosDb/
│   │   ├── HexMaster.Webshop.Users.Api/
│   │   └── HexMaster.Webshop.Users.Tests/
│   └── Catalog/
│       ├── HexMaster.Webshop.Catalog/
│       ├── HexMaster.Webshop.Catalog.Abstractions/
│       ├── HexMaster.Webshop.Catalog.Data.MongoDb/
│       └── HexMaster.Webshop.Catalog.Tests/
└── HexMaster.Webshop.sln
```

## Detailed Component Guidelines

### 1. Aspire Folder (Web-Enabled Projects Only)

**When to use**: Projects exposing HTTP endpoints (Web APIs, Web Apps, gRPC services)

**Contents**:
- **`Company.Product.AppHost`**: Aspire orchestration project that defines service topology, dependencies, and local development environment
- **`Company.Product.ServiceDefaults`**: Shared configurations for observability, health checks, service discovery, and common middleware

**Purpose**: Centralizes distributed application orchestration and shared service configurations.

**Reference**: See ADR 0003 for detailed Aspire adoption guidance.

### 2. Module/Domain Folders

Each business domain or bounded context gets its own folder containing related projects.

#### Core Library: `Company.Product.ModuleName`

**Purpose**: Contains domain logic, business rules, and application services.

**Contents**:
- Domain entities and value objects
- Domain services
- Application use cases/handlers (CQRS commands/queries)
- Validators and business rules
- Internal implementations

**Dependencies**: 
- May reference `Company.Product.ModuleName.Abstractions`
- Should NOT reference other modules directly (use abstractions)

**Example**:
```
HexMaster.Webshop.Persons/
├── Entities/
│   ├── Person.cs
│   └── Address.cs
├── Services/
│   └── PersonService.cs
├── Commands/
│   └── CreatePersonCommand.cs
└── Validators/
    └── PersonValidator.cs
```

#### Abstractions Library: `Company.Product.ModuleName.Abstractions`

**Purpose**: Defines contracts that other modules can depend on without circular references.

**Contents**:
- DTOs as C# `record` types
- Repository interfaces (`IPersonRepository`)
- Service interfaces (`IPersonService`)
- Event definitions
- Shared enums and constants

**Dependencies**: Minimal - only framework dependencies and shared kernel abstractions

**Why separate abstractions**:
- Enables other modules to depend on contracts without implementation
- Reduces coupling between modules
- Facilitates testing with mocks/stubs
- Supports plugin architectures

**Example**:
```csharp
namespace HexMaster.Webshop.Persons.Abstractions;

// DTO
public record PersonDto(Guid Id, string FirstName, string LastName, string Email);

// Repository interface
public interface IPersonRepository
{
    Task<PersonDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Guid> CreateAsync(PersonDto person, CancellationToken cancellationToken);
}

// Service interface
public interface IPersonService
{
    Task<PersonDto?> GetPersonAsync(Guid id);
    Task<Guid> CreatePersonAsync(PersonDto person);
}
```

#### Data Access: `Company.Product.ModuleName.Data.{StorageType}`

**When to use**: Module requires persistent storage

**Naming convention**: `{StorageType}` replaced with actual storage technology:
- `SqlServer`
- `PostgreSQL`
- `MongoDb`
- `CosmosDb`
- `Redis`
- `TableStorage`
- `Blob` (for blob/file storage)

**Contents**:
- Repository implementations
- Entity configurations (EF Core)
- Database context
- Migrations
- Data access utilities

**Dependencies**:
- References `Company.Product.ModuleName.Abstractions` (implements interfaces)
- Storage-specific NuGet packages (Npgsql, MongoDB.Driver, etc.)

**Example**:
```csharp
namespace HexMaster.Webshop.Persons.Data.SqlServer;

public class PersonRepository : IPersonRepository
{
    private readonly PersonDbContext _context;
    
    public PersonRepository(PersonDbContext context)
    {
        _context = context;
    }
    
    public async Task<PersonDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _context.Persons.FindAsync(new object[] { id }, cancellationToken);
        return entity is null ? null : MapToDto(entity);
    }
}
```

#### Tests: `Company.Product.ModuleName.Tests`

**Mandatory**: Every module MUST have unit tests

**Framework**: xUnit (see recommendations/unit-testing-xunit-moq-bogus.md)

**Contents**:
- Unit tests for domain logic
- Service tests
- Validator tests
- Test fixtures and helpers

**Naming convention**: `{ClassUnderTest}Tests.cs`

**Coverage requirement**: Minimum 80% line coverage (see ADR 0001)

**Example**:
```csharp
namespace HexMaster.Webshop.Persons.Tests;

public class PersonServiceTests
{
    [Fact]
    public async Task CreatePerson_WithValidData_ReturnsPersonId()
    {
        // Arrange
        var repository = Substitute.For<IPersonRepository>();
        var service = new PersonService(repository);
        var dto = new PersonDto(Guid.Empty, "John", "Doe", "john@example.com");
        
        // Act
        var result = await service.CreatePersonAsync(dto);
        
        // Assert
        Assert.NotEqual(Guid.Empty, result);
    }
}
```

#### Independent API: `Company.Product.ModuleName.Api` (Optional)

**When to use**: Module should be deployable as independent service

**Purpose**: Provides HTTP/gRPC API for the module

**Contents**:
- Minimal API endpoints or controllers
- API-specific middleware
- Swagger/OpenAPI configuration
- Health checks
- Program.cs

**Characteristics of independent module**:
- Can be deployed separately
- Has its own database/storage
- Communicates with other modules via events or HTTP
- Fully autonomous lifecycle

**Example structure**:
```
HexMaster.Webshop.Users.Api/
├── Endpoints/
│   ├── UserEndpoints.cs
│   └── AuthEndpoints.cs
├── Middleware/
│   └── ApiKeyMiddleware.cs
├── Program.cs
└── appsettings.json
```

**Example endpoint**:
```csharp
public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/users").WithTags("Users");
        
        group.MapGet("/{id:guid}", GetUserAsync)
            .WithName("GetUser")
            .Produces<UserDto>()
            .Produces(404);
            
        group.MapPost("/", CreateUserAsync)
            .WithName("CreateUser")
            .Produces<Guid>(201);
    }
}
```

## Naming Conventions

### Project Names

**Pattern**: `{Company}.{Product}.{Module}[.{Layer}][.{Technology}]`

**Components**:
- `Company`: Organization name (e.g., HexMaster, Contoso, HexMaster)
- `Product`: Product/solution name (e.g., Webshop, Ordering, Catalog)
- `Module`: Domain/module name (e.g., Persons, Inventory, Shipping)
- `Layer`: Optional layer designation (Abstractions, Data, Api)
- `Technology`: For data projects, the storage type (SqlServer, MongoDb)

**Examples**:
- ✅ `HexMaster.Webshop.Persons`
- ✅ `HexMaster.Webshop.Persons.Abstractions`
- ✅ `HexMaster.Webshop.Persons.Data.PostgreSQL`
- ✅ `HexMaster.Webshop.Persons.Api`
- ✅ `HexMaster.Webshop.Persons.Tests`
- ❌ `Persons` (too generic)
- ❌ `HexMaster.Webshop.PersonsAbstractions` (missing dot separator)
- ❌ `HexMaster.Webshop.Persons.Postgres` (use PostgreSQL for consistency)

### Folder Names

**Pattern**: Use module name without company/product prefix

**Examples**:
- ✅ `Persons/` (not `HexMaster.Webshop.Persons/`)
- ✅ `Inventory/`
- ✅ `Aspire/`
- ✅ `SharedKernel/`

## Dependency Rules

### Allowed Dependencies

```
Aspire Projects
  ↓ (can orchestrate)
Module APIs
  ↓ (can reference)
Module Core Libraries
  ↓ (can reference)
Module Abstractions ← Module Data Projects
  ↓ (can reference)
Shared Kernel
```

### Forbidden Dependencies

❌ Module Core → Other Module Core (use abstractions instead)  
❌ Module Abstractions → Module Core (circular dependency)  
❌ Module Tests → Other Module Core (test only your module)  
❌ Data Projects → Module Core (should only reference abstractions)

## Decision Guidelines

### When to Create a Separate Module

Create a new module folder when:
- ✅ Represents a distinct bounded context or domain
- ✅ Has its own data model and business rules
- ✅ Could potentially become an independent service
- ✅ Managed by a different team or sub-team
- ✅ Has different scaling or deployment requirements

### When to Use Abstractions Project

Create a separate abstractions project when:
- ✅ Other modules need to call your module's services
- ✅ You want to enable testing without concrete implementations
- ✅ You're building a plugin/extensibility system
- ✅ Multiple data implementations exist (e.g., SQL + NoSQL)

Skip abstractions if:
- ❌ Module is completely isolated with no external consumers
- ❌ Only used internally within a single bounded context

### When to Create Independent API

Create a separate API project when:
- ✅ Module needs to scale independently
- ✅ Module deployed to different infrastructure
- ✅ Module owned by separate team with independent release cycle
- ✅ Module communicates with other modules via events or async messaging
- ✅ Building microservices architecture

Use modular monolith (single API) when:
- ❌ Modules share the same deployment cadence
- ❌ Team is small or just starting
- ❌ No clear need for independent scaling
- ❌ Cross-module transactions are common

## Migration Path

### From Monolith to Modular Monolith

1. Create module folders for existing features
2. Extract abstractions into separate projects
3. Move domain logic into module core libraries
4. Create data projects for each storage boundary
5. Update dependency references

### From Modular Monolith to Microservices

1. Ensure module has its own data project (no shared database)
2. Create `Module.Api` project with HTTP endpoints
3. Replace direct references with HTTP clients or messaging
4. Move module to separate deployment pipeline
5. Update Aspire orchestration configuration

## Tools and Automation

### Solution File Organization

Use solution folders to organize projects:

```
Solution 'HexMaster.Webshop'
├── src
│   ├── Aspire
│   │   ├── HexMaster.Webshop.AppHost
│   │   └── HexMaster.Webshop.ServiceDefaults
│   ├── Inventory
│   │   ├── HexMaster.Webshop.Inventory
│   │   ├── HexMaster.Webshop.Inventory.Abstractions
│   │   └── HexMaster.Webshop.Inventory.Tests
│   └── Users
│       ├── HexMaster.Webshop.Users
│       └── HexMaster.Webshop.Users.Tests
└── tests
    └── HexMaster.Webshop.IntegrationTests
```

### Directory.Build.props

Place at solution root to enforce consistency:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  
  <PropertyGroup>
    <Company>HexMaster</Company>
    <Product>Webshop</Product>
    <Copyright>Copyright © HexMaster 2025</Copyright>
  </PropertyGroup>
</Project>
```

## Anti-Patterns to Avoid

❌ **Kitchen Sink Module**: Single massive module containing multiple domains  
❌ **Shared Data Project**: One data project accessing multiple module databases  
❌ **Circular Dependencies**: Module A depends on Module B which depends on Module A  
❌ **Leaky Abstractions**: Exposing EF Core entities or storage-specific types in abstractions  
❌ **God Abstractions Project**: Single abstractions project for entire solution  
❌ **Mixed Responsibilities**: Business logic in API controllers or data projects  
❌ **Missing Tests**: Modules without corresponding test projects

## Examples

### Small Project (Modular Monolith)

```
Contoso.Shop/
├── src/
│   ├── Aspire/
│   │   ├── Contoso.Shop.AppHost/
│   │   └── Contoso.Shop.ServiceDefaults/
│   ├── Catalog/
│   │   ├── Contoso.Shop.Catalog/
│   │   ├── Contoso.Shop.Catalog.Abstractions/
│   │   ├── Contoso.Shop.Catalog.Data.SqlServer/
│   │   └── Contoso.Shop.Catalog.Tests/
│   └── Orders/
│       ├── Contoso.Shop.Orders/
│       ├── Contoso.Shop.Orders.Abstractions/
│       ├── Contoso.Shop.Orders.Data.SqlServer/
│       └── Contoso.Shop.Orders.Tests/
└── Contoso.Shop.sln
```

### Large Project (Microservices)

```
Contoso.Ecommerce/
├── src/
│   ├── Aspire/
│   │   ├── Contoso.Ecommerce.AppHost/
│   │   └── Contoso.Ecommerce.ServiceDefaults/
│   ├── Catalog/
│   │   ├── Contoso.Ecommerce.Catalog/
│   │   ├── Contoso.Ecommerce.Catalog.Abstractions/
│   │   ├── Contoso.Ecommerce.Catalog.Data.MongoDb/
│   │   ├── Contoso.Ecommerce.Catalog.Api/
│   │   └── Contoso.Ecommerce.Catalog.Tests/
│   ├── Orders/
│   │   ├── Contoso.Ecommerce.Orders/
│   │   ├── Contoso.Ecommerce.Orders.Abstractions/
│   │   ├── Contoso.Ecommerce.Orders.Data.SqlServer/
│   │   ├── Contoso.Ecommerce.Orders.Api/
│   │   └── Contoso.Ecommerce.Orders.Tests/
│   ├── Payments/
│   │   ├── Contoso.Ecommerce.Payments/
│   │   ├── Contoso.Ecommerce.Payments.Abstractions/
│   │   ├── Contoso.Ecommerce.Payments.Data.CosmosDb/
│   │   ├── Contoso.Ecommerce.Payments.Api/
│   │   └── Contoso.Ecommerce.Payments.Tests/
│   └── SharedKernel/
│       ├── Contoso.Ecommerce.SharedKernel/
│       └── Contoso.Ecommerce.SharedKernel.Abstractions/
├── tests/
│   ├── Contoso.Ecommerce.IntegrationTests/
│   └── Contoso.Ecommerce.E2ETests/
└── Contoso.Ecommerce.sln
```

## Related Documents

- **ADR 0002**: Modular Monolith Project Structure
- **ADR 0003**: Recommend Aspire for ASP.NET Projects
- **ADR 0004**: CQRS Recommendation for ASP.NET API
- **Recommendation**: Unit Testing with xUnit, Moq, Bogus

## Summary

This structure provides:
- ✅ Clear separation of concerns
- ✅ Explicit dependencies via abstractions
- ✅ Testability with xUnit
- ✅ Flexibility to evolve from monolith to microservices
- ✅ Consistent naming across teams
- ✅ Technology-agnostic module core
- ✅ Aspire integration for modern distributed apps

Follow these guidelines to create maintainable, scalable, and well-organized .NET solutions.
