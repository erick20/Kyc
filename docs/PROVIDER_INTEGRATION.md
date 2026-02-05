# Provider Integration

This document describes how KYC providers are integrated into the system, the abstraction layer that isolates provider-specific implementations, and how to add new providers.

## Design Philosophy

Provider integration follows these principles:

1. **Isolation**: Provider-specific code is contained in separate assemblies
2. **Substitutability**: Providers can be swapped without changing core logic
3. **Configurability**: Provider selection can be runtime-configurable
4. **Testability**: Providers can be mocked for testing
5. **Resilience**: Failures in one provider don't affect others

## Provider Abstraction Layer

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        Application Layer                                │
│                                                                         │
│   ┌─────────────────────────────────────────────────────────────────┐  │
│   │                 IKycProviderService                              │  │
│   │  - StartVerification(applicant, checks)                         │  │
│   │  - GetStatus(verificationId)                                    │  │
│   │  - ProcessWebhook(payload)                                       │  │
│   └─────────────────────────────────────────────────────────────────┘  │
│                              │                                          │
│                              ▼                                          │
│   ┌─────────────────────────────────────────────────────────────────┐  │
│   │                 IProviderRegistry                                │  │
│   │  - GetProvider(providerId): IKycProvider                        │  │
│   │  - GetAllProviders(): IEnumerable<IKycProvider>                 │  │
│   └─────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                     Provider Abstraction Layer                          │
│                                                                         │
│   ┌──────────────────────────────────────────────────────────────────┐ │
│   │                      IKycProvider                                 │ │
│   │                                                                   │ │
│   │  Properties:                                                      │ │
│   │  - ProviderId: string                                            │ │
│   │  - DisplayName: string                                           │ │
│   │  - SupportedCheckTypes: IReadOnlySet<CheckType>                  │ │
│   │                                                                   │ │
│   │  Methods:                                                         │ │
│   │  - CreateApplicant(request): Task<ProviderApplicantResult>       │ │
│   │  - StartCheck(request): Task<ProviderCheckResult>                │ │
│   │  - GetCheckStatus(providerCheckId): Task<ProviderCheckStatus>    │ │
│   │  - GetSdkToken(applicantId): Task<SdkTokenResult>                │ │
│   │  - ValidateWebhookSignature(payload, signature): bool            │ │
│   │  - ParseWebhook(payload): WebhookEvent                           │ │
│   │  - HealthCheck(): Task<HealthCheckResult>                        │ │
│   └──────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────┘
                               │
           ┌───────────────────┼───────────────────┐
           │                   │                   │
           ▼                   ▼                   ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│ OnfidoProvider  │ │ JumioProvider   │ │ VeriffProvider  │
│                 │ │                 │ │                 │
│ - Onfido SDK    │ │ - Jumio REST    │ │ - Veriff API    │
│ - Configuration │ │ - Configuration │ │ - Configuration │
└─────────────────┘ └─────────────────┘ └─────────────────┘
```

## Core Interfaces

### IKycProvider

The main interface every provider must implement.

```csharp
public interface IKycProvider
{
    /// <summary>
    /// Unique identifier for this provider (e.g., "onfido", "jumio")
    /// </summary>
    string ProviderId { get; }
    
    /// <summary>
    /// Human-readable name for admin UI
    /// </summary>
    string DisplayName { get; }
    
    /// <summary>
    /// Check types this provider supports
    /// </summary>
    IReadOnlySet<CheckType> SupportedCheckTypes { get; }
    
