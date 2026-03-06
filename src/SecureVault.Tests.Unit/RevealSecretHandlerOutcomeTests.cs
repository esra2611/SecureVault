using FluentAssertions;
using SecureVault.Application.Secrets.RevealSecret;
using Xunit;

namespace SecureVault.Tests.Unit;

public sealed class RevealSecretHandlerOutcomeTests
{
    [Fact]
    public void RevealSecretSuccessOutcome_holds_result_with_plaintext()
    {
        var result = new RevealSecretResult("decrypted");
        var outcome = new RevealSecretSuccessOutcome(result);

        outcome.Result.Plaintext.Should().Be("decrypted");
    }

    [Fact]
    public void RevealSecretExpiredOutcome_is_distinct_type()
    {
        var outcome = new RevealSecretExpiredOutcome();
        outcome.Should().BeOfType<RevealSecretExpiredOutcome>();
    }

    [Fact]
    public void RevealSecretNotFoundOutcome_is_distinct_type()
    {
        var outcome = new RevealSecretNotFoundOutcome();
        outcome.Should().BeOfType<RevealSecretNotFoundOutcome>();
    }
}
