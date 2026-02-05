# ADR-003: Provider Abstraction Strategy

## Status

**Accepted**

## Date

2024-01-15

## Context

The KYC Aggregator must integrate with multiple identity verification providers (Onfido, Jumio, Veriff, etc.). Each provider has:

- Different API structures and authentication methods
- Different check types and naming conventions
- Different webhook formats and signatures
- Different SDK options for client-side verification

We need a strategy that:
1. Isolates provider-specific code from core business logic
2. Allows adding/removing providers without changing core code
3. Enables provider selection and fallback
4. Supports testing without real provider calls
5. Keeps provider packages independently deployable

### Options Considered

#### Option A: Direct Integration
Integrate each provider directly in the Application layer with conditional logic.

```csharp
if (providerId == "onfido")
    await _onfidoClient.CreateApplicant(...)
else if (providerId == "jumio")
    await _jumioClient.CreateApplicant(...)
```

**Pros:**
- Simple, no abstractions
- Easy to understand

**Cons:**
- Violates Open/Closed Principle
- Adding providers requires modifying core code
- Hard to test
- Tight coupling

#### Option B: Strategy Pattern with Interface
Define a common interface (`IKycProvider`) implemented by each provider.

```csharp
public interface IKycProvider
{
    Task<ProviderApplicantResult> CreateApplicantAsync(...);
    Task<ProviderCheckResult> StartCheckAsync(...);
}
```

**Pros:**
- Clean abstraction
- Providers are pluggable
- Easy to test with mocks
- Follows SOLID principles

**Cons:**
- Requires careful interface design
- May need adapter pattern for providers with different capabilities
- Interface might not fit all providers perfectly

#### Option C: Adapter Pattern with Normalized Models
Like Option B, but with additional adapter layer to normalize provider differences.

**Pros:**
- Maximum flexibility
- Provider quirks hidden from core

**Cons:**
- More complexity
- Additional mapping layer

## Decision

We will use the **Strategy Pattern with Interface** (Option B), enhanced with:

1. **Provider Registry**: Central registry of available providers
2. **Provider Selector**: Chooses provider based on context/rules
3. **Separate Provider Projects**: Each provider in its own assembly
4. **Normalized Request/Response Models**: Common models defined in abstractions

### Interface Design

```csharp
public interface IKycProvider
{
    // Identity
    string ProviderId { get; }
    string DisplayName { get; }
    IReadOnlySet<CheckType> SupportedCheckTypes { get; }
    
    // Operations
    Task<ProviderApplicantResult> CreateApplicantAsync(CreateProviderApplicantRequest request, CancellationToken ct);
    Task<ProviderCheckResult> StartCheckAsync(StartProviderCheckRequest request, CancellationToken ct);
    Task<ProviderCheckStatus> GetCheckStatusAsync(string providerCheckId, CancellationToken ct);
    Task<SdkTokenResult> GetSdkTokenAsync(string providerApplicantId, CancellationToken ct);
    
    // Webhooks
    bool ValidateWebhookSignature(string payload, string signature);
    WebhookEvent ParseWebhook(string payload);
    
    // Health
    Task<ProviderHealthResult> HealthCheckAsync(CancellationToken ct);
}
```

### Project Structure

```
src/Providers/
├── Kyc.Providers.Abstractions/     # Interface + normalized models
│   ├── IKycProvider.cs
│   ├── IProviderRegistry.cs
│   ├── Models/
│   │   ├── ProviderApplicantResult.cs
│   │   ├── ProviderCheckResult.cs
│   │   └── WebhookEvent.cs
│   └── Configuration/
│       └── ProviderConfiguration.cs
│
├── Kyc.Providers.Onfido/           # Onfido implementation
│   ├── OnfidoProvider.cs
│   ├── OnfidoConfiguration.cs
│   ├── Client/
│   │   ├── OnfidoApiClient.cs
│   │   └── Models/                 # Onfido-specific DTOs
│   └── DependencyInjection.cs
│
├── Kyc.Providers.Jumio/            # Jumio implementation
│   └── ...
│
└── Kyc.Providers.Veriff/           # Veriff implementation
    └── ...
```

### Provider Registration

