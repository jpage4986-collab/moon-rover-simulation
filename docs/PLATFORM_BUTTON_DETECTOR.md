# 底座按钮检测工具

源码位于 `MoonBase/button-detector/`。

在 Windows 上双击 `Run-ConsoleBaseButtonDetector.bat`，脚本会编译并启动控制台检测器。按下和松开底座按钮时，窗口会显示设备名称、按钮编号和时间。按 Ctrl+C 退出。

工具读取本机 HID/游戏控制器输入，用于确认 Windows 收到的按钮编号。Unity 场景中的按钮功能映射见 [`HARDWARE_BUTTON_INTEGRATION.md`](HARDWARE_BUTTON_INTEGRATION.md)。
