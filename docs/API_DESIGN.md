# API Design

This document describes the high-level REST API design for the KYC Aggregator service. It covers resource modeling, endpoint structure, authentication, and response formats.

## Design Principles

1. **RESTful Resources**: APIs model business resources, not operations
2. **Consistent Naming**: Plural nouns for collections, kebab-case for multi-word paths
3. **Predictable Responses**: Consistent envelope structure across all endpoints
4. **Meaningful Status Codes**: Proper HTTP semantics
5. **Versioning**: API versioning via URL path (`/api/v1/`)
6. **Idempotency**: Safe retries for non-idempotent operations

## Base URL

```
Production: https://api.kyc-aggregator.example.com/api/v1
Sandbox:    https://sandbox.kyc-aggregator.example.com/api/v1
```

## Authentication

### API Key Authentication

Primary authentication method for server-to-server integration.

```
Authorization: Bearer {api_key}
```

API keys are scoped to:
- **Environment**: Sandbox vs Production
- **Permissions**: Read-only, Full access, Admin
- **Rate Limits**: Requests per minute

### Webhook Signatures

Webhooks are signed using HMAC-SHA256:

```
X-Webhook-Signature: sha256={signature}
X-Webhook-Timestamp: {unix_timestamp}
```

## Response Envelope

All responses follow a consistent structure.

### Success Response

```json
{
  "success": true,
  "data": { /* resource or collection */ },
  "meta": {
    "requestId": "req_abc123",
    "timestamp": "2024-01-15T10:30:00Z"
  }
}
```

### Paginated Response

```json
{
  "success": true,
  "data": [ /* items */ ],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "totalItems": 150,
    "totalPages": 8,
    "hasNextPage": true,
    "hasPreviousPage": false
  },
  "meta": {
    "requestId": "req_abc123",
    "timestamp": "2024-01-15T10:30:00Z"
  }
}
```

### Error Response

```json
{
  "success": false,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "One or more validation errors occurred",
    "details": [
      {
        "field": "email",
        "code": "INVALID_FORMAT",
        "message": "Email must be a valid email address"
      }
    ]
  },
  "meta": {
    "requestId": "req_abc123",
    "timestamp": "2024-01-15T10:30:00Z"
  }
}
```

## HTTP Status Codes

| Status | Meaning | Usage |
|--------|---------|-------|
| 200 | OK | Successful GET, PUT, PATCH |
| 201 | Created | Successful POST creating a resource |
| 202 | Accepted | Request accepted for async processing |
| 204 | No Content | Successful DELETE |
| 400 | Bad Request | Validation errors, malformed request |
| 401 | Unauthorized | Missing or invalid authentication |
| 403 | Forbidden | Valid auth but insufficient permissions |
| 404 | Not Found | Resource doesn't exist |
| 409 | Conflict | Resource state conflict (e.g., duplicate) |
| 422 | Unprocessable Entity | Business rule violation |
| 429 | Too Many Requests | Rate limit exceeded |
| 500 | Internal Server Error | Unexpected server error |
| 503 | Service Unavailable | Maintenance or overload |

## API Resources

### Applicants

Manage individuals undergoing KYC verification.

#### Create Applicant

```
POST /api/v1/applicants
```

**Request:**
```json
{
  "externalReference": "user_12345",
  "firstName": "John",
  "middleName": "Robert",
  "lastName": "Smith",
  "email": "john.smith@example.com",
  "phoneNumber": "+1234567890",
  "dateOfBirth": "1990-05-15",
  "address": {
    "line1": "123 Main Street",
    "line2": "Apt 4B",
    "city": "New York",
    "state": "NY",
    "postalCode": "10001",
    "country": "US"
  },
  "nationality": "US",
  "metadata": {
    "internalId": "abc123"
  }
}
```

**Response (201 Created):**
```json
{
  "success": true,
  "data": {
    "id": "app_a1b2c3d4e5",
    "externalReference": "user_12345",
    "firstName": "John",
    "lastName": "Smith",
    "email": "john.smith@example.com",
    "status": "active",
    "createdAt": "2024-01-15T10:30:00Z"
  }
}
```

#### Get Applicant

```
GET /api/v1/applicants/{applicantId}
```

#### List Applicants

```
GET /api/v1/applicants?page=1&pageSize=20&email=john@example.com
```

**Query Parameters:**
- `page` (default: 1)
- `pageSize` (default: 20, max: 100)
- `email` - Filter by email
- `externalReference` - Filter by external reference
- `createdAfter` - ISO 8601 datetime
- `createdBefore` - ISO 8601 datetime

#### Update Applicant

