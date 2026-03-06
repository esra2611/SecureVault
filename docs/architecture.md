# SecureVault — Architecture

**Time-limited secret sharing.** This document describes the current implementation only. No speculative or planned features.

---

## 1. High-level architecture

```
                    ┌─────────────────────────────────────────────────────────────┐
                    │                        CLI / Browser                          │
                    └─────────────────────────────┬─────────────────────────────────┘
                                                  │ HTTPS
                    ┌─────────────────────────────▼─────────────────────────────────┐
                    │                    Nuxt 3 Frontend                           │
                    │              (submit secret, open link, view once)             │
                    └─────────────────────────────┬─────────────────────────────────┘
                                                  │ REST API
                    ┌─────────────────────────────▼─────────────────────────────────┐
                    │                  SecureVault.Api (ASP.NET Core 9)              │
                    │  Endpoints │ Middleware (rate limit, security headers)         │
                    └─────────────────────────────┬─────────────────────────────────┘
                                                  │
         ┌────────────────────────────────────────┼────────────────────────────────────────┐
         │                                        │                                        │
         ▼                                        ▼                                        ▼
┌─────────────────┐                  ┌─────────────────────┐                  ┌─────────────────────┐
│   Application   │                  │   Infrastructure     │                  │      Domain         │
│  (Use cases)    │◄─────────────────│  (Adapters)          │                  │  (Entities, VOs)    │
│  CreateSecret   │  depends on      │  Persistence (EF)     │─────────────────►│  SecretRecord,      │
│  RevealSecret   │  interfaces      │  Redis (cache, rate)  │  implements      │  TokenHash,         │
│  Validation     │                  │  RabbitMQ (audit)     │                  │  ExpiryType         │
│  Ports (IFs)    │                  │  Crypto (AES-GCM)     │                  └─────────────────────┘
└────────┬────────┘                  └──────────┬───────────┘
         │                                      │
         │                                      ├──────────────────┬──────────────────┐
         │                                      ▼                  ▼                  ▼
         │                              ┌────────────┐    ┌────────────┐    ┌────────────┐
         └─────────────────────────────►│ PostgreSQL │    │   Redis    │    │ RabbitMQ   │
                    (DB = source of     │  (secrets  │    │  cache +   │    │  (audit    │
                     truth)             │  encrypted)│    │  rate limit │    │   only)    │
                                        └────────────┘    └────────────┘    └────────────┘
                                                               │
                    ┌─────────────────────────────────────────┘
                    │  exchange: securevault.audit, routing: audit.secret
                    ▼
         ┌─────────────────────┐
         │ SecureVault.        │
         │ AuditConsumer       │  (separate host/container)
         │ Consumes → AuditLogs│
         └─────────────────────┘
```

**Audit flow:** API publishes create/reveal events to RabbitMQ (exchange `securevault.audit`, routing key `audit.secret`). AuditConsumer declares a durable queue (e.g. `securevault.audit.log`), consumes messages, and persists them to the `AuditLogs` table in PostgreSQL via raw Npgsql. No secret or plaintext is published or stored in audit.

**Dependency rule:** Domain ← Application ← Infrastructure ← Api. No framework or IO in Domain.

---

## 2. Architecture style: Clean Architecture

| | |
|---|---|
| **What we chose** | Clean Architecture (Onion / Ports & Adapters): Domain, Application, Infrastructure, Api. Application references only Domain; Infrastructure implements Application interfaces (ISecretRepository, IEncryptionService, IAuditPublisher, etc.). Api references Application and Infrastructure. |
| **Why** | Domain-centric design with clear boundaries; use cases are testable with fakes (no DB/crypto in unit tests). Security (crypto, validation) lives behind interfaces. [Clean Architecture (R. Martin)](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html), [Microsoft Application Architecture Guide](https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-client-side-web-technologies). |
| **What we considered and rejected** | **Vertical Slice:** Would duplicate crypto/expiry across slices. **Modular Monolith:** Overhead for current scope; can evolve to modules later. |

---

## 3. Encryption and key derivation

