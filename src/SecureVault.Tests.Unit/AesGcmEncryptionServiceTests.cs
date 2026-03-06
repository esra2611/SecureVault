using FluentAssertions;
using Microsoft.Extensions.Options;
using SecureVault.Application.Common.Interfaces;
using SecureVault.Infrastructure.Crypto;
using Xunit;

namespace SecureVault.Tests.Unit;

public sealed class AesGcmEncryptionServiceTests
{
    private static byte[] MasterKey32()
    {
        var key = new byte[32];
        new Random(42).NextBytes(key);
        return key;
    }

    private static IKeyProvider KeyProvider(byte[] key, int version = 1)
    {
        return new ConfigKeyProvider(Options.Create(new EncryptionOptions
        {
            MasterKeyBase64 = Convert.ToBase64String(key),
            CurrentKeyVersion = version
        }));
    }

    [Fact]
    public void Encrypt_decrypt_roundtrip_returns_plaintext()
    {
        var keyProvider = KeyProvider(MasterKey32());
        var encryption = new AesGcmEncryptionService(keyProvider);
        var plaintext = System.Text.Encoding.UTF8.GetBytes("secret payload");

        var (ciphertext, nonce) = encryption.Encrypt(plaintext);
        var decrypted = encryption.Decrypt(ciphertext, nonce, 1);

        decrypted.Should().BeEquivalentTo(plaintext);
    }

    [Fact]
    public void Two_encryptions_of_same_plaintext_produce_different_ciphertext()
    {
        var keyProvider = KeyProvider(MasterKey32());
        var encryption = new AesGcmEncryptionService(keyProvider);
        var plaintext = System.Text.Encoding.UTF8.GetBytes("same");

        var (c1, n1) = encryption.Encrypt(plaintext);
        var (c2, n2) = encryption.Encrypt(plaintext);

        n1.Should().NotBeEquivalentTo(n2, "nonce must be unique per encryption");
        c1.Should().NotBeEquivalentTo(c2, "ciphertext must differ (nonce-driven)");
    }

    [Fact]
    public void Decrypt_with_wrong_key_throws()
    {
        var key1 = MasterKey32();
        var key2 = new byte[32];
        new Random(99).NextBytes(key2);
        var enc = new AesGcmEncryptionService(KeyProvider(key1));
        var (ciphertext, nonce) = enc.Encrypt(System.Text.Encoding.UTF8.GetBytes("x"));
        var wrongKeyProvider = KeyProvider(key2);
        var dec = new AesGcmEncryptionService(wrongKeyProvider);

        var act = () => dec.Decrypt(ciphertext, nonce, 1);

        act.Should().Throw<System.Security.Cryptography.CryptographicException>();
    }

    [Fact]
    public void Decrypt_with_tampered_ciphertext_throws_CryptographicException()
    {
        var keyProvider = KeyProvider(MasterKey32());
        var encryption = new AesGcmEncryptionService(keyProvider);
        var plaintext = System.Text.Encoding.UTF8.GetBytes("secret");
        var (ciphertext, nonce) = encryption.Encrypt(plaintext);
        var tampered = (byte[])ciphertext.Clone();
        tampered[0] ^= 0xFF;

        var act = () => encryption.Decrypt(tampered, nonce, 1);

        act.Should().Throw<System.Security.Cryptography.CryptographicException>();
    }

    [Fact]
    public void Decrypt_with_wrong_nonce_throws_CryptographicException()
    {
        var keyProvider = KeyProvider(MasterKey32());
        var encryption = new AesGcmEncryptionService(keyProvider);
        var plaintext = System.Text.Encoding.UTF8.GetBytes("secret");
        var (ciphertext, nonce) = encryption.Encrypt(plaintext);
        var wrongNonce = new byte[nonce.Length];
        wrongNonce[0] = (byte)(nonce[0] ^ 0xFF);

        var act = () => encryption.Decrypt(ciphertext, wrongNonce, 1);

        act.Should().Throw<System.Security.Cryptography.CryptographicException>();
    }

    [Fact]
    public void Decrypt_with_tampered_tag_throws_CryptographicException()
    {
        var keyProvider = KeyProvider(MasterKey32());
        var encryption = new AesGcmEncryptionService(keyProvider);
        var plaintext = System.Text.Encoding.UTF8.GetBytes("secret");
        var (ciphertext, nonce) = encryption.Encrypt(plaintext);
        var tamperedTag = (byte[])ciphertext.Clone();
        tamperedTag[^1] ^= 0xFF;

        var act = () => encryption.Decrypt(tamperedTag, nonce, 1);

        act.Should().Throw<System.Security.Cryptography.CryptographicException>();
    }

    [Fact]
    public void Decrypt_with_ciphertext_shorter_than_tag_throws()
    {
        var keyProvider = KeyProvider(MasterKey32());
        var encryption = new AesGcmEncryptionService(keyProvider);
        var shortCiphertext = new byte[10];
        var nonce = new byte[12];

        var act = () => encryption.Decrypt(shortCiphertext, nonce, 1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Decrypt_with_wrong_keyVersion_throws_ArgumentOutOfRangeException()
    {
        var keyProvider = KeyProvider(MasterKey32(), version: 1);
        var encryption = new AesGcmEncryptionService(keyProvider);
        var (ciphertext, nonce) = encryption.Encrypt(System.Text.Encoding.UTF8.GetBytes("x"));

        var act = () => encryption.Decrypt(ciphertext, nonce, 99);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("version");
    }
}
