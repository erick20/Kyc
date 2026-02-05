# Architecture Decision Records

This directory contains Architecture Decision Records (ADRs) for the KYC Aggregator project.

## What is an ADR?

An Architecture Decision Record captures an important architectural decision made along with its context and consequences. ADRs help:

- Document the reasoning behind decisions
- Onboard new team members
- Avoid revisiting decided topics
- Track the evolution of the architecture

## ADR Index

| ADR | Title | Status | Date |
|-----|-------|--------|------|
| [ADR-001](./ADR-001-modular-monolith-architecture.md) | Modular Monolith Architecture | Accepted | 2024-01-15 |
| [ADR-002](./ADR-002-clean-architecture-layers.md) | Clean Architecture Layers | Accepted | 2024-01-15 |
| [ADR-003](./ADR-003-provider-abstraction-strategy.md) | Provider Abstraction Strategy | Accepted | 2024-01-15 |
| [ADR-004](./ADR-004-postgresql-database-choice.md) | PostgreSQL as Primary Database | Accepted | 2024-01-15 |
| [ADR-005](./ADR-005-dbcontext-strategy.md) | Single DbContext with Module Schema Separation | Accepted | 2024-01-15 |

## ADR Lifecycle

### Statuses

- **Proposed**: Under discussion
- **Accepted**: Approved and in effect
- **Deprecated**: No longer applies (superseded)
- **Superseded**: Replaced by another ADR

### Template

When creating a new ADR, use this structure:

```markdown
# ADR-XXX: Title

## Status

**Proposed** | **Accepted** | **Deprecated** | **Superseded by [ADR-YYY](./ADR-YYY.md)**

## Date

YYYY-MM-DD

## Context

What is the issue that we're seeing that is motivating this decision or change?

## Options Considered

### Option A: Name
Description, pros, cons

### Option B: Name
Description, pros, cons

## Decision

What is the change that we're proposing and/or doing?

## Consequences

### Positive
What becomes easier or possible?

### Negative
What becomes more difficult?

### Mitigations
How do we address the negative consequences?

## Related Decisions

Links to related ADRs

## References

External resources that influenced this decision
```

## Naming Convention

ADRs are numbered sequentially: `ADR-001`, `ADR-002`, etc.

File names use the format: `ADR-XXX-short-title.md`

## Future ADRs (Candidates)

Topics that may warrant future ADRs:

- [ ] Caching strategy (Redis vs in-memory)
- [ ] Event sourcing consideration
- [ ] Multi-tenancy approach
- [ ] Authentication method (API keys vs OAuth)
- [ ] Logging and observability stack
- [ ] CI/CD pipeline design
- [ ] Admin panel technology choice
