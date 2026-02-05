# Folder Structure

This document defines the solution and project organization for the KYC Aggregator system. The structure enforces Clean Architecture principles and modular boundaries.

## Solution Overview

```
Kyc/
├── src/
│   ├── Kyc.Domain/                    # Core domain logic (innermost layer)
│   ├── Kyc.Application/               # Use cases and application services
│   ├── Kyc.Infrastructure/            # Data access, external integrations
│   ├── Kyc.Api/                       # HTTP API host (Minimal APIs)
│   ├── Kyc.AppHost/                   # .NET Aspire orchestration host
│   ├── Kyc.ServiceDefaults/           # Shared hosting defaults (.NET Aspire ready)
│   │
│   ├── Providers/                     # KYC provider implementations (future)
│   │   ├── Kyc.Providers.Abstractions/  # Provider interfaces and contracts
│   │   ├── Kyc.Providers.Onfido/        # Onfido integration
│   │   ├── Kyc.Providers.Jumio/         # Jumio integration
│   │   └── Kyc.Providers.Veriff/        # Veriff integration
│   │
│   └── Kyc.Contracts/                 # Shared DTOs for NuGet consumers
│
├── tests/
│   ├── Kyc.Domain.Tests/              # Domain unit tests
│   ├── Kyc.Application.Tests/         # Application layer tests
│   ├── Kyc.Infrastructure.Tests/      # Infrastructure integration tests
│   ├── Kyc.Api.Tests/                 # API integration tests
│   ├── Kyc.Providers.Tests/           # Provider-specific tests
│   └── Kyc.Architecture.Tests/        # Architecture enforcement tests
│
├── docs/                              # Documentation
│   ├── ADRS/                          # Architecture Decision Records
│   └── *.md                           # Documentation files
│
├── scripts/                           # Build, deployment, migration scripts
├── docker/                            # Docker-related files
│
├── Kyc.sln                            # Main solution file
├── Directory.Build.props              # Shared MSBuild properties
├── Directory.Packages.props           # Central package management
├── .editorconfig                      # Code style configuration
└── README.md                          # Project overview
```

## Project Details

### Core Projects

#### Kyc.Domain

The heart of the system. Contains business logic with zero external dependencies.

```
Kyc.Domain/
├── Entities/
│   ├── Applicant.cs                   # Person being verified
│   ├── Verification.cs                # Verification process
│   ├── VerificationCheck.cs           # Individual check within verification
│   └── Document.cs                    # Uploaded identity document
│
├── ValueObjects/
│   ├── ApplicantId.cs                 # Strongly-typed ID
│   ├── VerificationId.cs              # Strongly-typed ID
│   ├── Email.cs                       # Validated email
│   ├── PhoneNumber.cs                 # Validated phone
│   ├── Address.cs                     # Structured address
│   └── PersonName.cs                  # First/middle/last name
│
├── Enums/
│   ├── VerificationStatus.cs          # Pending, InProgress, Completed, Failed
│   ├── VerificationResult.cs          # Approved, Rejected, NeedsReview
│   ├── CheckType.cs                   # Identity, Document, Liveness, AML
│   └── DocumentType.cs                # Passport, DriversLicense, NationalId
│
├── Events/
│   ├── VerificationStartedEvent.cs
│   ├── VerificationCompletedEvent.cs
│   ├── CheckCompletedEvent.cs
│   └── ApplicantCreatedEvent.cs
│
├── Exceptions/
│   ├── DomainException.cs             # Base domain exception
│   ├── InvalidVerificationStateException.cs
│   └── ApplicantNotFoundException.cs
│
├── Repositories/                      # Repository interfaces
│   ├── IApplicantRepository.cs
│   ├── IVerificationRepository.cs
│   └── IUnitOfWork.cs
│
├── Services/                          # Domain services
│   ├── IVerificationOrchestrator.cs
│   └── IApplicantDuplicateChecker.cs
│
└── Kyc.Domain.csproj
```

#### Kyc.Application

Use cases and application orchestration. Depends only on Domain.

