using FluentAssertions;
using Microsoft.Extensions.Options;
using SecureVault.Infrastructure.Config;
using Xunit;

namespace SecureVault.Tests.Unit;

public sealed class RevealSecurityConfigTests
{
    [Fact]
    public void Constructor_uses_delay_when_in_valid_range()
    {
        var options = Options.Create(new RevealSecurityOptions { RevealDecryptionFailureDelayMs = 250 });
        var sut = new RevealSecurityConfig(options);

        sut.DecryptionFailureDelay.Should().Be(TimeSpan.FromMilliseconds(250));
    }

    [Fact]
    public void Constructor_uses_default_100ms_when_value_is_null()
    {
        var options = Options.Create(new RevealSecurityOptions { RevealDecryptionFailureDelayMs = null });
        var sut = new RevealSecurityConfig(options);

        sut.DecryptionFailureDelay.Should().Be(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void Constructor_uses_default_100ms_when_value_is_zero()
    {
        var options = Options.Create(new RevealSecurityOptions { RevealDecryptionFailureDelayMs = 0 });
        var sut = new RevealSecurityConfig(options);

        sut.DecryptionFailureDelay.Should().Be(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void Constructor_uses_default_100ms_when_value_is_negative()
    {
        var options = Options.Create(new RevealSecurityOptions { RevealDecryptionFailureDelayMs = -1 });
        var sut = new RevealSecurityConfig(options);

        sut.DecryptionFailureDelay.Should().Be(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void Constructor_uses_default_100ms_when_value_exceeds_5000()
    {
        var options = Options.Create(new RevealSecurityOptions { RevealDecryptionFailureDelayMs = 5001 });
        var sut = new RevealSecurityConfig(options);

        sut.DecryptionFailureDelay.Should().Be(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void Constructor_uses_value_at_upper_boundary_5000()
    {
        var options = Options.Create(new RevealSecurityOptions { RevealDecryptionFailureDelayMs = 5000 });
        var sut = new RevealSecurityConfig(options);

        sut.DecryptionFailureDelay.Should().Be(TimeSpan.FromMilliseconds(5000));
    }

    [Fact]
    public void Constructor_uses_value_at_lower_boundary_1()
    {
        var options = Options.Create(new RevealSecurityOptions { RevealDecryptionFailureDelayMs = 1 });
        var sut = new RevealSecurityConfig(options);

        sut.DecryptionFailureDelay.Should().Be(TimeSpan.FromMilliseconds(1));
    }
}
