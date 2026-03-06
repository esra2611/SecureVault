using System.Security.Cryptography;
using SecureVault.Application.Common.Interfaces;

namespace SecureVault.Infrastructure.Crypto;

public sealed class AesGcmEncryptionService : IEncryptionService
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeyLength = 32;
    private readonly IKeyProvider _keyProvider;

    public AesGcmEncryptionService(IKeyProvider keyProvider)
    {
        _keyProvider = keyProvider;
    }

    public (byte[] Ciphertext, byte[] Nonce) Encrypt(byte[] plaintext)
    {
        var key = _keyProvider.GetKey(_keyProvider.GetCurrentVersion());

        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);
        var ciphertextBuf = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertextBuf, tag);
        var combined = new byte[ciphertextBuf.Length + tag.Length];
        Buffer.BlockCopy(ciphertextBuf, 0, combined, 0, ciphertextBuf.Length);
        Buffer.BlockCopy(tag, 0, combined, ciphertextBuf.Length, tag.Length);
        return (combined, nonce);
    }

    public byte[] Decrypt(byte[] ciphertext, byte[] nonce, int keyVersion)
    {
        var key = _keyProvider.GetKey(keyVersion);

        var ciphertextOnly = ciphertext.AsSpan(0, ciphertext.Length - TagSize);
        var tag = ciphertext.AsSpan(ciphertext.Length - TagSize, TagSize);
        var plaintext = new byte[ciphertext.Length - TagSize];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, ciphertextOnly, tag, plaintext);
        return plaintext;
    }
}

public sealed class EncryptionOptions
{
    public const string SectionName = "Encryption";
    public string MasterKeyBase64 { get; set; } = string.Empty;
    /// <summary>Key version for new encryptions; must be supported by IKeyProvider (default 1).</summary>
    public int CurrentKeyVersion { get; set; } = 1;
    /// <summary>Optional: multiple key versions for rotation, e.g. "1": "base64...", "2": "base64...". If set, overrides single MasterKeyBase64.</summary>
    public Dictionary<string, string>? Keys { get; set; }
}
