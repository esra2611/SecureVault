using Microsoft.Extensions.Options;
using SecureVault.Application.Common.Interfaces;

namespace SecureVault.Infrastructure.Crypto;

/// <summary>
/// Key provider that reads keys from configuration. Supports multiple key versions for rotation (NIST SP 800-57).
/// Use Encryption:Keys:1, Encryption:Keys:2, etc. for backward decryption; CurrentKeyVersion for new encryptions.
/// Falls back to single Encryption:MasterKeyBase64 as version 1 if Keys is not set.
/// </summary>
public sealed class ConfigKeyProvider : IKeyProvider
{
    private readonly EncryptionOptions _options;
    private readonly IReadOnlyDictionary<int, byte[]> _keysByVersion;

    public ConfigKeyProvider(IOptions<EncryptionOptions> options)
    {
        _options = options.Value;
        var dict = new Dictionary<int, byte[]>();

        if (_options.Keys is { Count: > 0 })
        {
            foreach (var kv in _options.Keys)
            {
                if (!int.TryParse(kv.Key, out var ver) || string.IsNullOrWhiteSpace(kv.Value))
                    continue;
                var keyBytes = Convert.FromBase64String(kv.Value.Trim());
                if (keyBytes.Length != 32)
                    throw new ArgumentException($"Encryption key version {ver} must be 32 bytes (256-bit).");
                dict[ver] = keyBytes;
            }
            if (dict.Count == 0)
                throw new ArgumentException("Encryption:Keys must contain at least one valid key (e.g. Keys:1).");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(_options.MasterKeyBase64))
                throw new ArgumentException("Encryption:MasterKeyBase64 or Encryption:Keys is required.");
            var keyV1 = Convert.FromBase64String(_options.MasterKeyBase64.Trim());
            if (keyV1.Length != 32)
                throw new ArgumentException("Master key must be 32 bytes (256-bit).");
            dict[1] = keyV1;
        }

        if (!dict.ContainsKey(_options.CurrentKeyVersion))
            throw new ArgumentException($"Encryption:CurrentKeyVersion ({_options.CurrentKeyVersion}) must exist in configured keys.");
        _keysByVersion = dict;
    }

    public int GetCurrentVersion() => _options.CurrentKeyVersion;

    public byte[] GetKey(int version)
    {
        if (_keysByVersion.TryGetValue(version, out var key))
            return key;
        throw new ArgumentOutOfRangeException(nameof(version), version, "Unknown key version.");
    }
}
