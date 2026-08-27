# 中间件运行文件说明

本目录的源代码可以通过 `build.bat` 编译，但完整的实体平台运行仍依赖厂商文件和授权环境。

通常需要由项目负责人通过授权渠道提供并放入本目录的文件：

- `MpDll.dll`
- `Algorithm.dll`
- `slm_runtime_dev32.dll`、`slm_runtime_dev64.dll`
- `MPSdkMiddleware.exe` 或由本目录源码编译出的 `MPSdkMiddleware_lowlatency.exe`
- `Newtonsoft.Json.dll`（若当前运行版本需要）
- `Config.cfg`、`48.xml`、`484.xml`

Unity 的可选 Logitech G29 集成还需要将 `Assets (2)/Assets/Plugins/LogitechSteeringWheelEnginesWrapper.dll`
及其 `.meta` 文件对应配置从授权的开发包补齐；本仓库默认忽略该 DLL。

还需要在运行电脑上完成：

- SafeNet/Sentinel 驱动与 USB 加密狗；
- `C:\ProgramData\MP\48.xml` 等系统配置部署；
- Mbox100 控制器与 PC 的网卡配置：PC `192.168.15.100/24`，控制器 `192.168.15.201:7408`。

请先确认供应商许可允许团队内部或公开分发，再决定是否解除 `.gitignore` 中的二进制忽略规则。公共 GitHub 仓库建议只提交源码、配置模板和部署说明。
