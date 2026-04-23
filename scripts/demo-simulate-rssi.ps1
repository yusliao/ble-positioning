# 无硬件客户演示：按路径损耗模型合成 RSSI 并循环 POST 到 /api/v1/rssi/report
# 需至少 3 个信标（与 API PositioningOptions.MinBeaconsRequired 一致）。
# 用法（仓库根目录）:
#   pwsh -File scripts/demo-simulate-rssi.ps1 -Config doc/demo-simulate-config.json
# Ctrl+C 停止。

param(
    [Parameter(Mandatory = $true)]
    [string] $Config
)

$ErrorActionPreference = "Stop"

$full = Resolve-Path -LiteralPath $Config
$json = Get-Content -LiteralPath $full -Raw -Encoding UTF8 | ConvertFrom-Json

$apiBase = $json.apiBase.Trim().TrimEnd("/")
$deviceId = [Guid]::Parse($json.deviceId)
$apiKey = [string]$json.apiKey
$intervalMs = [int]($json.intervalMs)
if ($intervalMs -lt 200) { $intervalMs = 200 }
$n = [double]($json.pathLossExponent)
if ($n -le 0) { $n = 2.0 }

$from = $json.path.from
$to = $json.path.to
$fx = [double]$from[0]; $fy = [double]$from[1]
$tx = [double]$to[0]; $ty = [double]$to[1]
$roundTrip = [double]$json.path.roundTripSeconds
if ($roundTrip -lt 1) { $roundTrip = 30 }

$beacons = @()
foreach ($b in $json.beacons) {
    $beacons += [pscustomobject]@{
        Uuid   = [string]$b.uuid
        Major  = [int]$b.major
        Minor  = [int]$b.minor
        X      = [double]$b.x
        Y      = [double]$b.y
        TxPower = [int]$b.txPower
    }
}
if ($beacons.Count -lt 3) {
    throw "Configuration must include at least 3 beacons (server MinBeaconsRequired)."
}

function Get-RssiFromDistance([double] $dMeters, [int] $txPower, [double] $pathN) {
    $d = [math]::Max(0.1, $dMeters)
    $r = $txPower - 10.0 * $pathN * [math]::Log10($d)
    $ri = [int][math]::Round($r)
    if ($ri -gt 0) { $ri = 0 }
    if ($ri -lt -100) { $ri = -100 }
    return $ri
}

$uri = "$apiBase/api/v1/rssi/report"
$headers = @{
    "X-Api-Key"   = $apiKey
    "Content-Type" = "application/json; charset=utf-8"
}

$start = [datetime]::UtcNow
Write-Host "Posting to $uri every ${intervalMs}ms. Ctrl+C to stop."
try {
    while ($true) {
        $elapsed = ([datetime]::UtcNow - $start).TotalSeconds
        # 0..1..0 三角波，沿 from->to 插值
        $phase = ($elapsed % $roundTrip) / $roundTrip
        if ($phase -le 0.5) { $k = 2.0 * $phase } else { $k = 2.0 * (1.0 - $phase) }
        $px = $fx + $k * ($tx - $fx)
        $py = $fy + $k * ($ty - $fy)

        $signalObjs = [System.Collections.Generic.List[object]]::new()
        foreach ($b in $beacons) {
            $dx = $px - $b.X
            $dy = $py - $b.Y
            $d = [math]::Sqrt($dx * $dx + $dy * $dy)
            $rssi = Get-RssiFromDistance $d $b.TxPower $n
            $signalObjs.Add([pscustomobject]@{
                uuid  = $b.Uuid
                major = $b.Major
                minor = $b.Minor
                rssi  = $rssi
            })
        }

        $bodyObj = [pscustomobject]@{
            deviceId  = $deviceId.ToString("D")
            signals   = $signalObjs
            timestamp = [datetime]::UtcNow.ToString("o")
        }
        $body = $bodyObj | ConvertTo-Json -Depth 5 -Compress

        try {
            $resp = Invoke-WebRequest -Uri $uri -Method Post -Headers $headers -Body $body -UseBasicParsing
            if ($resp.StatusCode -ne 202) {
                Write-Warning "Unexpected status: $($resp.StatusCode)"
            }
        }
        catch {
            Write-Warning "POST failed: $($_.Exception.Message)"
        }

        Start-Sleep -Milliseconds $intervalMs
    }
}
finally {
    Write-Host "Stopped."
}
