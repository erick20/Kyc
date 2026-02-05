# Architecture

This document describes the overall architecture of the KYC Aggregator system, explaining the design principles, patterns, and structural decisions that guide development.

## Architectural Style: Modular Monolith

KYC Aggregator follows a **Modular Monolith** architecture. This approach combines the deployment simplicity of a monolith with the organizational benefits of modular design, making it ideal for:

- Small-to-medium teams that don't need microservices complexity
- Systems that may evolve toward microservices later
- Projects requiring strong domain boundaries without network overhead

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           DEPLOYMENT UNIT                               │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────────────┐  │
│  │   Verification  │  │    Applicant    │  │      Providers          │  │
│  │     Module      │  │     Module      │  │       Module            │  │
│  │                 │  │                 │  │  ┌─────┐ ┌─────┐        │  │
│  │  Domain         │  │  Domain         │  │  │Onfido│ │Jumio│ ...   │  │
│  │  Application    │  │  Application    │  │  └─────┘ └─────┘        │  │
│  │  Infrastructure │  │  Infrastructure │  │                         │  │
│  └────────┬────────┘  └────────┬────────┘  └────────────┬────────────┘  │
│           │                    │                        │               │
│           └────────────────────┼────────────────────────┘               │
│                                │                                        │
│                    ┌───────────┴───────────┐                           │
│                    │    Shared Kernel      │                           │
│                    │  (Common Abstractions)│                           │
│                    └───────────────────────┘                           │
└─────────────────────────────────────────────────────────────────────────┘
```

### Module Boundaries

Each module:
- Owns its domain logic and data
- Exposes a public API (interfaces/contracts) for inter-module communication
- Has its own persistence (schema/tables) within the shared database
- Communicates with other modules through well-defined interfaces or domain events

### Why Not Microservices?

For this project's scope and team size, microservices would introduce:
- Unnecessary network latency for provider orchestration
- Operational complexity (service discovery, distributed tracing, etc.)
- Data consistency challenges across service boundaries

The modular monolith preserves the ability to extract modules into services later if scale demands it.

## Clean Architecture Layers

Within each module, we apply **Clean Architecture** (also known as Onion/Hexagonal Architecture):

```
                    ┌─────────────────────────────────────┐
                    │           API / Host                │
                    │   (Minimal API Endpoints, Middleware)│
                    └─────────────────┬───────────────────┘
                                      │
                    ┌─────────────────▼───────────────────┐
                    │         Application Layer           │
                    │   (Use Cases, Commands, Queries)    │
                    │   (DTOs, Validators, Mappers)       │
                    └─────────────────┬───────────────────┘
                                      │
                    ┌─────────────────▼───────────────────┐
                    │          Domain Layer               │
                    │  (Entities, Value Objects, Events)  │
                    │  (Repository Interfaces, Services)  │
                    └─────────────────┬───────────────────┘
                                      │
                    ┌─────────────────▼───────────────────┐
                    │       Infrastructure Layer          │
                    │  (EF Core, HTTP Clients, SDKs)      │
                    │  (Repository Implementations)       │
                    └─────────────────────────────────────┘
```

### Layer Responsibilities

#### Domain Layer (Innermost)
- **Zero external dependencies** (except .NET base libraries)
- Contains business logic, entities, value objects, domain events
- Defines repository interfaces (not implementations)
- Enforces invariants and business rules
- **Persistence-ignorant**: No EF Core attributes or references
- Example: `Verification`, `Applicant`, `VerificationStatus`

#### Application Layer
- Orchestrates domain objects to fulfill use cases
- Implements CQRS pattern (Commands and Queries)
- Contains application services, validators, DTOs
- Defines port interfaces for external dependencies
- **Depends on abstractions**: Uses `IApplicantRepository`, `IUnitOfWork` — NOT `DbContext`
- Example: `StartVerificationCommand`, `GetVerificationStatusQuery`

#### Infrastructure Layer
- Implements interfaces defined by inner layers
- Database access (Entity Framework Core with PostgreSQL)
- External API integrations (KYC provider SDKs/APIs)
- Caching, messaging, file storage implementations
- **Owns all EF Core concerns**: DbContext, configurations, migrations
- Example: `PostgresVerificationRepository`, `OnfidoApiClient`

#### API/Host Layer (Outermost)
- Minimal API endpoints and middleware
- Request/response mapping
- Authentication and authorization configuration
- Dependency injection composition root
- Example: `ApplicantEndpoints`, `VerificationEndpoints`

### Dependency Rule

**Dependencies point inward only.** Inner layers have no knowledge of outer layers.

```
API → Application → Domain ← Infrastructure
                      ↑
            Infrastructure implements
            interfaces defined in Domain