```
Kyc.Application/
├── Common/
│   ├── Behaviors/
│   │   ├── LoggingBehavior.cs         # Cross-cutting logging
│   │   ├── ValidationBehavior.cs      # Request validation
│   │   └── TransactionBehavior.cs     # Unit of work wrapper
│   │
│   ├── Interfaces/
│   │   ├── IKycProvider.cs            # Provider abstraction
│   │   ├── IDateTimeProvider.cs       # Testable time
│   │   └── ICurrentUserService.cs     # Current user context
│   │
│   ├── Mappings/
│   │   └── MappingProfile.cs          # AutoMapper or Mapster profiles
│   │
│   └── Exceptions/
│       ├── ApplicationException.cs
│       ├── ValidationException.cs
│       └── NotFoundException.cs
│
├── Applicants/
│   ├── Commands/
│   │   ├── CreateApplicant/
│   │   │   ├── CreateApplicantCommand.cs
│   │   │   ├── CreateApplicantCommandHandler.cs
│   │   │   └── CreateApplicantCommandValidator.cs
│   │   └── UpdateApplicant/
│   │       └── ...
│   │
│   ├── Queries/
│   │   ├── GetApplicant/
│   │   │   ├── GetApplicantQuery.cs
│   │   │   ├── GetApplicantQueryHandler.cs
│   │   │   └── ApplicantDto.cs
│   │   └── ListApplicants/
│   │       └── ...
│   │
│   └── EventHandlers/
│       └── ApplicantCreatedEventHandler.cs
│
├── Verifications/
│   ├── Commands/
│   │   ├── StartVerification/
│   │   │   ├── StartVerificationCommand.cs
│   │   │   ├── StartVerificationCommandHandler.cs
│   │   │   └── StartVerificationCommandValidator.cs
│   │   ├── CancelVerification/
│   │   │   └── ...
│   │   └── ProcessWebhook/
│   │       └── ...
│   │
│   ├── Queries/
│   │   ├── GetVerificationStatus/
│   │   │   └── ...
│   │   └── GetVerificationHistory/
│   │       └── ...
│   │
│   └── EventHandlers/
│       └── VerificationCompletedEventHandler.cs
│
├── DependencyInjection.cs             # IServiceCollection extensions
└── Kyc.Application.csproj
```

#### Kyc.Infrastructure

External concerns: database, APIs, messaging.

```
Kyc.Infrastructure/
├── Persistence/
│   ├── KycDbContext.cs                # EF Core DbContext
│   ├── Configurations/                # Entity configurations
│   │   ├── ApplicantConfiguration.cs
│   │   ├── VerificationConfiguration.cs
│   │   └── VerificationCheckConfiguration.cs
│   │
│   ├── Repositories/
│   │   ├── ApplicantRepository.cs
│   │   ├── VerificationRepository.cs
│   │   └── UnitOfWork.cs
│   │
│   ├── Migrations/                    # EF Core migrations
│   │   └── ...
│   │
│   └── Interceptors/
│       ├── AuditableEntityInterceptor.cs
│       └── DomainEventDispatcherInterceptor.cs
│
├── Services/
│   ├── DateTimeProvider.cs
│   └── CurrentUserService.cs
│
├── Outbox/                            # Outbox pattern for reliable messaging
│   ├── OutboxMessage.cs
│   └── OutboxProcessor.cs
│
├── DependencyInjection.cs
└── Kyc.Infrastructure.csproj
```

#### Kyc.Api

HTTP API host and composition root. Uses **Minimal APIs** (not controllers).

```
Kyc.Api/
├── Endpoints/                         # Minimal API endpoint groups
│   ├── ApplicantEndpoints.cs
│   ├── VerificationEndpoints.cs
│   └── WebhookEndpoints.cs
│
├── Middleware/
│   ├── ExceptionHandlingMiddleware.cs
│   ├── CorrelationIdMiddleware.cs
│   └── RequestLoggingMiddleware.cs
│
├── Models/
│   ├── Requests/                      # API request models
│   │   ├── CreateApplicantRequest.cs
│   │   └── StartVerificationRequest.cs
│   │
│   └── Responses/                     # API response models
│       ├── ApiResponse.cs
│       ├── ApplicantResponse.cs
│       └── VerificationResponse.cs
│
├── Configuration/
│   └── SwaggerConfiguration.cs
│
├── Program.cs                         # Entry point
├── appsettings.json
├── appsettings.Development.json
└── Kyc.Api.csproj
```

#### Kyc.AppHost

.NET Aspire orchestration host for local development.

```
Kyc.AppHost/
├── Program.cs                         # Aspire app host configuration
└── Kyc.AppHost.csproj
```

### Provider Projects

#### Kyc.Providers.Abstractions

Contracts shared across all providers.

```
Kyc.Providers.Abstractions/
├── IKycProvider.cs                    # Main provider interface
├── IProviderWebhookHandler.cs         # Webhook processing
├── Models/
│   ├── ProviderCheckRequest.cs
│   ├── ProviderCheckResult.cs
│   ├── ProviderApplicant.cs
│   └── WebhookPayload.cs
│
├── Configuration/
│   └── ProviderConfiguration.cs
│
└── Kyc.Providers.Abstractions.csproj
```

#### Provider Implementation (e.g., Kyc.Providers.Onfido)

