# Database Design

This document describes the PostgreSQL database schema design for the KYC Aggregator system, including table structures, relationships, indexing strategies, Entity Framework Core approach, and data management.

## Design Principles

1. **Domain Alignment**: Schema reflects domain model structure
2. **Persistence Ignorance**: Domain layer has no knowledge of EF Core
3. **Audit Trail**: All changes tracked for compliance
4. **Soft Deletes**: Critical data never physically deleted
5. **JSON Flexibility**: Metadata stored as JSONB for extensibility
6. **Strong Typing**: Use appropriate PostgreSQL types
7. **Performance**: Strategic indexing for common queries

## Entity Framework Core Approach

### Core Principles

| Principle | Implementation |
|-----------|----------------|
| **ORM** | Entity Framework Core 8+ (only supported ORM) |
| **Database** | PostgreSQL 14+ (only supported database) |
| **Approach** | Code-first with EF Core migrations |
| **Domain Isolation** | No EF Core attributes in Domain entities |
| **Abstraction** | Application layer depends on repository abstractions, not DbContext |

### Layer Responsibilities

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        PERSISTENCE ARCHITECTURE                             │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  Domain Layer (Kyc.Domain)                                                  │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │  • Pure C# entities (no EF Core attributes)                          │   │
│  │  • Repository interfaces (IApplicantRepository, etc.)                │   │
│  │  • IUnitOfWork interface                                             │   │
│  │  • Domain events                                                     │   │
│  │  • NO reference to EF Core or Npgsql packages                        │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                      ▲                                      │
│                                      │ implements                           │
│                                      │                                      │
│  Infrastructure Layer (Kyc.Infrastructure)                                  │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │  • KycDbContext : DbContext                                          │   │
│  │  • Entity configurations (Fluent API only)                           │   │
│  │  • Repository implementations                                        │   │
│  │  • UnitOfWork implementation                                         │   │
│  │  • EF Core migrations                                                │   │
│  │  • Interceptors (audit, domain events)                               │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Domain Entity Rules

**ALLOWED in Domain entities:**
- Plain C# properties
- Private setters for encapsulation
- Navigation properties (collections, references)
- Value object properties
- Domain methods with business logic

**NOT ALLOWED in Domain entities:**
- `[Key]`, `[Required]`, `[MaxLength]` or any EF Core attributes
- `[Table]`, `[Column]`, `[ForeignKey]` attributes
- Any reference to `Microsoft.EntityFrameworkCore` namespace
- `DbContext` awareness

**Exception**: In rare cases where EF Core attributes are necessary (e.g., complex inheritance), document the justification in an ADR.

### Example: Persistence-Ignorant Entity

```csharp
// Domain/Entities/Applicant.cs - NO EF CORE REFERENCES
namespace Kyc.Domain.Entities;

public class Applicant : Entity<ApplicantId>
{
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public Email Email { get; private set; }
    public Address? Address { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    
    private readonly List<Document> _documents = new();
    public IReadOnlyCollection<Document> Documents => _documents.AsReadOnly();
    
    private Applicant() { } // EF Core uses this
    
    public static Applicant Create(string firstName, string lastName, Email email)
    {
        // Domain logic here
    }
}
```

### Fluent API Configuration (Infrastructure Layer)

