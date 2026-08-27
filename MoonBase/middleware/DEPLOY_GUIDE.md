# 汇鼎 Mbox100 六自由度底座部署指南

> 本文档用于在新电脑上复现月球车项目与汇鼎 Mbox100 六自由度运动平台的对接。

---

## 一、硬件清单

| 硬件 | 说明 |
|------|------|
| PC (Windows 10/11) | 运行 Unity 和中间件 |
| 汇鼎 Mbox100 控制器 | 六轴六自由度运动控制器 |
| SafeNet USB 加密狗 | 授权许可，必须插在 PC 上 |
| 网线 | PC ↔ Mbox100 控制器直连 |
| 电源线 | 控制器供电 |

---

## 二、网络配置

### 2.1 PC 网卡 IP 设置

| 参数 | 值 |
|------|-----|
| IP 地址 | 192.168.15.100 |
| 子网掩码 | 255.255.255.0 |
| 默认网关 | 留空 |

### 2.2 控制器地址

| 参数 | 值 |
|------|-----|
| 控制器 IP | 192.168.15.201 |
| UDP 目标端口 | 7408 |
| 本机监听端口 | 8410 |
| TCP 中间件端口 | 9999 |

### 2.3 验证连接

```powershell
ping 192.168.15.201
# 应该看到: TTL=255, 延迟 1-3ms, 0% 丢包
```

---

## 三、软件依赖

### 3.1 必须安装

| 软件 | 用途 | 来源 |
|------|------|------|
| .NET Framework 4.6.1+ | 中间件运行环境 | Windows 自带或微软官网 |
| Unity (与原项目同版本) | 运行月球车模拟 | unity.com |
| SafeNet 加密狗驱动 | 授权验证 | 见下方"加密狗驱动" |

### 3.2 加密狗驱动

```
文件: 加密狗驱动.exe
位置: E:\Moon\MPsdkMiddleware使用说明2025\加密狗驱动.exe
```

安装步骤：
1. 运行 `加密狗驱动.exe`
2. 插入 SafeNet USB 加密狗
3. 重启电脑

---

## 四、文件部署

### 4.1 目录结构

```
C:\
├── ProgramData\MP\
│   ├── 48.xml              ← 平台参数数据库 (必须!)
│   ├── 484.xml             ← Mbox100 专用参数
│   ├── appset.xml           ← 运行参数配置
│   └── plat.xml             ← 平台硬件配置

E:\Moon\
├── MPSdkMiddleware\
│   ├── MPSdkMiddleware_官方版.exe   ← 中间件主程序
│   ├── MpDll.dll                    ← SDK 动态库
│   ├── 48.xml                       ← 平台参数数据库
│   ├── Config.cfg                   ← 中间件配置
│   ├── Algorithm.dll                ← 算法库
│   ├── Newtonsoft.Json.dll          ← JSON 库
│   ├── SharpDX.DirectInput.dll      ← DirectInput 库
│   ├── SharpDX.dll                  ← SharpDX 基础库
│   ├── slm_runtime_dev32.dll        ← 加密狗运行时 (32位)
│   └── slm_runtime_dev64.dll        ← 加密狗运行时 (64位)
│
├── unity\Moon\              ← Unity 项目 (完整复制)
│   └── Assets\Driver\
│       └── MotionPlatformController.cs  ← 底座控制脚本
│
├── 汇鼎Mbox100测试软件.exe   ← 硬件测试工具
└── god_tool.exe              ← 通用测试工具
```

### 4.2 关键文件说明

| 文件 | 来源 | 必须 |
|------|------|------|
| `48.xml` | MPsdkMiddleware使用说明2025 | ✅ 必须，没有它中间件会崩溃 |
| `MpDll.dll` | MPsdkMiddleware使用说明2025 | ✅ 必须，SDK 核心库 |
| `Config.cfg` | 手动创建 | ✅ 必须，中间件配置 |
| `MotionPlatformController.cs` | Unity 项目 | ✅ 必须，Unity 端控制脚本 |

---

## 五、配置文件内容

### 5.1 Config.cfg

```
DeviceIdentifier=28
TargetIP=192.168.15.201
TargetPort=7408
Debug=TRUE
```

- `DeviceIdentifier=28` → 六轴六自由度 (Mbox100)
- `TargetIP` → 控制器 IP
- `TargetPort` → 控制器 UDP 端口

### 5.2 C:\ProgramData\MP\appset.xml

```xml
<?xml version="1.0"?>
<ClassAppset xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <数据包间隔>100</数据包间隔>
  <旋转轴复位角度>0</旋转轴复位角度>
  <最大旋转角>180</最大旋转角>
  <最小旋转角>0</最小旋转角>
  <udp数据反馈>true</udp数据反馈>
</ClassAppset>
```

### 5.3 C:\ProgramData\MP\plat.xml

