---
title: "ADR 0008: Adopt OpenTelemetry for Comprehensive Observability"
date: 2025-11-26
status: Accepted
tags: [opentelemetry, observability, telemetry, metrics, tracing, logging, adr, backend]
---
# ADR 0008: Adopt OpenTelemetry for Comprehensive Observability

## Context
Modern distributed backend systems require deep observability to diagnose issues, understand performance characteristics, and monitor system health. Traditional logging alone is insufficient for understanding complex request flows across services, databases, message queues, and external APIs.

Observability has three pillars:
1. **Traces**: Distributed request flows showing how operations propagate through services
2. **Metrics**: Quantitative measurements (request counts, durations, error rates, resource utilization)
3. **Logs**: Discrete event records with contextual information

Historically, each pillar required different libraries and vendors (Jaeger for traces, Prometheus for metrics, Serilog for logs), leading to:
- **Vendor lock-in**: Switching observability backends required code changes
- **Inconsistent instrumentation**: Different patterns for traces vs metrics
- **Missing correlation**: Difficulty linking logs to traces to metrics
- **Maintenance burden**: Multiple SDKs to update and configure
- **Incomplete coverage**: Developers skip instrumentation due to complexity

OpenTelemetry (OTel) is a CNCF standard providing vendor-neutral APIs, SDKs, and exporters for all three pillars. It's natively supported in .NET via `System.Diagnostics` APIs (ActivitySource, Meter) and standardizes semantic conventions for common operations (HTTP, database, messaging).

With .NET Aspire's first-class OTel integration (ADR 0003), adopting OpenTelemetry aligns with our stack and provides production-ready observability out of the box.

## Decision
We REQUIRE all backend services (.NET APIs, workers, background services, microservices) to adopt **OpenTelemetry** as the standard observability framework.

### Core Requirements

#### 1. Instrumentation Scope
All backend projects MUST instrument:
- **HTTP requests/responses**: Automatically via ASP.NET Core OTel instrumentation
- **Database operations**: Automatically via Entity Framework Core or ADO.NET OTel instrumentation
- **Outbound HTTP calls**: Automatically via HttpClient OTel instrumentation
- **Message queue operations**: RabbitMQ, Azure Service Bus, Kafka (via vendor OTel libraries)
- **Application-level operations**: Custom activities for business logic spans
- **Key metrics**: Request rates, error rates, latencies, business KPIs

#### 2. Activities (Spans) for Business Logic
Handlers, services, and domain operations MUST create activities for significant operations:

```csharp
public sealed class CreateOrderHandler : ICommandHandler<CreateOrderCommand, CreateOrderResult>
{
    private static readonly ActivitySource ActivitySource = new("ProjectName.Orders");
    
    private readonly IOrderRepository _orders;
    private readonly IPaymentGateway _payments;
    private readonly ILogger<CreateOrderHandler> _logger;

    public async Task<CreateOrderResult> Handle(CreateOrderCommand command, CancellationToken ct)
    {
        using var activity = ActivitySource.StartActivity("CreateOrder", ActivityKind.Internal);
        activity?.SetTag("order.customer_id", command.CustomerId);
        activity?.SetTag("order.line_count", command.Lines.Count);

        try
        {
            // Validate customer
            using (var validateActivity = ActivitySource.StartActivity("ValidateCustomer"))
            {
                var customer = await _customers.GetByIdAsync(command.CustomerId, ct)
                    ?? throw new NotFoundException($"Customer {command.CustomerId} not found");
                validateActivity?.SetTag("customer.status", customer.Status);
            }

            // Create order entity (domain logic)
            var order = Order.Create(command.CustomerId, command.Lines, _clock.UtcNow);
            activity?.SetTag("order.id", order.Id);
            activity?.SetTag("order.total", order.Total);

            // Persist
            await _orders.AddAsync(order, ct);

            // Process payment
            using (var paymentActivity = ActivitySource.StartActivity("ProcessPayment"))
            {
                var paymentResult = await _payments.ChargeAsync(order.Total, command.PaymentToken, ct);
                paymentActivity?.SetTag("payment.transaction_id", paymentResult.TransactionId);
                paymentActivity?.SetTag("payment.status", paymentResult.Status);
                
                if (!paymentResult.Success)
                {
                    paymentActivity?.SetStatus(ActivityStatusCode.Error, "Payment failed");
                    throw new PaymentFailedException(paymentResult.ErrorMessage);
                }
            }

            activity?.SetStatus(ActivityStatusCode.Ok);
            _logger.LogInformation("Order {OrderId} created successfully", order.Id);

            return new CreateOrderResult(order.Id, order.Total, order.CreatedAt);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);
            throw;
        }
    }
}
```

