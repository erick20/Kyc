# Domain Model

This document describes the core domain concepts, entities, value objects, and their relationships within the KYC Aggregator system.

## Domain Overview

The KYC Aggregator domain models the process of verifying a person's identity through various checks performed by external providers. The core workflow is:

1. An **Applicant** is created with personal information
2. A **Verification** is initiated for the applicant
3. The verification contains one or more **Checks** (identity, document, liveness, AML)
4. Checks are executed by a **Provider** (Onfido, Jumio, etc.)
5. Results flow back, and the verification reaches a final **Status**

## Entity Relationship Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                                                                             │
│  ┌─────────────┐         ┌──────────────────┐        ┌─────────────────┐   │
│  │             │ 1    *  │                  │ 1   *  │                 │   │
│  │  Applicant  │────────▶│  Verification    │───────▶│ VerificationCheck│  │
│  │             │         │                  │        │                 │   │
│  └─────────────┘         └──────────────────┘        └────────┬────────┘   │
│        │                         │                            │            │
│        │                         │                            │            │
│        ▼                         ▼                            ▼            │
│  ┌─────────────┐         ┌──────────────────┐        ┌─────────────────┐   │
│  │  Document   │         │  ProviderSession │        │  CheckResult    │   │
│  │  (uploaded) │         │  (external ref)  │        │  (from provider)│   │
│  └─────────────┘         └──────────────────┘        └─────────────────┘   │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘

                    ┌──────────────────────────────────┐
                    │           Provider               │
                    │  (Onfido, Jumio, Veriff, etc.)   │
                    │  - External system reference     │
                    │  - Configuration per tenant      │
                    └──────────────────────────────────┘
```

## Core Entities

### Applicant

The person undergoing identity verification. An applicant can have multiple verifications over time (re-verification, additional checks).

```
Applicant
├── Id: ApplicantId (GUID)
├── ExternalReference: string?          # Client's reference for this person
├── FirstName: PersonName
├── MiddleName: PersonName?
├── LastName: PersonName
├── Email: Email
├── PhoneNumber: PhoneNumber?
├── DateOfBirth: DateOnly?
├── Address: Address?
├── Nationality: CountryCode?
├── CreatedAt: DateTime
├── UpdatedAt: DateTime
├── Metadata: Dictionary<string, string> # Custom key-value pairs
│
├── Documents: List<Document>
└── Verifications: List<Verification>
```

**Invariants:**
- Must have at least first name and last name
- Email must be valid format
- ExternalReference must be unique per tenant (if provided)

**Domain Events:**
- `ApplicantCreatedEvent`
- `ApplicantUpdatedEvent`

### Verification

A verification process for an applicant. Contains one or more checks that are executed by a provider.

```
Verification
├── Id: VerificationId (GUID)
├── ApplicantId: ApplicantId
├── ProviderId: string                   # Which provider handles this
├── ProviderVerificationId: string?      # Provider's external ID
├── Status: VerificationStatus
├── Result: VerificationResult?
├── RequestedChecks: List<CheckType>     # What checks were requested
├── CreatedAt: DateTime
├── StartedAt: DateTime?
├── CompletedAt: DateTime?
├── ExpiresAt: DateTime?                 # For time-limited verifications
├── FailureReason: string?
├── Metadata: Dictionary<string, string>
│
└── Checks: List<VerificationCheck>
```

**Invariants:**
- Cannot complete without all checks being processed
- Status transitions must follow valid state machine
- Cannot start if applicant has incomplete required fields

**Domain Events:**
- `VerificationCreatedEvent`
- `VerificationStartedEvent`
- `VerificationCompletedEvent`
- `VerificationExpiredEvent`
- `VerificationCancelledEvent`

### VerificationCheck

An individual check within a verification (e.g., document check, liveness check).

```
VerificationCheck
├── Id: Guid
├── VerificationId: VerificationId
├── CheckType: CheckType
├── Status: CheckStatus
├── Result: CheckResult?
├── ProviderCheckId: string?             # Provider's ID for this check
├── StartedAt: DateTime?
├── CompletedAt: DateTime?
├── RawProviderResponse: string?         # JSON blob for debugging
├── BreakdownResults: List<BreakdownItem> # Detailed sub-results
└── Metadata: Dictionary<string, string>
```

**Invariants:**
- CheckType must be one of the requested checks on parent Verification
- Cannot complete before starting

**Domain Events:**
- `CheckStartedEvent`
- `CheckCompletedEvent`

### Document

Identity documents uploaded for verification.

```
Document
├── Id: Guid
├── ApplicantId: ApplicantId
├── Type: DocumentType
├── Side: DocumentSide (Front, Back)
├── FileName: string
├── ContentType: string
├── StorageReference: string             # Reference to file storage
├── ProviderDocumentId: string?          # Provider's document ID
├── UploadedAt: DateTime
├── ExpiryDate: DateOnly?                # Document expiry
├── IssuingCountry: CountryCode?
└── Metadata: Dictionary<string, string>
```

## Value Objects

### ApplicantId / VerificationId

Strongly-typed identifiers prevent mixing up IDs of different entity types.

```
ApplicantId
├── Value: Guid

