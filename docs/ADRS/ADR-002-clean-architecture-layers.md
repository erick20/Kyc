# ADR-002: Clean Architecture Layers

## Status

**Accepted**

## Date

2024-01-15

## Context

Having decided on a Modular Monolith architecture (ADR-001), we need to define how code is organized within each module and across the solution.

The goals are:
1. Clear separation of concerns
2. Testability without infrastructure dependencies
3. Flexibility to change infrastructure (database, providers) without affecting business logic
4. Support for both API hosting and NuGet package consumption

### Options Considered

#### Option A: Traditional N-Tier
Layers: Presentation → Business Logic → Data Access

**Pros:**
- Familiar to most developers
- Simple structure

**Cons:**
- Business logic often leaks into other layers
- Data access shapes business logic
- Tight coupling to ORM/database
- Difficult to test business logic in isolation

#### Option B: Clean Architecture (Onion/Hexagonal)
Layers: Domain (center) → Application → Infrastructure → API (outer)

**Pros:**
- Domain logic is completely isolated
- Infrastructure is pluggable
- Highly testable
- Clear dependency direction

**Cons:**
- More initial ceremony
- Requires understanding of dependency inversion
- More projects to manage

#### Option C: Vertical Slices
Organize by feature, each slice containing all layers for that feature.

**Pros:**
- Changes are localized to one area
- Less jumping between projects
- Natural for CQRS

**Cons:**
- Can lead to duplication
- Shared concerns need careful handling
- Harder to enforce cross-cutting standards

## Decision

We will use **Clean Architecture** with the following layer definitions:

### Layer Definitions

```
┌────────────────────────────────────────────────────────────────┐
│                          Kyc.Api                               │
│  (Controllers, Middleware, Composition Root)                   │
├────────────────────────────────────────────────────────────────┤
│                     Kyc.Application                            │
│  (Commands, Queries, Handlers, DTOs, Validators)               │
├────────────────────────────────────────────────────────────────┤
│                       Kyc.Domain                               │
│  (Entities, Value Objects, Repository Interfaces, Events)      │
├────────────────────────────────────────────────────────────────┤
│                    Kyc.Infrastructure                          │
│  (EF Core, Repositories, External APIs, File Storage)          │
└────────────────────────────────────────────────────────────────┘
```

### Dependency Rules

1. **Domain**: No dependencies on other layers. Only .NET base class libraries.
2. **Application**: Depends on Domain. Defines ports (interfaces) for infrastructure.
3. **Infrastructure**: Depends on Domain and Application. Implements interfaces.
4. **API**: Depends on all layers. Serves as composition root.

### Project Structure

```
src/
├── Kyc.Domain/              # Entities, Value Objects, Domain Events
├── Kyc.Application/         # Use Cases, Commands/Queries, DTOs
├── Kyc.Infrastructure/      # EF Core, Repositories, External APIs
├── Kyc.Api/                 # HTTP API, Controllers, Middleware
└── Providers/
    ├── Kyc.Providers.Abstractions/  # Provider interfaces
    └── Kyc.Providers.Onfido/        # Onfido implementation
```

### Key Patterns

1. **CQRS**: Commands for writes, Queries for reads (via MediatR)
2. **Repository Pattern**: Abstract data access behind interfaces
3. **Domain Events**: Decouple modules and enable async processing
4. **Dependency Injection**: Wire up at composition root (API layer)

### Within Application Layer: Vertical Slices

While the overall architecture is Clean Architecture, within the Application layer we organize by feature (vertical slices):

```
Kyc.Application/
├── Applicants/
│   ├── Commands/
│   │   └── CreateApplicant/
│   │       ├── CreateApplicantCommand.cs
│   │       ├── CreateApplicantCommandHandler.cs
│   │       └── CreateApplicantCommandValidator.cs
│   └── Queries/
│       └── GetApplicant/
│           ├── GetApplicantQuery.cs
│           ├── GetApplicantQueryHandler.cs
│           └── ApplicantDto.cs
└── Verifications/
    └── ...
```

This hybrid approach gives us:
- Clean boundaries between layers (Clean Architecture)
- Feature locality within Application layer (Vertical Slices)

## Consequences

### Positive

- **Testability**: Domain and Application layers can be tested without database or HTTP
- **Flexibility**: Can swap PostgreSQL for another database by replacing Infrastructure
- **NuGet Packaging**: Domain + Application can be packaged as `Kyc.Aggregator.Core`
- **Maintainability**: Clear rules about where code belongs
- **Onboarding**: Standard pattern familiar to Clean Architecture practitioners

### Negative

- **Initial Overhead**: More projects and ceremony than simple N-tier
- **Learning Curve**: Developers unfamiliar with pattern need onboarding
- **Mapping Overhead**: DTOs require mapping to/from domain entities
- **Potential Over-Engineering**: Simple CRUD might feel heavy

### Mitigations

1. **Code Generation**: Consider source generators for repetitive mapping
2. **Templates**: Provide file templates for Commands, Queries, Handlers
3. **Pragmatism**: For simple CRUD, it's okay to skip domain layer and go directly to DTOs
4. **Architecture Tests**: Automate dependency rule verification

## Implementation Details

### Domain Layer Example

```csharp
// No dependencies except .NET BCL
public class Verification : Entity<VerificationId>
{
    public ApplicantId ApplicantId { get; private set; }
    public VerificationStatus Status { get; private set; }
    
    private readonly List<IDomainEvent> _events = new();
    
    public static Verification Create(ApplicantId applicantId, IEnumerable<CheckType> checks)
    {
        var verification = new Verification
        {
            Id = VerificationId.New(),
            ApplicantId = applicantId,
            Status = VerificationStatus.Created
        };
        
        verification._events.Add(new VerificationCreatedEvent(verification.Id));
        return verification;
    }
}
```

### Application Layer Example

```csharp
// Depends on Domain, defines interfaces for Infrastructure
public class StartVerificationCommandHandler 
    : IRequestHandler<StartVerificationCommand, VerificationDto>
{
    private readonly IVerificationRepository _repository;
    private readonly IKycProvider _provider;
    private readonly IUnitOfWork _unitOfWork;
    
    public async Task<VerificationDto> Handle(
        StartVerificationCommand request, 
        CancellationToken cancellationToken)
    {
        var verification = Verification.Create(request.ApplicantId, request.CheckTypes);
        
        await _repository.AddAsync(verification, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        return verification.ToDto();
    }
}
```

### Infrastructure Layer Example

```csharp
// Implements interfaces from Domain/Application
public class PostgresVerificationRepository : IVerificationRepository
{
    private readonly KycDbContext _context;
    
    public async Task<Verification?> GetByIdAsync(
        VerificationId id, 
        CancellationToken cancellationToken)
    {
        return await _context.Verifications
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    }
}
```

## Related Decisions

- [ADR-001: Modular Monolith Architecture](./ADR-001-modular-monolith-architecture.md)
- [ADR-003: Provider Abstraction Strategy](./ADR-003-provider-abstraction-strategy.md)

## References

- [Clean Architecture by Robert C. Martin](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [Implementing Clean Architecture](https://jasontaylor.dev/clean-architecture-getting-started/)
