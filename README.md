# KYC Aggregator

A modular, production-grade KYC (Know Your Customer) aggregation service built with .NET 10. Designed to integrate multiple KYC providers behind a unified interface, deployable as a standalone HTTP API or embeddable as a NuGet package.

## Overview

KYC Aggregator solves the common challenge of working with multiple identity verification providers. Instead of tightly coupling your application to a single provider, this service abstracts provider-specific implementations behind a clean, consistent API.

### Key Features

- **Multi-Provider Support**: Integrate with multiple KYC providers (Onfido, Jumio, Veriff, etc.) through a unified interface
- **Dual Deployment Mode**: Run as a standalone HTTP API service or embed directly into your .NET application via NuGet
- **Provider Orchestration**: Route verification requests based on rules, fallback strategies, or cost optimization
- **Audit Trail**: Complete logging of all verification attempts for compliance
- **Webhook Normalization**: Receive provider callbacks through a unified webhook format
- **Async-First Design**: Built for high-throughput verification workflows

## Quick Start

### As a Standalone API

```bash
# Clone the repository
git clone https://github.com/your-org/kyc-aggregator.git
cd kyc-aggregator

# Configure your providers (see Configuration section)
cp appsettings.example.json appsettings.json

# Run with Docker
docker-compose up -d

# Or run directly
dotnet run --project src/Kyc.Api
```

### As a NuGet Package

```bash
dotnet add package Kyc.Aggregator.Core
dotnet add package Kyc.Aggregator.Provider.Onfido  # Add providers as needed
```

```csharp
// In your Startup.cs or Program.cs
services.AddKycAggregator(options =>
{
    options.UsePostgres(connectionString);
    options.AddProvider<OnfidoProvider>(config =>
    {
        config.ApiKey = Configuration["Onfido:ApiKey"];
    });
});
```

## Configuration

### Provider Setup

Each provider requires specific credentials. Store these securely using your preferred secrets management solution:

```json
{
  "Kyc": {
    "Providers": {
      "Onfido": {
        "ApiKey": "your-api-key",
        "WebhookSecret": "your-webhook-secret",
        "Region": "EU"
      }
    },
    "DefaultProvider": "Onfido",
    "FallbackProvider": null
  }
}
```

### Database

KYC Aggregator uses PostgreSQL for persistence:

```json
{
  "ConnectionStrings": {
    "KycDatabase": "Host=localhost;Database=kyc;Username=kyc_user;Password=secret"
  }
}
```

## Architecture

This project follows Clean Architecture principles within a Modular Monolith structure:

```
┌─────────────────────────────────────────────────────────────┐
│                        API / Host                           │
├─────────────────────────────────────────────────────────────┤
│                     Application Layer                       │
│              (Use Cases, Commands, Queries)                 │
├─────────────────────────────────────────────────────────────┤
│                      Domain Layer                           │
│           (Entities, Value Objects, Domain Events)          │
├─────────────────────────────────────────────────────────────┤
│                   Infrastructure Layer                      │
│         (Database, External APIs, Provider SDKs)            │
└─────────────────────────────────────────────────────────────┘
```

See [ARCHITECTURE.md](./docs/ARCHITECTURE.md) for detailed architectural documentation.

## Documentation

| Document | Description |
|----------|-------------|
| [ARCHITECTURE.md](./docs/ARCHITECTURE.md) | System architecture and design decisions |
| [FOLDER_STRUCTURE.md](./docs/FOLDER_STRUCTURE.md) | Project and solution organization |
| [DOMAIN_MODEL.md](./docs/DOMAIN_MODEL.md) | Core domain concepts and models |
| [PROVIDER_INTEGRATION.md](./docs/PROVIDER_INTEGRATION.md) | How to integrate KYC providers |
| [API_DESIGN.md](./docs/API_DESIGN.md) | REST API design and endpoints |
| [DATABASE_DESIGN.md](./docs/DATABASE_DESIGN.md) | EF Core approach, database schema, migrations |
| [DEPLOYMENT.md](./docs/DEPLOYMENT.md) | Container deployment, Kubernetes, migration workflows |
| [NUGET_STRATEGY.md](./docs/NUGET_STRATEGY.md) | NuGet packaging strategy |
| [EXTENSIBILITY.md](./docs/EXTENSIBILITY.md) | Extending the system |
| [SECURITY.md](./docs/SECURITY.md) | Security considerations |
| [ROADMAP.md](./docs/ROADMAP.md) | Development roadmap |

## Requirements

- .NET 10.0 SDK or later
- PostgreSQL 14+
- Docker (optional, for containerized deployment)

## Development

```bash
# Restore dependencies
dotnet restore

# Build
dotnet build

# Run tests
dotnet test

# Run with hot reload
dotnet watch run --project src/Kyc.Api
```

## Contributing

1. Read the [ARCHITECTURE.md](./docs/ARCHITECTURE.md) to understand the design
2. Check [EXTENSIBILITY.md](./docs/EXTENSIBILITY.md) for adding new providers
3. Follow existing code patterns and conventions
4. Ensure all tests pass before submitting PRs

## License

[MIT License](./LICENSE) - See LICENSE file for details.

## Support

- **Issues**: GitHub Issues for bug reports and feature requests
- **Discussions**: GitHub Discussions for questions and ideas
