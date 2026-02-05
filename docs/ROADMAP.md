# Roadmap

This document outlines the phased development plan for the KYC Aggregator system, from initial MVP through future enhancements.

## Development Philosophy

- **Iterative delivery**: Ship working software early and often
- **Value-first**: Prioritize features that deliver immediate business value
- **Quality from day one**: Tests, documentation, and security are not afterthoughts
- **Flexible scope**: Phases may adjust based on learnings and priorities

## Phase Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           DEVELOPMENT ROADMAP                               │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  Phase 1: MVP                    Phase 2: Production Ready                 │
│  ───────────────────────────     ────────────────────────────────────────  │
│  • Core domain model             • Multi-provider support                  │
│  • Single provider (Onfido)      • Provider fallback                       │
│  • Basic API                     • Webhook reliability                     │
│  • PostgreSQL storage            • Audit logging                           │
│  • Manual verification flow      • Rate limiting                           │
│                                  • Monitoring & alerting                   │
│                                                                             │
│  Phase 3: NuGet & Scale          Phase 4: Admin Panel                      │
│  ───────────────────────────     ────────────────────────────────────────  │
│  • NuGet package extraction      • Admin web application                   │
│  • Horizontal scaling            • Verification management                 │
│  • Caching layer                 • Provider configuration UI               │
│  • Performance optimization      • Reporting & analytics                   │
│  • Multi-tenancy                 • User management                         │
│                                                                             │
│  Phase 5: Advanced Features                                                 │
│  ──────────────────────────────────────────────────────────────────────────│
│  • ML-based provider selection   • Document data extraction                │
│  • Risk scoring                  • Batch processing                        │
│  • Regulatory reporting          • Mobile SDKs                             │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Phase 1: MVP (Minimum Viable Product)

**Goal**: Validate the core architecture with a working end-to-end flow.

### Deliverables

#### Domain Layer
- [ ] Core entities: `Applicant`, `Verification`, `VerificationCheck`, `Document`
- [ ] Value objects: `ApplicantId`, `VerificationId`, `Email`, `Address`
- [ ] Domain events: `VerificationCreated`, `VerificationCompleted`, `CheckCompleted`
- [ ] Repository interfaces
- [ ] Unit tests for domain logic

#### Application Layer
- [ ] Commands: `CreateApplicant`, `StartVerification`, `ProcessWebhook`
- [ ] Queries: `GetApplicant`, `GetVerification`, `ListVerifications`
- [ ] MediatR pipeline with validation
- [ ] FluentValidation validators
- [ ] Application service tests

#### Infrastructure Layer
- [ ] EF Core DbContext with PostgreSQL
- [ ] Entity configurations (Fluent API)
- [ ] Repository implementations
- [ ] Initial database migration
- [ ] Outbox pattern for events
- [ ] Integration tests with test database

#### Provider: Onfido
- [ ] `OnfidoProvider` implementation
- [ ] Applicant creation
- [ ] Check initiation
- [ ] Status polling
- [ ] Webhook handling
- [ ] SDK token generation
- [ ] Sandbox testing

#### API Layer
- [ ] `ApplicantsController` (CRUD)
- [ ] `VerificationsController` (start, get, list)
- [ ] `WebhooksController` (Onfido)
- [ ] Error handling middleware
- [ ] Request/response logging
- [ ] Swagger documentation
- [ ] Health check endpoint

#### DevOps
- [ ] Dockerfile
- [ ] docker-compose.yml (API + PostgreSQL)
- [ ] GitHub Actions CI pipeline
- [ ] README with setup instructions

### Acceptance Criteria
- Can create an applicant via API
- Can start a verification that creates a check in Onfido sandbox
- Can receive webhook and update verification status
- Can query verification status and see results

---

## Phase 2: Production Ready

**Goal**: Harden the system for production deployment with observability and reliability.

### Deliverables