```csharp
// Infrastructure/Persistence/Configurations/ApplicantConfiguration.cs
namespace Kyc.Infrastructure.Persistence.Configurations;

public class ApplicantConfiguration : IEntityTypeConfiguration<Applicant>
{
    public void Configure(EntityTypeBuilder<Applicant> builder)
    {
        builder.ToTable("applicants", "kyc");
        
        builder.HasKey(x => x.Id);
        
        // Strongly-typed ID conversion
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(
                id => id.Value,
                value => new ApplicantId(value));
        
        builder.Property(x => x.FirstName)
            .HasColumnName("first_name")
            .HasMaxLength(100)
            .IsRequired();
        
        builder.Property(x => x.LastName)
            .HasColumnName("last_name")
            .HasMaxLength(100)
            .IsRequired();
        
        // Value object conversion
        builder.Property(x => x.Email)
            .HasColumnName("email")
            .HasMaxLength(255)
            .HasConversion(
                v => v.Value,
                v => new Email(v))
            .IsRequired();
        
        // Owned entity for Address (value object)
        builder.OwnsOne(x => x.Address, address =>
        {
            address.Property(a => a.Line1).HasColumnName("address_line1").HasMaxLength(255);
            address.Property(a => a.Line2).HasColumnName("address_line2").HasMaxLength(255);
            address.Property(a => a.City).HasColumnName("address_city").HasMaxLength(100);
            address.Property(a => a.State).HasColumnName("address_state").HasMaxLength(100);
            address.Property(a => a.PostalCode).HasColumnName("address_postal_code").HasMaxLength(20);
            address.Property(a => a.Country).HasColumnName("address_country").HasMaxLength(2);
        });
        
        // Private collection backing field
        builder.HasMany(x => x.Documents)
            .WithOne()
            .HasForeignKey("applicant_id");
        
        builder.Metadata.FindNavigation(nameof(Applicant.Documents))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        
        // Soft delete query filter
        builder.HasQueryFilter(x => x.DeletedAt == null);
        
        // Indexes
        builder.HasIndex(x => x.Email);
        builder.HasIndex(x => x.ExternalReference)
            .IsUnique()
            .HasFilter("deleted_at IS NULL");
    }
}
```

---

## DbContext Strategy

### Decision: Single DbContext with Module Separation

For the modular monolith, we use a **single `KycDbContext`** with clear module boundaries through:

1. **Schema separation**: Each module uses a distinct PostgreSQL schema
2. **Configuration organization**: Entity configurations grouped by module
3. **Future extensibility**: Designed to split into per-module contexts if needed

### Rationale

| Approach | Pros | Cons |
|----------|------|------|
| **Single DbContext** ✓ | Simple transactions, single connection, easy migrations | Potential coupling, all entities in one context |
| Per-module DbContext | Strong isolation, independent migrations | Complex cross-module transactions, multiple connections |

**Decision**: Start with single DbContext. The modular structure allows future extraction if needed.

### DbContext Structure

```csharp
// Infrastructure/Persistence/KycDbContext.cs
namespace Kyc.Infrastructure.Persistence;

public class KycDbContext : DbContext
{
    // KYC Module entities
    public DbSet<Applicant> Applicants => Set<Applicant>();
    public DbSet<Verification> Verifications => Set<Verification>();
    public DbSet<VerificationCheck> VerificationChecks => Set<VerificationCheck>();
    public DbSet<Document> Documents => Set<Document>();
    
    // Shared infrastructure
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    
    public KycDbContext(DbContextOptions<KycDbContext> options) : base(options) { }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply all configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(KycDbContext).Assembly);
        
        // Set default schema
        modelBuilder.HasDefaultSchema("kyc");
    }
}
```

### Module Configuration Organization

```
Infrastructure/
├── Persistence/
│   ├── KycDbContext.cs
│   ├── Configurations/
│   │   ├── Kyc/                          # KYC module configs
│   │   │   ├── ApplicantConfiguration.cs
│   │   │   ├── VerificationConfiguration.cs
│   │   │   ├── VerificationCheckConfiguration.cs
│   │   │   └── DocumentConfiguration.cs
│   │   │
│   │   └── Shared/                       # Shared infrastructure configs
│   │       ├── OutboxMessageConfiguration.cs
│   │       └── AuditLogConfiguration.cs
│   │
│   ├── Repositories/
│   │   ├── ApplicantRepository.cs
│   │   ├── VerificationRepository.cs
│   │   └── UnitOfWork.cs
│   │
│   ├── Interceptors/
│   │   ├── AuditableEntityInterceptor.cs
│   │   └── DomainEventDispatcherInterceptor.cs
│   │
│   └── Migrations/
│       └── ...
```