VerificationId
├── Value: Guid
```

### Email

Validated email address.

```
Email
├── Value: string
│
├── Validate(): bool
└── GetDomain(): string
```

### PersonName

Name component with normalization.

```
PersonName
├── Value: string
│
├── Normalize(): PersonName    # Trim, proper case
└── ToString(): string
```

### Address

Structured address information.

```
Address
├── Line1: string
├── Line2: string?
├── City: string
├── State: string?
├── PostalCode: string
├── Country: CountryCode
│
└── Format(): string           # Country-specific formatting
```

### CountryCode

ISO 3166-1 alpha-2 country code.

```
CountryCode
├── Value: string (2 chars)
│
├── IsValid(): bool
└── GetDisplayName(): string   # "US" → "United States"
```

## Enumerations

### VerificationStatus

```
VerificationStatus
├── Created      # Verification created, not yet started
├── Pending      # Waiting for provider to start processing
├── InProgress   # Provider is processing checks
├── AwaitingInput# Waiting for user to complete an action (e.g., upload doc)
├── Completed    # All checks finished
├── Expired      # Time limit reached
├── Cancelled    # Manually cancelled
└── Failed       # System error prevented completion
```

**State Machine:**

```
                    ┌──────────────┐
                    │   Created    │
                    └──────┬───────┘
                           │ start
                           ▼
┌──────────┐        ┌──────────────┐
│ Cancelled│◄───────│   Pending    │
└──────────┘        └──────┬───────┘
     ▲                     │ provider accepts
     │              ┌──────▼───────┐
     ├──────────────│  InProgress  │◄────────┐
     │              └──────┬───────┘         │
     │                     │                 │
     │         ┌───────────┼───────────┐     │
     │         ▼           ▼           ▼     │
     │  ┌───────────┐ ┌─────────┐ ┌────────┐ │
     │  │AwaitingIn │ │Completed│ │ Failed │ │
     │  │   put     │ └─────────┘ └────────┘ │
     │  └─────┬─────┘                        │
     │        │ input received               │
     └────────┴──────────────────────────────┘
                                    
         ┌───────────┐
         │  Expired  │ (from any non-terminal state)
         └───────────┘
