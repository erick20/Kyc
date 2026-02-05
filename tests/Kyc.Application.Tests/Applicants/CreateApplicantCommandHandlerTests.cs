using FluentAssertions;
using Kyc.Application.Applicants.Commands.CreateApplicant;
using Kyc.Application.Common.Exceptions;
using Kyc.Domain.Entities;
using Kyc.Domain.Repositories;
using Kyc.Domain.ValueObjects;
using NSubstitute;
using Xunit;

namespace Kyc.Application.Tests.Applicants;

public class CreateApplicantCommandHandlerTests
{
    private readonly IApplicantRepository _applicantRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CreateApplicantCommandHandler _handler;

    public CreateApplicantCommandHandlerTests()
    {
        _applicantRepository = Substitute.For<IApplicantRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _handler = new CreateApplicantCommandHandler(_applicantRepository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreateApplicant()
    {
        // Arrange
        var command = new CreateApplicantCommand(
            FirstName: "John",
            LastName: "Doe",
            Email: "john.doe@example.com");

        _applicantRepository
            .GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>())
            .Returns((Applicant?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.FirstName.Should().Be("John");
        result.LastName.Should().Be("Doe");
        result.Email.Should().Be("john.doe@example.com");

        await _applicantRepository
            .Received(1)
            .AddAsync(Arg.Any<Applicant>(), Arg.Any<CancellationToken>());

        await _unitOfWork
            .Received(1)
            .SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithDuplicateEmail_ShouldThrowConflictException()
    {
        // Arrange
        var command = new CreateApplicantCommand(
            FirstName: "John",
            LastName: "Doe",
            Email: "existing@example.com");

        var existingApplicant = Applicant.Create(
            "Jane",
            "Doe",
            Email.Create("existing@example.com"));

        _applicantRepository
            .GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>())
            .Returns(existingApplicant);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
    }
}