### Future Module Support

When adding a new module (e.g., Billing), extend the structure:

```csharp
// Option A: Add to existing DbContext
public class KycDbContext : DbContext
{
    // KYC Module
    public DbSet<Applicant> Applicants => Set<Applicant>();
    
    // Billing Module (future)
    public DbSet<Invoice> Invoices => Set<Invoice>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // KYC module uses 'kyc' schema
        // Billing module uses 'billing' schema
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KycDbContext).Assembly);
    }
}

// Billing configuration uses different schema
public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices", "billing");  // Different schema
    }
}
```

```csharp
// Option B: Separate DbContext per module (if isolation needed later)
public class BillingDbContext : DbContext
{
    public DbSet<Invoice> Invoices => Set<Invoice>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("billing");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BillingDbContext).Assembly);
    }
}
```

---

## Data Access Patterns

### Repository Pattern (Selective Use)

We use the repository pattern **selectively**, not as a generic abstraction:

**DO:**
- Create purpose-built repositories for aggregate roots
- Encapsulate complex queries
- Return domain entities, not DTOs

**DON'T:**
- Create generic `IRepository<T>` with CRUD methods
- Expose `IQueryable` outside Infrastructure
- Create repositories for every entity

### Repository Interface (Domain Layer)

```csharp
// Domain/Repositories/IApplicantRepository.cs
namespace Kyc.Domain.Repositories;

public interface IApplicantRepository
{
    Task<Applicant?> GetByIdAsync(ApplicantId id, CancellationToken ct = default);
    Task<Applicant?> GetByEmailAsync(Email email, CancellationToken ct = default);
    Task<Applicant?> GetByExternalReferenceAsync(string externalRef, CancellationToken ct = default);
    Task AddAsync(Applicant applicant, CancellationToken ct = default);
    void Update(Applicant applicant);
    void Remove(Applicant applicant);
}
```

### Repository Implementation (Infrastructure Layer)

```csharp
// Infrastructure/Persistence/Repositories/ApplicantRepository.cs
namespace Kyc.Infrastructure.Persistence.Repositories;

internal sealed class ApplicantRepository : IApplicantRepository
{
    private readonly KycDbContext _context;
    
    public ApplicantRepository(KycDbContext context)
    {
        _context = context;
    }
    
    public async Task<Applicant?> GetByIdAsync(ApplicantId id, CancellationToken ct = default)
    {
        return await _context.Applicants
            .Include(a => a.Documents)  // Eager load within repository
            .FirstOrDefaultAsync(a => a.Id == id, ct);
    }
    
    public async Task<Applicant?> GetByEmailAsync(Email email, CancellationToken ct = default)
    {
        return await _context.Applicants
            .FirstOrDefaultAsync(a => a.Email == email, ct);
    }
    
    public async Task AddAsync(Applicant applicant, CancellationToken ct = default)
    {
        await _context.Applicants.AddAsync(applicant, ct);
    }
    
    public void Update(Applicant applicant)
    {
        _context.Applicants.Update(applicant);
    }
    
    public void Remove(Applicant applicant)
    {
        // Soft delete via domain method, not EF Remove
        _context.Applicants.Update(applicant);
    }
}
```

### Unit of Work Pattern

```csharp
// Domain/Repositories/IUnitOfWork.cs
namespace Kyc.Domain.Repositories;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

// Infrastructure/Persistence/UnitOfWork.cs
namespace Kyc.Infrastructure.Persistence;

internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly KycDbContext _context;
    
    public UnitOfWork(KycDbContext context)
    {
        _context = context;
    }
    
    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }
}
```

### IQueryable Containment

**Critical Rule**: `IQueryable` must NEVER leak outside the Infrastructure layer.

