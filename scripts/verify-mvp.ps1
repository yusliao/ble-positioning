# 依赖：本机已安装 Docker Desktop
# 用法：在仓库根目录执行: pwsh -File scripts/verify-mvp.ps1
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot/..

try {
  docker compose up -d --build
} catch {
  Write-Error "docker compose 失败。若提示 project name，请使用仓库内已配置 name: blepositioning 的 docker-compose.yml"
  throw
}

Start-Sleep -Seconds 20

$base = "http://localhost:5000"
Write-Host "== GET $base/health =="
Invoke-RestMethod -Uri "$base/health" -Method Get

Write-Host "`n== GET $base/health/ready =="
try {
  $r = Invoke-RestMethod -Uri "$base/health/ready" -Method Get
  $r | ConvertTo-Json -Depth 3
} catch {
  Write-Warning "Readiness 非 200：$($_.Exception.Message)（可稍后重试，等待 API 与依赖就绪）"
}

Write-Host "`n完成。Admin UI: http://localhost:5001  ； API Swagger: $base/swagger"
