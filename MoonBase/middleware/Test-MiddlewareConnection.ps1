[CmdletBinding()]
param(
    [string]$HostName = '127.0.0.1',
    [int]$Port = 9999,
    [switch]$ExecuteMotion
)

$ErrorActionPreference = 'Stop'

Write-Host "检查中间件 TCP 监听: $HostName`:$Port" -ForegroundColor Cyan
$probe = Test-NetConnection -ComputerName $HostName -Port $Port -WarningAction SilentlyContinue

if (-not $probe.TcpTestSucceeded) {
    Write-Host 'FAIL: TCP 端口不可达。请先启动 MoonBase\start_middleware.bat。' -ForegroundColor Red
    exit 1
}

Write-Host 'PASS: Unity 应连接到该 TCP 端口。' -ForegroundColor Green
Write-Host '当前只完成端口探测，没有发送任何运动指令。' -ForegroundColor Yellow

if (-not $ExecuteMotion) {
    Write-Host '如已清空平台周围区域并确认急停可用，再追加 -ExecuteMotion 执行最小运动测试。' -ForegroundColor Yellow
    exit 0
}

Write-Host '即将发送 Zero、±1 度小幅俯仰和最终 Zero。请确认实体平台周围无人且急停可用。' -ForegroundColor Yellow
$answer = Read-Host '输入 MOVE 才继续'
if ($answer -cne 'MOVE') {
    Write-Host '已取消，未发送运动指令。' -ForegroundColor DarkYellow
    exit 2
}

function Send-Command {
    param([Parameter(Mandatory = $true)][string]$Message)
    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $client.Connect($HostName, $Port)
        $stream = $client.GetStream()
        $bytes = [System.Text.Encoding]::ASCII.GetBytes($Message)
        $stream.Write($bytes, 0, $bytes.Length)
        Write-Host "发送: $Message" -ForegroundColor Green
    }
    finally {
        if ($stream) { $stream.Dispose() }
        $client.Dispose()
    }
}

Send-Command 'Zero#end'
Start-Sleep -Seconds 2
Send-Command 'Runing#1#0#0#0#0#0#0#0#500#end'
Start-Sleep -Seconds 2
Send-Command 'Runing#-1#0#0#0#0#0#0#0#500#end'
Start-Sleep -Seconds 2
Send-Command 'Zero#end'
Write-Host '实体最小运动测试完成，已发送最终回零。' -ForegroundColor Green
