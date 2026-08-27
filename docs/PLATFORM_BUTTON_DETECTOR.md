# 底座实体按钮检测器

工具位置：`MoonBase/button-detector/`。

## 启动

双击：

```text
MoonBase/button-detector/Run-ButtonDetector.bat
```

或在 PowerShell 中运行：

```powershell
cd "G:\月球车项目_完整包1\月球车项目_完整包"
& ".\MoonBase\button-detector\Run-ButtonDetector.bat"
```

程序只读取 Windows Raw Input 和串口设备列表，不发送 `Runing`、`Zero`、`Reset` 或任何 UDP 运动报文。

## 如何判断

1. 启动程序后先看“系统输入设备”列表。
2. 逐个按下底座上的实体按钮。
3. 如果出现 `[HID BUTTON INPUT]`，说明按钮盒以 USB-HID 方式被系统识别，记录对应的设备名和原始数据。
4. 如果只显示串口（例如 `COM3`），说明按钮可能走串口，需要进一步确定波特率、数据格式和厂商协议。
5. 如果既没有 HID 事件，也没有串口设备，按钮可能只接入底座安全回路或 Mbox100 控制器，不能由这个 Unity 程序直接读取。

## 安全说明

这个检测器不会控制平台。急停、使能和安全回路按钮仍应由底座硬件或厂商控制器处理，不能用 Unity 程序替代。