```csharp
// ❌ BAD - Leaking IQueryable
public interface IApplicantRepository
{
    IQueryable<Applicant> GetAll();  // NEVER DO THIS
}

// ✅ GOOD - Return materialized results
public interface IApplicantRepository
{
    Task<IReadOnlyList<Applicant>> GetByStatusAsync(ApplicantStatus status, CancellationToken ct);
    Task<PagedResult<Applicant>> GetPagedAsync(int page, int pageSize, CancellationToken ct);
}
```

### Specification Pattern (Optional)

For complex, reusable query criteria:

```csharp
// Domain/Specifications/ISpecification.cs
public interface ISpecification<T>
{
    Expression<Func<T, bool>> ToExpression();
}

// Domain/Specifications/ActiveVerificationSpecification.cs
public class ActiveVerificationSpecification : ISpecification<Verification>
{
    public Expression<Func<Verification, bool>> ToExpression()
    {
        return v => v.Status != VerificationStatus.Completed 
                 && v.Status != VerificationStatus.Cancelled
                 && v.Status != VerificationStatus.Expired;
    }
}

// Usage in repository
public async Task<IReadOnlyList<Verification>> FindAsync(
    ISpecification<Verification> spec, 
    CancellationToken ct)
{
    return await _context.Verifications
        .Where(spec.ToExpression())
        .ToListAsync(ct);
}
```

---

## Schema Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              kyc schema                                     │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌─────────────────┐      ┌─────────────────────┐      ┌────────────────┐  │
│  │   applicants    │──────│   verifications     │──────│verification_   │  │
│  │                 │ 1  * │                     │ 1  * │   checks       │  │
│  └────────┬────────┘      └─────────────────────┘      └────────────────┘  │
│           │ 1                                                               │
│           │                                                                 │
│           │ *                                                               │
│  ┌────────┴────────┐                                                        │
│  │   documents     │                                                        │
│  │                 │                                                        │
│  └─────────────────┘                                                        │
│                                                                             │
│  ┌─────────────────┐      ┌─────────────────────┐      ┌────────────────┐  │
│  │   providers     │      │   outbox_messages   │      │  audit_logs    │  │
│  │  (reference)    │      │   (transactional)   │      │  (immutable)   │  │
│  └─────────────────┘      └─────────────────────┘      └────────────────┘  │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Table Definitions

### applicants

Stores individual applicant information.

```sql
CREATE TABLE kyc.applicants (
    -- Primary key
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    
    -- External reference
    external_reference  VARCHAR(255) NULL,
    
    -- Personal information
    first_name          VARCHAR(100) NOT NULL,
    middle_name         VARCHAR(100) NULL,
    last_name           VARCHAR(100) NOT NULL,
    email               VARCHAR(255) NOT NULL,
    phone_number        VARCHAR(50) NULL,
    date_of_birth       DATE NULL,
    nationality         CHAR(2) NULL,
    
    -- Address (embedded)
    address_line1       VARCHAR(255) NULL,
    address_line2       VARCHAR(255) NULL,
    address_city        VARCHAR(100) NULL,
    address_state       VARCHAR(100) NULL,
    address_postal_code VARCHAR(20) NULL,
    address_country     CHAR(2) NULL,
    
    -- Flexible metadata
    metadata            JSONB NOT NULL DEFAULT '{}',
    
    -- Audit columns
    created_at          TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at          TIMESTAMPTZ NULL,
    
    -- Constraints
    CONSTRAINT uq_applicants_external_reference 
        UNIQUE (external_reference) WHERE deleted_at IS NULL
);

-- Indexes
CREATE INDEX idx_applicants_email ON kyc.applicants(email) WHERE deleted_at IS NULL;
CREATE INDEX idx_applicants_external_reference ON kyc.applicants(external_reference) WHERE deleted_at IS NULL;
CREATE INDEX idx_applicants_created_at ON kyc.applicants(created_at);
CREATE INDEX idx_applicants_metadata ON kyc.applicants USING GIN(metadata);
```

### documents

Stores uploaded identity documents.

