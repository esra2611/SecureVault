namespace SecureVault.Infrastructure.Crypto;

/// <summary>
/// Configuration for Argon2id key derivation (password → encryption key).
/// OWASP: use Argon2id with time cost ≥ 2 and memory ≥ 15 MB.
/// </summary>
public sealed class Argon2Options
{
    public const string SectionName = "Encryption:Argon2";

    /// <summary>Time cost (iterations). Minimum 2 per OWASP; default 3 for stronger resistance.</summary>
    public int Iterations { get; set; } = 3;

    /// <summary>Memory size in KB (e.g. 65536 = 64 MB).</summary>
    public int MemorySizeKb { get; set; } = 64 * 1024;

    /// <summary>Degree of parallelism (1–8 typical).</summary>
    public int DegreeOfParallelism { get; set; } = 2;

    internal const int MinIterations = 2;
    internal const int MinMemoryKb = 1024;
}
