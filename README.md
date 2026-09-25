# 月球车仿真平台

基于 Unity 的月球车驾驶与地形仿真项目，可使用键盘、通用手柄和 Logitech G29 双方向盘进行驾驶，并可连接 Mbox100 六自由度动感平台。

## 环境

- Windows 10/11 64 位
- Unity `2022.3.62f3c1`
- 连接 G29 时安装 Logitech G HUB
- 连接 Mbox100 时按 [`MoonBase/middleware/README.md`](MoonBase/middleware/README.md) 配置中间件和控制器网络

## 启动 Unity 仿真

1. 用 Unity Hub 打开仓库中的 `Assets (2)` 文件夹。
2. 打开 `Assets/Scenes/SampleScene.unity`。
3. 点击 Play 开始仿真。

也可运行仓库根目录的 `启动Unity仿真.bat`。Unity 首次导入资源时等待导入完成即可。

## 驾驶与实体按键

- 场景中的 `1`、`2`、`3` 键和驾驶模式按钮切换手动、局部导航和混合导航模式。
- 手动模式默认使用方向盘。底座控制器显示编号 `1` 的按钮在方向盘与手柄之间切换；当前模式显示在 HUD。
- 手柄左摇杆控制转向和前进/倒车，按钮 `3` 为脚刹，按钮 `4` 为手刹。
- 两个方向盘上的 Bottom 3 按钮选择对应方向盘为主控。主控方向盘的左拨片选择倒车档，右拨片选择前进档；副方向盘跟随主控。
- 底座控制器显示编号 `11` 的按钮切换车辆灯光。

## Mbox100 动感平台

Unity 通过 TCP `127.0.0.1:9999` 连接本机中间件，中间件通过 UDP `192.168.15.201:7408` 连接 Mbox100。电脑连接控制器的网卡使用 `192.168.15.100/24`。

运行顺序：确认急停可用并清空平台活动范围，启动 `启动底座中间件.bat`，再运行 Unity 场景。部署和故障处理见 [`MoonBase/middleware/DEPLOY_GUIDE.md`](MoonBase/middleware/DEPLOY_GUIDE.md)。

## 目录

- `Assets (2)/`：Unity 项目、场景、车辆资源与输入/HUD 脚本。
- `MoonBase/middleware/`：Mbox100 中间件源码、配置和运行说明。
- `MoonBase/button-detector/`：底座 USB 按键检测工具源码。
- `docs/`：实体按钮和硬件联调说明。

Unity 项目中的 `.meta` 文件与资源一并保留，克隆后由 Unity 生成本机缓存。
