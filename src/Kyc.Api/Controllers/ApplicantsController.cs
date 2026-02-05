using Kyc.Api.Models.Requests;
using Kyc.Api.Models.Responses;
using Kyc.Application.Applicants.Commands.CreateApplicant;
using Kyc.Application.Common.Mediator;
using Microsoft.AspNetCore.Mvc;

namespace Kyc.Api.Controllers;

/// <summary>
/// Controller for managing applicants.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class ApplicantsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ApplicantsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Creates a new applicant.
    /// </summary>
    /// <param name="request">The applicant creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created applicant.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ApplicantResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateApplicantRequest request,
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

        var result = await _mediator.Send(command, cancellationToken);

        var response = new ApplicantResponse(
            Id: result.Id,
            FirstName: result.FirstName,
            LastName: result.LastName,
            Email: result.Email,
            CreatedAt: result.CreatedAt);

        return CreatedAtAction(
            nameof(Create),
            new { id = response.Id },
            ApiResponse<ApplicantResponse>.Success(response));
    }
}
