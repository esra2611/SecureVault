# SecureVault — Time-Limited Secret Sharing

## Overview

SecureVault is a time-limited secret sharing system. Users submit a secret and receive a shareable link; the secret can be revealed **only once** (burn-after-read) or until a configurable expiration. Optional password protection and expiration (e.g. 1h, 24h, 7d) are supported. Create and reveal events are published to RabbitMQ and persisted to an audit log by a separate consumer. The system is designed for a technical reviewer evaluating a take-home assignment.

**Architectural summary:** A Nuxt 3 frontend talks to an ASP.NET Core 9 API. The API uses Clean Architecture (Domain → Application → Infrastructure → Api). Secrets are encrypted at rest (AES-256-GCM), tokens are stored as SHA-256 hashes, and PostgreSQL is the source of truth for expiry and one-time reveal. Redis is used for rate limiting and optional cache; RabbitMQ carries audit events to an audit-consumer that writes to the same PostgreSQL database.

---

## Tech Stack

| Category    | Technologies |
|------------|--------------|
| **Backend** | ASP.NET Core 9, Minimal APIs, FluentValidation, Serilog |
| **Frontend** | Nuxt 3 (Vue 3), Tailwind CSS |
| **Database** | PostgreSQL 15 (Npgsql, Entity Framework Core) |
| **Cache** | Redis 7 (StackExchange.Redis, IDistributedCache) |
| **Messaging** | RabbitMQ 3 (audit events only) |
| **Testing** | xUnit, WebApplicationFactory, Testcontainers (Postgres, Redis, RabbitMQ), Coverlet, Playwright |
| **Quality** | SonarQube (dotnet-sonarscanner, sonar-scanner), Stryker.NET (mutation testing), ReportGenerator |

---

## Features Implemented

- **Create secret** — POST `/api/secrets` with plaintext, expiry (1h, 24h, 7d, never), and optional password; returns shareable link.
- **Reveal secret (one-time access)** — GET `/s/{token}` or `/api/s/{token}`; returns plaintext once then invalidates (burn-after-read). Same 404 response for invalid token, expired, already read, or wrong password (no enumeration).
- **Expiration handling** — Time-based expiry (1h, 24h, 7d) and burn-after-read; PostgreSQL is authoritative; background job cleans expired/revealed rows.
- **Optional password protection** — PBKDF2-SHA256 verification hash (configurable iterations); constant-time comparison and fixed delay on failure.
- **Rate limiting** — Redis-backed; configurable limits for create and reveal per window (defaults e.g. 30 create, 15 reveal per 60s); 429 with Retry-After.
- **Audit events** — API publishes create/reveal events to RabbitMQ (exchange `securevault.audit`); **audit-consumer** consumes and persists to `AuditLogs` table (no secret or plaintext in audit).
- **Dockerized local environment** — Full stack via `docker compose`: Postgres, Redis, RabbitMQ, API, frontend, audit-consumer; optional SonarQube and pgAdmin.

---

## Architecture

The backend follows **Clean Architecture** with clear layer boundaries:

- **Domain** — Entities and value objects; no external dependencies.
- **Application** — Use cases (CreateSecret, RevealSecret), ports (interfaces); references Domain only.
- **Infrastructure** — Implementations (EF Core persistence, Redis cache, RabbitMQ audit publisher, AES-GCM encryption, PBKDF2 password derivation, rate limiting); references Application (and thus Domain).
- **Api** — ASP.NET Core host, endpoints, middleware (rate limit, security headers), CORS, Swagger in Development.

Dependency rule: Domain ← Application ← Infrastructure ← Api. No framework or IO in Domain.

For detailed design (encryption, token lifecycle, expiry, concurrency, security): **[docs/architecture.md](docs/architecture.md)**.

---

## Infrastructure

Services run on a single Docker network `securevault_net` and resolve each other by service name. The API connects to **PostgreSQL** (secrets, schema) and **Redis** (rate limiting, optional TTL keys). It publishes audit messages to **RabbitMQ**; the **audit-consumer** (separate container) consumes and writes to the same PostgreSQL `AuditLogs` table. The **frontend** calls the API from the browser via `NUXT_PUBLIC_API_BASE` (e.g. `http://localhost:8080`) and from the server (SSR) via `NUXT_API_BASE` (e.g. `http://api:8080`).

For ports, startup order, health checks, and service communication: **[docs/DOCKER-CONNECTIVITY.md](docs/DOCKER-CONNECTIVITY.md)**.

---

## Local Development Setup

1. **Clone the repository**
   ```bash
   git clone <repo-url>
   cd SecureVault
   ```

2. **Configure environment (optional but recommended)**  
   Copy `.env.example` to `.env` in the repo root. For production-like use, set at least:
   - `Encryption__MasterKeyBase64` — see “Environment Variables” below.  
   Docker Compose works without `.env` (defaults for Postgres password and a dev-only encryption key).

