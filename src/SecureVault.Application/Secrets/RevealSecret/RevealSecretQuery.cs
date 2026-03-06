using MediatR;

namespace SecureVault.Application.Secrets.RevealSecret;

public sealed record RevealSecretQuery(string Token, string? Password = null) : IRequest<RevealSecretHandlerOutcome>;

public sealed record RevealSecretResult(string Plaintext);
