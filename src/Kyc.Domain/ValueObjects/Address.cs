namespace Kyc.Domain.ValueObjects;

/// <summary>
/// Value object representing a physical address.
/// </summary>
public sealed record Address
{
    public string Line1 { get; }
    public string? Line2 { get; }
    public string City { get; }
    public string? State { get; }
    public string PostalCode { get; }
    public string Country { get; }

    private Address(
        string line1,
        string? line2,
        string city,
        string? state,
        string postalCode,
        string country)
    {
        Line1 = line1;
        Line2 = line2;
        City = city;
        State = state;
        PostalCode = postalCode;
        Country = country;
    }

    public static Address Create(
        string line1,
        string? line2,
        string city,
        string? state,
        string postalCode,
        string country)
    {
        if (string.IsNullOrWhiteSpace(line1))
            throw new ArgumentException("Address line 1 cannot be empty.", nameof(line1));

        if (string.IsNullOrWhiteSpace(city))
            throw new ArgumentException("City cannot be empty.", nameof(city));

        if (string.IsNullOrWhiteSpace(postalCode))
            throw new ArgumentException("Postal code cannot be empty.", nameof(postalCode));

        if (string.IsNullOrWhiteSpace(country) || country.Length != 2)
            throw new ArgumentException("Country must be a 2-letter ISO code.", nameof(country));

        return new Address(
            line1.Trim(),
            line2?.Trim(),
            city.Trim(),
            state?.Trim(),
            postalCode.Trim(),
            country.Trim().ToUpperInvariant());
    }

    public override string ToString()
    {
        var parts = new List<string> { Line1 };
        if (!string.IsNullOrEmpty(Line2)) parts.Add(Line2);
        parts.Add(City);
        if (!string.IsNullOrEmpty(State)) parts.Add(State);
        parts.Add(PostalCode);
        parts.Add(Country);
        return string.Join(", ", parts);
    }
}