3. **Start containers**
   ```bash
   docker compose up --build -d
   ```
   Compose starts Postgres, Redis, RabbitMQ (and optional SonarQube DB), then API and audit-consumer (after DB/RabbitMQ are healthy), then frontend (after API is healthy).

4. **Access services**
   - **Frontend:** http://localhost:3000  
   - **API:** http://localhost:8080 — Health: http://localhost:8080/health, Ready: http://localhost:8080/ready  
   - **RabbitMQ management:** http://localhost:15672  
   - **pgAdmin (if used):** http://localhost:5050  
   - **SonarQube (if started):** http://localhost:9000  

   **Ports:** Postgres 5432, Redis 6379, RabbitMQ 5672 (AMQP) / 15672 (UI), API 8080, Frontend 3000, pgAdmin 5050, SonarQube 9000.

**Verification:** From repo root you can run `.\scripts\verify-docker-compose.ps1` (PowerShell) to start the stack and check API health, create-secret, Redis, RabbitMQ, and frontend.

---

## Environment Variables

Documented variables are those used in the repository (docker-compose and/or appsettings).

| Variable | Used by | Purpose |
|----------|---------|---------|
| `Encryption__MasterKeyBase64` | API | Base64-encoded 32-byte key for AES-256-GCM. **Required** for real use. Generate: `openssl rand -base64 32` or PowerShell equivalent. |
| `POSTGRES_PASSWORD` | postgres, api, audit-consumer | Postgres password (default: `securevault` in compose). |
| `ConnectionStrings__DefaultConnection` | API, audit-consumer | PostgreSQL connection string (e.g. `Host=postgres;Port=5432;Database=securevault;Username=securevault;Password=...`). |
| `ConnectionStrings__Redis` | API | Redis connection (e.g. `redis:6379`). |
| `RabbitMQ__HostName`, `RabbitMQ__Port` | API, audit-consumer | RabbitMQ host and port (e.g. `rabbitmq`, `5672`). |
| `RabbitMQ__Exchange`, `RabbitMQ__QueueName` | audit-consumer | Exchange and queue for audit (e.g. `securevault.audit`, `securevault.audit.log`). |
| `SecureVault__BaseUrl` | API | Base URL for share links (e.g. `http://localhost:3000`). |
| `Cors__Origins` | API | Allowed CORS origins (e.g. `http://localhost:3000`). |
| `NUXT_PUBLIC_API_BASE` | frontend (browser) | API base URL reachable from the client (e.g. `http://localhost:8080`). |
| `NUXT_API_BASE` | frontend (SSR) | API base URL from the frontend container (e.g. `http://api:8080`). |
| `RunMigrations` | API | Set to `"true"` to run EF migrations on startup (compose sets this). |
| `PGADMIN_DEFAULT_EMAIL`, `PGADMIN_DEFAULT_PASSWORD` | pgadmin | pgAdmin login (optional). |
| `SONAR_*` / `SONAR_POSTGRES_*` | sonarqube, sonarqube-db | SonarQube and its DB (optional). |

---

## Running Tests

- **Unit tests (with coverage)**  
  From repo root:
  ```bash
  dotnet test src/SecureVault.Tests.Unit/SecureVault.Tests.Unit.csproj -c Release \
    --collect:"XPlat Code Coverage" \
    --settings src/SecureVault.Tests.Unit/coverage.runsettings \
    --results-directory coverage
  ```
  Coverage is reported in `coverage/`. To merge and get an HTML report (requires ReportGenerator):
  ```bash
  ./scripts/test-coverage.sh
  ```

- **Integration tests (Testcontainers)**  
  From repo root (requires Docker):
  ```bash
  dotnet test src/SecureVault.sln -c Release --no-build \
    --filter "FullyQualifiedName~SecureVault.Tests.Integration"
  ```
  Or after a full build:
  ```bash
  dotnet build src/SecureVault.sln -c Release
  dotnet test src/SecureVault.sln -c Release --no-build --filter "FullyQualifiedName~SecureVault.Tests.Integration"
  ```

- **E2E (Playwright)**  
  From repo root, with API and dependencies running (e.g. `docker compose up -d postgres redis rabbitmq api`), then:
  ```bash
  cd frontend
  npm ci
  npx playwright install --with-deps
  npm run build
  # In another terminal: npm run preview
  npm run test:e2e
  ```
  Or use the CI flow: start API stack, build frontend with `NUXT_PUBLIC_API_BASE=http://localhost:8080`, run `npm run preview` and `npm run test:e2e`.

---

## Code Quality

- **SonarQube** — Backend and frontend can be analyzed via `./scripts/sonar.sh`. SonarQube (and its DB) must be running first, e.g. `docker compose up -d sonarqube-db sonarqube`. Set `SONAR_TOKEN` (create token in SonarQube UI). The script runs unit and integration tests with OpenCover and TRX under `TestResults/`, then runs dotnet-sonarscanner and sonar-scanner. Results: http://localhost:9000/dashboard?id=SecureVault.
- **Test coverage** — Unit tests use Coverlet with coverage.runsettings (Domain, Application, Infrastructure included; Api/Migrations excluded). CI enforces a line-coverage threshold (e.g. ≥ 80% per workflow).
- **Mutation testing** — Stryker.NET is run in CI on the unit test project (e.g. `--break-at 60`). Run locally from `src/SecureVault.Tests.Unit`: `dotnet stryker --break-at 60 --solution ../SecureVault.sln`.
- **CI pipeline** — See “Continuous Integration” below.

