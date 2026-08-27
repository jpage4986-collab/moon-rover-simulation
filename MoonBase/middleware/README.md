# MPSdkMiddleware — 汇鼎 Mbox100 六自由度运动平台中间件

## 项目概述

将 Unity 月球车模拟器的姿态数据，通过 TCP 协议转发给汇鼎 Mbox100 六自由度 Stewart 运动平台。

```
Unity (MotionPlatformController.cs)        Mbox100 控制器
        │                                        ▲
        │ TCP 127.0.0.1:9999                     │ UDP
        ▼                                        │
  MPSdkMiddleware.exe ────────────────────────────┘
        官方中间件（汇鼎客服提供），通过 MpDll.dll 发送控制指令
```

---

## 最新资源（2025 年客服提供）

目录：`G:\月球车底座完成版\MPsdkMiddleware使用说明2025 (2)\MPsdkMiddleware使用说明2025\`

| 资源 | 说明 |
|------|------|
| `MPsdkMiddleware使用说明.doc` | 官方使用说明书（Word） |
| `说明.jpg` / `说明1.png` | 安装步骤截图 |
| `加密狗驱动.exe` | SafeNet 加密狗驱动程序（需要安装） |
| `需要放到对应路径的文件\SDK\` | **官方中间件 + 所有依赖 DLL** |
| `需要放到对应路径的文件\C盘\` | 需要复制到 C 盘的文件 |
| `需要放到对应路径的文件\D盘\` | 需要复制到 D 盘的文件 |

---

## 目录结构（更新后完整版 - G 盘部署）

```
G:\MoonBase\
├── MPSdkMiddleware\                     ← 中间件项目文件夹（已部署到 G:\MoonBase\middleware\）
│   ├── README.md                        ← 本文件
│   ├── Config.cfg                       ← 备用配置参考
│   └── src\                             ← 我们自己写的中间件源码（留作参考）
│
├── MPsdkMiddleware使用说明2025\          ★ 客服提供的最新资源（优先使用）
│   ├── MPsdkMiddleware使用说明.doc       ← 官方说明书
│   ├── 说明.jpg / 说明1.png             ← 截图指导
│   ├── 加密狗驱动.exe                   ← SafeNet 加密狗驱动
│   └── 需要放到对应路径的文件\
│       ├── SDK\                         ★ 官方 MPSdkMiddleware.exe + DLL
│       │   ├── MPSdkMiddleware.exe       ← 中间件主程序（9.7 KB）
│       │   ├── MPSdkMiddleware.exe.config ← .NET 4.7.2 配置
│       │   ├── Config.cfg               ← 设备配置（DeviceIdentifier=17）
│       │   ├── 48.xml                   ← 平台机械参数库（所有平台类型）
│       │   ├── MpDll.dll                ← 汇鼎 SDK（2025-05-22 新版）
│       │   ├── MpDll.pdb                ← 调试符号文件
│       │   ├── Algorithm.dll            ← 运动学算法
│       │   ├── Newtonsoft.Json.dll      ← JSON 依赖
│       │   ├── Newtonsoft.Json.xml      ← JSON 文档
│       │   ├── SharpDX.DirectInput.dll  ← 摇杆/方向盘 DirectInput 支持
│       │   ├── SharpDX.dll              ← DirectX 底层库
│       │   ├── slm_runtime_dev32.dll    ← SafeNet 加密锁驱动（32位）
│       │   └── slm_runtime_dev64.dll    ← SafeNet 加密锁驱动（64位）
│       │
│       ├── C盘\MP\                      ★ 复制到 C:\MP\
│       │   └── 484.xml                  ← Mbox100 平台参数配置
│       │
│       └── D盘\MP\                      ★ 复制到 D:\MP\
│           └── player\
│               ├── configuration\
│               │   ├── NewPlayerSetting.xml  ← 播放器设置（语言/类型/增益等）
│               │   └── PlayerLog\            ← 日志目录
│               ├── dbFile\               ← 数据库目录（空）
│               ├── playerNone_protected\ ← Unity 独立播放器（铭派科技）
│               │   ├── player.exe        ← 可执行文件
│               │   ├── player_Data\      ← Unity 数据
│               │   └── MonoBleedingEdge\ ← Mono 运行时
│               └── playerNone_protected.rar ← 压缩包备份
│
├── 新座舱控制代码\                       ← 汇鼎原始 SDK 备份
│   └── MPDLL24_04_09\                   ← 旧版 MpDll.dll（可废弃）
│
├── 平台数据服务3.7\                      ← ProConvert 替代方案
│
├── 底座.md                              ← 汇鼎原始协议文档
├── god_tool.exe                          ← 汇鼎测试工具
├── 汇鼎Mbox100测试软件.exe               ← Mbox100 硬件测试工具
└── 月球车底座集成对话记录.md             ← 集成过程记录
```

---

## 通信协议（与之前一致）

### TCP 接口（Unity → 中间件）

中间件监听 `127.0.0.1:9999`，接收文本指令。

#### 运动指令

```
Runing#Rx#Ry#Rz#X#Y#Z#effcet1#effcet2#time#end
```

| 参数 | 类型 | 范围 | 说明 |
|------|------|------|------|
| Rx | float | ±设备限定 | Roll 侧倾角（度） |
| Ry | float | ±设备限定 | Pitch 俯仰角（度） |
| Rz | float | ±设备限定 | Yaw 偏航角（度） |
| X | float | ±设备限定 | 前后平移（mm） |
| Y | float | ±设备限定 | 左右平移（mm） |
| Z | float | ±设备限定 | 上下平移（mm） |
| effcet1 | byte | 0-255 | 特效口 1-8（二进制位） |
| effcet2 | byte | 0-255 | 特效口 9-12（二进制位） |
| time | int | ms | 运动完成时间 |

#### 回零指令

```
Zero#end
```

#### 复位指令

```
Reset#end
```

---

## 官方配置说明

### Config.cfg（与 SDK 文件夹中的 MPSdkMiddleware.exe 同目录）

```ini
DeviceIdentifier=28         # 已确认为六轴六自由度
TargetIP=192.168.15.201
TargetPort=7408
Debug=TRUE
```

| 参数 | 值 | 说明 |
|------|-----|------|
| DeviceIdentifier | **28** | **设备类型 ID**。**已确认为 28（六轴六自由度，Mbox100）**。17=单人蛋椅三轴 |
| TargetIP | 192.168.15.201 | **控制器 IP 地址**（注意：不是 192.168.3.201） |
| TargetPort | 7408 | 控制器 UDP 端口 |
| Debug | TRUE | 是否输出调试信息 |

> ⚠ **IP 地址差异**：之前我们假设控制器 IP 是 `192.168.3.201`，但官方 Config 写的是 `192.168.15.201`。客服提供的这个才是正确的，硬件连接时应以 `192.168.15.201` 为准。PC 以太网口 IP 也要相应改为 `192.168.15.x` 网段。

### C:\MP\484.xml（平台机械参数）

控制器据此计算运动学逆解。包含多种平台类型的参数：

| 平台类型 | 说明 |
|---------|------|
| SixDof | **六轴 Stewart 平台**（上下平台半径 550/570mm，电动缸行程 150mm） |
| Sixcrank | 六人曲柄机构 |
| OneThreeDof ~ FourThreeDof | 单人/双人/四人 三轴 |
| One360 / Two360 | 单人/双人 360°旋转 |
| RacingSixDof | 赛车六轴（大尺寸） |
| 等 | 共约 20 种平台类型 |

### D:\MP\player\configuration\NewPlayerSetting.xml

| 参数 | 说明 |
|------|------|
| Type | **设备类型**（28=六轴六自由度，17=单人蛋椅三轴） |
| Language | CN / EN（语言） |
| Gain | 动作幅度增益（1.0=正常，<1 减小，>1 增大） |
| IsPlay | 是否播放视频 |
| SteamVRPath | SteamVR 路径（可选） |
| JoystickName | 摇杆/方向盘名称（支持 DirectInput 设备） |

---

## 官方中间件安装步骤

### 第 1 步：安装加密狗驱动

运行 `加密狗驱动.exe` 安装 SafeNet 授权驱动。如果控制器不需要加密狗，可跳过。

### 第 2 步：复制文件到对应路径

```
将 SDK\ 整个文件夹复制到你的工作目录（如 G:\MoonBase\middleware\）