```

### VerificationResult

```
VerificationResult
├── Approved     # All checks passed
├── Rejected     # One or more checks failed decisively
├── NeedsReview  # Manual review required
└── Inconclusive # Cannot determine (poor image quality, etc.)
```

### CheckType

```
CheckType
├── Identity     # Name, DOB, address verification
├── Document     # Document authenticity check
├── Liveness     # Selfie/video liveness detection
├── FaceMatch    # Compare document photo to selfie
├── Aml          # Anti-money laundering / PEP / sanctions
├── Poa          # Proof of address
└── Custom       # Provider-specific check type
```

### CheckStatus

```
CheckStatus
├── Pending
├── InProgress
├── Completed
├── Failed
└── Cancelled
```

### CheckResult

```
CheckResult
├── Clear        # Check passed
├── Consider     # Needs review
├── Rejected     # Check failed
└── Inconclusive # Cannot determine
```

### DocumentType

```
DocumentType
├── Passport
├── DriversLicense
├── NationalId
├── ResidencePermit
├── UtilityBill    # For PoA
├── BankStatement  # For PoA
└── Other
```

### DocumentSide

```
DocumentSide
├── Front
├── Back
└── Single        # Documents without sides (passport bio page)
```

## Aggregates

### Applicant Aggregate

```
Applicant (Aggregate Root)
├── Documents (owned entities)
└── Verifications (reference by ID only)
```

The Applicant is responsible for:
- Managing its own Documents
- Validating completeness for verification

### Verification Aggregate

```
Verification (Aggregate Root)
└── VerificationChecks (owned entities)
```

The Verification is responsible for:
- Managing check lifecycle
- Enforcing status transitions
- Calculating overall result from check results

## Domain Services

### IVerificationOrchestrator

Coordinates complex verification workflows.

```
IVerificationOrchestrator
├── StartVerification(applicantId, checkTypes, providerId?): VerificationId
├── ProcessCheckResult(verificationId, checkId, result): void
├── HandleTimeout(verificationId): void
└── RetryFailedCheck(verificationId, checkId): void
```

### IApplicantDuplicateChecker

Detects potential duplicate applicants.

```
IApplicantDuplicateChecker
├── FindPotentialDuplicates(applicant): List<ApplicantId>
└── CalculateSimilarityScore(a, b): decimal
```

### IProviderSelector

Selects which provider to use for a verification.

```
IProviderSelector
├── SelectProvider(applicant, checkTypes): ProviderId
└── GetFallbackProvider(primaryProviderId): ProviderId?
```

## Domain Events

Domain events are raised when significant state changes occur. They enable loose coupling between modules and support eventual consistency.

| Event | Raised When | Key Data |
|-------|-------------|----------|
| `ApplicantCreatedEvent` | New applicant registered | ApplicantId, Email |
| `ApplicantUpdatedEvent` | Applicant data changed | ApplicantId, ChangedFields |
| `VerificationCreatedEvent` | Verification initiated | VerificationId, ApplicantId, CheckTypes |
| `VerificationStartedEvent` | Provider accepted verification | VerificationId, ProviderId |
| `VerificationCompletedEvent` | All checks finished | VerificationId, Result |
| `CheckCompletedEvent` | Individual check finished | VerificationId, CheckId, CheckType, Result |
| `DocumentUploadedEvent` | Document attached to applicant | ApplicantId, DocumentId, DocumentType |

## Invariants and Business Rules

1. **Applicant Completeness**: A verification cannot start unless the applicant has required fields for the requested check types
2. **Sequential Checks**: Some check types require others to complete first (e.g., FaceMatch requires Document)
3. **Single Active Verification**: An applicant can only have one active (non-terminal) verification at a time
4. **Immutable Results**: Once a verification is Completed, its result cannot change
5. **Document Reuse**: Documents uploaded for one verification can be reused for subsequent verifications
6. **Provider Consistency**: All checks within a single verification use the same provider

## Extension Points

The domain model is designed for extension:

- **Custom Check Types**: Add to `CheckType` enum, implement in provider
- **Custom Metadata**: All entities support arbitrary key-value metadata
- **Additional Document Types**: Extend `DocumentType` enum
- **Custom Validation**: Domain services can be replaced via DI

## Related Documents

- [ARCHITECTURE.md](./ARCHITECTURE.md) - System architecture
- [DATABASE_DESIGN.md](./DATABASE_DESIGN.md) - How domain maps to database
- [PROVIDER_INTEGRATION.md](./PROVIDER_INTEGRATION.md) - Provider abstraction
