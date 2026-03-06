namespace SecureVault.Infrastructure.Crypto;

public interface IArgon2KeyDerivation
{
    byte[] DeriveKey(byte[] password, byte[] salt, int outputLength);
}
