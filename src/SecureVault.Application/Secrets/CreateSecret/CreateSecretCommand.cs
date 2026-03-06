using MediatR;
using SecureVault.Domain.ValueObjects;

namespace SecureVault.Application.Secrets.CreateSecret;

public sealed record CreateSecretCommand(
    string Plaintext,
    ExpiryType ExpiryType,
    string? Password = null) : IRequest<CreateSecretResult>;

public sealed record CreateSecretResult(string ShareUrl, string TokenIdHint);
