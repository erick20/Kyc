using Kyc.Api.Models.Requests;
using Kyc.Api.Models.Responses;
using Kyc.Application.Applicants.Commands.CreateApplicant;
using Kyc.Application.Common.Mediator;

namespace Kyc.Api.Endpoints;

public static class ApplicantEndpoints
{
    public static IEndpointRouteBuilder MapApplicantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/v1/applicants")
            .WithTags("Applicants");

        group.MapPost("", CreateApplicant)
            .WithName("CreateApplicant")
            .Produces<ApiResponse<ApplicantResponse>>(StatusCodes.Status201Created)
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> CreateApplicant(
        CreateApplicantRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new CreateApplicantCommand(
            FirstName: request.FirstName,
            LastName: request.LastName,
            Email: request.Email,
            MiddleName: request.MiddleName,
            PhoneNumber: request.PhoneNumber,
            DateOfBirth: request.DateOfBirth,
            Nationality: request.Nationality,
            Address: request.Address is not null
                ? new CreateApplicantAddressDto(
                    request.Address.Line1,
                    request.Address.Line2,
                    request.Address.City,
                    request.Address.State,
                    request.Address.PostalCode,
                    request.Address.Country)
                : null,
            ExternalReference: request.ExternalReference,
            Metadata: request.Metadata);

        var result = await mediator.Send(command, cancellationToken);

        var response = new ApplicantResponse(
            Id: result.Id,
            FirstName: result.FirstName,
            LastName: result.LastName,
            Email: result.Email,
            CreatedAt: result.CreatedAt);

        return Results.Created(
            $"/api/v1/applicants/{response.Id}",
            ApiResponse<ApplicantResponse>.Success(response));
    }
}
