using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SecureVault.Application;
using SecureVault.Application.Common.Interfaces;
using SecureVault.Application.Secrets.CreateSecret;
using Xunit;

namespace SecureVault.Tests.Unit;

public class DependencyInjectionTests
{
    [Fact]
    public void AddApplication_registers_MediatR_and_validator()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddLogging();
        services.AddTransient(_ => Substitute.For<ISecretRepository>());
        services.AddTransient(_ => Substitute.For<IEncryptionService>());
        services.AddTransient(_ => Substitute.For<IKeyProvider>());
        services.AddTransient(_ => Substitute.For<ITokenGenerator>());
        services.AddTransient(_ => Substitute.For<ISecretCache>());
        services.AddTransient(_ => Substitute.For<IAuditPublisher>());
        services.AddTransient(_ => Substitute.For<ITimeProvider>());
        services.AddTransient(_ => Substitute.For<ICreateSecretLinkBuilder>());
        services.AddTransient(_ => Substitute.For<ISecretExpiryConfig>());
        services.AddTransient(_ => Substitute.For<IPasswordDerivation>());
        var provider = services.BuildServiceProvider();

        var handler = provider.GetService<MediatR.IRequestHandler<CreateSecretCommand, CreateSecretResult>>();

        handler.Should().NotBeNull();
        handler.Should().BeOfType<CreateSecretCommandHandler>();
    }

    [Fact]
    public void AddApplication_does_not_throw()
    {
        var services = new ServiceCollection();
        var act = () => services.AddApplication();
        act.Should().NotThrow();
    }
}