```
Kyc.Providers.Onfido/
├── OnfidoProvider.cs                  # IKycProvider implementation
├── OnfidoWebhookHandler.cs            # Webhook processing
├── OnfidoConfiguration.cs             # Onfido-specific settings
│
├── Client/
│   ├── OnfidoApiClient.cs             # HTTP client wrapper
│   ├── OnfidoApiException.cs
│   └── Models/                        # Onfido API DTOs
│       ├── OnfidoApplicant.cs
│       ├── OnfidoCheck.cs
│       └── OnfidoWebhookEvent.cs
│
├── Mappings/
│   └── OnfidoMappingProfile.cs        # Map Onfido models to abstractions
│
├── DependencyInjection.cs             # AddOnfidoProvider extension
└── Kyc.Providers.Onfido.csproj
```

### Shared Projects

#### Kyc.Contracts

Public contracts for NuGet package consumers.

```
Kyc.Contracts/
├── Requests/
│   ├── CreateApplicantRequest.cs
│   └── StartVerificationRequest.cs
│
├── Responses/
│   ├── ApplicantResponse.cs
│   ├── VerificationResponse.cs
│   └── VerificationStatusResponse.cs
│
├── Enums/
│   ├── VerificationStatus.cs
│   └── VerificationResult.cs
│
└── Kyc.Contracts.csproj
```

#### Kyc.ServiceDefaults

Shared hosting configuration.

```
Kyc.ServiceDefaults/
├── Extensions/
│   ├── HostingExtensions.cs
│   ├── OpenTelemetryExtensions.cs
│   └── HealthCheckExtensions.cs
│
└── Kyc.ServiceDefaults.csproj
```

### Test Projects

```
tests/
├── Kyc.Domain.Tests/
│   ├── Entities/
│   │   ├── VerificationTests.cs
│   │   └── ApplicantTests.cs
│   └── ValueObjects/
│       └── EmailTests.cs
│
├── Kyc.Application.Tests/
│   ├── Verifications/
│   │   └── StartVerificationCommandTests.cs
│   └── Common/
│       └── ValidationBehaviorTests.cs
│
├── Kyc.Architecture.Tests/
│   └── ArchitectureTests.cs           # Enforce layer dependencies
│
├── Kyc.Infrastructure.Tests/          # (future)
│   ├── Persistence/
│   │   └── VerificationRepositoryTests.cs
│   └── Fixtures/
│       └── DatabaseFixture.cs
│
├── Kyc.Api.Tests/                     # (future)
│   ├── Endpoints/
│   │   └── ApplicantEndpointTests.cs
│   └── Integration/
│       └── VerificationFlowTests.cs
│
└── Kyc.Providers.Tests/              # (future)
    └── ...
```

## Project References

```
Dependency Flow (arrows show "depends on"):

Kyc.AppHost
  └──▶ Kyc.Api (orchestrates via .NET Aspire)

Kyc.Api
  ├──▶ Kyc.Application
  ├──▶ Kyc.Infrastructure
  ├──▶ Kyc.Providers.Onfido (and other providers, future)
  └──▶ Kyc.ServiceDefaults

Kyc.Application
  └──▶ Kyc.Domain
  └──▶ Kyc.Providers.Abstractions

Kyc.Infrastructure
  ├──▶ Kyc.Domain
  └──▶ Kyc.Application

Kyc.Providers.Onfido
  └──▶ Kyc.Providers.Abstractions

Kyc.Contracts
  └──▶ (no dependencies - pure DTOs)
```

## NuGet Package Structure

For embedded usage, these projects are packaged:

| Package | Contains | Depends On |
|---------|----------|------------|
| `Kyc.Aggregator.Core` | Domain + Application | - |
| `Kyc.Aggregator.Infrastructure` | EF Core, PostgreSQL | Core |
| `Kyc.Aggregator.Contracts` | Public DTOs | - |
| `Kyc.Aggregator.Provider.Onfido` | Onfido integration | Core |
| `Kyc.Aggregator.Provider.Jumio` | Jumio integration | Core |

## Conventions

### Naming

- Commands: `{Verb}{Entity}Command` (e.g., `CreateApplicantCommand`)
- Queries: `Get{Entity}Query` or `List{Entities}Query`
- Handlers: `{Command/Query}Handler`
- DTOs: `{Entity}Dto` or `{Entity}Response`

### File Organization

- One class per file (except nested types)
- Folder structure mirrors namespace structure
- Feature folders for Application layer (vertical slices)

### Dependencies

- Central Package Management via `Directory.Packages.props`
- All projects use the same version of shared packages
- Provider projects are independently versioned

## Related Documents

- [ARCHITECTURE.md](./ARCHITECTURE.md) - Architectural principles
- [DOMAIN_MODEL.md](./DOMAIN_MODEL.md) - Domain entities
- [NUGET_STRATEGY.md](./NUGET_STRATEGY.md) - Package organization
