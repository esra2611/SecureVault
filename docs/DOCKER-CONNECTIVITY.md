# Docker connectivity and local startup

This document describes the Docker Compose setup and how services communicate. It reflects only what is implemented in the repository.

---

## 1. docker-compose services

| Service | Image / Build | Purpose |
|--------|----------------|---------|
| **postgres** | `postgres:15-alpine` | Primary database for Secrets and AuditLogs. |
| **redis** | `redis:7-alpine` | Distributed cache (secret TTL keys, rate limit counters). |
| **rabbitmq** | `rabbitmq:3-management-alpine` | Message broker for audit events (API → AuditConsumer). |
| **sonarqube-db** | `postgres:15-alpine` | Dedicated DB for SonarQube. |
| **sonarqube** | `sonarqube:lts` | Code analysis (optional; used by CI/scanner). |
| **api** | Build from `./src`, `SecureVault.Api/Dockerfile` | ASP.NET Core 9 API (secrets create/reveal, health). |
| **frontend** | Build from repo root, `frontend/Dockerfile` | Nuxt 3 app (submit secret, open link, view once). |
| **audit-consumer** | Build from `./src`, `Dockerfile.Consumer` | .NET 9 worker that consumes audit queue and writes to PostgreSQL. |
| **pgadmin** | `dpage/pgadmin4:latest` | Web UI for PostgreSQL (dev/admin). |

All services attach to the **securevault_net** bridge network. Inter-container resolution uses **service name as hostname** (e.g. `postgres`, `redis`, `api`).

---

## 2. Container dependencies and startup order

- **postgres, redis, rabbitmq:** No `depends_on`; start first. Each has a health check.
- **api:** `depends_on` postgres, redis, rabbitmq with `condition: service_healthy`. Runs migrations and optional schema ensure on startup.
- **frontend:** `depends_on` api with `condition: service_healthy`.
- **audit-consumer:** `depends_on` postgres and rabbitmq with `condition: service_healthy`.
- **sonarqube:** `depends_on` sonarqube-db with `condition: service_healthy`.
- **pgadmin:** `depends_on` postgres with `condition: service_healthy`.

Health checks:

- **postgres:** `pg_isready -U securevault` (interval 5s, timeout 5s, retries 5).
- **redis:** `redis-cli ping` (interval 5s, timeout 3s, retries 5).
- **rabbitmq:** `rabbitmq-diagnostics ping` (interval 10s, timeout 5s, retries 5, start_period 15s).
- **api:** `curl -sf http://localhost:8080/health` (interval 10s, timeout 5s, retries 5, start_period 15s).
- **frontend:** Node one-liner HTTP GET to `http://localhost:3000/` (interval 10s, timeout 5s, retries 5, start_period 15s).
- **sonarqube:** No health check in compose (comment in file: official image has no curl/wget; allow 2+ min for first start).

---

## 3. Networking

- **Network:** Single user-defined network `securevault_net` (driver: bridge). All listed services use it.
- **Ports (host → container):**
  - postgres: 5432
  - redis: 6379
  - rabbitmq: 5672 (AMQP), 15672 (management UI)
  - api: 8080
  - frontend: 3000
  - pgadmin: 5050 → 80
  - sonarqube: 9000

From the host, the API is at `http://localhost:8080`, frontend at `http://localhost:3000`. From another container on `securevault_net`, the API is at `http://api:8080`, Postgres at `postgres:5432`, etc.

---

## 4. Environment variables

**postgres**

- `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB` (defaults: securevault, `${POSTGRES_PASSWORD:-securevault}`, securevault).

**api**

- `ASPNETCORE_URLS`: `http://+:8080`
- `ConnectionStrings__DefaultConnection`: `Host=postgres;Port=5432;Database=securevault;Username=securevault;Password=...`
- `ConnectionStrings__Redis`: `redis:6379`
- `RabbitMQ__HostName`: `rabbitmq`, `RabbitMQ__Port`: `5672`
- `SecureVault__BaseUrl`: `${SECUREVAULT_BASE_URL:-http://localhost:3000}` (used for share links)
- `Cors__Origins`: `${CORS_ORIGINS:-http://localhost:3000}`
- `RunMigrations`: `"true"`
- `Encryption__MasterKeyBase64`: from env (compose default is a dev-only placeholder; production must set via `.env`)

**frontend**

- `NUXT_PUBLIC_API_BASE`: `${NUXT_PUBLIC_API_BASE:-http://localhost:8080}` (browser → API; must be reachable from client).
- `NUXT_API_BASE`: `http://api:8080` (SSR/server-side calls from frontend container → API by service name).

**audit-consumer**

- `ConnectionStrings__DefaultConnection`: same Postgres as API (`Host=postgres;Port=5432;...`)
- `RabbitMQ__HostName`: `rabbitmq`, `RabbitMQ__Port`: `5672`
- `RabbitMQ__Exchange`: `securevault.audit`
- `RabbitMQ__QueueName`: `securevault.audit.log`

