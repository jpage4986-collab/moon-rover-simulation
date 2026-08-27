# 月球车六自由度仿真项目——移植与运行说明

本包包含Unity月球车场景、Logitech G29输入代码以及Mbox100六自由度底座中间件。当前交付范围不包含机械臂功能，机械臂相关资源无需配置，也不影响月球车和底座运行。

## 一、交付时必须发送什么

最稳妥的方式是把整个“月球车项目_完整包”文件夹压缩后发送。必须保留以下内容及其相对目录：

```text
月球车项目_完整包/
├─ Assets (2)/
│  ├─ Assets/                 Unity资源、场景、脚本和Logitech包装DLL
│  ├─ Packages/               Unity包依赖
│  └─ ProjectSettings/        Unity项目和场景设置
├─ MoonBase/
│  ├─ middleware/             底座程序、厂商DLL和配置
│  ├─ copy_config.bat         首次部署系统配置
│  └─ start_middleware.bat    中间件启动脚本
├─ 启动Unity仿真.bat
├─ 启动底座中间件.bat
└─ README_项目移植说明.md
```

`MoonBase/middleware`建议整个文件夹原样发送，不要只挑选EXE。至少必须包含：

- `MPSdkMiddleware_lowlatency.exe`
- `MpDll.dll`、`Algorithm.dll`、`Newtonsoft.Json.dll`
- `slm_runtime_dev32.dll`、`slm_runtime_dev64.dll`
- `Config.cfg`、`48.xml`、`484.xml`

以下Unity生成目录可以不发送，接收方首次打开项目时会自动重建：

- `Assets (2)/Library`
- `Assets (2)/Temp`
- `Assets (2)/obj`
- `Assets (2)/Logs`
- `Assets (2)/UserSettings`
- `Assets (2)/.vs`、`*.csproj`、`*.sln`

若希望接收方拿到后打开得更快，也可以直接发送现有完整文件夹，包括Library。不要删除或漏发任何 `.meta` 文件。

## 二、还需要单独交付的东西

以下内容不能只靠项目文件解决：

1. SafeNet USB加密狗实体，必须随设备交接并插在运行电脑上。
2. SafeNet/Sentinel加密狗驱动安装程序。本项目包内目前没有该安装程序，需要从原厂安装包或平台供应商处另行提供。
3. Mbox100控制器、六自由度底座、电源、急停装置和网线。
4. Logitech G29方向盘、踏板及USB线；接收方需自行安装Logitech G HUB/对应驱动。
5. 可用的Unity许可证和Unity Hub。

厂商DLL、平台参数和加密狗可能受供应商授权限制，发送给第三方前请先确认授权范围。

## 三、接收方电脑要求

- Windows 10或Windows 11，64位。
- Unity Hub及Unity Editor `2022.3.62f3c1`，建议使用完全相同版本。
- .NET Framework 4.7.2或更高版本。
- 有线以太网口和可用USB口。
- 当前场景使用Logitech G29时，需要安装Logitech G HUB。
- 使用实体底座时，需要安装SafeNet/Sentinel运行时并插入加密狗。

## 四、首次部署

### 1. 解压并打开项目

建议解压到本地磁盘的短路径，例如：

```text
D:\MoonRover\
```

不要直接在压缩包、网盘同步目录或只读目录中运行。用Unity Hub打开其中的 `Assets (2)` 文件夹。第一次导入可能需要较长时间，等待Unity完成编译。

如果根目录的 `启动Unity仿真.bat` 找不到Unity，请从Unity Hub手动打开 `Assets (2)`；启动脚本会自动搜索C到G盘的常见安装位置。

### 2. 安装驱动并连接硬件

1. 安装SafeNet/Sentinel加密狗驱动。
2. 插入加密狗，确认Windows正确识别。
3. 安装Logitech G HUB并连接G29。
4. 连接Mbox100控制器和底座，确认急停可用，平台周围无人和障碍物。

### 3. 配置有线网卡

将与Mbox100直连的电脑网卡设置为：

```text
IP地址：192.168.15.100
子网掩码：255.255.255.0
默认网关：留空
```

控制器地址为 `192.168.15.201`，端口为 `7408`。在命令提示符验证：

```cmd
ping 192.168.15.201
```