#### 3. Metrics for Key Operations
Handlers and services MUST emit metrics for:
- **Counters**: Operation counts (orders created, payments processed, messages sent)
- **Histograms**: Duration distributions (request latency, query time)
- **Gauges**: Point-in-time values (queue depth, active connections, cache size)

```csharp
public sealed class OrderMetrics
{
    private readonly Counter<long> _ordersCreated;
    private readonly Histogram<double> _orderValue;
    private readonly Histogram<double> _orderProcessingDuration;

    public OrderMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("ProjectName.Orders");
        
        _ordersCreated = meter.CreateCounter<long>(
            "orders.created",
            unit: "{order}",
            description: "Total number of orders created");

        _orderValue = meter.CreateHistogram<double>(
            "orders.value",
            unit: "USD",
            description: "Order total value distribution");

        _orderProcessingDuration = meter.CreateHistogram<double>(
            "orders.processing.duration",
            unit: "ms",
            description: "Order processing time");
    }

    public void RecordOrderCreated(decimal orderValue, string customerTier)
    {
        _ordersCreated.Add(1, new KeyValuePair<string, object?>("customer.tier", customerTier));
        _orderValue.Record((double)orderValue, new KeyValuePair<string, object?>("customer.tier", customerTier));
    }

    public void RecordProcessingDuration(double durationMs, bool success)
    {
        _orderProcessingDuration.Record(durationMs, 
            new KeyValuePair<string, object?>("success", success));
    }
}

// Usage in handler
public async Task<CreateOrderResult> Handle(CreateOrderCommand command, CancellationToken ct)
{
    var stopwatch = Stopwatch.StartNew();
    try
    {
        // ... business logic ...
        
        _metrics.RecordOrderCreated(order.Total, customer.Tier);
        _metrics.RecordProcessingDuration(stopwatch.Elapsed.TotalMilliseconds, success: true);
        
        return result;
    }
    catch (Exception ex)
    {
        _metrics.RecordProcessingDuration(stopwatch.Elapsed.TotalMilliseconds, success: false);
        throw;
    }
}
```

#### 4. Semantic Conventions
Follow OpenTelemetry semantic conventions for consistent naming:
- **HTTP**: `http.method`, `http.status_code`, `http.route`, `http.target`
- **Database**: `db.system`, `db.operation`, `db.statement`, `db.name`
- **Messaging**: `messaging.system`, `messaging.operation`, `messaging.destination`
- **Custom**: Use domain-specific prefixes (e.g., `order.`, `customer.`, `payment.`)

Reference: https://opentelemetry.io/docs/specs/semconv/

#### 5. Structured Logging with Correlation
Logs MUST include trace context (TraceId, SpanId) for correlation:

```csharp
// Automatically added by ASP.NET Core + OTel integration
_logger.LogInformation("Order {OrderId} created for customer {CustomerId}", 
    order.Id, command.CustomerId);
// Output includes: TraceId=abc123, SpanId=def456, order.Id=..., customer.Id=...
```

Use structured logging (Serilog or Microsoft.Extensions.Logging with JSON formatter):
```csharp
builder.Services.AddLogging(logging =>
{
    logging.AddOpenTelemetry(options =>
    {
        options.IncludeFormattedMessage = true;
        options.IncludeScopes = true;
        options.ParseStateValues = true;
    });
});
```

### Configuration & Setup

#### ASP.NET Core API (with Aspire)
```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults (includes OTel with OTLP exporter)
builder.AddServiceDefaults();

// Register activity sources for custom instrumentation
builder.Services.AddSingleton(new ActivitySource("ProjectName.Orders"));
builder.Services.AddSingleton(new ActivitySource("ProjectName.Payments"));

// Register meters for metrics
builder.Services.AddSingleton<OrderMetrics>();

var app = builder.Build();
app.MapDefaultEndpoints(); // Health checks, metrics endpoint
```

#### Standalone Service (without Aspire)
```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddSource("ProjectName.Orders")
        .AddSource("ProjectName.Payments")
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddMeter("ProjectName.Orders")
        .AddOtlpExporter());

builder.Logging.AddOpenTelemetry(logging => logging.AddOtlpExporter());
```