**pgadmin**

- `PGADMIN_DEFAULT_EMAIL`: `${PGADMIN_DEFAULT_EMAIL:-admin@example.com}`
- `PGADMIN_DEFAULT_PASSWORD`: `${PGADMIN_DEFAULT_PASSWORD:-admin123}`

**sonarqube / sonarqube-db**

- `SONAR_*` and `SONAR_POSTGRES_*` for DB and JDBC URL; SonarQube connects to `sonarqube-db:5432`.

---

## 5. Local startup flow

1. Create `.env` from `.env.example` if needed; set at least `Encryption__MasterKeyBase64` for real use (compose has a dev default).
2. From repo root: `docker compose up -d` (or `docker compose up --build` for a full rebuild).
3. Compose starts postgres, redis, rabbitmq (and sonarqube-db); waits for their health.
4. API and audit-consumer start after postgres and rabbitmq (and redis for API) are healthy. API runs migrations / schema ensure; health check hits `/health`.
5. Frontend starts after API is healthy; it can call API at `http://api:8080` from inside the container (SSR).
6. Browser hits `http://localhost:3000` (frontend); frontend’s client-side code calls `NUXT_PUBLIC_API_BASE` (default `http://localhost:8080`) for create/reveal.

---

## 6. How services communicate

- **API → PostgreSQL:** Npgsql / EF Core; connection string from `ConnectionStrings__DefaultConnection` (`postgres:5432`). Used for Secrets and (via migrations/ensure) AuditLogs schema; API does not write to AuditLogs (audit-consumer does).
- **API → Redis:** StackExchange.Redis and `IDistributedCache`; connection from `ConnectionStrings__Redis` (`redis:6379`). Used for secret TTL keys (create path) and rate limiting (RateLimitMiddleware).
- **API → RabbitMQ:** Lazy connection via `RabbitMqAuditPublisher`; host/port from `RabbitMQ__HostName`/`RabbitMQ__Port` (`rabbitmq:5672`). Publishes audit events to exchange `securevault.audit`, routing key `audit.secret`. No dependency on RabbitMQ for startup (connection on first publish).
- **Frontend → API (browser):** Browser calls `NUXT_PUBLIC_API_BASE` (default `http://localhost:8080`). So from the user’s machine, requests go to host-mapped API port.
- **Frontend → API (SSR):** Inside the frontend container, server-side code uses `apiBaseInternal` from `NUXT_API_BASE` (`http://api:8080`) so SSR can reach the API over the Docker network.
- **Audit-consumer → RabbitMQ:** Connects to `rabbitmq:5672`; declares queue `RabbitMQ__QueueName` (e.g. `securevault.audit.log`), binds to `RabbitMQ__Exchange` with routing key `audit.secret`, consumes with manual ack.
- **Audit-consumer → PostgreSQL:** Raw Npgsql using `ConnectionStrings__DefaultConnection` (`postgres:5432`). Inserts into `AuditLogs` (same database as API; no EF in audit-consumer).

Health and readiness:

- **API:** `/health` returns 200 and is used by the compose health check. `/ready` runs health checks tagged `ready` (Npgsql and Redis); RabbitMQ is not part of readiness so API can start even if RabbitMQ is down (audit is best-effort).

---

## 7. Dockerfiles (summary)

- **API:** Multi-stage; SDK build, publish, then runtime image; installs `curl` for health check; runs as non-root user `app`; context `./src`, dockerfile `SecureVault.Api/Dockerfile`. Exposes 8080.
- **Frontend:** Multi-stage; Node 20 Alpine build (npm ci/install, npm run build), then runtime with `.output`; `HOST=0.0.0.0`, `PORT=3000`; non-root user `app`; context repo root, dockerfile `frontend/Dockerfile`. Build arg `NUXT_PUBLIC_API_BASE` for default client API URL.
- **Audit-consumer:** Multi-stage; SDK build, runtime image; context `./src`, dockerfile `Dockerfile.Consumer`. No curl; no health check in compose for this service.

---

## 8. Volumes

- `postgres_data`: Postgres data directory.
- `sonarqube_data`, `sonarqube_extensions`, `sonarqube_db_data`: SonarQube state and DB.
- `pgadmin_data`: PgAdmin state.

---

## Implementation verification

Source files used to derive this document:

- docker-compose.yml
- src/Dockerfile
- src/SecureVault.Api/Dockerfile
- src/Dockerfile.Consumer
- frontend/Dockerfile
- src/SecureVault.Api/Program.cs
- frontend/nuxt.config.ts
- src/SecureVault.Infrastructure/DependencyInjection.cs
- src/SecureVault.Infrastructure/Messaging/RabbitMqAuditPublisher.cs
- src/SecureVault.AuditConsumer/Program.cs
- src/SecureVault.AuditConsumer/AuditConsumerWorker.cs
- src/SecureVault.AuditConsumer/RabbitMqOptions.cs