---

## Continuous Integration

The GitHub Actions workflow (`.github/workflows/ci.yml`) runs on push/PR to `main` and `develop`:

- **build-and-test:** Restore and build `src/SecureVault.sln` (Release). Run unit tests with Coverlet; merge coverage and enforce ≥ 80% line coverage; run integration tests (Testcontainers); run Stryker mutation testing (≥ 60%). Upload coverage report and Stryker reports as artifacts.
- **e2e:** After build-and-test, start Postgres, Redis, RabbitMQ, and API with Docker Compose; build frontend and run Playwright E2E tests; upload Playwright report; tear down.

CI uses .NET 9 and Node 20; encryption key for E2E is generated with `openssl rand -base64 32`.

---

## Documentation

| Document | Description |
|----------|-------------|
| [docs/architecture.md](docs/architecture.md) | Architecture, encryption, token/expiry/concurrency, rate limiting, security. |
| [docs/DOCKER-CONNECTIVITY.md](docs/DOCKER-CONNECTIVITY.md) | Docker Compose services, ports, health checks, env vars, service communication. |
| [docs/ai-usage.md](docs/ai-usage.md) | How AI tools were used during development and human accountability. |

---

## AI Usage Disclosure

AI-assisted development (e.g. Cursor, GitHub Copilot) may have been used for implementation boilerplate, tests, and documentation. Architecture decisions, threat model, crypto strategy, and security controls are human-designed and documented in `docs/architecture.md`. Code and design are reviewed by maintainers; AI output is not accepted without verification. Full disclosure: **[docs/ai-usage.md](docs/ai-usage.md)**.

---

## Repository Structure

| Path | Description |
|------|-------------|
| `src/` | .NET solution: Domain, Application, Infrastructure, Api, AuditConsumer, Unit and Integration test projects. |
| `src/SecureVault.Api/` | ASP.NET Core host, endpoints, middleware, Swagger. |
| `src/SecureVault.Application/` | Use cases, validators, ports (interfaces). |
| `src/SecureVault.Domain/` | Entities and value objects. |
| `src/SecureVault.Infrastructure/` | Persistence (EF, Npgsql), Redis, RabbitMQ, crypto, rate limiting, background jobs. |
| `src/SecureVault.AuditConsumer/` | Standalone worker consuming RabbitMQ audit queue and writing to AuditLogs. |
| `frontend/` | Nuxt 3 app (Tailwind, Playwright). |
| `docs/` | Architecture, Docker connectivity, AI usage. |
| `scripts/` | `sonar.sh` (SonarQube analysis), `test-coverage.sh` (unit coverage + ReportGenerator), `verify-docker-compose.ps1` (stack verification). |
| `docker-compose.yml` | Postgres, Redis, RabbitMQ, API, frontend, audit-consumer, optional SonarQube and pgAdmin. |
| `.github/workflows/ci.yml` | CI: build, unit/integration/mutation tests, E2E with Playwright. |

---

## Notes

- **Encryption key:** The compose file uses a dev-only default key so `docker compose up` works without `.env`. For any real or production use, set `Encryption__MasterKeyBase64` in `.env` to a cryptographically random 32-byte value (base64).
- **Expiry authority:** PostgreSQL is the source of truth for expiry and one-time reveal; Redis is not used to deny access on reveal.
- **Audit:** Audit is best-effort (publish failures logged, not fatal); API startup does not depend on RabbitMQ. The audit-consumer persists events to the same database; no secret or plaintext is published or stored in audit.
- **Rate limiting:** Backed by Redis; on Redis failure the middleware allows the request and logs (degrade open).

---

## README verification sources

The following repository files were used to derive this README:

- `docker-compose.yml`
- `src/SecureVault.Api/Dockerfile`, `frontend/Dockerfile`, `src/Dockerfile.Consumer`, `src/Dockerfile`
- `src/SecureVault.Api/Program.cs`
- `frontend/nuxt.config.ts`, `frontend/package.json`
- `src/SecureVault.Tests.Unit/SecureVault.Tests.Unit.csproj`, `src/SecureVault.Tests.Integration/SecureVault.Tests.Integration.csproj`, `src/SecureVault.Tests.Unit/coverage.runsettings`
- `.github/workflows/ci.yml`
- `sonar-project.properties`
- `docs/architecture.md`, `docs/DOCKER-CONNECTIVITY.md`, `docs/ai-usage.md`
- `.env.example`, `frontend/.env.example`
- `scripts/sonar.sh`, `scripts/test-coverage.sh`, `scripts/verify-docker-compose.ps1`
