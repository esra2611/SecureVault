# .gitignore Audit Report

This document summarizes the repository’s `.gitignore` configuration and what to remove from Git tracking before pushing to GitHub. It was produced as part of a pre-push DevOps review.

---

## 1. Summary

- **Root `.gitignore`** has been expanded to cover .NET, Node, Docker, IDE, OS, coverage, Playwright, Stryker, and temporary artifacts.
- **Required files** (docker-compose, Sonar, CI workflows, docs, `coverage.runsettings`) are **not** ignored and remain committed.
- The repository was **not** a Git repo at audit time; the “files to remove from tracking” list is for use **after** `git init` / first `git add` if any of these paths were ever added.

---

## 2. Files / Paths to Remove from Git Tracking

If any of the following were ever committed, remove them from the index **without** deleting from disk:

```powershell
# Run from repository root (PowerShell). Only run for paths that exist and are tracked.

git rm -r --cached src/**/bin 2>$null; git rm -r --cached src/**/obj 2>$null
git rm -r --cached **/node_modules 2>$null
git rm -r --cached .env 2>$null
git rm -r --cached TestResults 2>$null
git rm -r --cached src/**/TestResults 2>$null
git rm -r --cached coverage 2>$null
git rm -r --cached coverage-out 2>$null
git rm -r --cached coverage-runsettings 2>$null
git rm -r --cached src/**/StrykerOutput 2>$null
git rm -r --cached stryker-report 2>$null
git rm -r --cached playwright-report 2>$null
git rm -r --cached frontend/playwright-report 2>$null
git rm -r --cached test-results 2>$null
git rm -r --cached frontend/test-results 2>$null
git rm -r --cached src/.vs 2>$null
git rm -r --cached src/.sonarqube 2>$null
git rm --cached "src/SecureVault.slnLaunch.user" 2>$null
git rm --cached "src/SecureVault.Api/SecureVault.Api.csproj.user" 2>$null
```

Or remove by directory (Bash-style; adjust for your shell):

```bash
git rm -r --cached src/*/bin src/*/obj 2>/dev/null || true
git rm -r --cached .env TestResults coverage coverage-out coverage-runsettings 2>/dev/null || true
git rm -r --cached src/SecureVault.Tests.Unit/StrykerOutput stryker-report 2>/dev/null || true
git rm -r --cached playwright-report frontend/playwright-report test-results frontend/test-results 2>/dev/null || true
git rm -r --cached src/.vs src/.sonarqube 2>/dev/null || true
git rm --cached 'src/*.user' 'src/*/*.user' 2>/dev/null || true
```

After running, commit the change: `git add .gitignore docs/GITIGNORE-AUDIT.md` and `git commit -m "chore: tighten .gitignore and stop tracking build/IDE artifacts"`.

---

## 3. Why Each Rule Exists

| Category | Pattern | Reason |
|----------|---------|--------|
| **Secrets / env** | `.env`, `**/.env` | Local env can contain keys and secrets; only `.env.example` is committed as a template. |
| **Secrets / config** | `appsettings.Production.json`, `appsettings.*.local.json` | Overrides may contain connection strings and secrets; baseline config is tracked elsewhere. |
| **.NET build** | `bin/`, `obj/`, `out/`, `**/packages/` | Build and NuGet output; reproducible with `dotnet restore` and `dotnet build`. |
| **.NET IDE** | `*.user`, `*.suo`, `*.userosscache`, `.vs/`, `.idea/` | Per-developer and machine-specific; not needed in repo. |
| **Node** | `node_modules/` | Dependencies; reproducible with `npm ci` / `yarn install`. |
| **Frontend build** | `.output`, `.nuxt`, `.nitro`, `.cache`, `dist/` | Nuxt/Vite build output; reproducible from source. |
| **Test results** | `TestResults/`, `**/TestResults/` | Test and coverage output; CI produces and may upload as artifacts. Sonar reads from here in CI; no need to commit. |
| **Coverage** | `coverage/`, `coverage-out/`, `coverage-runsettings/`, `*.trx`, `*.coverage`, `*.coveragexml` | Coverage and test logs; CI generates and uploads reports. |
| **Stryker** | `**/StrykerOutput/`, `stryker-report/` | Mutation test output; CI runs Stryker and uploads artifacts. |
| **Playwright** | `playwright-report/`, `test-results/` | E2E reports and traces; CI uploads as artifacts. |
| **Sonar (local)** | `.sonarqube/` | Local Sonar scanner cache; CI uses root `sonar-project.properties` only. |
| **OS** | `.DS_Store`, `Thumbs.db`, `Desktop.ini` | OS metadata; not part of source. |
| **Logs / temp** | `logs/`, `*.log`, `*.tmp`, `*.temp`, `tmp/`, `temp/` | Logs and temp files; not for version control. |

---

## 4. What Must Stay Committed

These are **not** ignored and must remain in the repository:

- `docker-compose.yml` – required for CI and local services.
- `sonar-project.properties` (at repo root) – used by CI and Sonar.
- `.github/workflows/*.yml` – CI pipeline.
- `docs/*` – documentation (including this audit).
- `src/SecureVault.Tests.Unit/coverage.runsettings` – coverage configuration (single file).
- `frontend/.gitignore` – frontend-specific ignores (optional but recommended).
- Source code, solution and project files, and app config templates (e.g. `appsettings*.example.json`, `.env.example`).

Coverage and Stryker **reports** are produced and uploaded as **CI artifacts**; they are correctly ignored in the repo. If an assignment explicitly requires a committed coverage or Stryker report, add a narrow exception in `.gitignore` (e.g. `!coverage/report/` or a specific path) as noted in the root `.gitignore`.

---

## 5. CI and Docker

- **CI** (`.github/workflows/ci.yml`) uses `docker compose`, root `sonar-project.properties`, and writes to `coverage/`, `TestResults/`, `StrykerOutput/`, and `frontend/playwright-report/` on the runner. None of these need to be committed.
- **Docker** (e.g. `docker compose up`) is unchanged; `.dockerignore` is separate and only affects build context, not Git.

No changes were made that break `docker compose up` or the CI pipeline.
