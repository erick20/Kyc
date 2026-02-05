using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Kyc.Architecture.Tests;

public class ArchitectureTests
{
    private const string DomainNamespace = "Kyc.Domain";
    private const string ApplicationNamespace = "Kyc.Application";
    private const string InfrastructureNamespace = "Kyc.Infrastructure";

    [Fact]
    public void Domain_ShouldNotHaveDependencyOnApplication()
    {
        // Arrange
        var assembly = typeof(Domain.Entities.Applicant).Assembly;

        // Act
        var result = Types
            .InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn(ApplicationNamespace)
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Domain_ShouldNotHaveDependencyOnInfrastructure()
    {
        // Arrange
        var assembly = typeof(Domain.Entities.Applicant).Assembly;

        // Act
        var result = Types
            .InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Application_ShouldNotHaveDependencyOnInfrastructure()
    {
        // Arrange
        var assembly = typeof(Application.DependencyInjection).Assembly;

        // Act
        var result = Types
            .InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Domain_ShouldNotHaveDependencyOnEntityFramework()
    {
        // Arrange
        var assembly = typeof(Domain.Entities.Applicant).Assembly;

        // Act
        var result = Types
            .InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue();
    }
}