```sql
CREATE TABLE kyc.documents (
    -- Primary key
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    
    -- Foreign key
    applicant_id        UUID NOT NULL REFERENCES kyc.applicants(id),
    
    -- Document info
    document_type       VARCHAR(50) NOT NULL,
    document_side       VARCHAR(20) NOT NULL,
    
    -- File info
    file_name           VARCHAR(255) NOT NULL,
    content_type        VARCHAR(100) NOT NULL,
    storage_reference   VARCHAR(500) NOT NULL,
    file_size_bytes     BIGINT NOT NULL,
    
    -- Document details
    issuing_country     CHAR(2) NULL,
    expiry_date         DATE NULL,
    
    -- Provider reference
    provider_document_id VARCHAR(255) NULL,
    
    -- Metadata
    metadata            JSONB NOT NULL DEFAULT '{}',
    
    -- Audit columns
    uploaded_at         TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at          TIMESTAMPTZ NULL,
    
    -- Constraints
    CONSTRAINT chk_documents_type 
        CHECK (document_type IN ('passport', 'drivers_license', 'national_id', 
                                  'residence_permit', 'utility_bill', 'bank_statement', 'other')),
    CONSTRAINT chk_documents_side 
        CHECK (document_side IN ('front', 'back', 'single'))
);

-- Indexes
CREATE INDEX idx_documents_applicant_id ON kyc.documents(applicant_id);
CREATE INDEX idx_documents_type ON kyc.documents(document_type);
```

### verifications

Stores verification processes.

```sql
CREATE TABLE kyc.verifications (
    -- Primary key
    id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    
    -- Foreign key
    applicant_id            UUID NOT NULL REFERENCES kyc.applicants(id),
    
    -- Provider info
    provider_id             VARCHAR(50) NOT NULL,
    provider_verification_id VARCHAR(255) NULL,
    
    -- Status
    status                  VARCHAR(50) NOT NULL DEFAULT 'created',
    result                  VARCHAR(50) NULL,
    
    -- Requested checks (array)
    requested_checks        TEXT[] NOT NULL,
    
    -- Timestamps
    created_at              TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    started_at              TIMESTAMPTZ NULL,
    completed_at            TIMESTAMPTZ NULL,
    expires_at              TIMESTAMPTZ NULL,
    
    -- Failure info
    failure_reason          TEXT NULL,
    failure_code            VARCHAR(100) NULL,
    
    -- SDK/redirect
    sdk_token               TEXT NULL,
    check_url               VARCHAR(500) NULL,
    redirect_url            VARCHAR(500) NULL,
    
    -- Metadata
    metadata                JSONB NOT NULL DEFAULT '{}',
    
    -- Constraints
    CONSTRAINT chk_verifications_status 
        CHECK (status IN ('created', 'pending', 'in_progress', 'awaiting_input',
                          'completed', 'expired', 'cancelled', 'failed')),
    CONSTRAINT chk_verifications_result 
        CHECK (result IS NULL OR result IN ('approved', 'rejected', 'needs_review', 'inconclusive'))
);

-- Indexes
CREATE INDEX idx_verifications_applicant_id ON kyc.verifications(applicant_id);
CREATE INDEX idx_verifications_status ON kyc.verifications(status);
CREATE INDEX idx_verifications_provider_id ON kyc.verifications(provider_id);
CREATE INDEX idx_verifications_provider_verification_id 
    ON kyc.verifications(provider_verification_id) WHERE provider_verification_id IS NOT NULL;
CREATE INDEX idx_verifications_created_at ON kyc.verifications(created_at);
CREATE INDEX idx_verifications_expires_at ON kyc.verifications(expires_at) 
    WHERE expires_at IS NOT NULL AND status NOT IN ('completed', 'expired', 'cancelled', 'failed');
```

### verification_checks

Stores individual checks within a verification.