#### Worker Service / Background Service
```csharp
var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices(services =>
{
    services.AddOpenTelemetry()
        .WithTracing(tracing => tracing
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddSource("ProjectName.MessageProcessor")
            .AddOtlpExporter())
        .WithMetrics(metrics => metrics
            .AddMeter("ProjectName.MessageProcessor")
            .AddOtlpExporter());
});
```

### Exporter Configuration
Use environment variables for exporter endpoints (12-factor app principle):

```bash
# OTLP gRPC endpoint (default for Aspire Dashboard, Jaeger, Tempo, etc.)
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317

# Or separate endpoints per signal
OTEL_EXPORTER_OTLP_TRACES_ENDPOINT=http://jaeger:4317
OTEL_EXPORTER_OTLP_METRICS_ENDPOINT=http://prometheus:4317
OTEL_EXPORTER_OTLP_LOGS_ENDPOINT=http://loki:4317

# Service name (appears in traces/metrics)
OTEL_SERVICE_NAME=order-service
OTEL_SERVICE_VERSION=1.2.3
```

### When to Create Activities

#### DO Create Activities For:
- **Command/query handlers**: Each use case (CreateOrder, GetOrderById)
- **Multi-step operations**: Operations with 3+ internal steps
- **External calls**: Explicitly wrap calls not auto-instrumented (e.g., custom gRPC, SOAP)
- **Background jobs**: Long-running tasks, batch processing
- **Domain operations**: Aggregates performing complex business logic

#### DO NOT Create Activities For:
- **Trivial operations**: Single-line getters, simple property access
- **Already instrumented**: HTTP requests, EF queries (auto-instrumented by libraries)
- **High-frequency loops**: Inner loop iterations (causes excessive overhead)
- **Pure functions**: Stateless calculations without I/O

### When to Emit Metrics

#### DO Emit Metrics For:
- **Business KPIs**: Orders created, revenue, user signups
- **Error rates**: Failed payments, validation errors, exceptions
- **Performance**: Handler duration, query latency, cache hit rate
- **Resource usage**: Queue depth, connection pool size, active requests
- **SLIs**: Request success rate, p95 latency, availability

#### DO NOT Emit Metrics For:
- **Every log statement**: Metrics are aggregates, not events
- **High-cardinality dimensions**: User IDs, email addresses (use traces instead)
- **Implementation details**: Internal variable states, debug counters

### Tags & Attributes Best Practices
- **Low cardinality**: Use dimensions with bounded values (e.g., `customer.tier=premium|standard|free`, not `customer.id=<uuid>`)
- **Meaningful names**: Descriptive tags (e.g., `payment.method=credit_card`, not `pm=cc`)
- **Consistent naming**: Use semantic conventions; prefix custom tags with domain (e.g., `order.`, `payment.`)
- **Avoid sensitive data**: Never include PII, passwords, tokens in tags or logs

### Testing Observability
Unit tests SHOULD verify activities are created:

```csharp
[Fact]
public async Task Handle_CreatesActivityWithCorrectTags()
{
    // Arrange
    var activities = new List<Activity>();
    using var listener = new ActivityListener
    {
        ShouldListenTo = source => source.Name == "ProjectName.Orders",
        Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        ActivityStarted = activity => activities.Add(activity)
    };
    ActivitySource.AddActivityListener(listener);

    var handler = new CreateOrderHandler(...);
    var command = new CreateOrderCommand(customerId, lines);

    // Act
    await handler.Handle(command, CancellationToken.None);

    // Assert
    activities.Should().ContainSingle(a => a.DisplayName == "CreateOrder");
    var activity = activities.First();
    activity.Tags.Should().Contain(new KeyValuePair<string, string?>("order.customer_id", customerId.ToString()));
}
```

### Compliance & Code Review
- PRs introducing new handlers MUST include activity creation with relevant tags
- PRs adding business operations MUST emit metrics for success/failure rates
- Code reviews MUST verify semantic convention usage and tag cardinality
- Quarterly audits SHOULD review observability coverage and identify gaps

## Consequences

