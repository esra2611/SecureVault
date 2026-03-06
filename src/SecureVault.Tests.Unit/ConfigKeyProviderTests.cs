using FluentAssertions;
using Microsoft.Extensions.Options;
using SecureVault.Infrastructure.Crypto;
using Xunit;

namespace SecureVault.Tests.Unit;

public sealed class ConfigKeyProviderTests
{
    private static readonly string ValidKeyBase64 = Convert.ToBase64String(new byte[32]);

    [Fact]
    public void Constructor_with_MasterKeyBase64_sets_version_1_and_GetKey_returns_key()
    {
        var options = new EncryptionOptions
        {
            MasterKeyBase64 = ValidKeyBase64,
            CurrentKeyVersion = 1
        };
        var sut = new ConfigKeyProvider(Options.Create(options));

        sut.GetCurrentVersion().Should().Be(1);
        var key = sut.GetKey(1);
        key.Should().NotBeNull().And.HaveCount(32);
    }

    [Fact]
    public void Constructor_with_Keys_dictionary_parses_versions_and_GetKey_returns_correct_key()
    {
        var key1 = new byte[32];
        var key2 = new byte[32];
        new Random(1).NextBytes(key1);
        new Random(2).NextBytes(key2);
        var options = new EncryptionOptions
        {
            Keys = new Dictionary<string, string>
            {
                ["1"] = Convert.ToBase64String(key1),
                ["2"] = Convert.ToBase64String(key2)
            },
            CurrentKeyVersion = 2
        };
        var sut = new ConfigKeyProvider(Options.Create(options));

        sut.GetCurrentVersion().Should().Be(2);
        sut.GetKey(1).Should().BeEquivalentTo(key1);
        sut.GetKey(2).Should().BeEquivalentTo(key2);
    }

    [Fact]
    public void Constructor_throws_when_key_length_is_not_32_bytes()
    {
        var options = new EncryptionOptions
        {
            Keys = new Dictionary<string, string> { ["1"] = Convert.ToBase64String(new byte[16]) },
            CurrentKeyVersion = 1
        };

        var act = () => new ConfigKeyProvider(Options.Create(options));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*32 bytes*")
            .Where(e => e.Message.Contains("version 1"));
    }

    [Fact]
    public void Constructor_throws_when_MasterKeyBase64_length_is_not_32_bytes()
    {
        var options = new EncryptionOptions
        {
            MasterKeyBase64 = Convert.ToBase64String(new byte[16]),
            CurrentKeyVersion = 1
        };

        var act = () => new ConfigKeyProvider(Options.Create(options));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Master key must be 32 bytes*");
    }

    [Fact]
    public void Constructor_throws_when_MasterKeyBase64_and_Keys_empty_required()
    {
        var options = new EncryptionOptions
        {
            MasterKeyBase64 = "",
            CurrentKeyVersion = 1
        };

        var act = () => new ConfigKeyProvider(Options.Create(options));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*MasterKeyBase64 or Encryption:Keys is required*");
    }

    [Fact]
    public void Constructor_throws_when_Keys_has_no_valid_entry()
    {
        var options = new EncryptionOptions
        {
            Keys = new Dictionary<string, string>
            {
                ["x"] = ValidKeyBase64,
                ["2"] = "   "
            },
            CurrentKeyVersion = 1
        };

        var act = () => new ConfigKeyProvider(Options.Create(options));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*at least one valid key*");
    }

    [Fact]
    public void Constructor_throws_when_CurrentKeyVersion_not_in_configured_keys()
    {
        var options = new EncryptionOptions
        {
            Keys = new Dictionary<string, string> { ["1"] = ValidKeyBase64 },
            CurrentKeyVersion = 99
        };

        var act = () => new ConfigKeyProvider(Options.Create(options));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*CurrentKeyVersion (99) must exist*");
    }

    [Fact]
    public void GetKey_throws_when_version_unknown()
    {
        var options = new EncryptionOptions
        {
            MasterKeyBase64 = ValidKeyBase64,
            CurrentKeyVersion = 1
        };
        var sut = new ConfigKeyProvider(Options.Create(options));

        var act = () => sut.GetKey(2);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .Where(e => e.ParamName == "version" && Equals(e.ActualValue, 2));
    }

    [Fact]
    public void Constructor_with_Keys_trims_whitespace_from_key_value()
    {
        var keyBytes = new byte[32];
        new Random(1).NextBytes(keyBytes);
        var options = new EncryptionOptions
        {
            Keys = new Dictionary<string, string> { ["1"] = "  " + Convert.ToBase64String(keyBytes) + "  " },
            CurrentKeyVersion = 1
        };
        var sut = new ConfigKeyProvider(Options.Create(options));

        sut.GetKey(1).Should().BeEquivalentTo(keyBytes);
    }

    [Fact]
    public void Constructor_with_MasterKeyBase64_fallback_when_Keys_null()
    {
        var options = new EncryptionOptions
        {
            Keys = null,
            MasterKeyBase64 = ValidKeyBase64,
            CurrentKeyVersion = 1
        };
        var sut = new ConfigKeyProvider(Options.Create(options));

        sut.GetCurrentVersion().Should().Be(1);
        sut.GetKey(1).Should().HaveCount(32);
    }
}
