using SecureVault.Domain.ValueObjects;

namespace SecureVault.Application.Common.Interfaces;

/// <summary>
/// Cryptographically secure token generation. Returns raw token (for link) and hash (for storage).
/// </summary>
public interface ITokenGenerator
{
    (byte[] TokenBytes, TokenHash Hash) Generate();
}
