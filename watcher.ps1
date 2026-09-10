# watcher.ps1 - Wage's Perks 自测监视器
# 常驻后台，每 IntervalSeconds 秒检查 WagesPerks.dll 是否有更新（大小/时间戳变化），
# 检测到更新自动运行 run_test.bat（全自动自测：同步飞书任务 -> 开游戏 -> 测完自动关 -> 复制报告）。
# 用法: powershell -ExecutionPolicy Bypass -File watcher.ps1    (可设开机自启)
param(
    [int]$IntervalSeconds = 30,
    [string]$DllPath = "D:\Steam\steamapps\common\Probably Stolen Playtest\Mods\WagesPerks.dll",
    [string]$RunScript = "D:\DoubaoWork\Project_001_WagesPerks\07_开发资产\ProbablyStolen_DevFiles\Mods_开发源码与临时文件\JacksonPerks\run_test.bat"
)

$ErrorActionPreference = "SilentlyContinue"
$lastSize = -1
$lastMtime = ""

if (Test-Path $DllPath) {
    $f = Get-Item $DllPath
    $lastSize = $f.Length
    $lastMtime = $f.LastWriteTime.ToString("o")
    Write-Host "watcher 已启动: 当前 DLL 大小=$lastSize 时间=$($f.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))"
} else {
    Write-Host "watcher 已启动: 等待 DLL 出现 $DllPath"
}
Write-Host "每 ${IntervalSeconds}s 检测一次。放新 DLL 即自动开测自动关。Ctrl+C 退出。"

while ($true) {
    Start-Sleep -Seconds $IntervalSeconds

    if (-not (Test-Path $DllPath)) { continue }
    $f = Get-Item $DllPath
    $curSize = $f.Length
    $curMtime = $f.LastWriteTime.ToString("o")

    if ($curSize -ne $lastSize -or $curMtime -ne $lastMtime) {
        Write-Host ""
        Write-Host "[$(Get-Date -Format 'HH:mm:ss')] 检测到 DLL 更新 (新时间 $($f.LastWriteTime.ToString('HH:mm:ss'))) -> 自动自测..."
        $lastSize = $curSize
        $lastMtime = $curMtime

        Start-Process -FilePath $RunScript -Wait
        Write-Host "[$(Get-Date -Format 'HH:mm:ss')] 自测完成，报告已生成。等待下一次更新..."
    }
}
