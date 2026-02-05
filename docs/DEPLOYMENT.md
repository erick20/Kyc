# Deployment

This document describes deployment strategies, container configuration, and database migration workflows for the KYC Aggregator system across different environments.

## Deployment Modes

The KYC Aggregator supports two primary deployment modes:

| Mode | Description | Use Case |
|------|-------------|----------|
| **Standalone API** | Containerized HTTP service | Production deployments, microservice architecture |
| **Embedded NuGet** | Library integrated into host application | Direct .NET integration, single-deployment apps |

This document focuses on the **Standalone API** deployment.

---

## Local Development with .NET Aspire

### Overview

.NET Aspire provides a developer-friendly orchestration experience for local development, including:

- Automatic service discovery
- Integrated dashboard for logs, traces, and metrics
- Easy database and dependency management
- Hot reload support

### Aspire App Host Structure

```
src/
├── Kyc.AppHost/                    # Aspire orchestration project
│   ├── Program.cs
│   └── Kyc.AppHost.csproj
│
├── Kyc.ServiceDefaults/            # Shared service configuration
│   ├── Extensions.cs
│   └── Kyc.ServiceDefaults.csproj
│
├── Kyc.Api/                        # Main API project
└── Kyc.Infrastructure/             # Contains migrations
```

### AppHost Configuration

```csharp
// Kyc.AppHost/Program.cs
var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL with persistent volume
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("kyc-postgres-data")
    .WithPgAdmin();

var database = postgres.AddDatabase("kyc");

// API service with database reference
var api = builder.AddProject<Projects.Kyc_Api>("kyc-api")
    .WithReference(database)
    .WaitFor(database);

builder.Build().Run();
```

### Database Migrations in Aspire

#### Option A: Automatic Migration on Startup (Development Only)

```csharp
// Kyc.Api/Program.cs
var app = builder.Build();

// Apply migrations on startup (development only!)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<KycDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.Run();
```

#### Option B: Migration Service (Recommended)

Create a separate migration service that runs before the API:

```csharp
// Kyc.AppHost/Program.cs
var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("kyc-postgres-data");

var database = postgres.AddDatabase("kyc");

// Migration service runs first
var migrator = builder.AddProject<Projects.Kyc_Migrator>("kyc-migrator")
    .WithReference(database)
    .WaitFor(database);

// API waits for migrations to complete
var api = builder.AddProject<Projects.Kyc_Api>("kyc-api")
    .WithReference(database)
    .WaitFor(migrator);

builder.Build().Run();
```

```csharp
// Kyc.Migrator/Program.cs
var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddDbContext<KycDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("kyc")));

var app = builder.Build();

using var scope = app.Services.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<KycDbContext>();
var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

logger.LogInformation("Applying database migrations...");
await dbContext.Database.MigrateAsync();
logger.LogInformation("Migrations completed successfully");

// Exit after migrations
Environment.Exit(0);
```

### Running Locally

```bash
# Start the Aspire app host
cd src/Kyc.AppHost
dotnet run

# Access the dashboard at https://localhost:17225
# Access the API at https://localhost:5001
```

### Creating Migrations (Local Development)

```bash
# Create a new migration
dotnet ef migrations add AddNewFeature \
    --project src/Kyc.Infrastructure \
    --startup-project src/Kyc.Api \
    --output-dir Migrations

# View pending migrations
dotnet ef migrations list \
    --project src/Kyc.Infrastructure \
    --startup-project src/Kyc.Api

# Generate SQL script for review
dotnet ef migrations script \
    --project src/Kyc.Infrastructure \
    --startup-project src/Kyc.Api \
    --output migrations.sql
```

---

## Container Configuration

### Dockerfile

```dockerfile
# Kyc.Api/Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files
COPY ["src/Kyc.Api/Kyc.Api.csproj", "src/Kyc.Api/"]
COPY ["src/Kyc.Application/Kyc.Application.csproj", "src/Kyc.Application/"]
COPY ["src/Kyc.Domain/Kyc.Domain.csproj", "src/Kyc.Domain/"]
COPY ["src/Kyc.Infrastructure/Kyc.Infrastructure.csproj", "src/Kyc.Infrastructure/"]
COPY ["src/Kyc.ServiceDefaults/Kyc.ServiceDefaults.csproj", "src/Kyc.ServiceDefaults/"]

# Restore dependencies
RUN dotnet restore "src/Kyc.Api/Kyc.Api.csproj"

# Copy source code
COPY . .

# Build
WORKDIR "/src/src/Kyc.Api"
RUN dotnet build "Kyc.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Kyc.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Run as non-root user
USER $APP_UID

ENTRYPOINT ["dotnet", "Kyc.Api.dll"]
```