| | |
|---|---|
| **What we chose** | **At-rest encryption:** AES-256-GCM (AEAD). Single 32-byte key from configuration (`Encryption:MasterKeyBase64` or `Encryption:Keys:<version>`). Optional key version per secret (`KeyVersion`) for rotation. **Nonce:** 12-byte random per encryption, stored with ciphertext. **Optional password:** Not used to encrypt the secret body. Secret body is always encrypted with the app key. When password is set we store a PBKDF2-SHA256–derived **verification hash** (and salt); at reveal we verify the password with constant-time comparison, then decrypt with the app key. |
| **Why** | AES-GCM: NIST-approved, confidentiality + integrity, first-class in .NET. [NIST SP 800-38D](https://csrc.nist.gov/publications/detail/sp/800-38d/final). PBKDF2 with high iteration count for password verification: [OWASP Password Storage Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html). |
| **What we considered and rejected** | AES-CBC+HMAC (two keys, ordering). ChaCha20-Poly1305 (standardized on GCM). Storing token in plaintext in DB (we store hash only). |

**Implementation:** `AesGcmEncryptionService` (nonce 12 bytes, tag 16 bytes, key 32 bytes). `ConfigKeyProvider` reads `Encryption:MasterKeyBase64` or `Encryption:Keys` and supports `CurrentKeyVersion` for new encryptions.

---

## 4. Password protection (optional)

| | |
|---|---|
| **What we chose** | Optional password at create: 16-byte random salt, PBKDF2-SHA256 (Rfc2898DeriveBytes) with configurable iterations (min 100,000). Derived 32-byte value stored as Base64 in `PasswordHashBase64` for verification only. At reveal: constant-time comparison (`CryptographicOperations.FixedTimeEquals`); fixed delay on failure (`RevealSecurityConfig.DecryptionFailureDelay`, default 100 ms). Same 404 response for wrong password, invalid token, or expired—no enumeration. |
| **Why** | OWASP recommends ≥100,000 iterations for PBKDF2-SHA256. Constant-time comparison and delay reduce timing oracles. [OWASP Password Storage Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html), [NIST SP 800-132](https://csrc.nist.gov/publications/detail/sp/800-132/final). |
| **What we considered and rejected** | Using password-derived key to encrypt the secret (adds complexity; we use app key for encryption and password only for access control). bcrypt/Argon2 (PBKDF2 is built-in, sufficient with high iterations for this use case). |

**Implementation:** `Pbkdf2PasswordDerivation` (section `Security:Pbkdf2`, `Iterations` default 100_000). Salt ≥16 bytes. Verification hash only; secret ciphertext is always encrypted with the app master key.

---

## 5. Token generation and storage

| | |
|---|---|
| **What we chose** | **Generation:** 32-byte (256-bit) cryptographically random (`RandomNumberGenerator.Fill`). **Storage:** SHA-256 hash of token stored in DB as Base64 (`TokenHashBase64`). Plaintext token appears only in the shareable URL and briefly in app memory. **URL:** Base64url-encoded token (replace `+`/`/` with `-`/`_`, trim padding) in path `/s/{token}`. |
| **Why** | Unguessable token; DB dump does not reveal tokens. [RFC 4648 base64url](https://www.rfc-editor.org/rfc/rfc4648). |
| **What we considered and rejected** | Storing token in plaintext in DB. Shorter token (e.g. 128-bit). |

**Implementation:** `TokenGenerator` produces 32-byte token and `TokenHash` (SHA-256). `LinkBuilder` builds `{BaseUrl}/s/{base64url(tokenBytes)}`. `SecretEntity.TokenHashBase64` is the stored hash.

---

## 6. Expiry mechanism

| | |
|---|---|
| **What we chose** | **Authoritative source:** PostgreSQL. Each secret has `UtcExpiresAt` and `UtcRevealedAt` (nullable). Burn-after-read: `UtcExpiresAt` set to creation + 10 years; “expiry” is first read. Time-based: 1h, 24h, 7d from creation. Every reveal path checks DB; Redis is not used to deny access. **Redis:** Used only on create path—`SecretCache.SetSecretTtlAsync` writes a key with TTL (best-effort). `IsKnownExpiredAsync` always returns false; we do not short-circuit reveal from cache. **Cleanup:** `SecretCleanupHostedService` runs every 15 minutes and deletes rows where `UtcExpiresAt < now` OR `UtcRevealedAt IS NOT NULL`. Idempotent; does not affect correctness. |
| **Why** | DB as single source of truth; Redis failure or eviction must not extend or grant access. [OWASP ASVS](https://owasp.org/www-project-application-security-verification-standard/) (data protection, retention). |
| **What we considered and rejected** | Redis TTL as authority (data loss could extend lifetime). RabbitMQ delayed message for expiry enforcement (expiry enforced at read time; no dependency on message delivery). |

**Implementation:** `SecretExpiryConfig` (optional `OverrideTtlSeconds` for tests). `CreateSecretCommandHandler` computes `UtcExpiresAt`; `SecretRepository.DeleteTerminalRowsAsync`; `SecretCleanupHostedService` interval 15 minutes.

---

## 7. Exactly-once reveal and concurrency control

| | |
|---|---|
| **What we chose** | **Non–password-protected path:** Single atomic SQL operation: `DELETE FROM "Secrets" WHERE ... RETURNING "Id", "Ciphertext", "Nonce", ...`. Only one concurrent request can delete the matching row; others get zero rows and are treated as expired/not found. Ciphertext is returned in the same statement before the row is removed. **Password-protected path:** Two steps: (1) `TryPeekSecretAsync` (read-only) to get ciphertext and verify password, (2) `ConsumeAsync` (ExecuteUpdateAsync: set `UtcRevealedAt`, clear `Ciphertext`/`Nonce`). First successful consumer wins; concurrent consumers may see the row until one updates it. |
| **Why** | DELETE ... RETURNING in PostgreSQL is atomic; one row, one winner. [PostgreSQL DELETE](https://www.postgresql.org/docs/current/sql-delete.html). For password path, peek then consume keeps logic simple while still clearing sensitive data. |
| **What we considered and rejected** | SELECT FOR UPDATE then UPDATE (implemented instead as DELETE RETURNING for non-password path to avoid lock then separate update). Redis Lua alone (we need DB for ciphertext). Application-level lock (does not work across instances). |

**Implementation:** `SecretRepository.TryRevealOnceAsync` uses raw `DbConnection` and `DELETE ... RETURNING`. `ConsumeAsync` uses EF Core `ExecuteUpdateAsync`. Reveal handler uses `TryRevealOnceAsync` for non-password and peek + `ConsumeAsync` for password-protected.

---

## 8. Secret storage strategy

| | |
|---|---|
| **What we chose** | PostgreSQL table `Secrets`: `Id` (Guid), `TokenHashBase64` (unique), `ExpiryType`, `UtcCreatedAt`, `UtcExpiresAt`, `UtcRevealedAt` (nullable), `Ciphertext`, `Nonce`, `KeyVersion`, optional `SaltForPassword`, `IsPasswordProtected`, `PasswordHashBase64`. Ciphertext and nonce cleared on reveal (or row deleted in DELETE RETURNING path). EF Core with Npgsql; migrations in Infrastructure. |
| **Why** | Single store of truth; durable; parameterized queries (Npgsql) to avoid injection. |
| **What we considered and rejected** | (Not applicable; single DB store.) |

**Implementation:** `SecretVaultDbContext`, `SecretEntity`, `SecretRepository`. Schema ensured in `Program.cs` (RunMigrationsIfEnabledAsync and fallback `CREATE TABLE IF NOT EXISTS` for Secrets/AuditLogs).

---

## 9. Cache usage

| | |
|---|---|
| **What we chose** | Redis as `IDistributedCache` (StackExchangeRedis). **Write path:** On create, we call `SecretCache.SetSecretTtlAsync(tokenHash, ttl)` to set a key with TTL (best-effort; failures logged, creation still succeeds). **Read path:** `IsKnownExpiredAsync` always returns false—we do not use cache to short-circuit reveal; DB is authoritative. Cache is also used by rate limiting (see below). |
| **Why** | TTL in Redis can support future fast-expiry checks or tooling; we do not rely on it for correctness. [Redis TTL](https://redis.io/commands/ttl/). |
| **What we considered and rejected** | Using Redis “missing key” as proof of expiry (would be wrong after eviction or restart). |

**Implementation:** `SecretCache` (prefix `secret:ttl:`). `ISecretCache` injected into create and reveal handlers.

---

## 10. Messaging (RabbitMQ) purpose

| | |
|---|---|
| **What we chose** | RabbitMQ used only for **audit**: API publishes events (created, revealed) to topic exchange `securevault.audit` with routing key `audit.secret`. Payload: `secretId`, `tokenIdHint`, `utcExpiresAt` (create only)—no secret or plaintext. Lazy connection (no connect in constructor) so API startup does not depend on RabbitMQ. Publish failures are logged and swallowed (best-effort). Separate **AuditConsumer** process: declares durable queue (e.g. `securevault.audit.log`), binds to exchange, consumes with manual ack, persists to `AuditLogs` via raw Npgsql. Idempotent insert by `MessageId` (or derived key) to handle at-least-once delivery. |
| **Why** | Decouples audit persistence from API; audit can be scaled or replayed independently. [RabbitMQ Consumer Acknowledgements](https://www.rabbitmq.com/confirms.html). |
| **What we considered and rejected** | Using RabbitMQ for expiry enforcement (expiry is at read time in DB). Publishing secret or plaintext (never). |

**Implementation:** `RabbitMqAuditPublisher` (exchange from `RabbitMQ:Exchange`, default `securevault.audit`). `AuditConsumerWorker`: queue from `RabbitMQ:QueueName`, `ON CONFLICT (MessageId) DO NOTHING` for idempotency.

---

## 11. Rate limiting

| | |
|---|---|
| **What we chose** | Redis-backed rate limit: atomic INCR + EXPIRE per key. Keys: `ratelimit:create:{clientId}`, `ratelimit:reveal:{clientId}`. Configurable per-window limits (default 30 create, 15 reveal per 60s). Client ID = RemoteIpAddress, or first value of X-Forwarded-For when `RateLimiting:TrustProxy` is true. Applied to POST `/api/secrets` and GET `/s/*`. On Redis failure we allow the request (degrade open) and log. Response 429 with Retry-After: 60. |
| **Why** | Limit abuse and enumeration; OWASP guidance on rate limiting. |
| **What we considered and rejected** | (No alternative backend in codebase.) |

**Implementation:** `RateLimitMiddleware`, `RedisRateLimitService`, `RedisRateLimitBackend`. Options: `RateLimiting:CreateSecretPerWindow`, `RevealPerWindow`, `WindowSeconds`, `TrustProxy`.

---

## 12. Security (summary)

- **Encryption:** AES-256-GCM; key from config; nonce per encryption stored with ciphertext.
- **Key management:** Master key(s) via `Encryption:MasterKeyBase64` or `Encryption:Keys`; optional password → PBKDF2 verification hash only.
- **Token:** 32-byte CSPRNG; URL-safe base64url in link; stored as SHA-256 hash in DB.
- **Constant-time comparison:** Password verification with `CryptographicOperations.FixedTimeEquals`; fixed delay on failure.
- **Enumeration:** 404 with same message for invalid token, expired, already read, wrong password.
- **Sensitive logging:** No secret, plaintext token, or decrypted content in logs; audit by tokenIdHint/secretId only.
- **Headers:** SecurityHeadersMiddleware sets X-Content-Type-Options, X-Frame-Options, Referrer-Policy, Content-Security-Policy; HSTS when not Development. Cache-Control no-store for secret routes.
- **Request size:** Kestrel MaxRequestBodySize 64 KB.

---

## 13. Folder structure

```
src/
  SecureVault.sln
  SecureVault.Domain/              # No external dependencies
    Entities/
    ValueObjects/
  SecureVault.Application/         # References Domain only
    Secrets/
      CreateSecret/
      RevealSecret/
    Common/
      Interfaces/
      Behaviors/
  SecureVault.Infrastructure/     # References Application (and Domain via Application)
    Persistence/
    Caching/
    Messaging/
    Crypto/
    Config/
    Jobs/
    RateLimiting/
  SecureVault.Api/                 # References Application + Infrastructure
    Endpoints/
    Middleware/
  SecureVault.AuditConsumer/       # Standalone worker
  SecureVault.Tests.Unit/
  SecureVault.Tests.Integration/
frontend/                          # Nuxt 3
docs/
```

---

## Implementation verification

Source files used to derive this document:

- src/SecureVault.Api/Program.cs
- src/SecureVault.Api/Endpoints/SecretEndpoints.cs
- src/SecureVault.Api/LinkBuilder.cs
- src/SecureVault.Api/Middleware/RateLimitMiddleware.cs
- src/SecureVault.Api/Middleware/SecurityHeadersMiddleware.cs
- src/SecureVault.Application/Secrets/CreateSecret/CreateSecretCommandHandler.cs
- src/SecureVault.Application/Secrets/CreateSecret/CreateSecretCommandValidator.cs
- src/SecureVault.Application/Secrets/RevealSecret/RevealSecretQueryHandler.cs
- src/SecureVault.Application/Common/Interfaces/IPasswordDerivation.cs
- src/SecureVault.Infrastructure/DependencyInjection.cs
- src/SecureVault.Infrastructure/Persistence/SecretRepository.cs
- src/SecureVault.Infrastructure/Persistence/SecretEntity.cs
- src/SecureVault.Infrastructure/Persistence/SecretVaultDbContext.cs
- src/SecureVault.Infrastructure/Crypto/AesGcmEncryptionService.cs
- src/SecureVault.Infrastructure/Crypto/TokenGenerator.cs
- src/SecureVault.Infrastructure/Crypto/ConfigKeyProvider.cs
- src/SecureVault.Infrastructure/Crypto/Pbkdf2PasswordDerivation.cs
- src/SecureVault.Infrastructure/Caching/SecretCache.cs
- src/SecureVault.Infrastructure/Config/SecretExpiryConfig.cs
- src/SecureVault.Infrastructure/Config/RevealSecurityConfig.cs
- src/SecureVault.Infrastructure/Messaging/RabbitMqAuditPublisher.cs
- src/SecureVault.Infrastructure/Jobs/SecretCleanupHostedService.cs
- src/SecureVault.Infrastructure/RateLimiting/RedisRateLimitService.cs
- src/SecureVault.Infrastructure/RateLimiting/RedisRateLimitBackend.cs
- src/SecureVault.Domain/ValueObjects/TokenHash.cs
- src/SecureVault.AuditConsumer/Program.cs
- src/SecureVault.AuditConsumer/AuditConsumerWorker.cs
- src/SecureVault.AuditConsumer/RabbitMqOptions.cs
