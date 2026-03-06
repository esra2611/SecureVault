namespace SecureVault.Application.Secrets.CreateSecret;

/// <summary>
/// Builds the shareable URL from token bytes. Abstraction so API can inject base URL.
/// </summary>
public interface ICreateSecretLinkBuilder
{
    string Build(byte[] tokenBytes);
}
