using System.Text.RegularExpressions;

namespace Kyc.Domain.ValueObjects;

/// <summary>
/// Value object representing a validated email address.
/// </summary>
public sealed partial class Email : IEquatable<Email>
{
    public string Value { get; }

    private Email(string value)
    {
        Value = value;
    }

    public static Email Create(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be empty.", nameof(email));

        var normalized = email.Trim().ToLowerInvariant();

        if (!EmailRegex().IsMatch(normalized))
            throw new ArgumentException("Invalid email format.", nameof(email));

        return new Email(normalized);
    }

    public string GetDomain() => Value.Split('@')[1];

    public override string ToString() => Value;

    public override bool Equals(object? obj) => obj is Email other && Equals(other);

    public bool Equals(Email? other) => other is not null && Value == other.Value;

    public override int GetHashCode() => Value.GetHashCode();

    public static bool operator ==(Email? left, Email? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(Email? left, Email? right) => !(left == right);

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();
}
