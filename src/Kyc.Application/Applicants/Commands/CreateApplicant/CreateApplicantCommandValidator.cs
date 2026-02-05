using FluentValidation;

namespace Kyc.Application.Applicants.Commands.CreateApplicant;

/// <summary>
/// Validator for CreateApplicantCommand.
/// </summary>
public sealed class CreateApplicantCommandValidator : AbstractValidator<CreateApplicantCommand>
{
    public CreateApplicantCommandValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100).WithMessage("First name must not exceed 100 characters.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100).WithMessage("Last name must not exceed 100 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.MiddleName)
            .MaximumLength(100).WithMessage("Middle name must not exceed 100 characters.")
            .When(x => x.MiddleName is not null);

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(50).WithMessage("Phone number must not exceed 50 characters.")
            .Matches(@"^\+?[1-9]\d{1,14}$").WithMessage("Phone number must be in E.164 format.")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));

        RuleFor(x => x.DateOfBirth)
            .LessThan(DateOnly.FromDateTime(DateTime.Today))
            .WithMessage("Date of birth must be in the past.")
            .When(x => x.DateOfBirth.HasValue);

        RuleFor(x => x.Nationality)
            .Length(2).WithMessage("Nationality must be a 2-letter ISO country code.")
            .Matches(@"^[A-Z]{2}$").WithMessage("Nationality must be uppercase letters only.")
            .When(x => !string.IsNullOrWhiteSpace(x.Nationality));

        RuleFor(x => x.ExternalReference)
            .MaximumLength(255).WithMessage("External reference must not exceed 255 characters.")
            .When(x => x.ExternalReference is not null);

        When(x => x.Address is not null, () =>
        {
            RuleFor(x => x.Address!.Line1)
                .NotEmpty().WithMessage("Address line 1 is required.")
                .MaximumLength(255).WithMessage("Address line 1 must not exceed 255 characters.");

            RuleFor(x => x.Address!.Line2)
                .MaximumLength(255).WithMessage("Address line 2 must not exceed 255 characters.")
                .When(x => x.Address!.Line2 is not null);

            RuleFor(x => x.Address!.City)
                .NotEmpty().WithMessage("City is required.")
                .MaximumLength(100).WithMessage("City must not exceed 100 characters.");

            RuleFor(x => x.Address!.State)
                .MaximumLength(100).WithMessage("State must not exceed 100 characters.")
                .When(x => x.Address!.State is not null);

            RuleFor(x => x.Address!.PostalCode)
                .NotEmpty().WithMessage("Postal code is required.")
                .MaximumLength(20).WithMessage("Postal code must not exceed 20 characters.");

            RuleFor(x => x.Address!.Country)
                .NotEmpty().WithMessage("Country is required.")
                .Length(2).WithMessage("Country must be a 2-letter ISO country code.")
                .Matches(@"^[A-Z]{2}$").WithMessage("Country must be uppercase letters only.");
        });
    }
}
