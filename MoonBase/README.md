# MoonBase 平台接入

`MoonBase` 包含 Unity 与 Mbox100 之间的中间件源码、部署脚本和 USB 按钮检测器。

## 启动顺序

1. 连接 Mbox100，确认急停可用并清空平台周边。
2. 在仓库根目录运行 `启动底座中间件.bat`。
3. 确认中间件开始监听 `127.0.0.1:9999`。
4. 启动 `Assets (2)` 中的 Unity 场景并进入 Play。

Unity 连接本机 TCP `127.0.0.1:9999`；中间件默认通过 UDP `192.168.15.201:7408` 与 Mbox100 通信。电脑与控制器直连时，电脑有线网卡使用 `192.168.15.100/24`。

## 目录

- `middleware/`：中间件源码、配置、运行依赖说明和部署指南。
- `button-detector/`：底座 HID 按钮检测源码。

部署步骤见 [`middleware/DEPLOY_GUIDE.md`](middleware/DEPLOY_GUIDE.md)，按钮映射见 [`../docs/HARDWARE_BUTTON_INTEGRATION.md`](../docs/HARDWARE_BUTTON_INTEGRATION.md)。
