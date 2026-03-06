using FluentAssertions;
using SecureVault.Application.Common.Interfaces;
using Xunit;

namespace SecureVault.Tests.Unit;

/// <summary>
/// Covers Application assembly outcome record constructors used by SecretRepository (TryPeek*/TryReveal*).
/// Ensures these types are exercised from unit tests for Sonar coverage.
/// </summary>
public sealed class SecretRepositoryOutcomeTests
{
    [Fact]
    public void TryPeekNotFoundOutcome_can_be_constructed()
    {
        var outcome = new TryPeekNotFoundOutcome();
        outcome.Should().NotBeNull();
    }

    [Fact]
    public void TryPeekExpiredOrViewedOutcome_can_be_constructed()
    {
        var outcome = new TryPeekExpiredOrViewedOutcome();
        outcome.Should().NotBeNull();
    }

    [Fact]
    public void TryPeekSuccessOutcome_holds_RevealResult()
    {
        var result = new RevealResult(
            Guid.NewGuid(),
            [1, 2, 3],
            [4, 5, 6],
            1,
            null,
            false,
            null);
        var outcome = new TryPeekSuccessOutcome(result);

        outcome.Result.SecretId.Should().Be(result.SecretId);
        outcome.Result.Ciphertext.Should().BeEquivalentTo(result.Ciphertext);
    }

    [Fact]
    public void TryRevealNotFoundOutcome_can_be_constructed()
    {
        var outcome = new TryRevealNotFoundOutcome();
        outcome.Should().NotBeNull();
    }

    [Fact]
    public void TryRevealExpiredOrViewedOutcome_can_be_constructed()
    {
        var outcome = new TryRevealExpiredOrViewedOutcome();
        outcome.Should().NotBeNull();
    }

    [Fact]
    public void TryRevealSuccessOutcome_holds_RevealResult()
    {
        var result = new RevealResult(
            Guid.NewGuid(),
            Array.Empty<byte>(),
            new byte[12],
            1,
            null,
            false,
            null);
        var outcome = new TryRevealSuccessOutcome(result);

        outcome.Result.Should().Be(result);
    }
}
