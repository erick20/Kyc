# Extensibility

This document describes how to extend the KYC Aggregator system with new providers, check types, and features while maintaining architectural integrity.

## Extension Points Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          EXTENSIBILITY POINTS                               │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────────────────┐ │
│  │   PROVIDERS     │  │   BEHAVIORS     │  │      DOMAIN EVENTS          │ │
│  │                 │  │                 │  │                             │ │
│  │  IKycProvider   │  │  IPipelineBeh.  │  │  INotificationHandler<T>   │ │
│  │  IWebhookHndlr  │  │  Logging        │  │  VerificationCompleted     │ │
│  │                 │  │  Validation     │  │  CheckCompleted            │ │
│  └─────────────────┘  │  Caching        │  │  Custom events             │ │
│                       └─────────────────┘  └─────────────────────────────┘ │
│                                                                             │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────────────────┐ │
│  │  PERSISTENCE    │  │   VALIDATION    │  │     PROVIDER SELECTION     │ │
│  │                 │  │                 │  │                             │ │
│  │  IRepository    │  │  IValidator<T>  │  │  IProviderSelector         │ │
│  │  Custom stores  │  │  FluentValid.   │  │  Custom routing rules      │ │
│  └─────────────────┘  └─────────────────┘  └─────────────────────────────┘ │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Adding a New KYC Provider

This is the most common extension scenario. Follow these steps to add a new provider.

### Step 1: Create Provider Project

```
src/Providers/Kyc.Providers.NewProvider/
├── NewProviderProvider.cs
├── NewProviderConfiguration.cs
├── NewProviderWebhookHandler.cs
├── Client/
│   ├── NewProviderApiClient.cs
│   └── Models/
│       ├── NewProviderApplicant.cs
│       ├── NewProviderCheck.cs
│       └── NewProviderWebhookEvent.cs
├── Mappings/
│   └── NewProviderMapper.cs
├── DependencyInjection.cs
└── Kyc.Providers.NewProvider.csproj
```

### Step 2: Implement Configuration

```csharp
// NewProviderConfiguration.cs
public class NewProviderConfiguration : ProviderConfiguration
{
    public const string ProviderId = "newprovider";
    
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public NewProviderRegion Region { get; set; } = NewProviderRegion.US;
    public string? CustomEndpoint { get; set; }
}

public enum NewProviderRegion
{
    US,
    EU,
    APAC
}
```

### Step 3: Implement API Client

```csharp
// Client/NewProviderApiClient.cs
public class NewProviderApiClient
{
    private readonly HttpClient _httpClient;
    private readonly NewProviderConfiguration _config;
    private readonly ILogger<NewProviderApiClient> _logger;
    
    public NewProviderApiClient(
        HttpClient httpClient,
        NewProviderConfiguration config,
        ILogger<NewProviderApiClient> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }
    
    public async Task<NewProviderApplicant> CreateApplicantAsync(
        CreateApplicantRequest request,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            first_name = request.FirstName,
            last_name = request.LastName,
            email = request.Email,
            date_of_birth = request.DateOfBirth?.ToString("yyyy-MM-dd")
        };
        
        var response = await _httpClient.PostAsJsonAsync(
            "/v1/applicants", 
            payload, 
            cancellationToken);
        
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadFromJsonAsync<NewProviderApplicant>(
            cancellationToken: cancellationToken);
    }
    
    // Additional methods...
}
```

### Step 4: Implement IKycProvider

