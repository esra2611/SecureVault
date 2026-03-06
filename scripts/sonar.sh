#!/usr/bin/env bash
# Run SonarQube analysis for backend (.NET) and frontend (Nuxt).
# Does NOT start Docker: ensure SonarQube is already running (e.g. docker compose up -d sonarqube-db sonarqube).
#
# Root cause (0% coverage): SonarScanner runs on HOST with CWD=src/; report paths must be
# relative to src/ (../TestResults/...) so the scanner finds coverage/TRX under repo root.
# Coverage/TRX are written to TestResults/<ProjectName>/ to avoid overwrites and match globs.
#
# Local run (from repo root):
#   1. Start SonarQube (if needed): docker compose up -d sonarqube-db sonarqube
#   2. Create token: SonarQube UI → My Account → Security → Generate Tokens
#   3. Export SONAR_TOKEN=your_token
#   4. Run: ./scripts/sonar.sh
#
# Usage: ./scripts/sonar.sh   (from repo root; requires SONAR_TOKEN in env)
# Prerequisites: .NET 9 SDK, Node (for frontend). Works on Windows (Git Bash) and CI.

set -e
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
# Always run from repo root so paths and finds are predictable (avoids analyzing wrong folder e.g. "SecureVault - Kopya (3)")
cd "$REPO_ROOT"
echo "[diagnostics] repo root: $REPO_ROOT"

SONAR_HOST="${SONAR_HOST_URL:-http://localhost:9000}"

if [ -z "${SONAR_TOKEN:-}" ]; then
  echo "Error: SONAR_TOKEN is not set. Create a token in SonarQube: $SONAR_HOST → Log in → My Account → Security → Generate Tokens."
  exit 1
fi

# Deterministic coverage/TRX from unit and integration only (no /coverage, no stale paths).
UNIT_RESULTS_DIR="$REPO_ROOT/TestResults/SecureVault.Tests.Unit"
INTEGRATION_RESULTS_DIR="$REPO_ROOT/TestResults/SecureVault.Tests.Integration"
rm -rf "$UNIT_RESULTS_DIR" "$INTEGRATION_RESULTS_DIR"
mkdir -p "$UNIT_RESULTS_DIR" "$INTEGRATION_RESULTS_DIR"

echo "=== Ensuring dotnet-sonarscanner is installed (< 11 for SonarQube 9.x) ==="
SONARSCANNER_VERSION="10.4.1"
if dotnet tool list -g 2>/dev/null | grep -q dotnet-sonarscanner; then
  CURRENT_VER=$(dotnet tool list -g | grep dotnet-sonarscanner | awk '{print $2}')
  case "$CURRENT_VER" in 11.*) dotnet tool uninstall -g dotnet-sonarscanner ;; esac
fi
if ! dotnet tool list -g 2>/dev/null | grep -q dotnet-sonarscanner; then
  dotnet tool install -g dotnet-sonarscanner --version "$SONARSCANNER_VERSION"
fi
export PATH="$HOME/.dotnet/tools:$PATH"

echo "=== Backend analysis (dotnet-sonarscanner): begin → build → test → end (all on host, no Docker) ==="
export MSYS2_ARG_CONV_EXCL="*"
export MSYS_NO_PATHCONV=1

# --- SAFE diagnostics: pwd and report paths only (no file contents, no secrets) ---
echo "[diagnostics] pwd before sonar begin: $(pwd)"
cd "$REPO_ROOT/src"
echo "[diagnostics] pwd for begin/build/test/end: $(pwd)"

# Sonar imports coverage and TRX from both unit and integration (paths relative to src/).
OPENCOVER_REPORTS="../TestResults/SecureVault.Tests.Unit/**/coverage.opencover.xml,../TestResults/SecureVault.Tests.Integration/**/coverage.opencover.xml"
VSTEST_REPORTS="../TestResults/SecureVault.Tests.Unit/**/*.trx,../TestResults/SecureVault.Tests.Integration/**/*.trx"
echo "[diagnostics] sonar.cs.opencover.reportsPaths=$OPENCOVER_REPORTS"
echo "[diagnostics] sonar.cs.vstest.reportsPaths=$VSTEST_REPORTS"
echo "[diagnostics] files under TestResults (before test):"
find "$UNIT_RESULTS_DIR" "$INTEGRATION_RESULTS_DIR" -name "coverage.opencover.xml" 2>/dev/null || true
find "$UNIT_RESULTS_DIR" "$INTEGRATION_RESULTS_DIR" -name "*.trx" 2>/dev/null || true

# Begin: coverage/TRX paths relative to src/ (scanner CWD is src/)
dotnet-sonarscanner begin /key:"SecureVault" /name:"SecureVault" /d:sonar.host.url="$SONAR_HOST" /d:sonar.login="$SONAR_TOKEN" \
  "/d:sonar.cs.opencover.reportsPaths=$OPENCOVER_REPORTS" \
  "/d:sonar.cs.vstest.reportsPaths=$VSTEST_REPORTS"