```csharp
// Fluent registration in host
services.AddKycAggregator(options => { ... })
    .AddOnfido(config =>
    {
        config.ApiKey = Configuration["Onfido:ApiKey"];
        config.Region = OnfidoRegion.EU;
    })
    .AddJumio(config =>
    {
        config.ApiToken = Configuration["Jumio:ApiToken"];
    });
```

### Provider Selection

```csharp
public interface IProviderSelector
{
    Task<ProviderSelection> SelectProviderAsync(ProviderSelectionContext context, CancellationToken ct);
}

// Default implementation uses configuration
// Can be replaced with custom logic (cost-based, geo-based, etc.)
```

## Consequences

### Positive

- **Pluggable**: Add Veriff by creating new project and calling `.AddVeriff()`
- **Testable**: Mock `IKycProvider` for unit tests
- **Independent Deployment**: Provider packages can be versioned independently
- **NuGet-Ready**: `Kyc.Aggregator.Provider.Onfido` as separate package
- **Maintainable**: Provider-specific bugs/changes isolated to one project
- **Fallback Support**: Provider registry enables fallback logic

### Negative

- **Interface Limitations**: Some provider features may not fit the interface
- **Lowest Common Denominator**: Interface must work for all providers
- **Mapping Overhead**: Converting between provider-specific and normalized models
- **Version Coordination**: Core changes may require provider updates

### Mitigations

1. **Metadata/Extras**: Include `Dictionary<string, object>` for provider-specific data
2. **Optional Features**: Use feature detection for provider-specific capabilities
3. **Semantic Versioning**: Clear contract versioning between core and providers
4. **Interface Segregation**: Split interface if some providers don't support all operations

## Implementation Details

### Normalized Models

```csharp
// Provider-agnostic request
public record StartProviderCheckRequest(
    string ProviderApplicantId,
    IReadOnlyList<CheckType> CheckTypes,
    IReadOnlyList<ProviderDocument>? Documents,
    string? RedirectUrl,
    IReadOnlyDictionary<string, string>? Metadata);

// Provider-agnostic result
public record ProviderCheckResult(
    bool Success,
    string? ProviderCheckId,
    string? SdkToken,
    string? CheckUrl,
    string? ErrorCode,
    string? ErrorMessage);
```

### Provider Registry

```csharp
public interface IProviderRegistry
{
    IKycProvider GetProvider(string providerId);
    IKycProvider? TryGetProvider(string providerId);
    IEnumerable<IKycProvider> GetAllProviders();
    IEnumerable<IKycProvider> GetProvidersForCheckTypes(IEnumerable<CheckType> checkTypes);
}
```

### Webhook Routing

```csharp
// Webhook controller routes to correct provider
[HttpPost("{providerId}")]
public async Task<IActionResult> HandleWebhook(string providerId, ...)
{
    var provider = _registry.GetProvider(providerId);
    if (!provider.ValidateWebhookSignature(payload, signature))
        return Unauthorized();
    
    var webhookEvent = provider.ParseWebhook(payload);
    await _webhookProcessor.ProcessAsync(providerId, webhookEvent);
    return Ok();
}
```

### Provider Health Aggregation

```csharp
// Health check aggregates all providers
public class ProvidersHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(...)
    {
        var results = await Task.WhenAll(
            _registry.GetAllProviders()
                .Select(p => p.HealthCheckAsync(cancellationToken)));
        
        if (results.All(r => r.Healthy))
            return HealthCheckResult.Healthy();
        
        return HealthCheckResult.Degraded("Some providers unhealthy");
    }
}
```

## Alternatives Rejected

- **Direct Integration**: Too coupled, violates OCP
- **Generic API Client**: Too generic, loses type safety
- **Event-Driven Only**: Adds latency for synchronous operations

## Related Decisions

- [ADR-001: Modular Monolith Architecture](./ADR-001-modular-monolith-architecture.md)
- [ADR-002: Clean Architecture Layers](./ADR-002-clean-architecture-layers.md)

## References

- [Strategy Pattern - Refactoring Guru](https://refactoring.guru/design-patterns/strategy)
- [Plugin Architecture in .NET](https://docs.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support)
