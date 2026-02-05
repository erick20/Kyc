# ADR-004: PostgreSQL as Primary Database

## Status

**Accepted**

## Date

2024-01-15

## Context

The KYC Aggregator needs a database to persist:
- Applicant information (PII)
- Verification records and status
- Document metadata
- Provider references
- Audit logs
- Webhook subscriptions

Requirements:
1. ACID compliance for financial/compliance data
2. Support for complex queries (filtering, joins)
3. JSON support for flexible metadata
4. Proven reliability at scale
5. Good .NET/EF Core support
6. Cloud-native options available
7. Cost-effective for varying scales

### Options Considered

#### Option A: SQL Server
Microsoft's enterprise RDBMS.

**Pros:**
- Excellent .NET integration
- Enterprise features (always-on, etc.)
- Strong tooling

**Cons:**
- Licensing costs at scale
- Less common in cloud-native ecosystems
- Vendor lock-in concerns

#### Option B: PostgreSQL
Open-source relational database.

**Pros:**
- Open source, no licensing costs
- Excellent JSON/JSONB support
- Strong community
- Cloud offerings (RDS, Cloud SQL, Azure Database)
- Excellent EF Core support
- Advanced features (partitioning, full-text search)

**Cons:**
- Different from SQL Server (team may need learning)
- Some enterprise features require extensions

#### Option C: MongoDB
Document-oriented NoSQL database.

**Pros:**
- Flexible schema
- Natural fit for document-like data
- Horizontal scaling

**Cons:**
- Weaker ACID guarantees (improved but still different)
- Complex queries can be challenging
- Different paradigm from relational
- Less suitable for highly relational data

#### Option D: DynamoDB / CosmosDB
Managed NoSQL options from AWS/Azure.

**Pros:**
- Fully managed
- Extreme scale
- Serverless options

**Cons:**
- Vendor lock-in
- Cost at moderate scale
- Query limitations
- Different programming model

## Decision

We will use **PostgreSQL** as the primary database.

### Rationale

1. **ACID Compliance**: Financial and compliance data needs strong consistency
2. **JSONB Support**: Flexible metadata storage without sacrificing relational features
3. **Cost**: No licensing costs, pay only for infrastructure
4. **Cloud Options**: Available as managed service on all major clouds
5. **EF Core Support**: Npgsql provider is mature and well-maintained
6. **Community**: Large ecosystem of extensions and tools
7. **Familiarity**: SQL skills are transferable; less exotic than NoSQL

### Version

PostgreSQL 14 or later, to ensure:
- Improved connection handling
- Better JSON path expressions
- Enhanced logical replication
- Performance improvements

### Managed Service Options

| Cloud | Service | Notes |
|-------|---------|-------|
| AWS | RDS PostgreSQL | Production-ready, Multi-AZ |
| Azure | Azure Database for PostgreSQL | Flexible server recommended |
| GCP | Cloud SQL for PostgreSQL | Good integration with GKE |

## Consequences

### Positive

- **Reliability**: Proven track record for transactional workloads
- **Flexibility**: JSONB enables schema-less metadata storage
- **Cost Savings**: No per-core licensing
- **Portability**: Standard SQL, easy to migrate from/to other databases
- **Tooling**: pgAdmin, DBeaver, DataGrip all work well
- **EF Core**: First-class support via Npgsql.EntityFrameworkCore.PostgreSQL

### Negative

- **Learning Curve**: Team members from SQL Server background may need adjustment
- **Operational Knowledge**: Self-hosted requires PostgreSQL expertise
- **Backup/HA Complexity**: Requires setup (solved by managed services)
- **Windows Development**: Slightly less native than SQL Server

### Mitigations

1. **Use Managed Service**: RDS/Azure/GCP handles backup, HA, patching
2. **Docker for Dev**: `docker-compose` provides consistent local environment
3. **Training**: Brief PostgreSQL primer for SQL Server developers
4. **EF Core Abstraction**: Most code won't directly use PostgreSQL-specific features

## Implementation Details

### Connection String

```json
{
  "ConnectionStrings": {
    "KycDatabase": "Host=localhost;Database=kyc;Username=kyc_user;Password=secret;Pooling=true;MinPoolSize=5;MaxPoolSize=100"
  }
}
```

### EF Core Configuration

```csharp
services.AddDbContext<KycDbContext>(options =>
    options.UseNpgsql(
        connectionString,
        npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly("Kyc.Infrastructure");
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorCodesToAdd: null);
        }));
```

### Schema Organization

```sql
-- Use schemas for logical separation
CREATE SCHEMA kyc;      -- Core tables
CREATE SCHEMA audit;    -- Audit logs (potentially archivable)
CREATE SCHEMA outbox;   -- Transactional outbox
```

### JSONB Usage

```sql
-- Flexible metadata without schema changes
ALTER TABLE kyc.applicants 
ADD COLUMN metadata JSONB NOT NULL DEFAULT '{}';

-- Query JSON fields
SELECT * FROM kyc.applicants 
WHERE metadata->>'tier' = 'premium';

-- GIN index for JSON queries
CREATE INDEX idx_applicants_metadata ON kyc.applicants USING GIN(metadata);
```

### Docker Compose for Development

```yaml
services:
  postgres:
    image: postgres:14
    environment:
      POSTGRES_USER: kyc_user
      POSTGRES_PASSWORD: secret
      POSTGRES_DB: kyc
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
```

## Alternatives Rejected

- **SQL Server**: Licensing costs, less common in cloud-native
- **MongoDB**: ACID concerns, different paradigm, less suitable for relational data
- **DynamoDB/CosmosDB**: Vendor lock-in, cost, query limitations

## Related Decisions

- [ADR-001: Modular Monolith Architecture](./ADR-001-modular-monolith-architecture.md)
- [ADR-002: Clean Architecture Layers](./ADR-002-clean-architecture-layers.md)

## References

- [Npgsql EF Core Provider](https://www.npgsql.org/efcore/)
- [PostgreSQL JSON Types](https://www.postgresql.org/docs/current/datatype-json.html)
- [PostgreSQL on AWS RDS](https://aws.amazon.com/rds/postgresql/)
