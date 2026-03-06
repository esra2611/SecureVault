using System.Data;
using Microsoft.EntityFrameworkCore;
using SecureVault.Application.Common.Interfaces;
using SecureVault.Application.Common.Models;
using SecureVault.Domain.ValueObjects;
using SecureVault.Infrastructure.Persistence;

namespace SecureVault.Infrastructure.Persistence;

public sealed class SecretRepository : ISecretRepository
{
    private readonly SecretVaultDbContext _db;

    public SecretRepository(SecretVaultDbContext db) => _db = db;

    public async Task<Guid> AddAsync(AddSecretRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new SecretEntity
        {
            Id = Guid.NewGuid(),
            TokenHashBase64 = request.TokenHash.ToBase64(),
            ExpiryType = (int)request.ExpiryType,
            UtcCreatedAt = DateTime.UtcNow,
            UtcExpiresAt = request.UtcExpiresAt,
            Ciphertext = request.Ciphertext,
            Nonce = request.Nonce,
            KeyVersion = request.KeyVersion,
            SaltForPassword = request.SaltForPassword,
            IsPasswordProtected = request.IsPasswordProtected,
            PasswordHashBase64 = request.PasswordHashBase64
        };
        _db.Secrets.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<TryPeekSecretOutcome> TryPeekSecretAsync(TokenHash tokenHash, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var hashBase64 = tokenHash.ToBase64();

        var entity = await _db.Secrets
            .Where(s => s.TokenHashBase64 == hashBase64 && s.UtcRevealedAt == null && s.UtcExpiresAt > utcNow)
            .AsNoTracking()
            .Select(s => new { s.Id, s.Ciphertext, s.Nonce, s.KeyVersion, s.SaltForPassword, s.IsPasswordProtected, s.PasswordHashBase64 })
            .FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
        {
            var exists = await _db.Secrets.AnyAsync(s => s.TokenHashBase64 == hashBase64, cancellationToken);
            return exists ? new TryPeekExpiredOrViewedOutcome() : new TryPeekNotFoundOutcome();
        }

        var ciphertext = entity.Ciphertext ?? [];
        var nonce = entity.Nonce ?? [];
        var result = new RevealResult(
            entity.Id,
            ciphertext,
            nonce,
            entity.KeyVersion,
            entity.SaltForPassword,
            entity.IsPasswordProtected,
            entity.PasswordHashBase64);
        return new TryPeekSuccessOutcome(result);
    }

    public async Task<bool> ConsumeAsync(Guid secretId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var updated = await _db.Secrets
            .Where(s => s.Id == secretId && s.UtcRevealedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.UtcRevealedAt, utcNow)
                .SetProperty(x => x.Ciphertext, (byte[]?)null)
                .SetProperty(x => x.Nonce, (byte[]?)null),
            cancellationToken);
        return updated == 1;
    }

    public async Task<TryRevealOnceOutcome> TryRevealOnceAsync(TokenHash tokenHash, DateTime utcNow, CancellationToken cancellationToken)
    {
        var hashBase64 = tokenHash.ToBase64();

        // Atomic DELETE ... RETURNING: consume and return ciphertext/nonce in one statement.
        // RETURNING yields row values *before* the delete, so we get real Ciphertext/Nonce for decryption.
        // Only one concurrent request can delete the single matching row; others get 0 rows and fail the reveal.
        var conn = _db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(cancellationToken);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DELETE FROM "Secrets"
            WHERE "TokenHashBase64" = @p0 AND "UtcRevealedAt" IS NULL AND "UtcExpiresAt" > @p1
            RETURNING "Id", "Ciphertext", "Nonce", "KeyVersion", "SaltForPassword", "IsPasswordProtected", "PasswordHashBase64"
            """;
        var p0 = cmd.CreateParameter();
        p0.ParameterName = "@p0";
        p0.Value = hashBase64;
        cmd.Parameters.Add(p0);
        var p1 = cmd.CreateParameter();
        p1.ParameterName = "@p1";
        p1.Value = utcNow;
        cmd.Parameters.Add(p1);

        RevealResult? successResult = null;
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                var id = reader.GetGuid(0);
                var ciphertext = reader.IsDBNull(1) ? Array.Empty<byte>() : reader.GetFieldValue<byte[]>(1);
                var nonce = reader.IsDBNull(2) ? Array.Empty<byte>() : reader.GetFieldValue<byte[]>(2);
                var keyVersion = reader.GetInt32(3);
                byte[]? salt = reader.IsDBNull(4) ? null : reader.GetFieldValue<byte[]>(4);
                var isPasswordProtected = reader.GetBoolean(5);
                string? passwordHashBase64 = reader.IsDBNull(6) ? null : reader.GetString(6);
                successResult = new RevealResult(id, ciphertext, nonce, keyVersion, salt, isPasswordProtected, passwordHashBase64);
            }
        }

        if (successResult != null)
            return new TryRevealSuccessOutcome(successResult);

        var exists = await _db.Secrets.AnyAsync(s => s.TokenHashBase64 == hashBase64, cancellationToken);
        return exists ? new TryRevealExpiredOrViewedOutcome() : new TryRevealNotFoundOutcome();
    }

    public async Task<bool> ExistsAndNotExpiredAsync(TokenHash tokenHash, DateTime utcNow, CancellationToken cancellationToken)
    {
        var hashBase64 = tokenHash.ToBase64();
        return await _db.Secrets
            .AnyAsync(s => s.TokenHashBase64 == hashBase64 && s.UtcRevealedAt == null && s.UtcExpiresAt > utcNow, cancellationToken);
    }

    public async Task<int> DeleteTerminalRowsAsync(DateTime utcNow, CancellationToken cancellationToken)
    {
        var toDelete = await _db.Secrets
            .Where(s => s.UtcExpiresAt < utcNow || s.UtcRevealedAt != null)
            .ExecuteDeleteAsync(cancellationToken);
        return toDelete;
    }
}
