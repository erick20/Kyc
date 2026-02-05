# Security

This document describes the security architecture, practices, and considerations for the KYC Aggregator system. It covers secrets management, PII protection, compliance requirements, and security controls.

## Security Principles

1. **Defense in Depth**: Multiple layers of security controls
2. **Least Privilege**: Minimal access rights by default
3. **Zero Trust**: Verify explicitly, assume breach
4. **Privacy by Design**: Data protection built into architecture
5. **Audit Everything**: Complete trail of security-relevant actions

## Threat Model Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                            THREAT LANDSCAPE                                 │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────────────────┐ │
│  │   EXTERNAL      │  │   INTERNAL      │  │      DATA-IN-TRANSIT        │ │
│  │                 │  │                 │  │                             │ │
│  │  API abuse      │  │  Insider threat │  │  Man-in-middle              │ │
│  │  Credential     │  │  Misconfig      │  │  Eavesdropping              │ │
│  │  theft          │  │  Privilege      │  │  Replay attacks             │ │
│  │  DDoS           │  │  escalation     │  │                             │ │
│  └─────────────────┘  └─────────────────┘  └─────────────────────────────┘ │
│                                                                             │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────────────────┐ │
│  │   DATA-AT-REST  │  │   APPLICATION   │  │      PROVIDER COMMS         │ │
│  │                 │  │                 │  │                             │ │
│  │  Database       │  │  Injection      │  │  API key exposure           │ │
│  │  breach         │  │  IDOR           │  │  Webhook forgery            │ │
│  │  Backup leak    │  │  Auth bypass    │  │  Provider impersonation     │ │
│  └─────────────────┘  └─────────────────┘  └─────────────────────────────┘ │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Secrets Management

### Types of Secrets

| Secret Type | Storage | Rotation | Access |
|-------------|---------|----------|--------|
| Provider API keys | Vault | 90 days | Application only |
| Provider webhook secrets | Vault | 90 days | Application only |
| Database credentials | Vault | 30 days | Application only |
| API client keys | Database (hashed) | Per client | Clients |
| Encryption keys | HSM/Vault | Annual | Application only |

### Secret Storage Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                            SECRET MANAGEMENT                                │
│                                                                             │
│   ┌─────────────────────────────────────────────────────────────────────┐  │
│   │                    Azure Key Vault / AWS Secrets Manager            │  │
│   │                         / HashiCorp Vault                           │  │
│   └─────────────────────────────────────────────────────────────────────┘  │
│                                     │                                       │
│                    ┌────────────────┼────────────────┐                     │
│                    │                │                │                     │
│                    ▼                ▼                ▼                     │
│           ┌──────────────┐  ┌──────────────┐  ┌──────────────┐            │
│           │   Provider   │  │   Database   │  │  Encryption  │            │
│           │   Secrets    │  │ Credentials  │  │    Keys      │            │
│           │              │  │              │  │              │            │
│           │ - Onfido     │  │ - Host       │  │ - PII key    │            │
│           │ - Jumio      │  │ - Username   │  │ - Doc key    │            │
│           │ - Veriff     │  │ - Password   │  │ - HMAC key   │            │
│           └──────────────┘  └──────────────┘  └──────────────┘            │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Configuration Example

```csharp
// appsettings.json - references only, not actual secrets
{
  "KeyVault": {
    "Uri": "https://mykyc-vault.vault.azure.net/"
  },
  "Secrets": {
    "DatabaseConnection": "kyc-db-connection",
    "OnfidoApiKey": "kyc-onfido-api-key",
    "OnfidoWebhookSecret": "kyc-onfido-webhook-secret",
    "EncryptionKey": "kyc-encryption-key"
  }
}

// Program.cs
builder.Configuration.AddAzureKeyVault(
    new Uri(builder.Configuration["KeyVault:Uri"]),
    new DefaultAzureCredential());
```

### Local Development

```csharp
// Use user secrets for local development
// dotnet user-secrets set "Onfido:ApiKey" "test_key_xxx"

builder.Configuration.AddUserSecrets<Program>();

// Or environment variables
// export ONFIDO__APIKEY=test_key_xxx
```

## PII Protection

### Data Classification

| Classification | Examples | Protection Level |
|---------------|----------|------------------|
| **Critical** | SSN, passport number, biometrics | Encrypted at rest, masked in logs, strict access |
| **Sensitive** | Name, DOB, address, email | Encrypted at rest, limited access |
| **Internal** | Verification IDs, timestamps | Standard protection |
| **Public** | Provider names, check types | No special protection |

