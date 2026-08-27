# 月球车仿真平台联调方案

## 一、先确认通信链路

项目的通信关系是：

```text
Unity SampleScene
  -> TCP 127.0.0.1:9999
  -> MPSdkMiddleware
  -> UDP 192.168.15.201:7408
  -> Mbox100 六自由度底座
```

先不连接实体底座时，在 PowerShell 中运行：

```powershell
cd '<项目根目录>\MoonBase\middleware'
.\Test-MiddlewareConnection.ps1
```

`PASS` 只代表本机 9999 端口有中间件监听，不代表硬件已经授权或可运动。

实体测试必须在确认平台周围无人、急停可用后运行：

```powershell
.\Test-MiddlewareConnection.ps1 -ExecuteMotion
```

脚本会要求输入 `MOVE`，并执行 `Zero -> ±1° 俯仰 -> Zero`。若中间件窗口出现
`[TCP] 客户端已连接`、`[指令] Runing ...`、`[MotionCtrl]`，说明 Unity/测试脚本到中间件的链路成立；平台是否实际动作还要看厂商 SDK、加密狗、配置文件和 UDP 网络。

## 二、Unity 内的驾驶模式按钮

真正的按钮对象名称是：

- `BtnManual`：手动+AEB
- `BtnLocalAI`：局部雷达 AI
- `BtnHybridAI`：卫星混合 AI

`BtnManual_R`、`BtnLocalAI_R` 等是界面装饰/布局对象，不是这三个驾驶模式的主按钮。

按钮事件在 `DriveModeManager.Start()` 中运行时通过 `Button.onClick.AddListener` 绑定，因此在 Unity Inspector 的 Persistent OnClick 列表为空是正常的。它们只有进入 Play 模式后才会切换。

## 三、底座上的实体按钮

底座实体按钮与上面的 Unity UI 按钮不是同一套输入。目前仓库没有读取底座实体按钮的实现；中间件只解析 `Runing`、`Zero`、`Reset` 三类 TCP 指令，底层 `MpDll.dll` 当前代码也只调用运动、回零和复位方法。

先按按钮的连接类型分类：

| 连接类型 | 正确处理方式 | 是否接入 Unity |
|---|---|---|
| 急停、使能、平台控制器面板按钮 | 由安全回路/Mbox100 控制器/厂商软件处理 | 通常不接入，不能用 Unity 替代急停 |
| USB HID 按钮盒 | Windows 会识别为键盘、游戏手柄或 HID 设备 | 可以新增 HID/Unity Input 适配层 |
| 串口/RS-485/PLC 数字量按钮 | 读取 COM 口或 PLC 协议，再转换为业务事件 | 可以新增串口/PLC 适配层 |
| 只接到底座、不接 PC 的按钮 | PC 和 Unity 无法读取其状态 | 需要厂商 SDK、控制器协议或额外采集模块 |

不能直接把实体按钮映射成 `Zero#end` 或 `Reset#end`：首先要确认它通过什么设备和协议被电脑看到。急停按钮必须保留硬件安全链路，不能改成软件按钮。

目标结构应为：

```text
底座按钮 -> USB HID / 串口 / PLC / Mbox100 控制器
         -> ButtonInputAdapter（按设备协议实现）
         -> Enable / Zero / Reset / Stop 业务事件
         -> 中间件安全命令
```

## 四、按钮验收表

| 操作 | 预期结果 |
|---|---|
| Play 后按 `1` 或点击“手动+AEB” | `当前模式: 手动+AEB`；手动驱动启用，两个 AI 驱动停用 |
| Play 后按 `2` 或点击“局部AI” | `当前模式: 局部雷达 AI`；局部 AI 驱动启用 |
| Play 后按 `3` 或点击“混合AI” | `当前模式: 卫星混合 AI`；混合 AI 驱动启用 |
| 每次切换 | 对应按钮高亮，HUD 事件日志记录 `驾驶模式 → ...` |

按键和鼠标各测一次：如果按键能切换、鼠标不能，优先检查 `EventSystem`、Canvas 的 `GraphicRaycaster`、按钮是否 `Interactable`，以及是否有遮挡 UI；如果两者都不能，检查 Unity Console 是否有脚本编译错误，以及 `car` 对象上的 `DriveModeManager` 是否启用且 3 个 Button 引用已绑定。

## 五、平台联调顺序

1. 在 `MotionPlatformController` 中将 `enablePlatform` 设为关闭，先验收车辆、AI 和 UI。
2. 启动中间件，确认监听 `127.0.0.1:9999`，再运行端口探测脚本。
3. Unity Play，确认外设状态中的“动感平台”变为“已连接”，底座频率接近 50 Hz。
4. 最后才使用 `-ExecuteMotion` 做小幅实体运动测试。
5. 停止 Unity Play 前先点击/调用回零；关闭中间件前确认已完成回零。

## 六、常见故障定位

- `TcpTestSucceeded=False`：中间件没有启动、端口被占用或启动失败。
- 中间件能监听但 SDK 初始化失败：检查 SafeNet、`C:\ProgramData\MP\48.xml`、`484.xml`、厂商 DLL 和日志中的绝对路径问题。
- Unity 显示“未连接”：确认 Play 顺序是先中间件后 Unity，并检查 `MotionPlatformController` 的 `127.0.0.1:9999`。
- 中间件收到 `Runing` 但平台不动：检查 Mbox100 电源、急停、PC 网卡 `192.168.15.100/24`、`ping 192.168.15.201`、UDP `7408` 和授权状态。
