namespace Kyc.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for Applicant entities.
/// </summary>
public readonly record struct ApplicantId(Guid Value)
{
    public static ApplicantId New() => new(Guid.NewGuid());
    public static ApplicantId Empty => new(Guid.Empty);

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(ApplicantId id) => id.Value;
    public static explicit operator ApplicantId(Guid value) => new(value);
}