```
PATCH /api/v1/applicants/{applicantId}
```

**Request:**
```json
{
  "phoneNumber": "+1987654321",
  "address": {
    "line1": "456 New Street"
  }
}
```

### Documents

Upload and manage identity documents.

#### Upload Document

```
POST /api/v1/applicants/{applicantId}/documents
Content-Type: multipart/form-data
```

**Form Fields:**
- `file` - Document file (JPEG, PNG, PDF)
- `type` - Document type (passport, drivers_license, national_id)
- `side` - Document side (front, back, single)
- `issuingCountry` - ISO 3166-1 alpha-2 code
- `expiryDate` - Document expiry date (optional)

**Response (201 Created):**
```json
{
  "success": true,
  "data": {
    "id": "doc_x1y2z3",
    "applicantId": "app_a1b2c3d4e5",
    "type": "passport",
    "side": "single",
    "fileName": "passport.jpg",
    "uploadedAt": "2024-01-15T10:35:00Z"
  }
}
```

#### List Documents

```
GET /api/v1/applicants/{applicantId}/documents
```

#### Delete Document

```
DELETE /api/v1/applicants/{applicantId}/documents/{documentId}
```

### Verifications

Manage verification processes.

#### Start Verification

```
POST /api/v1/verifications
```

**Request:**
```json
{
  "applicantId": "app_a1b2c3d4e5",
  "checks": ["document", "liveness", "face_match"],
  "providerId": "onfido",
  "redirectUrl": "https://yourapp.com/verification-complete",
  "metadata": {
    "orderId": "order_123"
  }
}
```

**Response (201 Created):**
```json
{
  "success": true,
  "data": {
    "id": "ver_m1n2o3p4",
    "applicantId": "app_a1b2c3d4e5",
    "status": "pending",
    "checks": ["document", "liveness", "face_match"],
    "providerId": "onfido",
    "sdkToken": "eyJhbGciOiJIUzI1...",
    "checkUrl": "https://onfido.com/verify/...",
    "createdAt": "2024-01-15T10:40:00Z",
    "expiresAt": "2024-01-15T11:40:00Z"
  }
}
```

#### Get Verification

```
GET /api/v1/verifications/{verificationId}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "id": "ver_m1n2o3p4",
    "applicantId": "app_a1b2c3d4e5",
    "status": "completed",
    "result": "approved",
    "checks": [
      {
        "type": "document",
        "status": "completed",
        "result": "clear",
        "completedAt": "2024-01-15T10:45:00Z"
      },
      {
        "type": "liveness",
        "status": "completed",
        "result": "clear",
        "completedAt": "2024-01-15T10:46:00Z"
      },
      {
        "type": "face_match",
        "status": "completed",
        "result": "clear",
        "completedAt": "2024-01-15T10:46:30Z"
      }
    ],
    "createdAt": "2024-01-15T10:40:00Z",
    "completedAt": "2024-01-15T10:47:00Z"
  }
}
```

#### List Verifications

```
GET /api/v1/verifications?applicantId={id}&status=completed
```

**Query Parameters:**
- `applicantId` - Filter by applicant
- `status` - Filter by status (pending, in_progress, completed, failed)
- `result` - Filter by result (approved, rejected, needs_review)
- `providerId` - Filter by provider
- `createdAfter` / `createdBefore`
- `page` / `pageSize`

#### Cancel Verification

```
POST /api/v1/verifications/{verificationId}/cancel
```

**Request:**
```json
{
  "reason": "User requested cancellation"
}
```

#### Get Verification Report

Retrieve a detailed report for a completed verification.

```
GET /api/v1/verifications/{verificationId}/report
```

**Response:**
```json
{
  "success": true,
  "data": {
    "verificationId": "ver_m1n2o3p4",
    "applicant": {
      "id": "app_a1b2c3d4e5",
      "name": "John Smith"
    },
    "result": "approved",
    "checks": [
      {
        "type": "document",
        "result": "clear",
        "breakdown": {
          "authenticity": "clear",
          "faceDetection": "clear",
          "dataConsistency": "clear",
          "imageIntegrity": "clear"
        },
        "extractedData": {
          "documentType": "passport",
          "documentNumber": "AB1234567",
          "firstName": "JOHN",
          "lastName": "SMITH",
          "dateOfBirth": "1990-05-15",
          "expiryDate": "2030-05-14",
          "nationality": "US"
        }
      }
    ],
    "generatedAt": "2024-01-15T10:50:00Z"
  }
}
```

### Providers

Query available KYC providers.

#### List Providers