```

---

## Persistence Architecture

### Overview

The persistence layer follows strict Clean Architecture principles:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                      PERSISTENCE RESPONSIBILITY                             │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  Domain Layer (Kyc.Domain)                                                  │
│  ─────────────────────────                                                  │
│  ✓ Pure C# entities (POCO)                                                  │
│  ✓ Repository interfaces (IApplicantRepository, IVerificationRepository)   │
│  ✓ IUnitOfWork interface                                                    │
│  ✓ Value objects, domain events                                             │
│  ✗ NO EF Core package reference                                             │
│  ✗ NO [Key], [Required], [Table] attributes                                 │
│  ✗ NO DbContext awareness                                                   │
│                                                                             │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  Application Layer (Kyc.Application)                                        │
│  ───────────────────────────────────                                        │
│  ✓ Injects IApplicantRepository, IUnitOfWork                                │
│  ✓ Orchestrates domain operations                                           │
│  ✗ NO EF Core package reference                                             │
│  ✗ NO DbContext injection                                                   │
│  ✗ NO IQueryable usage                                                      │
│                                                                             │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  Infrastructure Layer (Kyc.Infrastructure)                                  │
│  ─────────────────────────────────────────                                  │
│  ✓ KycDbContext : DbContext                                                 │
│  ✓ Entity configurations (Fluent API)                                       │
│  ✓ Repository implementations                                               │
│  ✓ EF Core migrations                                                       │
│  ✓ Interceptors (audit, domain events)                                      │
│  ✓ Value converters for value objects                                       │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### ORM and Database

| Aspect | Decision |
|--------|----------|
| **ORM** | Entity Framework Core 10+ (only supported ORM) |
| **Database** | PostgreSQL 14+ (only supported database) |
| **Approach** | Code-first with EF Core migrations |
| **Provider** | Npgsql.EntityFrameworkCore.PostgreSQL |

### DbContext Strategy

**Decision**: Single `KycDbContext` with module separation via schemas.

| Approach | Selected | Rationale |
|----------|----------|-----------|
| Single DbContext | ✓ | Simpler transactions, single connection, easier migrations |
| Per-module DbContext | | Future option if strong isolation needed |

Modules use separate PostgreSQL schemas (e.g., `kyc`, `billing`) for logical separation while sharing the same DbContext and transaction boundary.

### Data Access Patterns

| Pattern | Usage |
|---------|-------|
| **Repository Pattern** | Selective use for aggregate roots — not generic `IRepository<T>` |
| **Unit of Work** | Explicit `IUnitOfWork.SaveChangesAsync()` calls |
| **Specification Pattern** | Optional for complex, reusable query criteria |

**Explicitly Avoided:**
- Generic repository with CRUD methods
- Leaking `IQueryable` outside Infrastructure
- Direct DbContext injection in Application layer

### IQueryable Containment

```
                    Application Layer
                          │
                          │ calls
                          ▼
              ┌─────────────────────────┐
              │   IApplicantRepository  │
              │                         │
              │ Task<Applicant?> GetById│  ←── Returns materialized entities
              │ Task<List<..>> Find...  │
              └─────────────────────────┘
                          │
                          │ implemented by
                          ▼
              ┌─────────────────────────┐
              │ ApplicantRepository     │
              │                         │
              │ _context.Applicants     │  ←── IQueryable stays here
              │   .Where(...)           │
              │   .ToListAsync()        │  ←── Materialized before return
              └─────────────────────────┘
```

### Persistence-Ignorant Entities

Domain entities must remain pure C# with no EF Core dependencies:

```csharp
// ✓ ALLOWED - Pure domain entity
public class Applicant : Entity<ApplicantId>
{
    public string FirstName { get; private set; }
    public Email Email { get; private set; }
    private readonly List<Document> _documents = new();
    
    private Applicant() { }  // EF Core uses this
    
    public static Applicant Create(...) { ... }
}

// ✗ NOT ALLOWED - EF Core attributes in domain
public class Applicant
{
    [Key]
    public Guid Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; }
}
```

All mapping is done via Fluent API in Infrastructure layer.

---

## Key Architectural Patterns

### CQRS (Command Query Responsibility Segregation)

We separate operations that modify state (Commands) from those that read state (Queries):

```
┌─────────────┐     ┌──────────────────┐     ┌─────────────┐
│   Command   │────▶│  Command Handler │────▶│   Domain    │
│   (Write)   │     │                  │     │   Model     │
└─────────────┘     └──────────────────┘     └──────┬──────┘
                                                    │
                                                    ▼