### Migration Job Dockerfile

```dockerfile
# Kyc.Migrator/Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/Kyc.Migrator/Kyc.Migrator.csproj", "src/Kyc.Migrator/"]
COPY ["src/Kyc.Infrastructure/Kyc.Infrastructure.csproj", "src/Kyc.Infrastructure/"]
COPY ["src/Kyc.Domain/Kyc.Domain.csproj", "src/Kyc.Domain/"]

RUN dotnet restore "src/Kyc.Migrator/Kyc.Migrator.csproj"
COPY . .

WORKDIR "/src/src/Kyc.Migrator"
RUN dotnet publish "Kyc.Migrator.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Kyc.Migrator.dll"]
```

### Docker Compose (Local Testing)

```yaml
# docker-compose.yml
version: '3.8'

services:
  postgres:
    image: postgres:14
    environment:
      POSTGRES_USER: kyc_user
      POSTGRES_PASSWORD: kyc_password
      POSTGRES_DB: kyc
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U kyc_user -d kyc"]
      interval: 5s
      timeout: 5s
      retries: 5

  migrator:
    build:
      context: .
      dockerfile: src/Kyc.Migrator/Dockerfile
    environment:
      ConnectionStrings__kyc: "Host=postgres;Database=kyc;Username=kyc_user;Password=kyc_password"
    depends_on:
      postgres:
        condition: service_healthy

  api:
    build:
      context: .
      dockerfile: src/Kyc.Api/Dockerfile
    ports:
      - "5000:8080"
    environment:
      ConnectionStrings__kyc: "Host=postgres;Database=kyc;Username=kyc_user;Password=kyc_password"
      ASPNETCORE_ENVIRONMENT: Production
    depends_on:
      migrator:
        condition: service_completed_successfully

volumes:
  postgres_data:
```

---

## Kubernetes Deployment

### Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           KUBERNETES CLUSTER                                │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                        kyc-system Namespace                          │   │
│  │                                                                       │   │
│  │   ┌─────────────┐    ┌─────────────┐    ┌─────────────────────────┐  │   │
│  │   │   Ingress   │───▶│   Service   │───▶│     Deployment          │  │   │
│  │   │             │    │   kyc-api   │    │     kyc-api             │  │   │
│  │   └─────────────┘    └─────────────┘    │     (replicas: 3)       │  │   │
│  │                                          └───────────┬─────────────┘  │   │
│  │                                                      │                │   │
│  │                                                      ▼                │   │
│  │   ┌─────────────────────────────────────────────────────────────┐    │   │
│  │   │                     PostgreSQL                               │    │   │
│  │   │   (CloudNativePG Operator / RDS / Cloud SQL)                │    │   │
│  │   └─────────────────────────────────────────────────────────────┘    │   │
│  │                                                                       │   │
│  │   Pre-deployment:                                                     │   │
│  │   ┌─────────────────────────────────────────────────────────────┐    │   │
│  │   │              Job: kyc-migrations                             │    │   │
│  │   │              (runs before deployment)                        │    │   │
│  │   └─────────────────────────────────────────────────────────────┘    │   │
│  │                                                                       │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Migration Strategy for Kubernetes

**Principle**: Migrations run as a Kubernetes Job BEFORE the new application version is deployed.

#### Migration Job

```yaml
# k8s/migrations/job.yaml
apiVersion: batch/v1
kind: Job
metadata:
  name: kyc-migrations-{{ .Release.Revision }}
  namespace: kyc-system
  annotations:
    helm.sh/hook: pre-upgrade,pre-install
    helm.sh/hook-weight: "-1"
    helm.sh/hook-delete-policy: before-hook-creation
spec:
  backoffLimit: 3
  ttlSecondsAfterFinished: 300
  template:
    metadata:
      labels:
        app: kyc-migrations
    spec:
      restartPolicy: OnFailure
      containers:
        - name: migrator
          image: {{ .Values.image.repository }}:{{ .Values.image.tag }}
          command: ["dotnet", "Kyc.Migrator.dll"]
          env:
            - name: ConnectionStrings__kyc
              valueFrom:
                secretKeyRef:
                  name: kyc-database-credentials
                  key: connection-string
          resources:
            requests:
              memory: "128Mi"
              cpu: "100m"
            limits:
              memory: "256Mi"
              cpu: "500m"
```

#### Deployment with Migration Dependency