dotnet restore SecureVault.sln
dotnet build SecureVault.sln -c Release --no-restore

echo "[diagnostics] pwd before dotnet test: $(pwd)"

# Unit tests: OpenCover + TRX under TestResults/SecureVault.Tests.Unit (format from coverage.runsettings).
dotnet test SecureVault.Tests.Unit/SecureVault.Tests.Unit.csproj -c Release --no-build \
  --collect:"XPlat Code Coverage" \
  --settings SecureVault.Tests.Unit/coverage.runsettings \
  --results-directory "../TestResults/SecureVault.Tests.Unit" \
  --logger "trx;LogFileName=unit.trx"

# Integration tests: OpenCover + TRX under TestResults/SecureVault.Tests.Integration (same runsettings for OpenCover format).
dotnet test SecureVault.Tests.Integration/SecureVault.Tests.Integration.csproj -c Release --no-build \
  --collect:"XPlat Code Coverage" \
  --settings SecureVault.Tests.Unit/coverage.runsettings \
  --results-directory "../TestResults/SecureVault.Tests.Integration" \
  --logger "trx;LogFileName=integration.trx"

echo "[diagnostics] pwd before sonar end: $(pwd)"
echo "[diagnostics] sonar.cs.opencover.reportsPaths=$OPENCOVER_REPORTS"
echo "[diagnostics] sonar.cs.vstest.reportsPaths=$VSTEST_REPORTS"
echo "[diagnostics] matching files under TestResults/SecureVault.Tests.Unit:"
find "$UNIT_RESULTS_DIR" -name "coverage.opencover.xml" 2>/dev/null || true
find "$UNIT_RESULTS_DIR" -name "*.trx" 2>/dev/null || true
echo "[diagnostics] matching files under TestResults/SecureVault.Tests.Integration:"
find "$INTEGRATION_RESULTS_DIR" -name "coverage.opencover.xml" 2>/dev/null || true
find "$INTEGRATION_RESULTS_DIR" -name "*.trx" 2>/dev/null || true

dotnet-sonarscanner end /d:sonar.login="$SONAR_TOKEN"
cd "$REPO_ROOT"
echo "[diagnostics] pwd after end (verification from repo root): $(pwd)"

echo "=== Frontend analysis (sonar-scanner) ==="
npx --yes sonar-scanner \
  -Dsonar.projectKey=SecureVault:frontend \
  -Dsonar.projectName=SecureVault-Frontend \
  -Dsonar.sources=frontend \
  -Dsonar.host.url="$SONAR_HOST" \
  -Dsonar.token="$SONAR_TOKEN" \
  -Dsonar.exclusions="**/node_modules/**,**/.output/**,**/dist/**,**/coverage/**,**/.nuxt/**,**/.vuepress/**,**/nitro.json"

# --- Verification: unit and integration coverage + TRX under TestResults ---
echo "=== Verification (unit + integration) ==="
echo "Unit coverage:"
find "$UNIT_RESULTS_DIR" -name "coverage.opencover.xml" 2>/dev/null || true
echo "Unit TRX:"
find "$UNIT_RESULTS_DIR" -name "*.trx" 2>/dev/null || true
echo "Integration coverage:"
find "$INTEGRATION_RESULTS_DIR" -name "coverage.opencover.xml" 2>/dev/null || true
echo "Integration TRX:"
find "$INTEGRATION_RESULTS_DIR" -name "*.trx" 2>/dev/null || true

HAS_UNIT_OPENCOVER=$(find "$UNIT_RESULTS_DIR" -name "coverage.opencover.xml" 2>/dev/null | head -1)
HAS_UNIT_TRX=$(find "$UNIT_RESULTS_DIR" -name "*.trx" 2>/dev/null | head -1)
HAS_INT_OPENCOVER=$(find "$INTEGRATION_RESULTS_DIR" -name "coverage.opencover.xml" 2>/dev/null | head -1)
HAS_INT_TRX=$(find "$INTEGRATION_RESULTS_DIR" -name "*.trx" 2>/dev/null | head -1)
if [ -z "$HAS_UNIT_OPENCOVER" ]; then
  echo "Error: No unit coverage (coverage.opencover.xml) under TestResults/SecureVault.Tests.Unit. Sonar coverage will be incomplete."
  exit 1
fi
if [ -z "$HAS_UNIT_TRX" ]; then
  echo "Error: No unit TRX under TestResults/SecureVault.Tests.Unit."
  exit 1
fi
if [ -z "$HAS_INT_OPENCOVER" ]; then
  echo "Error: No integration coverage (coverage.opencover.xml) under TestResults/SecureVault.Tests.Integration. Sonar coverage will be incomplete."
  exit 1
fi
if [ -z "$HAS_INT_TRX" ]; then
  echo "Error: No integration TRX under TestResults/SecureVault.Tests.Integration."
  exit 1
fi

echo "=== Done. Open $SONAR_HOST/dashboard?id=SecureVault ==="
