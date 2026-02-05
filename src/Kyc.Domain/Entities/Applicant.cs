using Kyc.Domain.Common;
using Kyc.Domain.Events;
using Kyc.Domain.ValueObjects;

namespace Kyc.Domain.Entities;

/// <summary>
/// Represents a person undergoing KYC verification.
/// </summary>
public sealed class Applicant : Entity<ApplicantId>, IAuditableEntity, ISoftDeletable
{
    public string? ExternalReference { get; private set; }
    public string FirstName { get; private set; } = null!;
    public string? MiddleName { get; private set; }
    public string LastName { get; private set; } = null!;
    public Email Email { get; private set; } = null!;
    public string? PhoneNumber { get; private set; }
    public DateOnly? DateOfBirth { get; private set; }
    public string? Nationality { get; private set; }
    public Address? Address { get; private set; }
    public Dictionary<string, string> Metadata { get; private set; } = new();

    // Audit properties
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    /// <summary>
    /// Indicates whether the applicant has been soft deleted.
    /// </summary>
    public bool IsDeleted => DeletedAt.HasValue;

    // Private constructor for EF Core
    private Applicant() { }

    /// <summary>
    /// Creates a new applicant.
    /// </summary>
    public static Applicant Create(
        string firstName,
        string lastName,
        Email email,
        string? middleName = null,
        string? phoneNumber = null,
        DateOnly? dateOfBirth = null,
        string? nationality = null,
        Address? address = null,
        string? externalReference = null,
        Dictionary<string, string>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("First name cannot be empty.", nameof(firstName));

        if (string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("Last name cannot be empty.", nameof(lastName));

        var applicant = new Applicant
        {
            Id = ApplicantId.New(),
            FirstName = firstName.Trim(),
            MiddleName = middleName?.Trim(),
            LastName = lastName.Trim(),
            Email = email,
            PhoneNumber = phoneNumber?.Trim(),
            DateOfBirth = dateOfBirth,
            Nationality = nationality?.Trim().ToUpperInvariant(),
            Address = address,
            ExternalReference = externalReference?.Trim(),
            Metadata = metadata ?? new Dictionary<string, string>(),
            CreatedAt = DateTime.UtcNow
        };

        applicant.AddDomainEvent(new ApplicantCreatedEvent(applicant.Id, applicant.Email));

        return applicant;
    }

    /// <summary>
    /// Updates the applicant's personal information.
    /// </summary>
    public void Update(
        string? firstName = null,
        string? lastName = null,
        string? middleName = null,
        string? phoneNumber = null,
        DateOnly? dateOfBirth = null,
        string? nationality = null,
        Address? address = null)
    {
        if (firstName is not null)
        {
            if (string.IsNullOrWhiteSpace(firstName))
                throw new ArgumentException("First name cannot be empty.", nameof(firstName));
            FirstName = firstName.Trim();
        }

        if (lastName is not null)
        {
            if (string.IsNullOrWhiteSpace(lastName))
                throw new ArgumentException("Last name cannot be empty.", nameof(lastName));
            LastName = lastName.Trim();
        }

        if (middleName is not null)
            MiddleName = middleName.Trim();

        if (phoneNumber is not null)
            PhoneNumber = phoneNumber.Trim();

        if (dateOfBirth.HasValue)
            DateOfBirth = dateOfBirth;

        if (nationality is not null)
            Nationality = nationality.Trim().ToUpperInvariant();

        if (address is not null)
            Address = address;

        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Soft deletes the applicant.
    /// </summary>
    public void Delete()
    {
        if (DeletedAt.HasValue)
            return;

        DeletedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the full name of the applicant.
    /// </summary>
    public string FullName => string.IsNullOrWhiteSpace(MiddleName)
        ? $"{FirstName} {LastName}"
        : $"{FirstName} {MiddleName} {LastName}";
}