必须能够收到回复。若电脑同时连接Wi-Fi，可以保留Wi-Fi，但不要给底座网卡填写错误网关。

### 4. 部署平台参数

右键“以管理员身份运行”：

```text
MoonBase\copy_config.bat
```

脚本会把 `48.xml` 和 `484.xml` 部署到 `C:\ProgramData\MP\`。此操作只需在首次部署或更换配置后执行一次。

## 五、每次运行顺序

1. 确认平台处于安全状态，急停可用。
2. 插入加密狗，接通控制器和底座，连接网线。
3. 双击 `启动底座中间件.bat`。
4. 确认窗口没有授权、DLL或网络错误，并显示正在监听 `127.0.0.1:9999`。
5. 双击 `启动Unity仿真.bat`，或从Unity Hub打开项目。
6. 打开 `Assets/Scenes/SampleScene.unity`。
7. 点击Play进行测试。

关闭时先退出Unity播放模式，让程序发送回零命令；再用中间件窗口正常退出。不要用任务管理器强制结束中间件，以免跳过平台回零流程。

## 六、当前安全和延时设置

Unity和中间件均有限幅保护：

- 横滚：±5°
- 俯仰：−3°到+3°
- 偏航：关闭
- 平移：当前Unity指令为0
- 指令频率：50 Hz

场景启用了角速度预测和地形预瞄。默认角速度预测为80 ms，地形预瞄为120 ms。若接收方平台响应不同，应先实测延时，再在Car对象的 `MotionPlatformController` 中微调 `Prediction Horizon` 和 `Terrain Preview Horizon`，不要扩大安全角度。

车辆在场景中可以手动调整X、Z位置和朝向；开始播放时会根据四个车轮和Terrain自动调整Y高度，避免从空中下坠。车辆必须位于Terrain范围内。

## 七、常见问题

### 中间件提示缺少DLL

确认 `MoonBase/middleware` 是整个文件夹复制的，且EXE与所有DLL位于同一目录。若文件来自网络下载，检查Windows文件属性中是否需要“解除锁定”，并检查杀毒软件隔离记录。

### 提示授权失败或平台不动作

确认SafeNet驱动已安装、加密狗已插入，并重新启动中间件。加密狗驱动安装包不在当前项目包中，缺少时必须联系原设备电脑管理员或平台供应商获取。

### `ping 192.168.15.201`不通

检查网线、控制器电源、网卡是否为 `192.168.15.100/24`，并暂时排查Windows防火墙及其他同网段网卡冲突。

### Unity运行但底座不动

必须先启动中间件，再进入Unity Play。确认中间件监听 `127.0.0.1:9999`，Unity的 `MotionPlatformController` 已启用，并查看中间件窗口是否持续收到约50 Hz指令。

### G29没有输入或力反馈

安装并打开Logitech G HUB，确认G29在系统中识别完成后，再启动Unity。Unity侧包装DLL已经包含在 `Assets/Plugins`。

### 第一次打开Unity出现大量编译

删除Library后首次打开会重新导入，这是正常现象。应等待右下角导入完成，不要在导入过程中点击Play。

## 八、交付前核对清单

- [ ] Unity项目的Assets、Packages、ProjectSettings完整，所有 `.meta` 文件均在。
- [ ] `SampleScene.unity`存在并能打开。
- [ ] `MoonBase/middleware`整个目录已发送。
- [ ] `48.xml`和`484.xml`均在交付包中。
- [ ] SafeNet驱动安装程序已通过合规方式另行提供。
- [ ] USB加密狗实体已交接。
- [ ] 接收方知道控制器IP、电脑IP和正确启动顺序。
- [ ] 接收方已知安全角度限制，首次测试时平台周围无人。
- [ ] 明确说明机械臂不属于本次可运行交付范围。

## 九、关键路径和通信关系

```text
Unity SampleScene
  └─ MotionPlatformController
       └─ TCP 127.0.0.1:9999
            └─ MPSdkMiddleware_lowlatency.exe
                 └─ UDP 192.168.15.201:7408
                      └─ Mbox100六自由度底座
```

本说明对应当前工程版本：Unity `2022.3.62f3c1`，Mbox100控制器 `192.168.15.201`，PC底座网卡 `192.168.15.100/24`。
