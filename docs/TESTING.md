# 运行与联调

## Unity 场景

1. 用 Unity Hub 打开 `Assets (2)`，打开 `Assets/Scenes/SampleScene.unity` 并进入 Play。
2. 使用 `1`、`2`、`3` 或场景按钮切换手动、局部导航、混合导航模式。
3. 在手动模式下，底座控制器按钮 `1` 切换方向盘/手柄输入，HUD 显示当前输入模式。
4. 按底座控制器按钮 `11` 检查车辆灯光切换。
5. 连接通用手柄后，左摇杆控制转向和前进/倒车；按钮 `3` 脚刹，按钮 `4` 手刹。
6. 使用 G29 时，按对应方向盘的 Bottom 3 接管主控；左拨片选择后退档，右拨片选择前进档。

## Mbox100 通信

通信链路：

```text
Unity SampleScene
  -> TCP 127.0.0.1:9999
  -> MoonBase/middleware
  -> UDP 192.168.15.201:7408
  -> Mbox100
```

1. 检查 Mbox100 控制器与电脑网线连接，确认硬件急停可用且平台周围无人和障碍物。
2. 启动仓库根目录的 `启动底座中间件.bat`，确认中间件监听 `127.0.0.1:9999`。
3. 启动 Unity 场景并进入 Play，检查 HUD 中的平台连接状态。
4. 需要检查 TCP 端口时，在 `MoonBase/middleware/` 运行 `Test-MiddlewareConnection.ps1`。
5. 运动测试使用 `-ExecuteMotion` 参数；测试脚本会要求输入 `MOVE`，并发送小幅俯仰和回零指令。执行前确认现场安全并可立即使用急停。

## 常见检查

- Unity 显示平台未连接：先确认中间件已启动并监听本机 `9999` 端口。
- 控制器网络不通：检查电脑有线网卡 `192.168.15.100/24`、网线和控制器地址 `192.168.15.201`。
- 中间件初始化失败：检查 `MoonBase/middleware/DEPLOY_GUIDE.md` 中的软件文件和系统配置步骤。
- 按钮没有输入：运行 [`PLATFORM_BUTTON_DETECTOR.md`](PLATFORM_BUTTON_DETECTOR.md) 中的检测器，并检查 Unity 的 `baseControllerJoystickNumber` 设置。
