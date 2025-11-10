# Recommendation: Unit Testing with xUnit, Moq, and Bogus
Date: 2025-11-10
Status: Stable

## Purpose
Provide consistent, lightweight, and capable unit testing guidance for .NET 9 C# projects in this repository and projects following these guidelines.

## Recommendation
- Test framework: Use xUnit for all unit test projects.
- Mocking: You may depend on Moq for mocking interfaces and collaborators.
- Test data: Use Bogus to generate realistic fake data. Prefer deterministic seeds in tests that assert on generated values.
- Data factories: Encapsulate test object creation behind factory methods or builder classes. Configure factories to use Bogus internally for object graphs and random-but-plausible defaults.
- Coverage: Maintain ≥80% unit test coverage for Core and Server (as per Testing Strategy). Enforce via CI with coverlet collector or equivalent.
- Dependency hygiene: Avoid adding other test libraries without explicit approval via an ADR or issue/PR discussion. Keep the test stack minimal and standard across modules.

## Rationale
- xUnit integrates well with .NET tooling, offers parallelization controls, and a simple attribute model.
- Moq is widely used, ergonomic for interface mocking, and reduces brittle hand-written fakes when interaction behavior matters.
- Bogus produces realistic values and can reduce duplication in test data setup. Centralized factories improve readability and encourage reuse.

## Usage Patterns

### Project setup (example)
- Test project targets net9.0
- References: Microsoft.NET.Test.Sdk, xunit, xunit.runner.visualstudio (PrivateAssets=all), coverlet.collector (PrivateAssets=all)
- Optionally reference Moq and Bogus as needed by the tests.

### Factory pattern (recommended)
Place factories under `Tests/Factories` or similar.

```csharp
public static class OrderFactory
{
    private static readonly Faker faker = new();

    public static Order Create(
        Guid? id = null,
        Customer? customer = null,
        IReadOnlyList<OrderLine>? lines = null)
    {
        var cust = customer ?? new Customer(faker.Random.Guid(), faker.Person.FullName);
        var orderLines = lines ?? Enumerable.Range(0, faker.Random.Int(1, 3))
            .Select(_ => new OrderLine(faker.Commerce.Ean13(), faker.Random.Int(1,5)))
            .ToList();
        return Order.Create(id ?? Guid.NewGuid(), cust, orderLines);
    }
}
```

Configure a deterministic seed when asserting on specific values:

```csharp
[Fact]
public void CreateOrder_UsesDeterministicData_WhenSeeded()
{
    Randomizer.Seed = new Random(1234); // Bogus deterministic seed
    var order = OrderFactory.Create();
    Assert.NotNull(order);
}
```

### Using Moq for ports

```csharp
var repo = new Mock<IOrderRepository>();
repo.Setup(r => r.SaveAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
    .Returns(Task.CompletedTask);
```

## Anti-patterns to avoid
- Spreading ad-hoc object construction across tests; use factories/builders.
- Relying on random non-deterministic data for assertions; seed when asserting values.
- Over-mocking behavior that’s better validated via domain logic; prefer pure domain tests with concrete objects where feasible.
- Introducing additional test frameworks or assertion libraries without prior approval.

## Approval for additional dependencies
If you need additional test libraries (snapshot testing, specialized generators, etc.), open an ADR or GitHub issue to request approval before adding the dependency. Include justification and impact.

## References
- ADR 0001: Adopt .NET 9
- Testing Strategy in `.github/copilot-instructions.md`
- Moq: https://github.com/moq/moq
- Bogus: https://github.com/bchavez/Bogus
- xUnit: https://xunit.net/