```yaml
# k8s/deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: kyc-api
  namespace: kyc-system
  annotations:
    # Ensure migrations ran successfully
    checksum/migrations: {{ include (print $.Template.BasePath "/migrations/job.yaml") . | sha256sum }}
spec:
  replicas: {{ .Values.replicaCount }}
  strategy:
    type: RollingUpdate
    rollingUpdate:
      maxUnavailable: 0
      maxSurge: 1
  selector:
    matchLabels:
      app: kyc-api
  template:
    metadata:
      labels:
        app: kyc-api
    spec:
      containers:
        - name: api
          image: {{ .Values.image.repository }}:{{ .Values.image.tag }}
          ports:
            - containerPort: 8080
          env:
            - name: ConnectionStrings__kyc
              valueFrom:
                secretKeyRef:
                  name: kyc-database-credentials
                  key: connection-string
            - name: ASPNETCORE_ENVIRONMENT
              value: Production
          livenessProbe:
            httpGet:
              path: /health/live
              port: 8080
            initialDelaySeconds: 10
            periodSeconds: 10
          readinessProbe:
            httpGet:
              path: /health/ready
              port: 8080
            initialDelaySeconds: 5
            periodSeconds: 5
          resources:
            requests:
              memory: "256Mi"
              cpu: "250m"
            limits:
              memory: "512Mi"
              cpu: "1000m"
```

### Database Secret

```yaml
# k8s/secrets/database.yaml
apiVersion: v1
kind: Secret
metadata:
  name: kyc-database-credentials
  namespace: kyc-system
type: Opaque
stringData:
  connection-string: "Host=postgres.database.svc;Database=kyc;Username=kyc_user;Password=${DATABASE_PASSWORD}"
```

### Service and Ingress

```yaml
# k8s/service.yaml
apiVersion: v1
kind: Service
metadata:
  name: kyc-api
  namespace: kyc-system
spec:
  selector:
    app: kyc-api
  ports:
    - port: 80
      targetPort: 8080
---
# k8s/ingress.yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: kyc-api
  namespace: kyc-system
  annotations:
    kubernetes.io/ingress.class: nginx
    cert-manager.io/cluster-issuer: letsencrypt-prod
spec:
  tls:
    - hosts:
        - api.kyc.example.com
      secretName: kyc-api-tls
  rules:
    - host: api.kyc.example.com
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: kyc-api
                port:
                  number: 80
```

---

## Migration Workflow

### Development Workflow

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        DEVELOPMENT MIGRATION FLOW                           │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  1. Create Migration                                                        │
│     dotnet ef migrations add FeatureName                                   │
│                     │                                                       │
│                     ▼                                                       │
│  2. Review Migration Code                                                   │
│     - Check Up() and Down() methods                                        │
│     - Verify backward compatibility                                         │
│                     │                                                       │
│                     ▼                                                       │
│  3. Test Locally                                                            │
│     - Run Aspire (auto-applies migrations)                                 │
│     - Verify application works                                             │
│                     │                                                       │
│                     ▼                                                       │
│  4. Commit & Push                                                           │
│     - Migration files included in PR                                       │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### CI/CD Pipeline Workflow

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          CI/CD MIGRATION FLOW                               │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  1. Build Stage                                                             │
│     ┌─────────────────────────────────────────────────────────────────┐    │
│     │  - Build application                                             │    │
│     │  - Build migration container                                     │    │
│     │  - Run unit tests                                                │    │
│     │  - Push images to registry                                       │    │
│     └─────────────────────────────────────────────────────────────────┘    │
│                                     │                                       │
│                                     ▼                                       │
│  2. Migration Test Stage (Staging)                                          │
│     ┌─────────────────────────────────────────────────────────────────┐    │
│     │  - Deploy to staging cluster                                     │    │
│     │  - Run migration job                                             │    │
│     │  - Validate migration success                                    │    │
│     │  - Run integration tests                                         │    │
│     └─────────────────────────────────────────────────────────────────┘    │
│                                     │                                       │
│                                     ▼                                       │
│  3. Production Deploy                                                       │
│     ┌─────────────────────────────────────────────────────────────────┐    │
│     │  - Run pre-deploy migration job                                  │    │
│     │  - Wait for job completion (success)                             │    │
│     │  - Rolling deployment of new pods                                │    │
│     │  - Health check verification                                     │    │
│     └─────────────────────────────────────────────────────────────────┘    │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### GitHub Actions Example

