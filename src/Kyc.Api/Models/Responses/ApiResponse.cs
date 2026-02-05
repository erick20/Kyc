namespace Kyc.Api.Models.Responses;

/// <summary>
/// Standard API response wrapper.
/// </summary>
public sealed record ApiResponse<T>
{
    public bool IsSuccess { get; init; }
    public T? Data { get; init; }
    public ApiError? Error { get; init; }
    public ApiMeta Meta { get; init; } = new();

    public static ApiResponse<T> Success(T data) => new()
    {
        IsSuccess = true,
        Data = data
    };

    public static ApiResponse<T> Failure(string code, string message, IDictionary<string, string[]>? details = null) => new()
    {
        IsSuccess = false,
        Error = new ApiError(code, message, details)
    };
}

/// <summary>
/// API error details.
/// </summary>
public sealed record ApiError(
    string Code,
    string Message,
    IDictionary<string, string[]>? Details = null
);

/// <summary>
/// API response metadata.
/// </summary>
public sealed record ApiMeta
{
    public string RequestId { get; init; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