将 C盘\MP\484.xml          →  C:\MP\484.xml
将 D盘\MP\player\ 整个     →  D:\MP\player\
```

### 第 3 步：确认 Config.cfg

已确认为 **Mbox100 六轴六自由度**，`DeviceIdentifier=28`：

```ini
DeviceIdentifier=28         # 六轴六自由度（已确认）
TargetIP=192.168.15.201
TargetPort=7408
Debug=TRUE
```

> 如果换用其他平台，参考：17=单人蛋椅三轴，28=六轴六自由度

### 第 4 步：硬件连接

```
PC 以太网口 —— 网线直连 —— Mbox100 控制器网口
```

PC 配固定 IP：

```
IP 地址:     192.168.15.100
子网掩码:     255.255.255.0
默认网关:     （留空）
```

验证连接：
```cmd
ping 192.168.15.201
```

### 第 5 步：启动中间件

```cmd
cd G:\MoonBase\middleware\（或你存放 SDK 的目录）
MPSdkMiddleware.exe
```

### 第 6 步：启动 Unity / 播放器

- **直接用 Unity 编辑器**：开 Unity 项目，确保 MotionPlatformController 连 `127.0.0.1:9999`
- **用客服提供的播放器**：运行 `D:\MP\player\playerNone_protected\player.exe`

---

## 安全保护

之前我们自建的中间件包含 5 层安全保护（限幅/自检/看门狗/断连回零/退出回零）。**官方中间件是否自带安全保护尚不确定**。

建议在 Unity 侧的 `MotionPlatformController.cs` 加入保护逻辑：

| 保护 | 建议实现方式 |
|------|------------|
| 角度限幅 | 发送前 clamp Rx/Ry/Rz 到 ±15° |
| 回零机制 | 场景退出时发送 `Zero#end` |
| 心跳 | 定期发送 Runing 指令保持连接 |

