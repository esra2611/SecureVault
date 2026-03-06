# Verification script for docker-compose stack. Run from repo root with Docker installed.
# Usage: .\scripts\verify-docker-compose.ps1

$ErrorActionPreference = "Stop"
$api = "http://localhost:8080"
$frontend = "http://localhost:3000"
$rabbitMgmt = "http://localhost:15672"
$sonar = "http://localhost:9000"

Write-Host "=== 1. Starting stack ===" -ForegroundColor Cyan
docker compose up -d
Start-Sleep -Seconds 5

Write-Host "`n=== 2. Service status ===" -ForegroundColor Cyan
docker compose ps

Write-Host "`n=== 3. API /health (liveness) ===" -ForegroundColor Cyan
try {
    $r = Invoke-WebRequest -Uri "$api/health" -UseBasicParsing -TimeoutSec 5
    Write-Host "OK $($r.StatusCode) $($r.Content)"
} catch { Write-Host "FAIL: $_" -ForegroundColor Red }

Write-Host "`n=== 4. API /ready (readiness) ===" -ForegroundColor Cyan
try {
    $r = Invoke-WebRequest -Uri "$api/ready" -UseBasicParsing -TimeoutSec 5
    Write-Host "OK $($r.StatusCode)"
} catch { Write-Host "FAIL: $_" -ForegroundColor Red }

Write-Host "`n=== 5. Create secret (minimal) ===" -ForegroundColor Cyan
try {
    $body = '{"plaintext":"verify-test","expiry":"1h"}'
    $r = Invoke-WebRequest -Uri "$api/api/secrets" -Method POST -ContentType "application/json" -Body $body -UseBasicParsing -TimeoutSec 10
    $json = $r.Content | ConvertFrom-Json
    Write-Host "OK Created; shareUrl present: $($null -ne $json.shareUrl)"
} catch { Write-Host "FAIL: $_" -ForegroundColor Red }

Write-Host "`n=== 6. Redis ping (exec) ===" -ForegroundColor Cyan
docker compose exec -T redis redis-cli ping 2>$null
if ($LASTEXITCODE -eq 0) { Write-Host "OK" } else { Write-Host "FAIL or redis not running" -ForegroundColor Red }

Write-Host "`n=== 7. RabbitMQ management UI ===" -ForegroundColor Cyan
try {
    $r = Invoke-WebRequest -Uri $rabbitMgmt -UseBasicParsing -TimeoutSec 5
    Write-Host "OK $($r.StatusCode)"
} catch { Write-Host "FAIL: $_" -ForegroundColor Red }

Write-Host "`n=== 8. SonarQube (may be slow first time) ===" -ForegroundColor Cyan
try {
    $r = Invoke-WebRequest -Uri "$sonar/api/system/status" -UseBasicParsing -TimeoutSec 10
    Write-Host "OK $($r.StatusCode)"
} catch { Write-Host "WARN or not ready: $_" -ForegroundColor Yellow }

Write-Host "`n=== 9. Frontend ===" -ForegroundColor Cyan
try {
    $r = Invoke-WebRequest -Uri $frontend -UseBasicParsing -TimeoutSec 5
    Write-Host "OK $($r.StatusCode)"
} catch { Write-Host "FAIL: $_" -ForegroundColor Red }

Write-Host "`n=== Done ===" -ForegroundColor Green
