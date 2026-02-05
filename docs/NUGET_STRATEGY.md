# NuGet Strategy

This document describes how the KYC Aggregator is designed to work both as a standalone HTTP API service and as an embeddable NuGet package for direct integration into .NET applications.

## Dual Deployment Model

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                      Deployment Option A: Standalone API                    │
│                                                                             │
│   ┌─────────────────┐     ┌─────────────────────────────────────────────┐  │
│   │  Your App       │────▶│           KYC Aggregator API                │  │
│   │  (Any Language) │HTTP │  ┌─────────────────────────────────────┐    │  │
│   └─────────────────┘     │  │         Kyc.Api Host                │    │  │
│                           │  │  ┌─────────────────────────────┐    │    │  │
│                           │  │  │ Kyc.Application + Domain    │    │    │  │
│                           │  │  └─────────────────────────────┘    │    │  │
│                           │  └─────────────────────────────────────┘    │  │
│                           └─────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                      Deployment Option B: Embedded NuGet                    │
│                                                                             │
│   ┌─────────────────────────────────────────────────────────────────────┐  │
│   │                         Your .NET Application                        │  │
│   │                                                                       │  │
│   │   ┌─────────────────────────────────────────────────────────────┐    │  │
│   │   │              Kyc.Aggregator.Core (NuGet)                    │    │  │
│   │   │  ┌─────────────────────────────────────────────────────┐    │    │  │
│   │   │  │ Kyc.Application + Domain (embedded)                 │    │    │  │
│   │   │  └─────────────────────────────────────────────────────┘    │    │  │
│   │   └─────────────────────────────────────────────────────────────┘    │  │
│   │                                                                       │  │
│   │   ┌─────────────────────────────────────────────────────────────┐    │  │
│   │   │         Kyc.Aggregator.Provider.Onfido (NuGet)              │    │  │
│   │   └─────────────────────────────────────────────────────────────┘    │  │
│   └─────────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Package Structure

### Core Packages

| Package | Description | Dependencies |
|---------|-------------|--------------|
| `Kyc.Aggregator.Contracts` | Public DTOs and interfaces | None |
| `Kyc.Aggregator.Core` | Domain + Application layers | Contracts |
| `Kyc.Aggregator.Infrastructure` | EF Core, PostgreSQL, services | Core |

### Provider Packages

| Package | Description | Dependencies |
|---------|-------------|--------------|
| `Kyc.Aggregator.Provider.Abstractions` | Provider interfaces | Contracts |
| `Kyc.Aggregator.Provider.Onfido` | Onfido integration | Abstractions |
| `Kyc.Aggregator.Provider.Jumio` | Jumio integration | Abstractions |
| `Kyc.Aggregator.Provider.Veriff` | Veriff integration | Abstractions |

### Optional Packages

| Package | Description | Dependencies |
|---------|-------------|--------------|
| `Kyc.Aggregator.AspNetCore` | ASP.NET Core integration helpers | Core, Infrastructure |
| `Kyc.Aggregator.Testing` | Test utilities and fakes | Core |

## Package Dependency Graph

```
                          Kyc.Aggregator.Contracts
                                   │
                    ┌──────────────┼──────────────┐
                    │              │              │
                    ▼              ▼              │
          Kyc.Aggregator.Core    Kyc.Aggregator  │
                    │            .Provider       │
                    │            .Abstractions   │
                    │              │              │
                    ▼              │              │
          Kyc.Aggregator          │              │
          .Infrastructure         │              │
                    │              │              │
                    ▼              ▼              │
          Kyc.Aggregator        Provider         │
          .AspNetCore          Packages ─────────┘
                                (Onfido, etc.)
```

## Integration Patterns

### Pattern 1: Full Embedded (Most Common)

For .NET applications that want complete control.

```csharp
// Install packages:
// dotnet add package Kyc.Aggregator.Core
// dotnet add package Kyc.Aggregator.Infrastructure
// dotnet add package Kyc.Aggregator.Provider.Onfido

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddKycAggregator(options =>
        {
            options.UsePostgres(Configuration.GetConnectionString("Kyc"));
        })
        .AddOnfido(config =>
        {
            config.ApiKey = Configuration["Onfido:ApiKey"];
            config.WebhookSecret = Configuration["Onfido:WebhookSecret"];
        })
        .AddJumio(config =>
        {
            config.ApiToken = Configuration["Jumio:ApiToken"];
            config.ApiSecret = Configuration["Jumio:ApiSecret"];
        });
    }
}
```

### Pattern 2: Core Only (Custom Infrastructure)

For applications with existing database infrastructure.