### Encryption at Rest

```csharp
// Column-level encryption for PII
public class ApplicantConfiguration : IEntityTypeConfiguration<Applicant>
{
    public void Configure(EntityTypeBuilder<Applicant> builder)
    {
        // Encrypt sensitive fields
        builder.Property(x => x.DateOfBirth)
            .HasConversion(
                v => _encryptor.Encrypt(v.ToString()),
                v => DateOnly.Parse(_encryptor.Decrypt(v)));
        
        builder.Property(x => x.SocialSecurityNumber)
            .HasConversion(
                v => _encryptor.Encrypt(v),
                v => _encryptor.Decrypt(v));
    }
}
```

### Database Encryption

```sql
-- PostgreSQL Transparent Data Encryption (TDE)
-- Or use column encryption with pgcrypto

-- Encrypted column example
ALTER TABLE kyc.applicants 
ADD COLUMN date_of_birth_encrypted BYTEA;

-- Application decrypts using key from vault
```

### PII in Logs

```csharp
// Structured logging with PII masking
public static class LoggingExtensions
{
    public static string MaskPii(this string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        if (value.Length <= 4) return "****";
        return value[..2] + new string('*', value.Length - 4) + value[^2..];
    }
    
    public static string MaskEmail(this string email)
    {
        if (string.IsNullOrEmpty(email)) return email;
        var parts = email.Split('@');
        if (parts.Length != 2) return "***@***.***";
        return $"{parts[0][..1]}***@{parts[1]}";
    }
}

// Usage
_logger.LogInformation(
    "Verification started for applicant {ApplicantId}, email: {Email}",
    verification.ApplicantId,
    applicant.Email.Value.MaskEmail());
```

### Serilog Destructuring

```csharp
// Configure Serilog to mask PII automatically
.Destructure.ByTransforming<Applicant>(a => new
{
    a.Id,
    Email = a.Email.Value.MaskEmail(),
    Name = $"{a.FirstName.Value[..1]}*** {a.LastName.Value[..1]}***",
    // Omit sensitive fields entirely
})
```

## Authentication & Authorization

### API Authentication

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         AUTHENTICATION FLOW                                 │
│                                                                             │
│   ┌──────────┐     ┌──────────────┐     ┌──────────────┐                   │
│   │  Client  │────▶│ API Gateway  │────▶│  Auth Check  │                   │
│   │          │     │              │     │              │                   │
│   └──────────┘     └──────────────┘     └──────┬───────┘                   │
│        │                                       │                            │
│        │ Authorization: Bearer {api_key}       │                            │
│        │                                       ▼                            │
│        │                              ┌──────────────┐                     │
│        │                              │  Key Store   │                     │
│        │                              │  (Hashed)    │                     │
│        │                              └──────────────┘                     │
│        │                                       │                            │
│        │                                       ▼                            │
│        │                              ┌──────────────┐                     │
│        │                              │  Rate Limit  │                     │
│        │                              │  Check       │                     │
│        │                              └──────────────┘                     │
│        │                                       │                            │
│        └───────────────────────────────────────┘                            │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### API Key Structure

```csharp
public class ApiKey
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string KeyHash { get; set; }        // SHA-256 hash
    public string KeyPrefix { get; set; }       // First 8 chars for identification
    public ApiKeyScope Scope { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public int RateLimitPerMinute { get; set; }
}

public enum ApiKeyScope
{
    ReadOnly,
    FullAccess,
    WebhookOnly,
    Admin
}
```

### Authorization Policies

```csharp
// Define policies
services.AddAuthorization(options =>
{
    options.AddPolicy("ReadApplicants", policy =>
        policy.RequireClaim("scope", "applicants:read", "applicants:write", "admin"));
    
    options.AddPolicy("WriteApplicants", policy =>
        policy.RequireClaim("scope", "applicants:write", "admin"));
    
    options.AddPolicy("StartVerification", policy =>
        policy.RequireClaim("scope", "verifications:write", "admin"));
    
    options.AddPolicy("Admin", policy =>
        policy.RequireClaim("scope", "admin"));
});

// Apply to endpoints
[Authorize(Policy = "WriteApplicants")]
public async Task<IActionResult> CreateApplicant(...) { }
```

## Webhook Security

### Signature Verification