```csharp
// NewProviderProvider.cs
public class NewProviderProvider : IKycProvider
{
    private readonly NewProviderApiClient _client;
    private readonly NewProviderConfiguration _config;
    private readonly ILogger<NewProviderProvider> _logger;
    
    public string ProviderId => NewProviderConfiguration.ProviderId;
    public string DisplayName => "New Provider";
    
    public IReadOnlySet<CheckType> SupportedCheckTypes { get; } = new HashSet<CheckType>
    {
        CheckType.Document,
        CheckType.Liveness,
        CheckType.FaceMatch
    };
    
    public NewProviderProvider(
        NewProviderApiClient client,
        NewProviderConfiguration config,
        ILogger<NewProviderProvider> logger)
    {
        _client = client;
        _config = config;
        _logger = logger;
    }
    
    public async Task<ProviderApplicantResult> CreateApplicantAsync(
        CreateProviderApplicantRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _client.CreateApplicantAsync(
                new CreateApplicantRequest(
                    request.FirstName,
                    request.LastName,
                    request.Email,
                    request.DateOfBirth),
                cancellationToken);
            
            return new ProviderApplicantResult(
                Success: true,
                ProviderApplicantId: result.Id,
                ErrorCode: null,
                ErrorMessage: null);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to create applicant in NewProvider");
            
            return new ProviderApplicantResult(
                Success: false,
                ProviderApplicantId: null,
                ErrorCode: "API_ERROR",
                ErrorMessage: ex.Message);
        }
    }
    
    public async Task<ProviderCheckResult> StartCheckAsync(
        StartProviderCheckRequest request,
        CancellationToken cancellationToken = default)
    {
        // Implementation...
    }
    
    public async Task<ProviderCheckStatus> GetCheckStatusAsync(
        string providerCheckId,
        CancellationToken cancellationToken = default)
    {
        // Implementation...
    }
    
    public async Task<SdkTokenResult> GetSdkTokenAsync(
        string providerApplicantId,
        CancellationToken cancellationToken = default)
    {
        // Implementation...
    }
    
    public bool ValidateWebhookSignature(string payload, string signature)
    {
        var expectedSignature = ComputeHmacSha256(payload, _config.WebhookSecret);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(signature),
            Encoding.UTF8.GetBytes(expectedSignature));
    }
    
    public WebhookEvent ParseWebhook(string payload)
    {
        var providerEvent = JsonSerializer.Deserialize<NewProviderWebhookEvent>(payload);
        
        return new WebhookEvent(
            EventType: MapEventType(providerEvent.Type),
            ProviderCheckId: providerEvent.CheckId,
            NewStatus: MapStatus(providerEvent.Status),
            Result: MapResult(providerEvent.Result),
            Timestamp: providerEvent.Timestamp,
            RawPayload: payload);
    }
    
    public async Task<ProviderHealthResult> HealthCheckAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.PingAsync(cancellationToken);
            return new ProviderHealthResult(Healthy: true, Message: "OK");
        }
        catch (Exception ex)
        {
            return new ProviderHealthResult(Healthy: false, Message: ex.Message);
        }
    }
}
```

### Step 5: Create Dependency Injection Extension

```csharp
// DependencyInjection.cs
public static class NewProviderServiceCollectionExtensions
{
    public static IKycAggregatorBuilder AddNewProvider(
        this IKycAggregatorBuilder builder,
        Action<NewProviderConfiguration> configure)
    {
        var config = new NewProviderConfiguration();
        configure(config);
        
        // Validate configuration
        if (string.IsNullOrEmpty(config.ApiKey))
            throw new ArgumentException("ApiKey is required");
        
        builder.Services.AddSingleton(config);
        
        // Configure HTTP client
        builder.Services.AddHttpClient<NewProviderApiClient>(client =>
        {
            client.BaseAddress = GetBaseUrl(config.Region, config.CustomEndpoint);
            client.DefaultRequestHeaders.Add("X-API-Key", config.ApiKey);
            client.Timeout = config.Timeout;
        })
        .AddPolicyHandler(GetRetryPolicy(config));
        
        // Register provider
        builder.Services.AddSingleton<IKycProvider, NewProviderProvider>();
        
        return builder;
    }
    
    private static Uri GetBaseUrl(NewProviderRegion region, string? customEndpoint)
    {
        if (!string.IsNullOrEmpty(customEndpoint))
            return new Uri(customEndpoint);
        
        return region switch
        {
            NewProviderRegion.US => new Uri("https://api.newprovider.com"),
            NewProviderRegion.EU => new Uri("https://api.eu.newprovider.com"),
            NewProviderRegion.APAC => new Uri("https://api.apac.newprovider.com"),
            _ => throw new ArgumentOutOfRangeException(nameof(region))
        };
    }
    
    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(
        NewProviderConfiguration config)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                config.MaxRetries,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }
}
```

### Step 6: Add Tests

```csharp
// tests/Kyc.Providers.NewProvider.Tests/NewProviderProviderTests.cs
public class NewProviderProviderTests
{
    [Fact]
    public async Task CreateApplicant_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler();
        mockHandler.When("/v1/applicants")
            .Respond("application/json", """{"id": "app_123"}""");
        
        var client = new NewProviderApiClient(
            mockHandler.ToHttpClient(),
            new NewProviderConfiguration { ApiKey = "test" },
            NullLogger<NewProviderApiClient>.Instance);
        
        var sut = new NewProviderProvider(
            client,
            new NewProviderConfiguration(),
            NullLogger<NewProviderProvider>.Instance);
        
        // Act
        var result = await sut.CreateApplicantAsync(new CreateProviderApplicantRequest(
            InternalApplicantId: "int_123",
            FirstName: "John",
            LastName: "Doe",
            Email: "john@example.com",
            DateOfBirth: null,
            Address: null,
            Metadata: null));
        
        // Assert
        result.Success.Should().BeTrue();
        result.ProviderApplicantId.Should().Be("app_123");
    }
    
    [Fact]
    public void ValidateWebhookSignature_ValidSignature_ReturnsTrue()
    {
        // Arrange
        var config = new NewProviderConfiguration { WebhookSecret = "secret" };
        var sut = new NewProviderProvider(/* ... */);
        
        var payload = """{"event": "check.completed"}""";
        var signature = ComputeExpectedSignature(payload, "secret");
        
        // Act
        var result = sut.ValidateWebhookSignature(payload, signature);
        
        // Assert
        result.Should().BeTrue();
    }
}
```