```csharp
// Install packages:
// dotnet add package Kyc.Aggregator.Core
// dotnet add package Kyc.Aggregator.Provider.Onfido

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Use your own DbContext
        services.AddDbContext<MyDbContext>();
        
        // Register core services only
        services.AddKycAggregatorCore();
        
        // Implement your own repositories
        services.AddScoped<IApplicantRepository, MyApplicantRepository>();
        services.AddScoped<IVerificationRepository, MyVerificationRepository>();
        
        // Add providers
        services.AddKycProvider<OnfidoProvider>(config => { ... });
    }
}
```

### Pattern 3: Contracts Only (API Client)

For applications that call the standalone API.

```csharp
// Install packages:
// dotnet add package Kyc.Aggregator.Contracts

public class KycService
{
    private readonly HttpClient _httpClient;
    
    public async Task<VerificationResponse> StartVerificationAsync(
        StartVerificationRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "/api/v1/verifications", 
            request);
        
        return await response.Content
            .ReadFromJsonAsync<VerificationResponse>();
    }
}
```

## Service Registration API

### Builder Pattern

```csharp
public interface IKycAggregatorBuilder
{
    IServiceCollection Services { get; }
    
    IKycAggregatorBuilder UsePostgres(string connectionString);
    IKycAggregatorBuilder UsePostgres(Action<DbContextOptionsBuilder> configure);
    
    IKycAggregatorBuilder AddProvider<TProvider>(Action<ProviderConfiguration> configure)
        where TProvider : class, IKycProvider;
    
    IKycAggregatorBuilder ConfigureOptions(Action<KycAggregatorOptions> configure);
}
```

### Extension Methods

```csharp
public static class KycAggregatorServiceCollectionExtensions
{
    public static IKycAggregatorBuilder AddKycAggregator(
        this IServiceCollection services,
        Action<KycAggregatorOptions>? configure = null)
    {
        var options = new KycAggregatorOptions();
        configure?.Invoke(options);
        
        services.AddSingleton(options);
        
        // Register core services (custom mediator)
        services.AddApplication(); // Registers mediator, validators, behaviors

        services.AddScoped<IVerificationService, VerificationService>();
        services.AddScoped<IApplicantService, ApplicantService>();
        services.AddSingleton<IProviderRegistry, ProviderRegistry>();

        return new KycAggregatorBuilder(services, options);
    }

    public static IKycAggregatorBuilder AddKycAggregatorCore(
        this IServiceCollection services)
    {
        // Core services only, no infrastructure
        services.AddApplication(); // Custom mediator with pipeline behaviors
        
        return new KycAggregatorBuilder(services, new KycAggregatorOptions());
    }
}
```

## Configuration Options

```csharp
public class KycAggregatorOptions
{
    /// <summary>
    /// Default provider to use when none specified
    /// </summary>
    public string? DefaultProviderId { get; set; }
    
    /// <summary>
    /// Fallback provider when primary fails
    /// </summary>
    public string? FallbackProviderId { get; set; }
    
    /// <summary>
    /// Enable automatic webhook processing
    /// </summary>
    public bool EnableWebhooks { get; set; } = true;
    
    /// <summary>
    /// Enable outbox pattern for reliable events
    /// </summary>
    public bool EnableOutbox { get; set; } = true;
    
    /// <summary>
    /// Verification expiry time
    /// </summary>
    public TimeSpan VerificationExpiry { get; set; } = TimeSpan.FromHours(1);
    
    /// <summary>
    /// Enable audit logging
    /// </summary>
    public bool EnableAuditLog { get; set; } = true;
}
```

## Direct Usage (No DI)

For scenarios without dependency injection.

```csharp
// Create services manually
var options = new KycAggregatorOptions { DefaultProviderId = "onfido" };
var dbContext = new KycDbContext(dbOptions);
var applicantRepo = new PostgresApplicantRepository(dbContext);
var verificationRepo = new PostgresVerificationRepository(dbContext);

var onfidoConfig = new OnfidoConfiguration { ApiKey = "..." };
var onfidoProvider = new OnfidoProvider(onfidoConfig, httpClientFactory);

var providerRegistry = new ProviderRegistry(new[] { onfidoProvider });
var verificationService = new VerificationService(
    verificationRepo, 
    applicantRepo, 
    providerRegistry,
    options);

// Use directly
var verification = await verificationService.StartVerificationAsync(
    applicantId,
    new[] { CheckType.Document, CheckType.Liveness });
```

## Webhook Handling

### With ASP.NET Core Integration