```csharp
public class WebhookSecurityMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/webhooks"))
        {
            await _next(context);
            return;
        }
        
        // Read body for signature verification
        context.Request.EnableBuffering();
        var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
        context.Request.Body.Position = 0;
        
        var signature = context.Request.Headers["X-Webhook-Signature"].FirstOrDefault();
        var timestamp = context.Request.Headers["X-Webhook-Timestamp"].FirstOrDefault();
        
        // Verify timestamp (prevent replay attacks)
        if (!VerifyTimestamp(timestamp, TimeSpan.FromMinutes(5)))
        {
            context.Response.StatusCode = 401;
            return;
        }
        
        // Store for later verification by provider
        context.Items["WebhookPayload"] = body;
        context.Items["WebhookSignature"] = signature;
        context.Items["WebhookTimestamp"] = timestamp;
        
        await _next(context);
    }
}
```

### Provider-Specific Verification

```csharp
// Each provider implements its own signature format
public class OnfidoProvider : IKycProvider
{
    public bool ValidateWebhookSignature(string payload, string signature)
    {
        // Onfido uses HMAC-SHA256
        var expectedSignature = ComputeHmacSha256(payload, _config.WebhookSecret);
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(signature),
            Convert.FromHexString(expectedSignature));
    }
}
```

## Transport Security

### TLS Configuration

```csharp
// Enforce TLS 1.2+
builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureHttpsDefaults(https =>
    {
        https.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
        https.ClientCertificateMode = ClientCertificateMode.NoCertificate;
    });
});

// For provider HTTP clients
services.AddHttpClient<OnfidoApiClient>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
    });
```

### Certificate Pinning (Optional)

```csharp
// Pin provider certificates for high-security deployments
services.AddHttpClient<OnfidoApiClient>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
        {
            var expectedThumbprint = _config.OnfidoCertificateThumbprint;
            return cert?.GetCertHashString() == expectedThumbprint;
        }
    });
```

## Input Validation

### Request Validation

```csharp
public class CreateApplicantRequestValidator 
    : AbstractValidator<CreateApplicantRequest>
{
    public CreateApplicantRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(255)
            .EmailAddress()
            .Must(NotContainMaliciousCharacters);
        
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MaximumLength(100)
            .Matches(@"^[\p{L}\s\-']+$")
            .WithMessage("Name contains invalid characters");
        
        RuleFor(x => x.PhoneNumber)
            .Matches(@"^\+?[1-9]\d{1,14}$")
            .When(x => !string.IsNullOrEmpty(x.PhoneNumber));
    }
    
    private bool NotContainMaliciousCharacters(string value)
    {
        if (string.IsNullOrEmpty(value)) return true;
        
        // Check for script injection, SQL injection patterns
        var suspicious = new[] { "<script", "javascript:", "DROP TABLE", "--", "/*" };
        return !suspicious.Any(s => 
            value.Contains(s, StringComparison.OrdinalIgnoreCase));
    }
}
```

### File Upload Validation

```csharp
public class DocumentUploadValidator : AbstractValidator<DocumentUploadRequest>
{
    private readonly string[] _allowedTypes = { "image/jpeg", "image/png", "application/pdf" };
    private readonly long _maxFileSize = 10 * 1024 * 1024; // 10MB
    
    public DocumentUploadValidator()
    {
        RuleFor(x => x.File)
            .Must(f => f.Length <= _maxFileSize)
            .WithMessage($"File size must not exceed {_maxFileSize / 1024 / 1024}MB")
            .Must(f => _allowedTypes.Contains(f.ContentType))
            .WithMessage("Only JPEG, PNG, and PDF files are allowed")
            .Must(BeValidFileContent)
            .WithMessage("File content does not match declared type");
    }
    
    private bool BeValidFileContent(IFormFile file)
    {
        // Verify magic bytes match content type
        using var reader = new BinaryReader(file.OpenReadStream());
        var headerBytes = reader.ReadBytes(8);
        
        return file.ContentType switch
        {
            "image/jpeg" => headerBytes[0] == 0xFF && headerBytes[1] == 0xD8,
            "image/png" => headerBytes.Take(4).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47 }),
            "application/pdf" => Encoding.ASCII.GetString(headerBytes.Take(4).ToArray()) == "%PDF",
            _ => false
        };
    }
}
```

## Rate Limiting

```csharp
// Configure rate limiting
services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var apiKey = context.Request.Headers["Authorization"].FirstOrDefault();
        
        return RateLimitPartition.GetTokenBucketLimiter(
            apiKey ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 100,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                TokensPerPeriod = 100,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 10
            });
    });
    
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            error = "Too many requests",
            retryAfter = 60
        }, token);
    };
});
```

## Audit Logging

### Security Events