```
GET /api/v1/providers
```

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "id": "onfido",
      "displayName": "Onfido",
      "supportedChecks": ["document", "liveness", "face_match", "aml"],
      "enabled": true,
      "healthStatus": "healthy"
    },
    {
      "id": "jumio",
      "displayName": "Jumio",
      "supportedChecks": ["document", "liveness", "face_match"],
      "enabled": true,
      "healthStatus": "healthy"
    }
  ]
}
```

#### Get Provider Health

```
GET /api/v1/providers/{providerId}/health
```

### Webhooks

Manage webhook subscriptions.

#### Register Webhook

```
POST /api/v1/webhooks
```

**Request:**
```json
{
  "url": "https://yourapp.com/webhooks/kyc",
  "events": [
    "verification.completed",
    "verification.failed",
    "check.completed"
  ],
  "secret": "your_webhook_secret"
}
```

#### List Webhooks

```
GET /api/v1/webhooks
```

#### Delete Webhook

```
DELETE /api/v1/webhooks/{webhookId}
```

## Webhook Events

Outgoing webhooks for real-time updates.

### Event Types

| Event | Description |
|-------|-------------|
| `verification.created` | New verification started |
| `verification.in_progress` | Provider started processing |
| `verification.completed` | All checks finished |
| `verification.failed` | Verification encountered error |
| `verification.expired` | Verification timed out |
| `check.completed` | Individual check finished |
| `applicant.created` | New applicant registered |

### Webhook Payload

```json
{
  "id": "evt_abc123",
  "type": "verification.completed",
  "createdAt": "2024-01-15T10:47:00Z",
  "data": {
    "verificationId": "ver_m1n2o3p4",
    "applicantId": "app_a1b2c3d4e5",
    "status": "completed",
    "result": "approved"
  }
}
```

### Webhook Headers

```
Content-Type: application/json
X-Webhook-Id: evt_abc123
X-Webhook-Timestamp: 1705315620
X-Webhook-Signature: sha256=abc123def456...
```

## Rate Limiting

Rate limits are applied per API key.

| Tier | Requests/Minute | Burst |
|------|-----------------|-------|
| Standard | 60 | 100 |
| Professional | 300 | 500 |
| Enterprise | 1000 | 2000 |

**Rate Limit Headers:**
```
X-RateLimit-Limit: 60
X-RateLimit-Remaining: 45
X-RateLimit-Reset: 1705315680
```

**Rate Limit Exceeded Response (429):**
```json
{
  "success": false,
  "error": {
    "code": "RATE_LIMIT_EXCEEDED",
    "message": "Too many requests. Please retry after 30 seconds.",
    "retryAfter": 30
  }
}
```

## Idempotency

For non-idempotent operations (POST), use the idempotency key:

```
Idempotency-Key: unique-request-id-12345
```

- Keys expire after 24 hours
- Duplicate requests with the same key return the original response
- Prevents duplicate resource creation on retries

## Request ID Tracking

Every request includes a correlation ID for debugging:

**Request Header (optional):**
```
X-Request-Id: your-correlation-id
```

**Response Header (always):**
```
X-Request-Id: req_abc123xyz
```

## SDK Integration

For client-side verification flows, the API provides SDK tokens.

### Web SDK Flow

1. Start verification → receive `sdkToken`
2. Initialize provider SDK with token
3. User completes verification in SDK
4. Receive webhook with results

### Mobile SDK Flow

Same as web, but token may have different permissions/expiry.

## Error Codes

| Code | Description |
|------|-------------|
| `VALIDATION_ERROR` | Request failed validation |
| `NOT_FOUND` | Resource not found |
| `DUPLICATE_RESOURCE` | Resource already exists |
| `INVALID_STATE` | Operation not valid in current state |
| `PROVIDER_ERROR` | Upstream provider error |
| `PROVIDER_UNAVAILABLE` | Provider temporarily unavailable |
| `RATE_LIMIT_EXCEEDED` | Too many requests |
| `AUTHENTICATION_FAILED` | Invalid or expired API key |
| `PERMISSION_DENIED` | Insufficient permissions |
| `INTERNAL_ERROR` | Unexpected server error |

## Versioning Strategy

- Current version: `v1`
- Version in URL path: `/api/v1/`
- Breaking changes → new version
- Non-breaking additions → same version
- Deprecation notice: 6 months minimum
- Sunset header for deprecated versions

## Related Documents

- [ARCHITECTURE.md](./ARCHITECTURE.md) - System architecture
- [SECURITY.md](./SECURITY.md) - Authentication and security
- [PROVIDER_INTEGRATION.md](./PROVIDER_INTEGRATION.md) - Provider webhooks