```csharp
// Automatic webhook endpoint registration
app.MapKycWebhooks("/webhooks/kyc");

// Or manual setup
app.MapPost("/webhooks/kyc/{providerId}", async (
    string providerId,
    HttpContext context,
    IWebhookProcessor processor) =>
{
    await processor.ProcessAsync(providerId, context.Request);
    return Results.Ok();
});
```

### Without ASP.NET Core

```csharp
// Process webhooks manually
public class MyWebhookHandler
{
    private readonly IWebhookProcessor _processor;
    
    public async Task HandleWebhookAsync(
        string providerId, 
        string payload, 
        string signature)
    {
        var result = await _processor.ProcessAsync(
            providerId, 
            payload, 
            signature);
        
        if (!result.Success)
        {
            // Handle error
        }
    }
}
```

## Event Integration

### Publishing Domain Events

```csharp
// Configure event publishing
services.AddKycAggregator(options => { ... })
    .AddEventPublishing(events =>
    {
        // Publish to your message bus
        events.OnVerificationCompleted(async evt =>
        {
            await messageBus.PublishAsync(new VerificationCompletedMessage
            {
                VerificationId = evt.VerificationId,
                Result = evt.Result
            });
        });
    });
```

### Subscribing to Events

```csharp
// In your application
public class VerificationCompletedHandler 
    : INotificationHandler<VerificationCompletedEvent>
{
    public async Task Handle(
        VerificationCompletedEvent notification, 
        CancellationToken cancellationToken)
    {
        // Your custom logic
        await SendNotificationAsync(notification.ApplicantId);
    }
}
```

## Versioning Strategy

### Semantic Versioning

- **Major**: Breaking changes to public APIs
- **Minor**: New features, backward compatible
- **Patch**: Bug fixes, backward compatible

### Version Compatibility

| Core Version | Infrastructure Version | Provider Versions |
|--------------|------------------------|-------------------|
| 1.x | 1.x | 1.x |
| 2.x | 2.x | 1.x or 2.x |

### Breaking Change Policy

1. Deprecation warnings for one minor version
2. Breaking changes only in major versions
3. Clear migration guides provided
4. Long-term support for previous major version

## Package Publishing

### NuGet.org (Public)

For open-source distribution:

```xml
<PropertyGroup>
    <PackageId>Kyc.Aggregator.Core</PackageId>
    <Version>1.0.0</Version>
    <Authors>Your Company</Authors>
    <Description>KYC Aggregator core functionality</Description>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/your-org/kyc-aggregator</PackageProjectUrl>
    <RepositoryUrl>https://github.com/your-org/kyc-aggregator</RepositoryUrl>
    <PackageTags>kyc;identity-verification;onfido;jumio</PackageTags>
</PropertyGroup>
```

### Private Feed (Enterprise)

For internal distribution:

```xml
<!-- nuget.config -->
<configuration>
    <packageSources>
        <add key="YourCompany" value="https://pkgs.yourcompany.com/nuget/v3/index.json" />
    </packageSources>
</configuration>
```

## Testing Support

### Testing Package

```csharp
// Install: dotnet add package Kyc.Aggregator.Testing

public class VerificationTests
{
    [Fact]
    public async Task Should_Complete_Verification()
    {
        // Arrange
        var fakeProvider = new FakeKycProvider()
            .WithCheckResult(CheckType.Document, CheckResult.Clear)
            .WithCheckResult(CheckType.Liveness, CheckResult.Clear);
        
        var services = new ServiceCollection()
            .AddKycAggregator()
            .UseInMemoryDatabase()
            .AddProvider(fakeProvider)
            .BuildServiceProvider();
        
        var sut = services.GetRequiredService<IVerificationService>();
        
        // Act
        var result = await sut.StartVerificationAsync(applicantId, checks);
        
        // Assert
        result.Status.Should().Be(VerificationStatus.Completed);
    }
}
```

### In-Memory Infrastructure

```csharp
services.AddKycAggregator(options => { ... })
    .UseInMemoryDatabase()  // For testing
    .AddFakeProvider("test", config =>
    {
        config.DefaultResult = CheckResult.Clear;
        config.DelayMs = 0;
    });
```

## Migration Path

### From Standalone API to Embedded

1. Install NuGet packages
2. Remove HTTP client code
3. Inject services directly
4. Configure providers in DI
5. Handle webhooks locally

### From Embedded to Standalone API

1. Deploy Kyc.Api service
2. Configure API client
3. Update service calls to HTTP
4. Route webhooks to API

## Related Documents

- [ARCHITECTURE.md](./ARCHITECTURE.md) - System architecture
- [FOLDER_STRUCTURE.md](./FOLDER_STRUCTURE.md) - Project organization
- [EXTENSIBILITY.md](./EXTENSIBILITY.md) - Extension points
