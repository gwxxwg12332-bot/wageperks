# run_tests.ps1 - Automated test runner
# Starts game with -runtests, waits for completion, reads results
# Features: timeout kill + heartbeat watchdog

param(
    [int]$TimeoutSeconds = 360,
    [int]$HeartbeatTimeoutSeconds = 99999,
    [int]$StartupGraceSeconds = 60
)

$gameExe = "D:\Steam\steamapps\common\Probably Stolen Playtest\Probably Stolen.exe"
$gameDir = "D:\Steam\steamapps\common\Probably Stolen Playtest"
$resultFile = Join-Path $gameDir "Mods\test_results.txt"
$heartbeatFile = Join-Path $gameDir "Mods\test_heartbeat.txt"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Wage's Perks Automated Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Game: $gameExe"
Write-Host "Timeout: ${TimeoutSeconds}s"
Write-Host "Heartbeat timeout: ${HeartbeatTimeoutSeconds}s"
Write-Host ""

if (-not (Test-Path $gameExe)) {
    Write-Host "ERROR: Game exe not found: $gameExe" -ForegroundColor Red
    exit 1
}

# Clean old results
if (Test-Path $resultFile) { Remove-Item $resultFile -Force }
if (Test-Path $heartbeatFile) { Remove-Item $heartbeatFile -Force }

# Start game with -runtests
Write-Host "Starting game..." -ForegroundColor Yellow
$process = Start-Process -FilePath $gameExe -ArgumentList "-runtests" -WorkingDirectory $gameDir -PassThru
$gamePid = $process.Id
Write-Host "Game PID: $gamePid" -ForegroundColor Green
Write-Host ""

# Wait for exit, monitor heartbeat
$startTime = Get-Date
$lastHeartbeatTime = Get-Date
$timedOut = $false
$heartbeatDead = $false
$lastHeartbeatContent = ""
$gracePeriodEnded = $false

while (-not $process.HasExited) {
    $elapsed = (Get-Date) - $startTime

    # Check total timeout
    if ($elapsed.TotalSeconds -gt $TimeoutSeconds) {
        $timedOut = $true
        Write-Host ""
        Write-Host "TIMEOUT (${TimeoutSeconds}s), killing process..." -ForegroundColor Red
        break
    }

    # Check heartbeat (after startup grace time)
    if ($elapsed.TotalSeconds -gt $StartupGraceSeconds) {
        # Reset heartbeat time at the end of grace period to avoid false positive
        if (-not $gracePeriodEnded) {
            $lastHeartbeatTime = Get-Date
            $gracePeriodEnded = $true
            Write-Host "  Startup grace period ended, starting heartbeat monitoring..." -ForegroundColor Gray
        }

        if (Test-Path $heartbeatFile) {
            try {
                $heartbeatContent = Get-Content $heartbeatFile -Raw -ErrorAction SilentlyContinue
                if ($heartbeatContent -ne $lastHeartbeatContent) {
                    $lastHeartbeatContent = $heartbeatContent
                    $lastHeartbeatTime = Get-Date
                    $heartbeatPart = $heartbeatContent.Split('|')[0]
                    Write-Host "  Heartbeat: $heartbeatPart ($([math]::Round($elapsed.TotalSeconds,1))s)" -ForegroundColor Gray
                }
            } catch { }
        }

        # Check if heartbeat stopped
        $heartbeatAge = (Get-Date) - $lastHeartbeatTime
        if ($heartbeatAge.TotalSeconds -gt $HeartbeatTimeoutSeconds) {
            $heartbeatDead = $true
            Write-Host ""
            Write-Host "HEARTBEAT STOPPED (${HeartbeatTimeoutSeconds}s), killing process..." -ForegroundColor Red
            if ($lastHeartbeatContent) {
                Write-Host "  Last heartbeat: $lastHeartbeatContent" -ForegroundColor Yellow
            }
            break
        }
    }

    Start-Sleep -Milliseconds 500
}

# Force kill if timeout or heartbeat dead
if ($timedOut -or $heartbeatDead) {
    try {
        Stop-Process -Id $gamePid -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 500
        if (-not $process.HasExited) {
            taskkill /F /PID $gamePid /T 2>$null
        }
        Write-Host "Process killed" -ForegroundColor Red
    } catch {
        Write-Host "Failed to kill process: $_" -ForegroundColor Red
    }
}

try { $process.WaitForExit(5000) | Out-Null } catch { }

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Test Results" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if (Test-Path $resultFile) {
    $results = Get-Content $resultFile -Encoding UTF8
    Write-Host ""
    Write-Host "--- Details ---" -ForegroundColor Yellow
    Write-Host $results
    Write-Host ""

    $passCount = ($results | Select-String "PASS").Count
    $failCount = ($results | Select-String "FAIL").Count
    $skipCount = ($results | Select-String "SKIP").Count

    Write-Host "--- Summary ---" -ForegroundColor Yellow
    Write-Host "Passed: $passCount" -ForegroundColor Green
    Write-Host "Failed: $failCount" -ForegroundColor $(if ($failCount -gt 0) { "Red" } else { "Green" })
    if ($skipCount -gt 0) { Write-Host "Skipped: $skipCount" -ForegroundColor Yellow }

    if ($timedOut) {
        Write-Host "Status: TIMEOUT (killed)" -ForegroundColor Red
    } elseif ($heartbeatDead) {
        Write-Host "Status: HEARTBEAT DEAD (stuck)" -ForegroundColor Red
    } else {
        Write-Host "Status: COMPLETED" -ForegroundColor Green
    }

    if ($failCount -gt 0) { exit 1 } else { exit 0 }
} else {
    Write-Host "ERROR: Result file not found: $resultFile" -ForegroundColor Red
    Write-Host "Tests may not have executed properly" -ForegroundColor Red
    exit 1
}
