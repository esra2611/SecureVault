using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SecureVault.Application.Common.Behaviors;
using SecureVault.Application.Secrets.CreateSecret;
using SecureVault.Domain.ValueObjects;
using Xunit;

namespace SecureVault.Tests.Unit;

public class ValidationBehaviorTests
{
    private static readonly RequestHandlerDelegate<CreateSecretResult> Next =
        () => Task.FromResult(new CreateSecretResult("http://link", "hint"));

    [Fact]
    public async Task When_no_validators_invokes_next()
    {
        var sut = new ValidationBehavior<CreateSecretCommand, CreateSecretResult>(Array.Empty<IValidator<CreateSecretCommand>>());
        var request = new CreateSecretCommand("secret", ExpiryType.OneHour, null);

        var result = await sut.Handle(request, Next, CancellationToken.None);

        result.Should().NotBeNull();
        result.ShareUrl.Should().Be("http://link");
    }

    [Fact]
    public async Task When_validators_pass_invokes_next()
    {
        var validator = Substitute.For<IValidator<CreateSecretCommand>>();
        validator.ValidateAsync(Arg.Any<ValidationContext<CreateSecretCommand>>(), Arg.Any<CancellationToken>())
            .Returns(new FluentValidation.Results.ValidationResult());
        var sut = new ValidationBehavior<CreateSecretCommand, CreateSecretResult>(new[] { validator });
        var request = new CreateSecretCommand("secret", ExpiryType.OneHour, null);

        var result = await sut.Handle(request, Next, CancellationToken.None);

        result.ShareUrl.Should().Be("http://link");
    }

    [Fact]
    public async Task When_validator_has_failures_throws_ValidationException()
    {
        var validator = Substitute.For<IValidator<CreateSecretCommand>>();
        validator.ValidateAsync(Arg.Any<ValidationContext<CreateSecretCommand>>(), Arg.Any<CancellationToken>())
            .Returns(new FluentValidation.Results.ValidationResult(new[]
            {
                new FluentValidation.Results.ValidationFailure("Plaintext", "Required")
            }));
        var sut = new ValidationBehavior<CreateSecretCommand, CreateSecretResult>(new[] { validator });
        var request = new CreateSecretCommand("", ExpiryType.OneHour, null);

        var act = () => sut.Handle(request, Next, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Errors.Any(e => e.PropertyName == "Plaintext"));
    }

    [Fact]
    public async Task When_multiple_validators_and_one_fails_throws_ValidationException_with_all_failures()
    {
        var passValidator = Substitute.For<IValidator<CreateSecretCommand>>();
        passValidator.ValidateAsync(Arg.Any<ValidationContext<CreateSecretCommand>>(), Arg.Any<CancellationToken>())
            .Returns(new FluentValidation.Results.ValidationResult());
        var failValidator = Substitute.For<IValidator<CreateSecretCommand>>();
        failValidator.ValidateAsync(Arg.Any<ValidationContext<CreateSecretCommand>>(), Arg.Any<CancellationToken>())
            .Returns(new FluentValidation.Results.ValidationResult(new[]
            {
                new FluentValidation.Results.ValidationFailure("ExpiryType", "Invalid")
            }));
        var sut = new ValidationBehavior<CreateSecretCommand, CreateSecretResult>(new[] { passValidator, failValidator });
        var request = new CreateSecretCommand("secret", ExpiryType.OneHour, null);

        var act = () => sut.Handle(request, Next, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Errors.Any(e => e.PropertyName == "ExpiryType"));
    }
}