```xml
<?xml version="1.0" encoding="utf-16"?>
<PlatParameter xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
  <PlatDriverType>Vegafu_CAN</PlatDriverType>
  <DriverEx>192.168.15.201</DriverEx>
  <DOF>6</DOF>
  <legtype>cylinder</legtype>
  <PlatSpacedef1>870</PlatSpacedef1>
  <PlatSpacedef2>100</PlatSpacedef2>
  <PlatSpacedef3>870</PlatSpacedef3>
  <PlatSpacedef4>100</PlatSpacedef4>
  <PlatoffsetX>0</PlatoffsetX>
  <PlatoffsetY>0</PlatoffsetY>
  <PlatBaseHeight>515</PlatBaseHeight>
  <CylinderMaxlen>290</CylinderMaxlen>
  <CylinFixLen>620</CylinFixLen>
  <PulseUnit>1500</PulseUnit>
  <HasTurnMoto>false</HasTurnMoto>
  <PulsePerAngle>1</PulsePerAngle>
  <PulsePerAngle_crank>1</PulsePerAngle_crank>
</PlatParameter>
```

---

## 六、部署步骤

### Step 1: 安装加密狗驱动

1. 运行 `加密狗驱动.exe`
2. 插入 SafeNet USB 加密狗
3. 重启电脑

### Step 2: 配置网络

1. 设置 PC 网卡 IP: `192.168.15.100 / 255.255.255.0`
2. 用网线连接 PC 和 Mbox100 控制器
3. 验证: `ping 192.168.15.201` 应成功

### Step 3: 部署文件

1. 复制 `MPSdkMiddleware` 文件夹到 `E:\Moon\`
2. 复制 `48.xml` 到 `C:\ProgramData\MP\` (没有此文件夹则创建)
3. 复制 `484.xml` 到 `C:\ProgramData\MP\`
4. 复制 `appset.xml` 到 `C:\ProgramData\MP\`
5. 复制 `plat.xml` 到 `C:\ProgramData\MP\`
6. 复制 Unity 项目到 `E:\unity\Moon\`

### Step 4: 测试中间件

```powershell
cd E:\Moon\MPSdkMiddleware
.\MPSdkMiddleware_官方版.exe
```

应该看到:
```
C:\ProgramData\MP\48.xml
Get:BackToZero
已开启TCPServer127.0.0.1:9999等待连接
```

如果看到 `NullReferenceException` → 检查 `C:\ProgramData\MP\48.xml` 是否存在。

### Step 5: 测试平台运动

```powershell
# 新开一个 PowerShell 窗口
# 发送测试指令
"Zero#end" | ForEach-Object { $c=New-Object Net.Sockets.TcpClient('127.0.0.1',9999); $s=$c.GetStream(); $s.Write([Text.Encoding]::ASCII.GetBytes($_),0,$_.Length); $c.Close() }

# 抬头5度 (持续1秒)
"Runing#5#0#0#0#0#0#0#0#1000#end" | ForEach-Object { $c=New-Object Net.Sockets.TcpClient('127.0.0.1',9999); $s=$c.GetStream(); $s.Write([Text.Encoding]::ASCII.GetBytes($_),0,$_.Length); $c.Close() }

