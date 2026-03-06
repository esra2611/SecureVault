namespace SecureVault.Application.Common.Interfaces;

/// <summary>
/// AES-256-GCM encrypt/decrypt. Always uses the server master key (via keyVersion) for secret payload.
/// Password protection is a gate only (verification step); passwords are never used as encryption or decryption keys.
/// </summary>
public interface IEncryptionService
{
    /// <summary>Encrypt with master key for current key version.</summary>
    (byte[] Ciphertext, byte[] Nonce) Encrypt(byte[] plaintext);

    /// <summary>Decrypt with master key for the given key version.</summary>
    byte[] Decrypt(byte[] ciphertext, byte[] nonce, int keyVersion);
}