```yaml
# .github/workflows/deploy.yml
name: Deploy

on:
  push:
    branches: [main]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      
      - name: Build
        run: dotnet build --configuration Release
      
      - name: Test
        run: dotnet test --configuration Release --no-build
      
      - name: Build and push Docker images
        run: |
          docker build -f src/Kyc.Api/Dockerfile -t ${{ secrets.REGISTRY }}/kyc-api:${{ github.sha }} .
          docker build -f src/Kyc.Migrator/Dockerfile -t ${{ secrets.REGISTRY }}/kyc-migrator:${{ github.sha }} .
          docker push ${{ secrets.REGISTRY }}/kyc-api:${{ github.sha }}
          docker push ${{ secrets.REGISTRY }}/kyc-migrator:${{ github.sha }}

  deploy-staging:
    needs: build
    runs-on: ubuntu-latest
    environment: staging
    steps:
      - name: Deploy to staging
        run: |
          helm upgrade --install kyc ./helm/kyc \
            --namespace kyc-staging \
            --set image.tag=${{ github.sha }}
      
      - name: Wait for migrations
        run: |
          kubectl wait --for=condition=complete job/kyc-migrations \
            --namespace kyc-staging \
            --timeout=300s
      
      - name: Run integration tests
        run: |
          # Run integration tests against staging

  deploy-production:
    needs: deploy-staging
    runs-on: ubuntu-latest
    environment: production
    steps:
      - name: Deploy to production
        run: |
          helm upgrade --install kyc ./helm/kyc \
            --namespace kyc-production \
            --set image.tag=${{ github.sha }}
      
      - name: Wait for migrations
        run: |
          kubectl wait --for=condition=complete job/kyc-migrations \
            --namespace kyc-production \
            --timeout=300s
      
      - name: Verify deployment
        run: |
          kubectl rollout status deployment/kyc-api \
            --namespace kyc-production \
            --timeout=300s
```

---

## Rollback Strategy

### Application Rollback

```bash
# Rollback to previous deployment
kubectl rollout undo deployment/kyc-api --namespace kyc-production

# Or rollback to specific revision
kubectl rollout undo deployment/kyc-api --to-revision=3 --namespace kyc-production
```

### Migration Rollback

**Important**: Database rollbacks require careful consideration.

#### Safe Rollback (Non-Destructive Changes)

For additive migrations (new tables, new nullable columns):

```bash
# Application can run on both old and new schema
# Simply deploy the previous version
kubectl rollout undo deployment/kyc-api
```

#### Dangerous Rollback (Destructive Changes)

For migrations that remove or modify data:

1. **Generate rollback script**:
   ```bash
   dotnet ef migrations script CurrentMigration PreviousMigration \
       --project src/Kyc.Infrastructure \
       --startup-project src/Kyc.Api \
       --output rollback.sql
   ```

2. **Review carefully before execution**

3. **Apply with caution**:
   ```bash
   # Connect to database and run rollback script
   psql -h $DB_HOST -U $DB_USER -d kyc -f rollback.sql
   ```

4. **Deploy previous application version**

---

## Environment Configuration

### Configuration by Environment

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Environment-specific configuration
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

// In Kubernetes, use environment variables or ConfigMaps
// ConnectionStrings__kyc is set via Secret
```

### ConfigMap for Non-Sensitive Config

```yaml
# k8s/configmap.yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: kyc-config
  namespace: kyc-system
data:
  Kyc__DefaultProvider: "onfido"
  Kyc__VerificationExpiry: "01:00:00"
  Logging__LogLevel__Default: "Information"
```

---

## Health Checks

```csharp
// Program.cs
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "database")
    .AddCheck<ProviderHealthCheck>("providers");

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false  // No checks for liveness
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
```

---

## Monitoring and Observability

### OpenTelemetry Configuration

```csharp
// ServiceDefaults/Extensions.cs
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation();
        metrics.AddHttpClientInstrumentation();
        metrics.AddRuntimeInstrumentation();
    })
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation();
        tracing.AddHttpClientInstrumentation();
        tracing.AddEntityFrameworkCoreInstrumentation();
        tracing.AddNpgsql();
    });
```

### Prometheus Annotations

```yaml
# k8s/deployment.yaml
spec:
  template:
    metadata:
      annotations:
        prometheus.io/scrape: "true"
        prometheus.io/port: "8080"
        prometheus.io/path: "/metrics"
```

---

## Related Documents

- [ARCHITECTURE.md](./ARCHITECTURE.md) - System architecture
- [DATABASE_DESIGN.md](./DATABASE_DESIGN.md) - Database schema and migrations
- [SECURITY.md](./SECURITY.md) - Secrets management
