namespace SecureVault.Application.Common.Interfaces;

/// <summary>
/// Abstraction for current time to allow deterministic tests and avoid static coupling.
/// </summary>
public interface ITimeProvider
{
    DateTime UtcNow { get; }
}
