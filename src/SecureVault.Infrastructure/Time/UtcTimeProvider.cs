using SecureVault.Application.Common.Interfaces;

namespace SecureVault.Infrastructure.Time;

public sealed class UtcTimeProvider : ITimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