```sql
CREATE TABLE kyc.verification_checks (
    -- Primary key
    id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    
    -- Foreign key
    verification_id         UUID NOT NULL REFERENCES kyc.verifications(id),
    
    -- Check info
    check_type              VARCHAR(50) NOT NULL,
    status                  VARCHAR(50) NOT NULL DEFAULT 'pending',
    result                  VARCHAR(50) NULL,
    
    -- Provider reference
    provider_check_id       VARCHAR(255) NULL,
    
    -- Timestamps
    started_at              TIMESTAMPTZ NULL,
    completed_at            TIMESTAMPTZ NULL,
    
    -- Results
    breakdown_results       JSONB NULL,
    raw_provider_response   JSONB NULL,
    
    -- Metadata
    metadata                JSONB NOT NULL DEFAULT '{}',
    
    -- Constraints
    CONSTRAINT chk_verification_checks_type 
        CHECK (check_type IN ('identity', 'document', 'liveness', 'face_match', 
                               'aml', 'poa', 'custom')),
    CONSTRAINT chk_verification_checks_status 
        CHECK (status IN ('pending', 'in_progress', 'completed', 'failed', 'cancelled')),
    CONSTRAINT chk_verification_checks_result 
        CHECK (result IS NULL OR result IN ('clear', 'consider', 'rejected', 'inconclusive'))
);

-- Indexes
CREATE INDEX idx_verification_checks_verification_id ON kyc.verification_checks(verification_id);
CREATE INDEX idx_verification_checks_type ON kyc.verification_checks(check_type);
CREATE INDEX idx_verification_checks_status ON kyc.verification_checks(status);
```

### providers

Reference table for configured providers.

```sql
CREATE TABLE kyc.providers (
    id                  VARCHAR(50) PRIMARY KEY,
    display_name        VARCHAR(100) NOT NULL,
    enabled             BOOLEAN NOT NULL DEFAULT true,
    is_sandbox          BOOLEAN NOT NULL DEFAULT false,
    priority            INTEGER NOT NULL DEFAULT 100,
    supported_checks    TEXT[] NOT NULL,
    last_health_check   TIMESTAMPTZ NULL,
    health_status       VARCHAR(20) NULL,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
```

### outbox_messages

Transactional outbox for reliable event publishing.

```sql
CREATE TABLE kyc.outbox_messages (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    event_type          VARCHAR(100) NOT NULL,
    aggregate_type      VARCHAR(100) NOT NULL,
    aggregate_id        UUID NOT NULL,
    payload             JSONB NOT NULL,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    processed_at        TIMESTAMPTZ NULL,
    error               TEXT NULL,
    retry_count         INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX idx_outbox_messages_unprocessed 
    ON kyc.outbox_messages(created_at) WHERE processed_at IS NULL;
CREATE INDEX idx_outbox_messages_aggregate 
    ON kyc.outbox_messages(aggregate_type, aggregate_id);
```

### audit_logs

Immutable audit trail for compliance.

```sql
CREATE TABLE kyc.audit_logs (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    entity_type         VARCHAR(100) NOT NULL,
    entity_id           UUID NOT NULL,
    action              VARCHAR(50) NOT NULL,
    actor_type          VARCHAR(50) NOT NULL,
    actor_id            VARCHAR(255) NULL,
    old_values          JSONB NULL,
    new_values          JSONB NULL,
    ip_address          INET NULL,
    user_agent          TEXT NULL,
    correlation_id      VARCHAR(100) NULL,
    occurred_at         TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_audit_logs_entity ON kyc.audit_logs(entity_type, entity_id);
CREATE INDEX idx_audit_logs_occurred_at ON kyc.audit_logs(occurred_at);
CREATE INDEX idx_audit_logs_actor ON kyc.audit_logs(actor_type, actor_id);
CREATE INDEX idx_audit_logs_correlation ON kyc.audit_logs(correlation_id) 
    WHERE correlation_id IS NOT NULL;

-- Prevent updates/deletes
CREATE RULE audit_logs_no_update AS ON UPDATE TO kyc.audit_logs DO INSTEAD NOTHING;
CREATE RULE audit_logs_no_delete AS ON DELETE TO kyc.audit_logs DO INSTEAD NOTHING;
```

