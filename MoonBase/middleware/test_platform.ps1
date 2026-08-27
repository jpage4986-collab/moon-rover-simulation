# 汇鼎Mbox100 平台测试脚本
# 使用方法: 先启动中间件，再打开另一个PS窗口运行此脚本

$host.UI.RawUI.WindowTitle = "Mbox100 测试控制台"

function Send-Cmd {
    param([string]$msg)
    try {
        $c = New-Object System.Net.Sockets.TcpClient("127.0.0.1", 9999)
        $s = $c.GetStream()
        $d = [System.Text.Encoding]::ASCII.GetBytes($msg)
        $s.Write($d, 0, $d.Length)
        Write-Host "  → $msg" -ForegroundColor Green
        $s.Close(); $c.Close()
    } catch {
        Write-Host "  ✗ 发送失败! 中间件是否在运行?" -ForegroundColor Red
        Write-Host "  $_" -ForegroundColor Red
    }
}

Write-Host "==================================" -ForegroundColor Cyan
Write-Host "  汇鼎 Mbox100 运动平台测试" -ForegroundColor Cyan
Write-Host "  确保中间件已在另一个窗口运行" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan
Write-Host ""

# 测试回零
Write-Host "[1/4] 回零..." -ForegroundColor Yellow
Send-Cmd "Zero#end"
Start-Sleep 2

# 测试俯仰 (抬头5度)
Write-Host "[2/4] 俯仰5度 (持续1秒)..." -ForegroundColor Yellow
Send-Cmd "Runing#5#0#0#0#0#0#0#0#1000#end"
Start-Sleep 2

# 测试回零
Write-Host "[3/4] 回零..." -ForegroundColor Yellow
Send-Cmd "Zero#end"
Start-Sleep 1

# 测试升降50mm
Write-Host "[4/4] 升降50mm (持续1秒)..." -ForegroundColor Yellow
Send-Cmd "Runing#0#0#0#0#0#50#0#0#1000#end"
Start-Sleep 2

# 最终回零
Send-Cmd "Zero#end"
Write-Host ""
Write-Host "测试完成" -ForegroundColor Green
Write-Host "如果平台没有动，请告诉我，我调整方案" -ForegroundColor Cyan
