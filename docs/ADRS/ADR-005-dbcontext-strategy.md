# ADR-005: Single DbContext with Module Schema Separation

## Status

**Accepted**

## Date

2024-01-15

## Context

The KYC Aggregator follows a modular monolith architecture. As we implement persistence using Entity Framework Core, we need to decide how to structure the DbContext:

1. Should each module have its own DbContext?
2. Should we use a single shared DbContext?
3. How do we maintain module boundaries while sharing database infrastructure?

### Requirements

- Support future addition of modules (e.g., Billing, Reporting)
- Maintain clear module boundaries
- Keep transactions simple within a module
- Allow cross-module queries when explicitly needed
- Single PostgreSQL database for operational simplicity
- Clear migration ownership

### Options Considered

#### Option A: Single Shared DbContext

One `KycDbContext` containing all entities from all modules.

```csharp
public class KycDbContext : DbContext
{
    // KYC Module
    public DbSet<Applicant> Applicants => Set<Applicant>();
    public DbSet<Verification> Verifications => Set<Verification>();
    
    // Future: Billing Module
    public DbSet<Invoice> Invoices => Set<Invoice>();
}
```

**Pros:**
- Simple transaction management
- Single connection pool
- Easy migrations (one migration history)
- Cross-module queries possible

**Cons:**
- All entities visible from anywhere
- Harder to enforce module boundaries in code
- Large DbContext as system grows
- Module coupling at database layer

#### Option B: Per-Module DbContext

Each module owns its own DbContext, all targeting the same database.

```csharp
public class KycDbContext : DbContext
{
    public DbSet<Applicant> Applicants => Set<Applicant>();
    public DbSet<Verification> Verifications => Set<Verification>();
}

public class BillingDbContext : DbContext
{
    public DbSet<Invoice> Invoices => Set<Invoice>();
}
```

**Pros:**
- Strong module isolation
- Clear ownership of entities
- Independent migrations per module
- Smaller, focused contexts

**Cons:**
- Cross-module transactions require distributed transaction or saga
- Multiple migration histories
- Multiple connection pools
- Complex cross-module queries

#### Option C: Single DbContext with Schema Separation (Hybrid)

One DbContext, but modules use separate PostgreSQL schemas with organized configuration classes.

```csharp
public class KycDbContext : DbContext
{
    // All entities
    public DbSet<Applicant> Applicants => Set<Applicant>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply module-specific configurations
        // Configurations specify their own schemas
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KycDbContext).Assembly);
    }
}

// KYC module uses 'kyc' schema
public class ApplicantConfiguration : IEntityTypeConfiguration<Applicant>
{
    public void Configure(EntityTypeBuilder<Applicant> builder)
    {
        builder.ToTable("applicants", "kyc");
    }
}

// Billing module uses 'billing' schema
public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices", "billing");
    }
}
```

**Pros:**
- Simple transaction management (single context)
- Clear database-level separation (schemas)
- Single migration history with logical organization
- Cross-module queries possible when explicitly needed
- Easy to extract to separate context later if needed

**Cons:**
- Requires discipline to maintain boundaries
- Code-level access to all entities
- Need architecture tests to enforce boundaries

## Decision

We will use **Option C: Single DbContext with Schema Separation**.

### Implementation Details

1. **Single `KycDbContext`** containing all module entities
2. **PostgreSQL schemas** for logical separation:
   - `kyc` schema for KYC module entities
   - `billing` schema for future Billing module
   - Shared infrastructure in appropriate schema
3. **Organized configuration classes** grouped by module:
   ```
   Infrastructure/
   └── Persistence/
       └── Configurations/
           ├── Kyc/
           │   ├── ApplicantConfiguration.cs
           │   └── VerificationConfiguration.cs
           └── Billing/
               └── InvoiceConfiguration.cs
   ```
4. **Architecture tests** to enforce that Application layer doesn't directly access entities from other modules

### Migration Organization

All migrations live in a single `Migrations/` folder but are logically organized:

```csharp
// Each migration can affect multiple schemas
public partial class AddInvoices : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema("billing");
        migrationBuilder.CreateTable(
            name: "invoices",
            schema: "billing",
            ...);
    }
}
```

### Cross-Module Access Rules

| Scenario | Approach |
|----------|----------|
| Module A reads own entities | Direct DbSet access |
| Module A needs Module B data | Module B exposes query interface |
| Module A reacts to Module B event | Domain events |
| Admin needs cross-module view | Dedicated read models/queries |

### Future Extraction Path

If module isolation becomes critical:

1. Create separate `BillingDbContext`
2. Move billing configurations to new context
3. Extract billing migrations to separate history
4. Update DI registration

The schema separation makes this straightforward.

## Consequences

### Positive

- **Simple Transactions**: Single SaveChanges() commits all changes
- **Clear Schema Boundaries**: Database structure reflects module ownership
- **Flexible**: Can evolve to separate contexts if needed
- **Single Migration Pipeline**: One set of migrations to manage
- **Cross-Module Queries**: Possible when legitimately needed (e.g., admin reports)

### Negative

- **Code Access**: All entities technically accessible from anywhere
- **Discipline Required**: Must resist cross-module shortcuts
- **Testing Overhead**: Need architecture tests to enforce boundaries
- **Growing Context**: Context grows with each module

### Mitigations

1. **Architecture Tests**: Use ArchUnitNET to enforce module boundaries
   ```csharp
   [Fact]
   public void ApplicationLayer_ShouldNotAccessOtherModuleEntities()
   {
       // Test that Kyc.Application doesn't reference Billing entities
   }
   ```

2. **Code Reviews**: Review cross-module data access carefully

3. **Module Interfaces**: Modules expose public interfaces, not raw entities

4. **Monitoring**: Track cross-schema queries in production

## Alternatives Rejected

- **Per-Module DbContext**: Too complex for current scale; cross-module transactions would require sagas
- **No Schema Separation**: Would lose database-level organization

## Related Decisions

- [ADR-001: Modular Monolith Architecture](./ADR-001-modular-monolith-architecture.md)
- [ADR-002: Clean Architecture Layers](./ADR-002-clean-architecture-layers.md)
- [ADR-004: PostgreSQL as Primary Database](./ADR-004-postgresql-database-choice.md)

## References

- [EF Core DbContext Lifetime](https://docs.microsoft.com/en-us/ef/core/dbcontext-configuration/)
- [PostgreSQL Schemas](https://www.postgresql.org/docs/current/ddl-schemas.html)