#### Multi-Provider Support
- [ ] Provider registry and discovery
- [ ] `IProviderSelector` implementation
- [ ] Provider health checks
- [ ] Jumio provider integration
- [ ] Veriff provider integration (optional)
- [ ] Provider-agnostic webhook routing

#### Reliability
- [ ] Provider fallback mechanism
- [ ] Retry policies with Polly
- [ ] Circuit breaker pattern
- [ ] Idempotency for API operations
- [ ] Outbox message retry logic
- [ ] Dead letter handling

#### Security
- [ ] API key authentication
- [ ] Authorization policies
- [ ] Webhook signature verification
- [ ] Rate limiting per API key
- [ ] PII encryption at rest
- [ ] Security headers
- [ ] Secrets management integration

#### Observability
- [ ] Structured logging with Serilog
- [ ] Correlation ID propagation
- [ ] OpenTelemetry tracing
- [ ] Prometheus metrics
- [ ] Health check dashboard
- [ ] Error alerting (PagerDuty/Slack)

#### Audit & Compliance
- [ ] Audit log table and service
- [ ] Change tracking for entities
- [ ] Data retention policies
- [ ] GDPR data export endpoint
- [ ] GDPR deletion workflow

#### API Enhancements
- [ ] Pagination for list endpoints
- [ ] Filtering and sorting
- [ ] Field selection
- [ ] Webhook subscription management
- [ ] API versioning (v1)

#### Testing
- [ ] Architecture enforcement tests
- [ ] Load testing with k6 or similar
- [ ] Security scanning (OWASP ZAP)
- [ ] Chaos testing basics

### Acceptance Criteria
- Multiple providers can be configured and selected
- System recovers gracefully from provider failures
- All API calls are authenticated and authorized
- Complete audit trail exists
- Monitoring dashboards show system health

---

## Phase 3: NuGet & Scale

**Goal**: Package core functionality as NuGet and enable horizontal scaling.

### Deliverables

#### NuGet Packages
- [ ] `Kyc.Aggregator.Contracts` - public DTOs
- [ ] `Kyc.Aggregator.Core` - domain + application
- [ ] `Kyc.Aggregator.Infrastructure` - EF Core + PostgreSQL
- [ ] `Kyc.Aggregator.Provider.Onfido`
- [ ] `Kyc.Aggregator.Provider.Jumio`
- [ ] `Kyc.Aggregator.AspNetCore` - hosting helpers
- [ ] `Kyc.Aggregator.Testing` - test utilities
- [ ] NuGet package CI/CD pipeline
- [ ] Package documentation
- [ ] Sample consumer application

#### Scaling
- [ ] Distributed caching (Redis)
- [ ] Session state externalization
- [ ] Database connection pooling optimization
- [ ] Read replicas for queries
- [ ] Horizontal pod autoscaling config
- [ ] Load balancer configuration

#### Multi-Tenancy (Optional)
- [ ] Tenant identification middleware
- [ ] Per-tenant database schema or filtering
- [ ] Per-tenant provider configuration
- [ ] Per-tenant rate limits
- [ ] Tenant isolation testing

#### Performance
- [ ] Query optimization
- [ ] Response caching for read endpoints
- [ ] Async verification processing
- [ ] Background job processing (Hangfire/Quartz)
- [ ] Database index analysis

### Acceptance Criteria
- NuGet packages published and consumable
- Sample application works with embedded packages
- System scales horizontally under load
- Response times meet SLA under concurrent load

---

## Phase 4: Admin Panel

**Goal**: Provide a web-based administration interface.

### Deliverables

#### Admin API
- [ ] Admin-specific endpoints
- [ ] Verification search and filtering
- [ ] Manual verification override
- [ ] Provider configuration CRUD
- [ ] API key management
- [ ] Webhook subscription management
- [ ] System health overview

