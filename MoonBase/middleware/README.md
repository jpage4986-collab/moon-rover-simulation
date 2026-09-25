# Mbox100 中间件

中间件接收 Unity 的 TCP 运动指令，并通过厂商 SDK 向 Mbox100 控制器发送运动数据。

## 通信配置

配置文件：`Config.cfg`

| 配置 | 默认值 |
|---|---:|
| Unity TCP 监听 | `127.0.0.1:9999` |
| 控制器地址 | `192.168.15.201:7408` |
| 设备类型 | `28` |
| 控制器网卡 | `192.168.15.100/24` |

按现场网络修改 `Config.cfg` 中的地址和端口。Unity 的 `MotionPlatformController` 连接本机 TCP `9999` 端口。

## 启动

从仓库根目录运行 `启动底座中间件.bat`。中间件成功启动后，再启动 Unity 场景。首次部署、系统配置和连接检查见 [`DEPLOY_GUIDE.md`](DEPLOY_GUIDE.md)。

本目录提供中间件 C# 源码。厂商 SDK 运行文件和系统级配置按 [`MIDDLEWARE_BINARIES.md`](MIDDLEWARE_BINARIES.md) 准备。

## 目录

- `src/`：TCP 服务、平台通信、配置加载和安全限幅源码。
- `Config.cfg`：中间件网络与安全参数。
- `Test-MiddlewareConnection.ps1`：TCP 监听检查与受控运动验证。
