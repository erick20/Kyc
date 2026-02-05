using FluentAssertions;
using Kyc.Domain.Entities;
using Kyc.Domain.Events;
using Kyc.Domain.ValueObjects;
using Xunit;

namespace Kyc.Domain.Tests.Entities;

public class ApplicantTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateApplicant()
    {
        // Arrange
        var email = Email.Create("test@example.com");

        // Act
        var applicant = Applicant.Create(
            firstName: "John",
            lastName: "Doe",
            email: email);

        // Assert
        applicant.Should().NotBeNull();
        applicant.FirstName.Should().Be("John");
        applicant.LastName.Should().Be("Doe");
        applicant.Email.Should().Be(email);
        applicant.Id.Value.Should().NotBe(Guid.Empty);
        applicant.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Create_ShouldRaiseApplicantCreatedEvent()
    {
        // Arrange
        var email = Email.Create("test@example.com");

        // Act
        var applicant = Applicant.Create("John", "Doe", email);

        // Assert
        applicant.DomainEvents.Should().ContainSingle();
        applicant.DomainEvents.First().Should().BeOfType<ApplicantCreatedEvent>();
    }

    [Fact]
    public void Create_WithEmptyFirstName_ShouldThrow()
    {
        // Arrange
        var email = Email.Create("test@example.com");

        // Act
        var act = () => Applicant.Create("", "Doe", email);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*First name*");
    }

    [Fact]
    public void FullName_WithMiddleName_ShouldReturnFullName()
    {
        // Arrange
        var email = Email.Create("test@example.com");
        var applicant = Applicant.Create("John", "Doe", email, middleName: "William");

        // Act
        var fullName = applicant.FullName;

        // Assert
        fullName.Should().Be("John William Doe");
    }

    [Fact]
    public void Delete_ShouldSetDeletedAt()
    {
        // Arrange
        var email = Email.Create("test@example.com");
        var applicant = Applicant.Create("John", "Doe", email);

        // Act
        applicant.Delete();

        // Assert
        applicant.DeletedAt.Should().NotBeNull();
        applicant.IsDeleted.Should().BeTrue();
    }
}