### Positive
1. **Unified observability**: Single framework for traces, metrics, and logs reduces complexity
2. **Vendor neutrality**: Switch backends (Jaeger, Tempo, Prometheus, Datadog, New Relic) without code changes
3. **Rich context**: Distributed traces show complete request flows across services
4. **Faster troubleshooting**: Correlated logs, traces, and metrics speed up root cause analysis
5. **Performance insights**: Metrics reveal bottlenecks, slow queries, and optimization opportunities
6. **Production readiness**: OTel is battle-tested, CNCF graduated project with enterprise support
7. **Native .NET integration**: Built on System.Diagnostics APIs, no third-party dependencies
8. **Aspire alignment**: .NET Aspire provides OTel out-of-the-box (ADR 0003)
9. **Standardized instrumentation**: Semantic conventions ensure consistent naming across teams
10. **Better SLOs/SLIs**: Accurate metrics enable data-driven reliability engineering

### Negative
1. **Learning curve**: Developers unfamiliar with distributed tracing need training
2. **Performance overhead**: Instrumentation adds CPU/memory cost (typically <5% with sampling)
3. **Initial setup effort**: Requires configuring exporters, dashboards, and alerting rules
4. **Verbose code**: Activity creation adds boilerplate to handlers (mitigated by middleware/decorators)
5. **Data volume**: High-traffic services generate significant telemetry data (address with sampling)
6. **Cardinality pitfalls**: High-cardinality tags can overwhelm metrics backends (requires discipline)

### Mitigation Strategies
1. **Training**: Conduct OTel workshops; provide code examples and templates
2. **Sampling**: Use head-based sampling (e.g., 10% of traces) or tail-based sampling (100% of errors)
3. **Middleware/decorators**: Wrap handlers with OTel decorators to reduce boilerplate:

```csharp
public sealed class OpenTelemetryCommandHandlerDecorator<TCommand, TResult> 
    : ICommandHandler<TCommand, TResult>
{
    private readonly ICommandHandler<TCommand, TResult> _inner;
    private readonly ActivitySource _activitySource;

    public async Task<TResult> Handle(TCommand command, CancellationToken ct)
    {
        using var activity = _activitySource.StartActivity(
            $"{typeof(TCommand).Name}",
            ActivityKind.Internal);
        
        activity?.SetTag("command.type", typeof(TCommand).Name);
        
        try
        {
            var result = await _inner.Handle(command, ct);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);
            throw;
        }
    }
}
```

4. **Guidelines enforcement**: Stylelint-style analyzer for OTel (future: custom Roslyn analyzer)
5. **Dashboards**: Provide Grafana/Aspire dashboard templates for common metrics
6. **Cost controls**: Monitor telemetry data volume; adjust sampling rates per environment (dev=100%, prod=10%)

## Integration with Existing ADRs
- **ADR 0003 (Aspire)**: Aspire includes OTel by default; use `AddServiceDefaults()`
- **ADR 0004 (CQRS)**: Handlers are natural activity boundaries; instrument all handlers
- **ADR 0007 (Vertical Slices)**: Each slice's handler emits telemetry independently

## Examples

