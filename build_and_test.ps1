# build_and_test.ps1 - 编译 + 自动化测试
param([int]$TimeoutSeconds = 120)

$projectDir = "D:\DoubaoWork\Project_001_WagesPerks\07_开发资产\ProbablyStolen_DevFiles\Mods_开发源码与临时文件\JacksonPerks"
$sourceDll = Join-Path $projectDir "bin\Release\net6.0\JacksonPerks.dll"
$targetDll = "D:\Steam\steamapps\common\Probably Stolen Playtest\Mods\WagesPerks.dll"
$runTestsScript = Join-Path $projectDir "run_tests.ps1"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Build + Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Step 1: Build
Write-Host "[1/3] Building..." -ForegroundColor Yellow
Set-Location $projectDir
dotnet build -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build FAILED" -ForegroundColor Red
    exit 1
}
Write-Host "Build OK" -ForegroundColor Green

# Step 2: Deploy
Write-Host "[2/3] Deploying DLL..." -ForegroundColor Yellow
if (-not (Test-Path $sourceDll)) {
    Write-Host "Source DLL not found: $sourceDll" -ForegroundColor Red
    exit 1
}
Copy-Item -Path $sourceDll -Destination $targetDll -Force
Write-Host "Deployed: $targetDll" -ForegroundColor Green

# Step 3: Run tests
Write-Host "[3/3] Running tests..." -ForegroundColor Yellow
& $runTestsScript -TimeoutSeconds 120 -HeartbeatTimeoutSeconds 15
$testExitCode = $LASTEXITCODE

Write-Host ""
if ($testExitCode -eq 0) {
    Write-Host "ALL TESTS PASSED" -ForegroundColor Green
} else {
    Write-Host "SOME TESTS FAILED" -ForegroundColor Red
}

exit $testExitCode