    /// <summary>
    /// Create or update an applicant in the provider's system
    /// </summary>
    Task<ProviderApplicantResult> CreateApplicantAsync(
        CreateProviderApplicantRequest request,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Start a verification check
    /// </summary>
    Task<ProviderCheckResult> StartCheckAsync(
        StartProviderCheckRequest request,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Poll for check status (used when webhooks are unavailable)
    /// </summary>
    Task<ProviderCheckStatus> GetCheckStatusAsync(
        string providerCheckId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get SDK token for client-side integrations
    /// </summary>
    Task<SdkTokenResult> GetSdkTokenAsync(
        string providerApplicantId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validate webhook signature
    /// </summary>
    bool ValidateWebhookSignature(string payload, string signature);
    
    /// <summary>
    /// Parse webhook payload into normalized event
    /// </summary>
    WebhookEvent ParseWebhook(string payload);
    
    /// <summary>
    /// Health check for monitoring
    /// </summary>
    Task<ProviderHealthResult> HealthCheckAsync(
        CancellationToken cancellationToken = default);
}
```

### IProviderRegistry

Manages registered providers and their lifecycle.

```csharp
public interface IProviderRegistry
{
    /// <summary>
    /// Get a specific provider by ID
    /// </summary>
    IKycProvider GetProvider(string providerId);
    
    /// <summary>
    /// Try to get a provider, returns null if not found
    /// </summary>
    IKycProvider? TryGetProvider(string providerId);
    
    /// <summary>
    /// Get all registered providers
    /// </summary>
    IEnumerable<IKycProvider> GetAllProviders();
    
    /// <summary>
    /// Check if a provider is registered
    /// </summary>
    bool IsProviderRegistered(string providerId);
    
    /// <summary>
    /// Get providers that support specific check types
    /// </summary>
    IEnumerable<IKycProvider> GetProvidersForCheckTypes(
        IEnumerable<CheckType> checkTypes);
}
```

### IProviderSelector

Determines which provider to use for a verification.

```csharp
public interface IProviderSelector
{
    /// <summary>
    /// Select the best provider for given context
    /// </summary>
    Task<ProviderSelection> SelectProviderAsync(
        ProviderSelectionContext context,
        CancellationToken cancellationToken = default);
}

public record ProviderSelectionContext(
    Applicant Applicant,
    IReadOnlyList<CheckType> RequestedChecks,
    string? PreferredProviderId = null,
    string? TenantId = null);

public record ProviderSelection(
    string ProviderId,
    string? FallbackProviderId,
    string SelectionReason);
```

## Provider Request/Response Models

### Normalized Models

Provider-agnostic models used across the system.

```csharp
public record CreateProviderApplicantRequest(
    string InternalApplicantId,
    string FirstName,
    string LastName,
    string Email,
    DateOnly? DateOfBirth,
    Address? Address,
    IReadOnlyDictionary<string, string>? Metadata);

public record ProviderApplicantResult(
    bool Success,
    string? ProviderApplicantId,
    string? ErrorCode,
    string? ErrorMessage);

public record StartProviderCheckRequest(
    string ProviderApplicantId,
    IReadOnlyList<CheckType> CheckTypes,
    IReadOnlyList<ProviderDocument>? Documents,
    string? RedirectUrl,
    IReadOnlyDictionary<string, string>? Metadata);

public record ProviderCheckResult(
    bool Success,
    string? ProviderCheckId,
    string? SdkToken,
    string? CheckUrl,
    string? ErrorCode,
    string? ErrorMessage);

public record ProviderCheckStatus(
    string ProviderCheckId,
    CheckStatus Status,
    CheckResult? Result,
    IReadOnlyList<CheckBreakdown>? Breakdowns,
    DateTime? CompletedAt);

public record WebhookEvent(
    string EventType,
    string ProviderCheckId,
    CheckStatus? NewStatus,
    CheckResult? Result,
    DateTime Timestamp,
    string RawPayload);
```

## Provider Configuration

Each provider has its own configuration class.

```csharp
public abstract class ProviderConfiguration
{
    public bool Enabled { get; set; } = true;
    public bool IsSandbox { get; set; } = false;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxRetries { get; set; } = 3;
}

public class OnfidoConfiguration : ProviderConfiguration
{
    public string ApiKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public OnfidoRegion Region { get; set; } = OnfidoRegion.EU;
    public string? ReferrerPattern { get; set; }
}

public class JumioConfiguration : ProviderConfiguration
{
    public string ApiToken { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public string DataCenter { get; set; } = "EU";
    public string WorkflowId { get; set; } = string.Empty;
}
```

## Provider Registration

Providers are registered through extension methods.

```csharp
// In Kyc.Providers.Onfido
public static class OnfidoServiceCollectionExtensions
{
    public static IKycAggregatorBuilder AddOnfido(
        this IKycAggregatorBuilder builder,
        Action<OnfidoConfiguration> configure)
    {
        var config = new OnfidoConfiguration();
        configure(config);
        
        builder.Services.AddSingleton(config);
        builder.Services.AddHttpClient<OnfidoApiClient>(client =>
        {
            client.BaseAddress = GetBaseUrl(config.Region);
            client.DefaultRequestHeaders.Add("Authorization", $"Token token={config.ApiKey}");
        });
        
        builder.Services.AddSingleton<IKycProvider, OnfidoProvider>();
        
        return builder;
    }
}

// Usage in host
services.AddKycAggregator(options =>
{
    options.UsePostgres(connectionString);
})
.AddOnfido(config =>
{
    config.ApiKey = Configuration["Onfido:ApiKey"];
    config.WebhookSecret = Configuration["Onfido:WebhookSecret"];
    config.Region = OnfidoRegion.EU;
})
.AddJumio(config =>
{
    config.ApiToken = Configuration["Jumio:ApiToken"];
    config.ApiSecret = Configuration["Jumio:ApiSecret"];
});
```

## Webhook Handling

```
┌──────────────┐     ┌──────────────────┐     ┌─────────────────────┐
│   Provider   │────▶│  WebhookController│────▶│  IWebhookProcessor  │
│  (External)  │     │  /webhooks/{pid}  │     │                     │
└──────────────┘     └──────────────────┘     └──────────┬──────────┘
                                                         │
                                                         ▼
                     ┌───────────────────────────────────────────────┐
                     │              Provider Registry                 │
                     │  - Lookup provider by path parameter          │
                     │  - Validate signature                         │
                     │  - Parse webhook                              │
                     └───────────────────────────────────────────────┘
                                         │
                                         ▼
                     ┌───────────────────────────────────────────────┐
                     │           WebhookEvent (normalized)           │
                     │  - Route to appropriate handler               │
                     │  - Update verification status                 │
                     │  - Emit domain events                         │
                     └───────────────────────────────────────────────┘
```

### Webhook Controller

```csharp
[ApiController]
[Route("webhooks")]
public class WebhooksController : ControllerBase
{
    [HttpPost("{providerId}")]
    public async Task<IActionResult> HandleWebhook(
        string providerId,
        [FromBody] JsonDocument payload,
        [FromHeader(Name = "X-Signature")] string? signature)
    {
        var provider = _providerRegistry.TryGetProvider(providerId);
        if (provider is null)
            return NotFound();
        
        var rawPayload = payload.RootElement.GetRawText();
        
        // Validate signature
        if (!provider.ValidateWebhookSignature(rawPayload, signature ?? ""))
            return Unauthorized();
        
        // Parse and process
        var webhookEvent = provider.ParseWebhook(rawPayload);
        await _webhookProcessor.ProcessAsync(providerId, webhookEvent);
        
        return Ok();
    }
}
```

## Error Handling

Providers should throw specific exceptions that the application layer can handle.

```csharp
public abstract class ProviderException : Exception
{
    public string ProviderId { get; }
    public string? ProviderErrorCode { get; }
    public bool IsRetryable { get; }
}

public class ProviderApiException : ProviderException
{
    public HttpStatusCode StatusCode { get; }
}

public class ProviderRateLimitException : ProviderException
{
    public TimeSpan? RetryAfter { get; }
}

public class ProviderValidationException : ProviderException
{
    public IReadOnlyList<ValidationError> Errors { get; }
}

public class ProviderConfigurationException : ProviderException
{
    // Missing or invalid configuration
}
```

## Provider Implementation Checklist

When implementing a new provider:

### 1. Create Provider Project
```
Kyc.Providers.NewProvider/
├── NewProviderProvider.cs          # IKycProvider implementation
├── NewProviderConfiguration.cs     # Configuration class
├── NewProviderWebhookHandler.cs    # Webhook parsing
├── Client/
│   ├── NewProviderApiClient.cs     # HTTP client
│   └── Models/                     # Provider-specific DTOs
├── Mappings/
│   └── NewProviderMapper.cs        # Map provider models to abstractions
└── DependencyInjection.cs          # AddNewProvider extension
```

### 2. Implement Required Interface Methods
- [ ] `CreateApplicantAsync` - Create applicant in provider system
- [ ] `StartCheckAsync` - Initiate verification check
- [ ] `GetCheckStatusAsync` - Poll for status
- [ ] `GetSdkTokenAsync` - Get client-side SDK token
- [ ] `ValidateWebhookSignature` - Verify webhook authenticity
- [ ] `ParseWebhook` - Parse provider webhook format
- [ ] `HealthCheckAsync` - Verify provider connectivity

### 3. Handle Provider-Specific Concerns
- [ ] Authentication (API keys, OAuth, etc.)
- [ ] Rate limiting and retries
- [ ] Sandbox vs production environments
- [ ] Region-specific endpoints
- [ ] Document upload handling
- [ ] Error code mapping

### 4. Add Tests
- [ ] Unit tests for mapping logic
- [ ] Integration tests with sandbox API
- [ ] Webhook parsing tests
- [ ] Error handling tests

### 5. Document
- [ ] Configuration options
- [ ] Required credentials
- [ ] Supported check types
- [ ] Known limitations

## Provider Comparison Matrix

| Feature | Onfido | Jumio | Veriff |
|---------|--------|-------|--------|
| Document Check | ✓ | ✓ | ✓ |
| Liveness | ✓ | ✓ | ✓ |
| Face Match | ✓ | ✓ | ✓ |
| AML/PEP | ✓ | ✓ | ✓ |
| Address Proof | ✓ | ✓ | Limited |
| SDK (Mobile) | ✓ | ✓ | ✓ |
| SDK (Web) | ✓ | ✓ | ✓ |
| Webhooks | ✓ | ✓ | ✓ |
| Sandbox | ✓ | ✓ | ✓ |

## Related Documents

- [ARCHITECTURE.md](./ARCHITECTURE.md) - Overall architecture
- [EXTENSIBILITY.md](./EXTENSIBILITY.md) - Extending the system
- [SECURITY.md](./SECURITY.md) - Handling provider credentials
