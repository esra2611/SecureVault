#!/usr/bin/env bash
# Run unit tests with Coverlet, then ReportGenerator. Reproduces CI coverage locally.
# Usage: ./scripts/test-coverage.sh   (from repo root)
# Requires: dotnet, reportgenerator (dotnet tool install -g dotnet-reportgenerator-globaltool)

set -e
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$REPO_ROOT"

echo "=== Unit tests with coverage (Domain + Application only) ==="
dotnet test src/SecureVault.Tests.Unit/SecureVault.Tests.Unit.csproj -c Release \
  --collect:"XPlat Code Coverage" \
  --settings src/SecureVault.Tests.Unit/coverage.runsettings \
  --results-directory coverage

echo ""
echo "=== Merge coverage and generate report ==="
COVERAGE_DIR="coverage/report"
mkdir -p "$COVERAGE_DIR"
REPORTS=$(find coverage -name "coverage.opencover.xml" 2>/dev/null | tr '\n' ';' | sed 's/;$//')
if [ -z "$REPORTS" ]; then
  echo "No coverage files found."
  exit 1
fi
reportgenerator "-reports:$REPORTS" "-targetdir:$COVERAGE_DIR" "-reporttypes:TextSummary;Html"

echo ""
echo "=== Summary ==="
cat "$COVERAGE_DIR/Summary.txt"
