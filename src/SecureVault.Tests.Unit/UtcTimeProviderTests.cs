using FluentAssertions;
using SecureVault.Infrastructure.Time;
using Xunit;

namespace SecureVault.Tests.Unit;

public sealed class UtcTimeProviderTests
{
    [Fact]
    public void UtcNow_returns_current_UTC_time()
    {
        var sut = new UtcTimeProvider();
        var before = DateTime.UtcNow;
        var result = sut.UtcNow;
        var after = DateTime.UtcNow;

        result.Kind.Should().Be(DateTimeKind.Utc);
        result.Should().BeOnOrAfter(before).And.BeOnOrBefore(after.AddMilliseconds(100));
    }
}
