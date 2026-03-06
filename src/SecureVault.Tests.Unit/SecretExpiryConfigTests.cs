using FluentAssertions;
using Microsoft.Extensions.Options;
using SecureVault.Infrastructure.Config;
using Xunit;

namespace SecureVault.Tests.Unit;

public sealed class SecretExpiryConfigTests
{
    [Fact]
    public void Constructor_sets_OverrideTtlForTests_when_in_valid_range()
    {
        var options = Options.Create(new SecretExpiryOptions { OverrideTtlSeconds = 60 });
        var sut = new SecretExpiryConfig(options);

        sut.OverrideTtlForTests.Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void Constructor_returns_null_when_OverrideTtlSeconds_is_null()
    {
        var options = Options.Create(new SecretExpiryOptions { OverrideTtlSeconds = null });
        var sut = new SecretExpiryConfig(options);

        sut.OverrideTtlForTests.Should().BeNull();
    }

    [Fact]
    public void Constructor_returns_null_when_OverrideTtlSeconds_is_zero()
    {
        var options = Options.Create(new SecretExpiryOptions { OverrideTtlSeconds = 0 });
        var sut = new SecretExpiryConfig(options);

        sut.OverrideTtlForTests.Should().BeNull();
    }

    [Fact]
    public void Constructor_returns_null_when_OverrideTtlSeconds_equals_or_exceeds_86400()
    {
        var options = Options.Create(new SecretExpiryOptions { OverrideTtlSeconds = 86400 });
        var sut = new SecretExpiryConfig(options);

        sut.OverrideTtlForTests.Should().BeNull();
    }

    [Fact]
    public void Constructor_returns_null_when_OverrideTtlSeconds_exceeds_86400()
    {
        var options = Options.Create(new SecretExpiryOptions { OverrideTtlSeconds = 86401 });
        var sut = new SecretExpiryConfig(options);

        sut.OverrideTtlForTests.Should().BeNull();
    }

    [Fact]
    public void Constructor_sets_OverrideTtlForTests_at_upper_boundary_86399()
    {
        var options = Options.Create(new SecretExpiryOptions { OverrideTtlSeconds = 86399 });
        var sut = new SecretExpiryConfig(options);

        sut.OverrideTtlForTests.Should().Be(TimeSpan.FromSeconds(86399));
    }

    [Fact]
    public void Constructor_sets_OverrideTtlForTests_at_lower_boundary_1()
    {
        var options = Options.Create(new SecretExpiryOptions { OverrideTtlSeconds = 1 });
        var sut = new SecretExpiryConfig(options);

        sut.OverrideTtlForTests.Should().Be(TimeSpan.FromSeconds(1));
    }
}
