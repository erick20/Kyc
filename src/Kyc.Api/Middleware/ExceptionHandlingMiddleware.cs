using System.Text.Json;
using Kyc.Api.Models.Responses;
using Kyc.Application.Common.Exceptions;

namespace Kyc.Api.Middleware;

/// <summary>
/// Middleware for handling exceptions and returning consistent error responses.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "An unhandled exception occurred");
            await HandleExceptionAsync(context, exception);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, response) = exception switch
        {
            ValidationException validationException => (
                StatusCodes.Status400BadRequest,
                ApiResponse<object>.Failure(
                    "VALIDATION_ERROR",
                    "One or more validation errors occurred.",
                    validationException.Errors)),

            NotFoundException notFoundException => (
                StatusCodes.Status404NotFound,
                ApiResponse<object>.Failure(
                    "NOT_FOUND",
                    notFoundException.Message)),

            ConflictException conflictException => (
                StatusCodes.Status409Conflict,
                ApiResponse<object>.Failure(
                    "CONFLICT",
                    conflictException.Message)),

            ArgumentException argumentException => (
                StatusCodes.Status400BadRequest,
                ApiResponse<object>.Failure(
                    "BAD_REQUEST",
                    argumentException.Message)),

            _ => (
                StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Failure(
                    "INTERNAL_ERROR",
                    "An unexpected error occurred."))
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }
}
