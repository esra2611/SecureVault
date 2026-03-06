using System.Security.Cryptography;
using SecureVault.Application.Common.Interfaces;
using SecureVault.Domain.ValueObjects;

namespace SecureVault.Infrastructure.Crypto;

public sealed class TokenGenerator : ITokenGenerator
{
    private const int TokenLength = 32;

    public (byte[] TokenBytes, TokenHash Hash) Generate()
    {
        var tokenBytes = new byte[TokenLength];
        RandomNumberGenerator.Fill(tokenBytes);
        var hashBytes = SHA256.HashData(tokenBytes);
        return (tokenBytes, new TokenHash(hashBytes));
    }
}
