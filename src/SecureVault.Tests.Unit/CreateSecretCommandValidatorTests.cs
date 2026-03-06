using FluentAssertions;
using SecureVault.Application.Secrets.CreateSecret;
using SecureVault.Domain.ValueObjects;
using Xunit;

namespace SecureVault.Tests.Unit;

public class CreateSecretCommandValidatorTests
{
    private readonly CreateSecretCommandValidator _sut = new();

    [Fact]
    public void Should_pass_when_plaintext_valid_and_short()
    {
        var command = new CreateSecretCommand("hello", ExpiryType.OneHour, null);
        var result = _sut.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Should_fail_when_plaintext_empty_or_whitespace(string? plaintext)
    {
        var command = new CreateSecretCommand(plaintext!, ExpiryType.OneHour, null);
        var result = _sut.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateSecretCommand.Plaintext));
        result.Errors.Should().Contain(e => e.ErrorCode == CreateSecretCommandValidator.CodeSecretEmpty);
    }

    [Fact]
    public void Should_fail_when_plaintext_exceeds_max_length_with_code()
    {
        var command = new CreateSecretCommand(new string('x', CreateSecretCommandValidator.MaxPlaintextLength + 1), ExpiryType.OneHour, null);
        var result = _sut.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateSecretCommand.Plaintext) && e.ErrorCode == CreateSecretCommandValidator.CodeSecretTooLong);
    }

    [Fact]
    public void Should_pass_when_plaintext_exactly_1000_chars()
    {
        var command = new CreateSecretCommand(new string('a', CreateSecretCommandValidator.MaxPlaintextLength), ExpiryType.OneHour, null);
        var result = _sut.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Should_pass_for_all_expiry_types()
    {
        foreach (ExpiryType expiryType in Enum.GetValues<ExpiryType>())
        {
            var command = new CreateSecretCommand("secret", expiryType, null);
            var result = _sut.Validate(command);
            result.IsValid.Should().BeTrue($"ExpiryType {expiryType} should be valid");
        }
    }

    [Fact]
    public void Should_fail_when_password_exceeds_max_length()
    {
        var command = new CreateSecretCommand("secret", ExpiryType.OneHour, new string('x', 501));
        var result = _sut.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateSecretCommand.Password));
    }

    [Fact]
    public void Should_pass_when_password_within_max_length()
    {
        var command = new CreateSecretCommand("secret", ExpiryType.OneHour, new string('x', 500));
        var result = _sut.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Should_fail_when_plaintext_is_null_byte_only()
    {
        var command = new CreateSecretCommand("\0", ExpiryType.OneHour, null);
        var result = _sut.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateSecretCommand.Plaintext));
        result.Errors.Should().Contain(e => e.ErrorCode == CreateSecretCommandValidator.CodeSecretEmpty);
    }

    [Theory]
    [InlineData("\u0001\u0002")]
    [InlineData("\t\u0000\n")]
    public void Should_fail_when_plaintext_contains_only_control_characters(string plaintext)
    {
        var command = new CreateSecretCommand(plaintext, ExpiryType.OneHour, null);
        var result = _sut.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateSecretCommand.Plaintext) && e.ErrorCode == CreateSecretCommandValidator.CodeSecretEmpty);
    }

    [Theory]
    [InlineData("café")]
    [InlineData("naïve")]
    [InlineData("日本語")]
    [InlineData("Hello \u00E4")] // a-umlaut
    [InlineData("emoji \uD83D\uDE00")] // grinning face
    public void Should_pass_when_plaintext_is_valid_UTF8_non_ASCII(string plaintext)
    {
        var command = new CreateSecretCommand(plaintext, ExpiryType.OneHour, null);
        var result = _sut.Validate(command);
        result.IsValid.Should().BeTrue($"valid UTF-8 plaintext should pass: {plaintext}");
    }

    [Fact]
    public void Should_fail_when_ExpiryType_is_invalid_enum_value()
    {
        var command = new CreateSecretCommand("secret", (ExpiryType)99, null);
        var result = _sut.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateSecretCommand.ExpiryType));
    }
}
