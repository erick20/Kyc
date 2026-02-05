using FluentAssertions;
using Kyc.Domain.ValueObjects;
using Xunit;

namespace Kyc.Domain.Tests.ValueObjects;

public class EmailTests
{
    [Theory]
    [InlineData("test@example.com")]
    [InlineData("user.name@domain.co.uk")]
    [InlineData("USER@EXAMPLE.COM")]
    public void Create_WithValidEmail_ShouldCreateEmail(string validEmail)
    {
        // Act
        var email = Email.Create(validEmail);

        // Assert
        email.Should().NotBeNull();
        email.Value.Should().Be(validEmail.ToLowerInvariant());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid")]
    [InlineData("@domain.com")]
    [InlineData("user@")]
    public void Create_WithInvalidEmail_ShouldThrow(string invalidEmail)
    {
        // Act
        var act = () => Email.Create(invalidEmail);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetDomain_ShouldReturnDomain()
    {
        // Arrange
        var email = Email.Create("user@example.com");

        // Act
        var domain = email.GetDomain();

        // Assert
        domain.Should().Be("example.com");
    }

    [Fact]
    public void Equals_SameEmail_ShouldBeEqual()
    {
        // Arrange
        var email1 = Email.Create("test@example.com");
        var email2 = Email.Create("TEST@EXAMPLE.COM");

        // Assert
        email1.Should().Be(email2);
    }
}