### Complete Handler with Telemetry
```csharp
namespace ProjectName.Application.Orders.CreateOrder;

public sealed class CreateOrderHandler : ICommandHandler<CreateOrderCommand, CreateOrderResult>
{
    private static readonly ActivitySource ActivitySource = new("ProjectName.Orders");
    
    private readonly IOrderRepository _orders;
    private readonly ICustomerRepository _customers;
    private readonly IPaymentGateway _payments;
    private readonly OrderMetrics _metrics;
    private readonly IClock _clock;
    private readonly ILogger<CreateOrderHandler> _logger;

    public CreateOrderHandler(
        IOrderRepository orders,
        ICustomerRepository customers,
        IPaymentGateway payments,
        OrderMetrics metrics,
        IClock clock,
        ILogger<CreateOrderHandler> logger)
    {
        _orders = orders;
        _customers = customers;
        _payments = payments;
        _metrics = metrics;
        _clock = clock;
        _logger = logger;
    }

    public async Task<CreateOrderResult> Handle(CreateOrderCommand command, CancellationToken ct)
    {
        using var activity = ActivitySource.StartActivity("CreateOrder", ActivityKind.Internal);
        activity?.SetTag("order.customer_id", command.CustomerId);
        activity?.SetTag("order.line_count", command.Lines.Count);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Validate customer exists
            _logger.LogDebug("Validating customer {CustomerId}", command.CustomerId);
            var customer = await _customers.GetByIdAsync(command.CustomerId, ct)
                ?? throw new NotFoundException($"Customer {command.CustomerId} not found");
            
            activity?.SetTag("customer.tier", customer.Tier);

            // Create order (domain logic)
            var order = Order.Create(
                customer.Id,
                command.Lines.Select(l => new OrderLine(l.ProductId, l.Quantity, l.UnitPrice)),
                _clock.UtcNow);

            activity?.SetTag("order.id", order.Id);
            activity?.SetTag("order.total", order.Total);

            // Persist order
            await _orders.AddAsync(order, ct);

            // Process payment
            using (var paymentActivity = ActivitySource.StartActivity("ProcessPayment"))
            {
                paymentActivity?.SetTag("payment.amount", order.Total);
                paymentActivity?.SetTag("payment.currency", "USD");

                var paymentResult = await _payments.ChargeAsync(
                    order.Total, 
                    command.PaymentToken, 
                    ct);

                paymentActivity?.SetTag("payment.transaction_id", paymentResult.TransactionId);

                if (!paymentResult.Success)
                {
                    paymentActivity?.SetStatus(ActivityStatusCode.Error, "Payment declined");
                    _logger.LogWarning("Payment declined for order {OrderId}: {Reason}", 
                        order.Id, paymentResult.ErrorMessage);
                    throw new PaymentFailedException(paymentResult.ErrorMessage);
                }
            }

            activity?.SetStatus(ActivityStatusCode.Ok);
            
            // Emit metrics
            _metrics.RecordOrderCreated(order.Total, customer.Tier);
            _metrics.RecordProcessingDuration(stopwatch.Elapsed.TotalMilliseconds, success: true);

            _logger.LogInformation("Order {OrderId} created successfully for customer {CustomerId}", 
                order.Id, customer.Id);

            return new CreateOrderResult(order.Id, order.Total, order.CreatedAt);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);
            
            _metrics.RecordProcessingDuration(stopwatch.Elapsed.TotalMilliseconds, success: false);
            
            _logger.LogError(ex, "Failed to create order for customer {CustomerId}", command.CustomerId);
            throw;
        }
    }
}
```

### Metrics Class
```csharp
namespace ProjectName.Application.Orders;

public sealed class OrderMetrics
{
    private readonly Counter<long> _ordersCreated;
    private readonly Counter<long> _ordersFailed;
    private readonly Histogram<double> _orderValue;
    private readonly Histogram<double> _processingDuration;

    public OrderMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("ProjectName.Orders", "1.0.0");

        _ordersCreated = meter.CreateCounter<long>(
            name: "orders.created",
            unit: "{order}",
            description: "Total number of orders successfully created");

        _ordersFailed = meter.CreateCounter<long>(
            name: "orders.failed",
            unit: "{order}",
            description: "Total number of failed order creation attempts");

        _orderValue = meter.CreateHistogram<double>(
            name: "orders.value",
            unit: "USD",
            description: "Distribution of order total values");

        _processingDuration = meter.CreateHistogram<double>(
            name: "orders.processing.duration",
            unit: "ms",
            description: "Time taken to process order creation");
    }

    public void RecordOrderCreated(decimal orderValue, string customerTier)
    {
        var tags = new TagList
        {
            { "customer.tier", customerTier }
        };
        
        _ordersCreated.Add(1, tags);
        _orderValue.Record((double)orderValue, tags);
    }

    public void RecordOrderFailed(string errorType, string customerTier)
    {
        _ordersFailed.Add(1, new TagList
        {
            { "error.type", errorType },
            { "customer.tier", customerTier }
        });
    }

    public void RecordProcessingDuration(double durationMs, bool success)
    {
        _processingDuration.Record(durationMs, new TagList
        {
            { "success", success }
        });
    }
}
```

## References
- OpenTelemetry Documentation: https://opentelemetry.io/docs/
- .NET OpenTelemetry: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-with-otel
- Semantic Conventions: https://opentelemetry.io/docs/specs/semconv/
- .NET Aspire Telemetry: https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/telemetry
- System.Diagnostics.Activity: https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.activity
- System.Diagnostics.Metrics: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics
- ADR 0003: Adopt .NET Aspire for ASP.NET Web Services
- ADR 0004: CQRS Recommendation for ASP.NET API Projects
- ADR 0007: Adopt Vertical Slice Architecture for Feature Organization