## Adding Custom Check Types

### Step 1: Extend the Enum

```csharp
// In Kyc.Domain/Enums/CheckType.cs
public enum CheckType
{
    Identity,
    Document,
    Liveness,
    FaceMatch,
    Aml,
    Poa,
    Custom,
    
    // New check types
    CreditCheck,
    EmploymentVerification,
    AddressVerification
}
```

### Step 2: Update Provider Support

```csharp
// Providers declare support for new check types
public IReadOnlySet<CheckType> SupportedCheckTypes { get; } = new HashSet<CheckType>
{
    CheckType.Document,
    CheckType.Liveness,
    CheckType.CreditCheck  // New
};
```

### Step 3: Add Validation Rules

```csharp
// In Application layer
public class StartVerificationCommandValidator 
    : AbstractValidator<StartVerificationCommand>
{
    public StartVerificationCommandValidator(IProviderRegistry providers)
    {
        RuleFor(x => x.CheckTypes)
            .Must(checks => checks.All(c => 
                providers.GetProvider(x.ProviderId).SupportedCheckTypes.Contains(c)))
            .WithMessage("Provider does not support requested check types");
    }
}
```

## Custom Provider Selection Logic

### Implement IProviderSelector

```csharp
public class SmartProviderSelector : IProviderSelector
{
    private readonly IProviderRegistry _registry;
    private readonly IConfiguration _config;
    
    public async Task<ProviderSelection> SelectProviderAsync(
        ProviderSelectionContext context,
        CancellationToken cancellationToken = default)
    {
        // Priority 1: Explicit preference
        if (!string.IsNullOrEmpty(context.PreferredProviderId))
        {
            var preferred = _registry.TryGetProvider(context.PreferredProviderId);
            if (preferred != null && SupportsAllChecks(preferred, context.RequestedChecks))
            {
                return new ProviderSelection(
                    preferred.ProviderId,
                    GetFallback(preferred.ProviderId),
                    "User preference");
            }
        }
        
        // Priority 2: Cost optimization for high-volume tenants
        if (IsHighVolumeTenant(context.TenantId))
        {
            var cheapest = GetCheapestProvider(context.RequestedChecks);
            if (cheapest != null)
            {
                return new ProviderSelection(
                    cheapest.ProviderId,
                    GetFallback(cheapest.ProviderId),
                    "Cost optimization");
            }
        }
        
        // Priority 3: Country-specific routing
        var countryProvider = GetCountryOptimalProvider(
            context.Applicant.Nationality, 
            context.RequestedChecks);
        if (countryProvider != null)
        {
            return new ProviderSelection(
                countryProvider.ProviderId,
                GetFallback(countryProvider.ProviderId),
                $"Optimal for {context.Applicant.Nationality}");
        }
        
        // Default
        return new ProviderSelection(
            _config["Kyc:DefaultProvider"],
            _config["Kyc:FallbackProvider"],
            "Default");
    }
}
```

### Register Custom Selector

```csharp
services.AddKycAggregator(options => { ... })
    .UseProviderSelector<SmartProviderSelector>();
```

## Adding Pipeline Behaviors

### Caching Behavior

```csharp
public class CachingBehavior<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICacheableQuery
{
    private readonly IDistributedCache _cache;
    
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var cacheKey = request.GetCacheKey();
        
        var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (cached != null)
        {
            return JsonSerializer.Deserialize<TResponse>(cached)!;
        }
        
        var response = await next();
        
        await _cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(response),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = request.CacheDuration
            },
            cancellationToken);
        
        return response;
    }
}
```

### Registration

```csharp
services.AddKycAggregator(options => { ... })
    .AddBehavior<CachingBehavior<,>>();
```

## Custom Domain Event Handlers

### Subscribe to Events

```csharp
public class NotifyOnVerificationCompleted 
    : INotificationHandler<VerificationCompletedEvent>
{
    private readonly IEmailService _emailService;
    private readonly IApplicantRepository _applicants;
    
    public async Task Handle(
        VerificationCompletedEvent notification,
        CancellationToken cancellationToken)
    {
        var applicant = await _applicants.GetByIdAsync(
            notification.ApplicantId, 
            cancellationToken);
        
        await _emailService.SendAsync(
            applicant.Email,
            "Verification Complete",
            $"Your verification is {notification.Result}");
    }
}
```

### Custom Events

