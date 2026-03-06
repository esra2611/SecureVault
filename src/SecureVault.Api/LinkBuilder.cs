using SecureVault.Application.Secrets.CreateSecret;

namespace SecureVault.Api;

public sealed class LinkBuilder : ICreateSecretLinkBuilder
{
    private readonly string _baseUrl;

    public LinkBuilder(string baseUrl)
    {
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public string Build(byte[] tokenBytes)
    {
        var token = Convert.ToBase64String(tokenBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        return $"{_baseUrl}/s/{token}";
    }
}