### webhook_subscriptions

```sql
CREATE TABLE kyc.webhook_subscriptions (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    url                 VARCHAR(500) NOT NULL,
    secret_hash         VARCHAR(255) NOT NULL,
    events              TEXT[] NOT NULL,
    enabled             BOOLEAN NOT NULL DEFAULT true,
    consecutive_failures INTEGER NOT NULL DEFAULT 0,
    last_failure_at     TIMESTAMPTZ NULL,
    last_success_at     TIMESTAMPTZ NULL,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
```

### webhook_deliveries

```sql
CREATE TABLE kyc.webhook_deliveries (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    subscription_id     UUID NOT NULL REFERENCES kyc.webhook_subscriptions(id),
    outbox_message_id   UUID NOT NULL REFERENCES kyc.outbox_messages(id),
    attempt_number      INTEGER NOT NULL DEFAULT 1,
    request_url         VARCHAR(500) NOT NULL,
    request_headers     JSONB NOT NULL,
    request_body        JSONB NOT NULL,
    response_status     INTEGER NULL,
    response_body       TEXT NULL,
    sent_at             TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    response_at         TIMESTAMPTZ NULL,
    duration_ms         INTEGER NULL,
    success             BOOLEAN NOT NULL,
    error               TEXT NULL
);

CREATE INDEX idx_webhook_deliveries_subscription ON kyc.webhook_deliveries(subscription_id);
CREATE INDEX idx_webhook_deliveries_message ON kyc.webhook_deliveries(outbox_message_id);
```

---

## Migration Strategy

### Code-First Approach

All schema changes are managed through EF Core migrations:

```bash
# Create migration
dotnet ef migrations add AddNewColumn --project src/Kyc.Infrastructure --startup-project src/Kyc.Api

# Apply migrations
dotnet ef database update --project src/Kyc.Infrastructure --startup-project src/Kyc.Api

# Generate SQL script for review
dotnet ef migrations script --project src/Kyc.Infrastructure --startup-project src/Kyc.Api
```

### Migration Organization

```
Infrastructure/
└── Migrations/
    ├── 20240115100000_InitialCreate.cs
    ├── 20240115100000_InitialCreate.Designer.cs
    ├── 20240120150000_AddApplicantMetadata.cs
    ├── 20240120150000_AddApplicantMetadata.Designer.cs
    └── KycDbContextModelSnapshot.cs
```

### Schema Evolution Rules

| Change Type | Backward Compatible | Approach |
|-------------|---------------------|----------|
| Add nullable column | ✅ Yes | Direct migration |
| Add table | ✅ Yes | Direct migration |
| Add index | ✅ Yes | Direct migration (concurrent in production) |
| Rename column | ⚠️ Requires care | Add new → migrate data → remove old |
| Remove column | ❌ Breaking | Remove code references first → deploy → then remove column |
| Change column type | ⚠️ Requires care | Add new column → migrate → remove old |
| Add NOT NULL constraint | ⚠️ Requires care | Add with default → backfill → optionally remove default |

### Backward Compatibility Expectations

1. **Application code deploys before migrations run**: New code must work with old schema
2. **Migrations run before old code is removed**: Old code must work with new schema
3. **Two-phase deployments**: For breaking changes, deploy in two phases

### Example: Safe Column Rename

```csharp
// Migration 1: Add new column
public partial class RenameColumnStep1 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "new_column_name",
            table: "applicants",
            schema: "kyc",
            nullable: true);
        
        // Copy data
        migrationBuilder.Sql(@"
            UPDATE kyc.applicants 
            SET new_column_name = old_column_name");
    }
}

// Migration 2 (after deploying code that uses new column):
public partial class RenameColumnStep2 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "old_column_name",
            table: "applicants",
            schema: "kyc");
    }
}
```

### Migrations in Different Environments