#### Admin Web Application
- [ ] Technology selection (Blazor/React/Angular)
- [ ] Authentication (SSO/OAuth)
- [ ] Role-based access control
- [ ] Dashboard with KPIs
- [ ] Verification list and detail views
- [ ] Applicant management
- [ ] Provider status monitoring
- [ ] Configuration management
- [ ] Audit log viewer

#### Reporting
- [ ] Verification volume reports
- [ ] Provider performance metrics
- [ ] Pass/fail rate analytics
- [ ] Average processing time
- [ ] Cost tracking per provider
- [ ] Export to CSV/PDF

#### User Management
- [ ] Admin user CRUD
- [ ] Role definitions
- [ ] Permission management
- [ ] Activity logging
- [ ] Password policies

### Acceptance Criteria
- Admins can log in and view verifications
- Admins can override verification results
- Admins can configure providers without code changes
- Reports show meaningful business metrics

---

## Phase 5: Advanced Features

**Goal**: Add intelligent automation and advanced capabilities.

### Deliverables

#### Intelligent Routing
- [ ] ML-based provider selection
- [ ] Cost optimization algorithms
- [ ] Success rate prediction
- [ ] Geographic optimization
- [ ] A/B testing framework for providers

#### Risk Scoring
- [ ] Risk score calculation engine
- [ ] Configurable risk rules
- [ ] Risk-based routing
- [ ] Fraud detection signals
- [ ] Risk score API

#### Document Intelligence
- [ ] OCR data extraction
- [ ] Document classification
- [ ] Data validation against submission
- [ ] Duplicate document detection
- [ ] Document quality scoring

#### Batch Processing
- [ ] Bulk applicant import
- [ ] Batch verification initiation
- [ ] Progress tracking
- [ ] Batch results export
- [ ] Scheduled batch jobs

#### Mobile Support
- [ ] Mobile SDK wrappers
- [ ] React Native integration guide
- [ ] Flutter integration guide
- [ ] Deep linking support
- [ ] Mobile-specific flows

#### Regulatory
- [ ] Regulatory reporting templates
- [ ] AML screening integration
- [ ] PEP database integration
- [ ] Sanctions list checking
- [ ] Compliance dashboard

### Acceptance Criteria
- System recommends optimal provider based on context
- Risk scores influence verification routing
- Document data auto-populates applicant fields
- Batch operations work at scale

---

## Technical Debt & Maintenance

Ongoing throughout all phases:

- [ ] Dependency updates (quarterly)
- [ ] Security vulnerability patching
- [ ] Performance regression monitoring
- [ ] Documentation updates
- [ ] Test coverage maintenance (>80%)
- [ ] Code quality metrics (SonarQube)
- [ ] API deprecation management

---

## Success Metrics

### Phase 1
- Successful end-to-end verification flow
- <2s API response time (p95)
- Zero critical bugs

### Phase 2
- 99.9% uptime
- <5s verification initiation (p95)
- <1% provider fallback rate
- Complete audit coverage

### Phase 3
- NuGet packages with >100 downloads
- Linear scaling to 10x baseline load
- <100ms cache hit response time

### Phase 4
- Admin panel daily active users
- <30s average admin task completion
- Reduced support ticket volume

### Phase 5
- 20% cost reduction via intelligent routing
- 10% fraud prevention improvement
- 50% reduction in manual reviews

---

## Decision Points

At each phase boundary, evaluate:

1. **Build vs Buy**: Should we build this feature or integrate a third-party?
2. **Scope**: What features should move to next phase?
3. **Architecture**: Does current architecture support next phase needs?
4. **Team**: Do we have skills for next phase technologies?
5. **Market**: Have customer needs changed?

---

## Related Documents

- [ARCHITECTURE.md](./ARCHITECTURE.md) - Technical architecture
- [EXTENSIBILITY.md](./EXTENSIBILITY.md) - Extension points
- [NUGET_STRATEGY.md](./NUGET_STRATEGY.md) - Package strategy
