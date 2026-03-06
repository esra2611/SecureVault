# Running SonarQube analysis locally

The script does **not** start Docker. Have SonarQube running before you run the script.

1. **Start SonarQube** (if needed):
   ```bash
   docker compose up -d sonarqube-db sonarqube
   ```
   Wait until the UI is up (e.g. http://localhost:9000).

2. **Create a token** in SonarQube: **My Account → Security → Generate Tokens**.

3. **Set the token** (do not commit or log it):
   ```bash
   export SONAR_TOKEN=your_token
   ```

4. **Run the script** from the repo root:
   ```bash
   ./scripts/sonar.sh
   ```

The script will: install/check `dotnet-sonarscanner`, run **begin → build → test (with coverage) → end** for the backend, then run the frontend scanner. Coverage and TRX are written under `TestResults/` at the repo root. At the end it prints found coverage and TRX paths and exits with an error if none are found.

Optional: run tests only (no Sonar):
```bash
dotnet test src/SecureVault.Tests.Unit/SecureVault.Tests.Unit.csproj -c Release --collect:"XPlat Code Coverage" --logger "trx" --results-directory TestResults --settings src/SecureVault.Tests.Unit/coverage.runsettings
```