# 回零
"Zero#end" | ForEach-Object { $c=New-Object Net.Sockets.TcpClient('127.0.0.1',9999); $s=$c.GetStream(); $s.Write([Text.Encoding]::ASCII.GetBytes($_),0,$_.Length); $c.Close() }
```

平台应该会抬头然后回零。

### Step 6: Unity 配置

1. 打开 Unity 项目 `E:\unity\Moon`
2. 打开场景 `SampleScene`
3. 选中 **Car** GameObject
4. Add Component → 搜索 `MotionPlatformController`
5. Inspector 设置:
   - **Server IP**: `127.0.0.1`
   - **Server Port**: `9999`
   - **Rover Transform**: 拖 Car 自身
   - **Rover Rigidbody**: 拖 Car 上的 Rigidbody
   - **Car Controller**: 拖 Car 上的 CarController
   - **Enable Platform**: ✅ 勾选

### Step 7: 运行测试

1. 确保中间件窗口在运行
2. Unity 点 Play
3. WASD 开车 → 平台应跟随车的姿态运动

---

## 七、MotionPlatformController 参数说明

### 发送参数

| 参数 | 默认值 | 说明 |
|------|--------|------|
| sendInterval | 0.05f (20Hz) | 发送频率 |
| motionTime | 60 | 每条指令执行时间(ms) |
| predictionTime | 0.1 | 预测补偿时间(秒)，0=不预测 |

### 旋转系数

| 参数 | 默认值 | 说明 |
|------|--------|------|
| pitchScale | 1.5 | 俯仰放大系数 |
| rollScale | 1.5 | 侧倾放大系数 |
| yawScale | 0.5 | 偏航放大系数 |

### 平移系数

| 参数 | 默认值 | 说明 |
|------|--------|------|
| surgeScale | 80 | 前后加速度放大 |
| swayScale | 80 | 左右加速度放大 |
| heaveScale | 100 | 垂直加速度放大 |

### 滤波参数

| 参数 | 默认值 | 说明 |
|------|--------|------|
| filterSmooth | 0.15 | 低通滤波系数，越小越平滑 |
| washoutRate | 2.0 | 回中速率，越大回中越快 |

### 安全限位

| 参数 | 默认值 | 说明 |
|------|--------|------|
| maxRotationDeg | 15 | 最大旋转角度(度) |
| maxTranslationMm | 100 | 最大平移量(mm) |

---

## 八、通信协议

### TCP 指令格式 (Unity → 中间件)

```
Runing#Rx#Ry#Rz#X#Y#Z#effcet1#effcet2#time#end
```

| 参数 | 类型 | 说明 |
|------|------|------|
| Rx | float | Roll (侧倾，度) |
| Ry | float | Pitch (俯仰，度) |
| Rz | float | Yaw (偏航，度) |
| X | float | Surge (前后平移，mm) |
| Y | float | Sway (左右平移，mm) |
| Z | float | Heave (垂直平移，mm) |
| effcet1 | byte | 特效口 1-8 (二进制位开关) |
| effcet2 | byte | 特效口 9-12 |
| time | int | 运动时间(ms) |

### 特殊指令

| 指令 | 说明 |
|------|------|
| `Zero#end` | 回零复位 |
| `Reset#end` | 设置当前位置为原点 |

### 方向映射

| Unity 轴 | 平台轴 | 方向 |
|----------|--------|------|
| Transform.euler.z (Roll) | Rx | 取反 |
| Transform.euler.x (Pitch) | Ry | 正向 |
| Transform.euler.y (Yaw) | Rz | 取反 |
| Rigidbody 加速度 forward | X (Surge) | 取反 |
| Rigidbody 加速度 right | Y (Sway) | 取反 |
| Rigidbody 加速度 up | Z (Heave) | 正向 |

---

## 九、故障排查

### 问题: 中间件启动崩溃 (NullReferenceException)

**原因**: `C:\ProgramData\MP\48.xml` 不存在

**解决**: 复制 `48.xml` 到 `C:\ProgramData\MP\`

### 问题: ping 不通 192.168.15.201

**原因**: 网络配置错误

**解决**:
1. 检查 PC IP 是否为 192.168.15.100
2. 检查网线是否插好
3. 检查控制器是否上电

### 问题: 平台不动

**原因**: 可能是加密狗问题

**解决**:
1. 确认加密狗已插入
2. 确认加密狗驱动已安装
3. 用 `汇鼎Mbox100测试软件.exe` 测试

### 问题: Unity 连接不上中间件

**原因**: 中间件未启动或端口不对

**解决**:
1. 确认中间件窗口在运行
2. 确认端口是 9999
3. 检查防火墙是否阻止

### 问题: 平台方向反了

**解决**: 修改 `MotionPlatformController.cs` 中的正负号:
- 旋转: `rx = -roll * rollScale` → `rx = roll * rollScale`
- 平移: `x = -surgeFiltered` → `x = surgeFiltered`

---

## 十、紧急停止

### 方法 1: 发送回零指令

```powershell
"Zero#end" | ForEach-Object { $c=New-Object Net.Sockets.TcpClient('127.0.0.1',9999); $s=$c.GetStream(); $s.Write([Text.Encoding]::ASCII.GetBytes($_),0,$_.Length); $c.Close() }
```

### 方法 2: 关闭 Unity

Unity 退出时会自动发送回零指令。

### 方法 3: 关闭中间件

关闭中间件窗口，平台会保持最后位置（需要手动回零）。

---

## 十一、文件校验

部署完成后，检查以下文件是否存在:

```
✅ C:\ProgramData\MP\48.xml          (17387 bytes)
✅ C:\ProgramData\MP\484.xml         (15658 bytes)
✅ C:\ProgramData\MP\appset.xml      (378 bytes)
✅ C:\ProgramData\MP\plat.xml        (787 bytes)
✅ E:\Moon\MPSdkMiddleware\MPSdkMiddleware_官方版.exe (9728 bytes)
✅ E:\Moon\MPSdkMiddleware\MpDll.dll (112640 bytes)
✅ E:\Moon\MPSdkMiddleware\48.xml    (17387 bytes)
✅ E:\Moon\MPSdkMiddleware\Config.cfg (71 bytes)
✅ E:\unity\Moon\Assets\Driver\MotionPlatformController.cs
```

---

*文档生成时间: 2026-07-03*