---

## 常见问题

### Q: 控制器 IP 到底是哪个？
以客服配置为准：**192.168.15.201**。如果 ping 不通，尝试用 `汇鼎Mbox100测试软件.exe` 自动扫描。

### Q: DeviceIdentifier 填什么？
已确认 Mbox100 = **28**（六轴六自由度）。

### Q: 加密狗怎么用？
MpDll.dll 内部有 SafeNet 授权校验，需要 USB 加密锁（硬件锁）。使用步骤：

1. **先安装驱动**：运行 `加密狗驱动.exe`
2. **插上加密狗**：USB 口插入加密锁 U 盘，系统识别后会多一个盘符（或指示灯亮）
3. **重启中间件**：先启动中间件，如果加载 DLL 时不报授权错误就说明已生效

> ⚠ 加密狗驱动只需安装一次。如果不用加密狗也能启动，说明当前 DLL 不强制校验。

### Q: 官方播放器 player.exe 是做什么的？
客服提供了一个完整的 Unity 独立播放器（铭派科技），可以用来**单独测试底座功能**，不依赖你们自己的 Unity 项目。

---

## 版本历史

| 日期 | 版本 | 变更 |
|------|------|------|
| 2026-04-08 | 1.0 | Unity 项目分析完成 |
| 2026-05-16 | 2.0 | 自行编译 MPSdkMiddleware（MoveDll 方案） |
| 2026-05-16 | 2.1 | 改用 Movement 反射方案 |
| 2026-05-17 | 3.0 | 加入 5 层安全保护 |
| 2026-06-06 | 4.0 | 获客服官方资源，改为官方中间件方案，更新 IP 为 192.168.15.201 |
| **2026-06-06** | **4.1** | **确认 DeviceIdentifier=28（六轴六自由度），加密狗就绪，Config 更新** |
