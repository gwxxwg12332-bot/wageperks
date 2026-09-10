@echo off
rem ============================================================
rem  Wage's Perks Auto Test - one-click
rem  1. Sync Feishu pending-test tasks -> test_targets.txt
rem  2. Launch game -runtests -> auto test -> auto quit
rem  3. Copy report to fixed location
rem ============================================================
setlocal
set "PY=C:\Users\1\AppData\Local\Python\pythoncore-3.14-64\python.exe"
set "PROJ=D:\DoubaoWork\Project_001_WagesPerks\07_开发资产\ProbablyStolen_DevFiles\Mods_开发源码与临时文件\JacksonPerks"
set "GAME=D:\Steam\steamapps\common\Probably Stolen Playtest"
set "REPORT=%GAME%\TestReport"

echo ============================================
echo   Wage's Perks Auto Test
echo ============================================

echo [1/3] Sync Feishu test targets...
"%PY%" "%PROJ%\sync_test_targets.py"
if errorlevel 1 (
    echo [WARN] Feishu sync failed, using old targets
)

echo [2/3] Launch game and auto test...
powershell -ExecutionPolicy Bypass -File "%PROJ%\run_tests.ps1"
set "EXITCODE=%ERRORLEVEL%"

echo [3/3] Copy report...
if not exist "%REPORT%" mkdir "%REPORT%" 2>nul
set "STAMP=%date:~0,10%_%time:~0,2%%time:~3,2%%time:~6,2%"
set "STAMP=%STAMP: =0%"
copy /Y "%GAME%\Mods\test_results.txt" "%REPORT%\test_results_%STAMP%.txt" >nul 2>&1
copy /Y "%GAME%\Mods\test_results.txt" "%REPORT%\test_results_latest.txt" >nul 2>&1
copy /Y "%GAME%\Mods\test_targets.txt" "%REPORT%\test_targets_latest.txt" >nul 2>&1
echo Report saved to: %REPORT%
echo.

if "%EXITCODE%"=="0" (
    echo ==== ALL TESTS PASSED ====
) else (
    echo ==== SOME TESTS FAILED, see report ====
)
exit /b %EXITCODE%
