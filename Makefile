# SecureVault – run from repo root
# test-coverage: run unit tests with Coverlet (Domain + Application only), then ReportGenerator
# sonar: start local SonarQube, wait for ready, run backend + frontend analysis (requires SONAR_TOKEN)

.PHONY: test-coverage sonar

test-coverage:
	./scripts/test-coverage.sh

sonar:
	./scripts/sonar.sh