┌─────────────┐     ┌──────────────────┐     ┌─────────────┐
│   Query     │────▶│  Query Handler   │────▶│  Read Model │
│   (Read)    │     │                  │     │  (Optimized)│
└─────────────┘     └──────────────────┘     └─────────────┘
```

Benefits:
- Optimized read paths (can bypass domain layer for simple queries)
- Clear separation of concerns
- Easier testing and reasoning about side effects

### Domain Events

Modules communicate through domain events, enabling loose coupling:

```csharp
// Published when verification completes
public record VerificationCompletedEvent(
    Guid VerificationId,
    VerificationStatus Status,
    DateTime CompletedAt
) : IDomainEvent;
```

Events are:
- Raised by domain entities
- Dispatched by the application layer (via EF Core interceptor)
- Handled by other modules or external systems

### Repository Pattern

Domain defines repository interfaces; infrastructure provides implementations:

```
Domain Layer:
  interface IVerificationRepository
    - GetByIdAsync(id): Task<Verification?>
    - AddAsync(verification): Task
    - Update(verification): void

Infrastructure Layer:
  class VerificationRepository : IVerificationRepository
    - Injects KycDbContext
    - Uses EF Core for data access
    - Returns domain entities
```

### Strategy Pattern for Providers

KYC providers are abstracted behind a common interface:

```
┌───────────────────┐
│   IKycProvider    │◄─────────────────────────────────────┐
│                   │                                      │
│ + StartCheck()    │     ┌──────────────┐  ┌────────────┐ │
│ + GetStatus()     │     │OnfidoProvider│  │JumioProvider│ │
│ + HandleWebhook() │     └──────────────┘  └────────────┘ │
└───────────────────┘              ▲               ▲       │
                                   └───────────────┴───────┘
```

This enables:
- Adding/removing providers without changing core logic
- Provider-specific configuration isolation
- Easy mocking for tests

---

## Cross-Cutting Concerns

### Logging
- Structured logging using Serilog
- Correlation IDs for request tracing
- Sensitive data masking (PII protection)

### Validation
- Input validation at API layer (FluentValidation)
- Business rule validation in domain layer
- Provider-specific validation in infrastructure

### Error Handling
- Domain exceptions for business rule violations
- Application exceptions for use case failures
- Global exception middleware for consistent API responses

### Caching
- Provider response caching where appropriate
- Distributed cache support for scaled deployments
- Cache invalidation on state changes

---

## Future Considerations

### Admin Panel
The architecture anticipates an Admin Panel by:
- Exposing query endpoints suitable for admin dashboards
- Separating write operations that admins will need
- Keeping authorization granular (role-based access)

### Scalability Path
If needed, modules can be extracted:
1. Provider module → Separate service for high-volume providers
2. Webhook handling → Dedicated worker service
3. Reporting → Read replica with dedicated query service

### Event Sourcing (Optional)
The domain event infrastructure could evolve to full event sourcing if audit requirements increase. Current design makes this a natural progression.

### Multi-Module DbContext
If strong module isolation becomes necessary:
1. Extract per-module DbContext classes
2. Each module manages its own migrations
3. Cross-module queries via explicit integration contracts

---

## Technology Stack

| Concern | Technology | Rationale |
|---------|------------|-----------|
| Runtime | .NET 10 LTS | Long-term support, performance, ecosystem |
| Database | PostgreSQL 14+ | ACID compliance, JSON support, proven reliability |
| ORM | EF Core 10 | Strong .NET integration, migration support |
| Provider | Npgsql 10 | Mature PostgreSQL provider for EF Core |
| Validation | FluentValidation | Expressive, testable validation rules |
| CQRS | Custom Mediator | Lightweight, reflection-based pipeline with explicit control |
| Logging | Serilog | Structured logging, multiple sinks |
| Testing | xUnit + NSubstitute | Modern test framework, clean mocking |
| Containers | Docker | Consistent deployment, infrastructure as code |
| Orchestration | .NET Aspire | Local development experience |

---

## Related Documents

- [FOLDER_STRUCTURE.md](./FOLDER_STRUCTURE.md) - Detailed project organization
- [DOMAIN_MODEL.md](./DOMAIN_MODEL.md) - Domain entities and relationships
- [PROVIDER_INTEGRATION.md](./PROVIDER_INTEGRATION.md) - Provider abstraction details
- [DATABASE_DESIGN.md](./DATABASE_DESIGN.md) - EF Core approach, DbContext, migrations
- [DEPLOYMENT.md](./DEPLOYMENT.md) - Migrations in containers and Kubernetes
- [ADRS/](./ADRS/) - Architecture Decision Records
