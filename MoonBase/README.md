# 月球车底座程序 - 可移动部署版

## 目录结构

```
G:\
├── Assets (2)\                        ← Unity 项目（月球车模拟）
│   └── Assets\Driver\
│       └── MotionPlatformController.cs ← 底座控制脚本（TCP 127.0.0.1:9999）
│
└── MoonBase\                          ← 底座程序部署根目录
    ├── README.md                      ← 本文件
    ├── start_middleware.bat           ← 【启动】中间件
    ├── middleware\                    ← MPSdkMiddleware 中间件
    │   ├── MPSdkMiddleware_官方版.exe  ← 官方中间件主程序
    │   ├── MPSdkMiddleware.exe        ← 自建中间件（含安全保护）
    │   ├── MpDll.dll                  ← 汇鼎 SDK
    │   ├── Config.cfg                 ← 设备配置
    │   ├── 48.xml                     ← 平台参数数据库
    │   ├── Algorithm.dll              ← 运动学算法库
    │   ├── src\                       ← 中间件源码（参考用）
    │   └── ...
    │
    └── player\                       ← 客服提供的独立播放器（可选）
```

## 系统架构

```
Unity (G:\Assets (2))                    Mbox100 控制器
       │                                        ▲
       │ TCP 127.0.0.1:9999                     │ UDP 192.168.15.201:7408
       ▼                                        │
  MPSdkMiddleware.exe ────────────────────────────┘
       (G:\MoonBase\middleware\)
```

## 前置条件

| 项目 | 说明 |
|------|------|
| .NET Framework 4.6.1+ | 中间件运行环境 |
| SafeNet 加密狗驱动 | 见 `加密狗驱动.exe` |
| SafeNet USB 加密狗 | 必须插入 |
| 网络配置 | PC IP: 192.168.15.100, 控制器: 192.168.15.201 |

## 快速启动

### 第 1 步：配置系统文件（只需一次）

`C:\ProgramData\MP\` 目录应包含：
- `48.xml`   ← 已部署
- `484.xml`  ← 已部署
- `plat.xml` ← 中间件首次启动时自动生成
- `appset.xml` ← 中间件首次启动时自动生成

### 第 2 步：启动中间件

双击项目包内的 `启动底座中间件.bat`。启动脚本使用相对路径，项目放在任意盘符均可。

或手动运行：
```cmd
cd /d "<项目包路径>\MoonBase\middleware"
MPSdkMiddleware_官方版.exe
```

成功看到：
```
C:\ProgramData\MP\48.xml
Get:BackToZero
已开启TCPServer127.0.0.1:9999等待连接
```

### 第 3 步：启动 Unity

1. 双击项目包内的 `启动Unity仿真.bat`，或用 Unity Hub 打开 `Assets (2)`
2. 打开场景，选中 Car GameObject
3. 确保 `MotionPlatformController` 组件：
   - **Server IP**: `127.0.0.1`
   - **Server Port**: `9999`
4. 点击 Play

## 测试指令

```powershell
# 回零
"Zero#end" | ForEach-Object { $c=New-Object Net.Sockets.TcpClient('127.0.0.1',9999); $s=$c.GetStream(); $s.Write([Text.Encoding]::ASCII.GetBytes($_),0,$_.Length); $c.Close() }

# 抬头 5 度
"Runing#0#5#0#0#0#0#0#0#1000#end" | ForEach-Object { $c=New-Object Net.Sockets.TcpClient('127.0.0.1',9999); $s=$c.GetStream(); $s.Write([Text.Encoding]::ASCII.GetBytes($_),0,$_.Length); $c.Close() }
```

## 网络配置

| 设备 | IP | 说明 |
|------|-----|------|
| PC 以太网口 | 192.168.15.100/24 | 固定 IP |
| Mbox100 控制器 | 192.168.15.201 | 目标设备 |

网线直连 PC ↔ 控制器。

## 修改记录

| 日期 | 变更 |
|------|------|
| 2026-07-05 | 从同事电脑 (E:\, 192.168.3.201) 迁移到本地 G 盘部署 |
| 2026-07-05 | Config.cs 默认 IP 修正: 192.168.3.201 → 192.168.15.201 |
| 2026-07-05 | 系统配置部署到 C:\ProgramData\MP\ |