See [DEPLOYMENT.md](./DEPLOYMENT.md) for detailed migration workflows in:
- Local development with .NET Aspire
- Kubernetes production environments
- CI/CD pipelines

---

## EF Core Interceptors

### Audit Interceptor

```csharp
// Infrastructure/Persistence/Interceptors/AuditableEntityInterceptor.cs
public class AuditableEntityInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;
    
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken ct = default)
    {
        var context = eventData.Context;
        if (context is null) return base.SavingChangesAsync(eventData, result, ct);
        
        foreach (var entry in context.ChangeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = _dateTime.UtcNow;
                entry.Entity.CreatedBy = _currentUser.UserId;
            }
            
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = _dateTime.UtcNow;
                entry.Entity.UpdatedBy = _currentUser.UserId;
            }
        }
        
        return base.SavingChangesAsync(eventData, result, ct);
    }
}
```

### Domain Event Dispatcher Interceptor

```csharp
// Infrastructure/Persistence/Interceptors/DomainEventDispatcherInterceptor.cs
public class DomainEventDispatcherInterceptor : SaveChangesInterceptor
{
    private readonly IMediator _mediator;
    
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken ct = default)
    {
        var context = eventData.Context;
        if (context is null) return result;
        
        var domainEvents = context.ChangeTracker.Entries<IHasDomainEvents>()
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();
        
        foreach (var domainEvent in domainEvents)
        {
            await _mediator.Publish(domainEvent, ct);
        }
        
        // Clear events after publishing
        foreach (var entry in context.ChangeTracker.Entries<IHasDomainEvents>())
        {
            entry.Entity.ClearDomainEvents();
        }
        
        return result;
    }
}
```

---

## Performance Considerations

### Indexing Strategy

| Query Pattern | Index |
|---------------|-------|
| Get applicant by ID | Primary key (automatic) |
| Get applicant by email | `idx_applicants_email` |
| Get applicant by external ref | `idx_applicants_external_reference` |
| List verifications by applicant | `idx_verifications_applicant_id` |
| Find pending verifications | `idx_verifications_status` |
| Provider webhook lookup | `idx_verifications_provider_verification_id` |
| Expiring verifications | `idx_verifications_expires_at` (partial) |
| Audit trail by entity | `idx_audit_logs_entity` |

### Connection Pooling

```json
{
  "ConnectionStrings": {
    "KycDatabase": "Host=localhost;Database=kyc;Username=kyc_user;Password=secret;Pooling=true;MinPoolSize=5;MaxPoolSize=100"
  }
}
```

### Partitioning (Future)

For high-volume deployments:

```sql
CREATE TABLE kyc.audit_logs (
    ...
) PARTITION BY RANGE (occurred_at);

CREATE TABLE kyc.audit_logs_2024_01 
    PARTITION OF kyc.audit_logs 
    FOR VALUES FROM ('2024-01-01') TO ('2024-02-01');
```

---

## Data Retention

| Data Type | Retention | Action |
|-----------|-----------|--------|
| Applicants | 7 years | Archive to cold storage |
| Verifications | 7 years | Archive to cold storage |
| Documents | 90 days post-verification | Delete from hot storage |
| Audit logs | Permanent | Partition and archive |
| Outbox messages | 30 days after processing | Hard delete |
| Webhook deliveries | 30 days | Hard delete |

---

## Backup Strategy

1. **Full backups**: Daily
2. **Incremental backups**: Hourly
3. **Point-in-time recovery**: WAL archiving enabled
4. **Cross-region replication**: For disaster recovery
5. **Backup testing**: Monthly restore drills

---

## Related Documents

- [ARCHITECTURE.md](./ARCHITECTURE.md) - System architecture and persistence responsibility
- [DOMAIN_MODEL.md](./DOMAIN_MODEL.md) - Domain entity design
- [SECURITY.md](./SECURITY.md) - Data encryption and access
- [DEPLOYMENT.md](./DEPLOYMENT.md) - Migrations in containers and Kubernetes
