# ADR-001: Modular Monolith Architecture

## Status

**Accepted**

## Date

2024-01-15

## Context

We are building a KYC aggregation service that needs to:

1. Integrate multiple KYC providers behind a unified interface
2. Support deployment as both a standalone API and an embedded NuGet package
3. Be maintainable by a small team
4. Allow for future extraction of modules if scale demands
5. Support an Admin Panel in future phases

We need to decide on the high-level architectural style for this system.

### Options Considered

#### Option A: Traditional Monolith
A single application with all code in one project, organized by technical layers (Controllers, Services, Repositories).

**Pros:**
- Simple to start
- Easy deployment
- No network overhead

**Cons:**
- Tight coupling between features
- Difficult to enforce boundaries
- Hard to extract parts for NuGet packaging
- Becomes unwieldy as it grows

#### Option B: Microservices
Separate services for Applicants, Verifications, Providers, etc., communicating via HTTP/messaging.

**Pros:**
- Strong isolation
- Independent scaling
- Technology flexibility

**Cons:**
- Significant operational complexity
- Network latency for provider orchestration
- Distributed transaction challenges
- Overkill for current team size and scale

#### Option C: Modular Monolith
A single deployable unit with clear internal module boundaries, each module following Clean Architecture.

**Pros:**
- Deployment simplicity of monolith
- Strong boundaries like microservices (enforced via projects/namespaces)
- Natural path to NuGet extraction
- Can evolve to microservices if needed
- Simpler than microservices for current needs

**Cons:**
- Requires discipline to maintain boundaries
- Shared database can lead to coupling
- All modules scale together

## Decision

We will use a **Modular Monolith** architecture with the following characteristics:

1. **Module Definition**: Each module (Verification, Applicant, Providers) is a logical grouping with its own:
   - Domain entities and logic
   - Application services and commands/queries
   - Infrastructure implementations
   - Public interface for other modules

2. **Clean Architecture per Module**: Each module follows the dependency inversion principle:
   - Domain layer has no dependencies
   - Application layer depends on Domain
   - Infrastructure implements interfaces from Domain/Application
   - API layer composes everything

3. **Module Communication**: Modules communicate through:
   - Well-defined interfaces (for synchronous operations)
   - Domain events (for decoupled notifications)
   - Never direct database access across modules

4. **Shared Kernel**: Common abstractions live in a shared kernel:
   - Base entity classes
   - Common value objects
   - Infrastructure interfaces

5. **Single Database, Separate Schemas**: All modules share a PostgreSQL instance but use logical separation (schemas or table prefixes).

## Consequences

### Positive

- **NuGet-Ready**: Core modules can be packaged into NuGet packages by exposing public interfaces and hiding implementations
- **Testable**: Clean boundaries enable unit testing without infrastructure
- **Evolvable**: Modules can be extracted to separate services if scaling needs change
- **Pragmatic**: Avoids distributed system complexity while maintaining structure
- **Team-Friendly**: New developers can focus on one module at a time

### Negative

- **Discipline Required**: Team must resist taking shortcuts across module boundaries
- **Boundary Enforcement**: Need architecture tests to ensure dependencies flow correctly
- **Shared Deployment**: A bug in one module affects the entire deployment
- **Database Coupling Risk**: Must be vigilant about cross-module queries

### Mitigations

1. **Architecture Tests**: Use ArchUnitNET or NetArchTest to enforce dependency rules
2. **Code Reviews**: Review PRs for boundary violations
3. **Module Ownership**: Assign clear ownership for each module
4. **Integration Tests**: Test module interactions explicitly

## Alternatives Rejected

- **Microservices**: Too complex for current scale; can evolve to this later if needed
- **Traditional Monolith**: Would not support NuGet packaging goal; too coupled

## Related Decisions

- [ADR-002: Clean Architecture Layers](./ADR-002-clean-architecture-layers.md)
- [ADR-003: Provider Abstraction Strategy](./ADR-003-provider-abstraction-strategy.md)

## References

- [Modular Monolith with DDD](https://github.com/kgrzybek/modular-monolith-with-ddd)
- [Clean Architecture by Robert C. Martin](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