```csharp
// Define custom event
public record HighRiskApplicantDetectedEvent(
    ApplicantId ApplicantId,
    string RiskScore,
    IReadOnlyList<string> RiskFactors
) : IDomainEvent;

// Raise from domain
public class Verification
{
    public void CompleteCheck(CheckType checkType, CheckResult result)
    {
        // ... existing logic ...
        
        if (result == CheckResult.Consider && 
            CalculateRiskScore() > RiskThreshold)
        {
            AddDomainEvent(new HighRiskApplicantDetectedEvent(
                ApplicantId,
                CalculateRiskScore().ToString(),
                GetRiskFactors()));
        }
    }
}
```

## Custom Validators

### FluentValidation Extension

```csharp
public static class KycValidatorExtensions
{
    public static IRuleBuilderOptions<T, string> MustBeValidCountryCode<T>(
        this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .Length(2)
            .Matches("^[A-Z]{2}$")
            .Must(code => ISO3166.IsValid(code))
            .WithMessage("Must be a valid ISO 3166-1 alpha-2 country code");
    }
    
    public static IRuleBuilderOptions<T, DateOnly?> MustBeAdult<T>(
        this IRuleBuilder<T, DateOnly?> ruleBuilder,
        int minimumAge = 18)
    {
        return ruleBuilder
            .Must(dob => !dob.HasValue || 
                         dob.Value.AddYears(minimumAge) <= DateOnly.FromDateTime(DateTime.Today))
            .WithMessage($"Must be at least {minimumAge} years old");
    }
}

// Usage
public class CreateApplicantRequestValidator 
    : AbstractValidator<CreateApplicantRequest>
{
    public CreateApplicantRequestValidator()
    {
        RuleFor(x => x.Nationality)
            .MustBeValidCountryCode();
        
        RuleFor(x => x.DateOfBirth)
            .MustBeAdult(18);
    }
}
```

## Custom Repository Implementations

### Alternative Storage

```csharp
// MongoDB implementation
public class MongoApplicantRepository : IApplicantRepository
{
    private readonly IMongoCollection<ApplicantDocument> _collection;
    
    public async Task<Applicant?> GetByIdAsync(
        ApplicantId id,
        CancellationToken cancellationToken = default)
    {
        var document = await _collection
            .Find(x => x.Id == id.Value)
            .FirstOrDefaultAsync(cancellationToken);
        
        return document?.ToDomain();
    }
    
    public async Task SaveAsync(
        Applicant applicant,
        CancellationToken cancellationToken = default)
    {
        var document = ApplicantDocument.FromDomain(applicant);
        
        await _collection.ReplaceOneAsync(
            x => x.Id == applicant.Id.Value,
            document,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }
}

// Registration
services.AddKycAggregatorCore()
    .UseRepository<IApplicantRepository, MongoApplicantRepository>();
```

## Extending the API

### Additional Endpoints

```csharp
// In your host project, using Minimal APIs
public static class CustomVerificationEndpoints
{
    public static void MapCustomVerificationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("api/v1/verifications/{verificationId}/resend-notification",
            async (Guid verificationId, IMediator mediator, CancellationToken ct) =>
            {
                var command = new ResendVerificationNotificationCommand(verificationId);
                await mediator.Send(command, ct);
                return Results.Ok();
            });
    }
}
```

### Custom Middleware

```csharp
public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;
    
    public async Task InvokeAsync(
        HttpContext context,
        ITenantContext tenantContext)
    {
        var tenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        
        if (!string.IsNullOrEmpty(tenantId))
        {
            tenantContext.SetTenant(tenantId);
        }
        
        await _next(context);
    }
}

// Registration
app.UseMiddleware<TenantContextMiddleware>();
```

## Plugin Architecture (Future)

For runtime-loaded providers:

```csharp
public interface IProviderPlugin
{
    string ProviderId { get; }
    IKycProvider CreateProvider(IServiceProvider services);
    void ConfigureServices(IServiceCollection services);
}

// Plugin loading
public class PluginLoader
{
    public void LoadPlugins(string pluginDirectory)
    {
        foreach (var dll in Directory.GetFiles(pluginDirectory, "*.dll"))
        {
            var assembly = Assembly.LoadFrom(dll);
            var pluginTypes = assembly.GetTypes()
                .Where(t => typeof(IProviderPlugin).IsAssignableFrom(t));
            
            foreach (var pluginType in pluginTypes)
            {
                var plugin = (IProviderPlugin)Activator.CreateInstance(pluginType)!;
                RegisterPlugin(plugin);
            }
        }
    }
}
```

## Related Documents

- [ARCHITECTURE.md](./ARCHITECTURE.md) - System architecture
- [PROVIDER_INTEGRATION.md](./PROVIDER_INTEGRATION.md) - Provider details
- [NUGET_STRATEGY.md](./NUGET_STRATEGY.md) - Package distribution
