# 月球车仿真平台

这是一个基于 Unity 2022.3.62f3c1 的月球车仿真项目，包含车辆驾驶、局部/混合 AI 导航、地形反馈、状态 HUD，以及可选的 Mbox100 六自由度动感平台接口。底座上的实体按钮尚未在本仓库中完成输入适配，详见联调文档。

## 项目结构

- `Assets (2)/`：Unity 项目根目录，请用 Unity Hub 打开此目录。
- `MoonBase/middleware/`：Unity 与 Mbox100 之间的 TCP/UDP 中间件源码、配置说明和测试脚本。
- `docs/TESTING.md`：按钮、通信链路和实体平台的分阶段联调方案。
- `CONTRIBUTING.md`：分支、提交和硬件协作约定。

## 快速开始

1. 安装 Unity `2022.3.62f3c1`。
2. 用 Unity Hub 打开 `Assets (2)`，等待首次导入和编译完成。
3. 打开 `Assets/Scenes/SampleScene.unity` 并点击 Play。
4. 用键盘 `1/2/3` 或界面按钮切换手动、局部 AI、混合 AI 模式。
5. 无实体底座时，将 `MotionPlatformController.enablePlatform` 关闭；有实体底座时，先按 `docs/TESTING.md` 的顺序启动和验证中间件。

## 通信链路

```text
Unity MotionPlatformController
  -> TCP 127.0.0.1:9999
  -> MPSdkMiddleware
  -> UDP 192.168.15.201:7408
  -> Mbox100
```

端口自检默认不会发送运动指令：

```powershell
cd '<项目根目录>\MoonBase\middleware'
.\Test-MiddlewareConnection.ps1
```

实体运动测试需要明确追加 `-ExecuteMotion`，并在现场安全条件满足后输入 `MOVE`。厂商 DLL、SafeNet/Sentinel 驱动、USB 加密狗及其授权要求见 `MoonBase/middleware/MIDDLEWARE_BINARIES.md`。

## 协作提示

仓库忽略 Unity `Library`、临时文件、运行日志和未确认可再分发的厂商二进制；第一次克隆后由 Unity 自动重建缓存。不要把许可证、加密狗驱动或密钥提交到公共仓库。
