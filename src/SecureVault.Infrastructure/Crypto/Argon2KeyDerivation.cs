using Konscious.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace SecureVault.Infrastructure.Crypto;

public sealed class Argon2KeyDerivation : IArgon2KeyDerivation
{
    private readonly Argon2Options _options;

    public Argon2KeyDerivation(IOptions<Argon2Options> options)
    {
        _options = options.Value;
        var iter = Math.Max(Argon2Options.MinIterations, _options.Iterations);
        var mem = Math.Max(Argon2Options.MinMemoryKb, _options.MemorySizeKb);
        if (iter != _options.Iterations || mem != _options.MemorySizeKb)
            _options = new Argon2Options { Iterations = iter, MemorySizeKb = mem, DegreeOfParallelism = _options.DegreeOfParallelism };
    }

    public byte[] DeriveKey(byte[] password, byte[] salt, int outputLength)
    {
        var iterations = Math.Max(Argon2Options.MinIterations, _options.Iterations);
        var memorySize = Math.Max(Argon2Options.MinMemoryKb, _options.MemorySizeKb);
        using var argon2 = new Argon2id(password)
        {
            Salt = salt,
            DegreeOfParallelism = Math.Clamp(_options.DegreeOfParallelism, 1, 16),
            Iterations = iterations,
            MemorySize = memorySize
        };
        return argon2.GetBytes(outputLength);
    }
}