```csharp
public enum SecurityEventType
{
    AuthenticationSuccess,
    AuthenticationFailure,
    AuthorizationFailure,
    ApiKeyCreated,
    ApiKeyRevoked,
    SensitiveDataAccessed,
    WebhookReceived,
    WebhookVerificationFailed,
    RateLimitExceeded,
    SuspiciousActivity
}

public class SecurityAuditService
{
    public async Task LogSecurityEventAsync(SecurityEvent securityEvent)
    {
        await _auditRepository.InsertAsync(new AuditLog
        {
            EntityType = "SecurityEvent",
            EntityId = Guid.NewGuid(),
            Action = securityEvent.EventType.ToString(),
            ActorType = securityEvent.ActorType,
            ActorId = securityEvent.ActorId,
            IpAddress = securityEvent.IpAddress,
            UserAgent = securityEvent.UserAgent,
            CorrelationId = securityEvent.CorrelationId,
            OccurredAt = DateTime.UtcNow,
            NewValues = JsonSerializer.SerializeToDocument(new
            {
                securityEvent.Details,
                securityEvent.Severity
            })
        });
        
        // Alert on critical events
        if (securityEvent.Severity == SecuritySeverity.Critical)
        {
            await _alertService.SendAlertAsync(securityEvent);
        }
    }
}
```

## Compliance Considerations

### GDPR

| Requirement | Implementation |
|-------------|----------------|
| Right to access | Export endpoint with all applicant data |
| Right to erasure | Soft delete + scheduled hard delete |
| Data minimization | Only collect necessary fields |
| Purpose limitation | Clear data processing purposes documented |
| Data portability | JSON export of applicant data |
| Breach notification | Incident response procedures |

### Data Retention

```csharp
public class DataRetentionService
{
    public async Task ExecuteRetentionPolicyAsync()
    {
        // Delete verification documents after 90 days
        await DeleteExpiredDocumentsAsync(TimeSpan.FromDays(90));
        
        // Anonymize completed verifications after 7 years
        await AnonymizeOldVerificationsAsync(TimeSpan.FromDays(365 * 7));
        
        // Hard delete soft-deleted records after 30 days
        await PurgeSoftDeletedRecordsAsync(TimeSpan.FromDays(30));
    }
    
    private async Task AnonymizeOldVerificationsAsync(TimeSpan age)
    {
        var cutoff = DateTime.UtcNow - age;
        
        await _dbContext.Applicants
            .Where(a => a.CreatedAt < cutoff)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.FirstName, "ANONYMIZED")
                .SetProperty(a => a.LastName, "ANONYMIZED")
                .SetProperty(a => a.Email, "anonymized@deleted.local")
                .SetProperty(a => a.DateOfBirth, (DateOnly?)null)
                .SetProperty(a => a.Address, (Address?)null));
    }
}
```

### Data Subject Rights

```csharp
[ApiController]
[Route("api/v1/data-subjects")]
public class DataSubjectController : ControllerBase
{
    // GDPR Article 15 - Right of access
    [HttpGet("{email}/export")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> ExportData(string email)
    {
        var data = await _dataExportService.ExportApplicantDataAsync(email);
        return File(data, "application/json", $"data-export-{DateTime.UtcNow:yyyyMMdd}.json");
    }
    
    // GDPR Article 17 - Right to erasure
    [HttpDelete("{email}")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> DeleteData(string email)
    {
        await _dataExportService.InitiateDeletionAsync(email);
        return Accepted();
    }
}
```

## Security Checklist

### Development

- [ ] No secrets in source code or config files
- [ ] Input validation on all endpoints
- [ ] Parameterized queries (EF Core handles this)
- [ ] Output encoding for any rendered content
- [ ] Secure random number generation
- [ ] Proper error handling (no stack traces in production)

### Deployment

- [ ] TLS 1.2+ enforced
- [ ] Security headers configured (HSTS, CSP, etc.)
- [ ] Rate limiting enabled
- [ ] API keys rotated regularly
- [ ] Logs shipped to secure storage
- [ ] Secrets in vault, not environment variables

### Monitoring

- [ ] Failed authentication attempts tracked
- [ ] Unusual access patterns alerted
- [ ] Error rates monitored
- [ ] Provider API failures logged
- [ ] Webhook verification failures alerted

## Related Documents

- [ARCHITECTURE.md](./ARCHITECTURE.md) - System architecture
- [API_DESIGN.md](./API_DESIGN.md) - API authentication details
- [DATABASE_DESIGN.md](./DATABASE_DESIGN.md) - Data storage
