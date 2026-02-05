namespace Kyc.Application.Common.Exceptions;

/// <summary>
/// Exception thrown when a conflict occurs (e.g., duplicate entity).
/// </summary>
public sealed class ConflictException : Exception
{
    public ConflictException() : base()
    {
    }

    public ConflictException(string message) : base(message)
    {
    }

    public ConflictException(string name, object key)
        : base($"Entity \"{name}\" ({key}) already exists.")
    {
    }
}
