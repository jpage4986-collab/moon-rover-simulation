using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Globalization;

namespace MPSdkMiddleware
{
    /// <summary>
    /// MPSdkMiddleware — TCP bridge between Unity (MotionPlatformController)
    /// and the 汇鼎 Mbox100 6-DOF Stewart platform via MpDll.dll.
    ///
    /// Protocol (per 底座.md):
    ///   Runing#Rx#Ry#Rz#X#Y#Z#effcet1#effcet2#time#end
    ///   Zero#end
    ///   Reset#end
    ///
    /// Safety features:
    ///   1. Input limiting — clamps rotation/translation to safe range
    ///   2. Startup homing — auto Zero on startup
    ///   3. Disconnect safety — auto Zero on client disconnect
    ///   4. Heartbeat watchdog — auto Zero if no commands for N ms
    ///   5. Graceful shutdown — auto Zero on Ctrl+C
    /// </summary>
    class Program
    {
        private static Config config;
        private static MotionController motionCtrl;
        private static TcpListener tcpListener;
        private static volatile bool running = true;

        // ---- Safety state ----
        private static volatile bool clientConnected = false;
        private static long lastCommandTicks = DateTime.MinValue.Ticks;
        private static volatile bool watchdogZeroSent = false;
        private static Timer heartbeatTimer;
        private static readonly object safetyLock = new object();
        private static readonly object perfLock = new object();
        private static int perfCommandCount = 0;
        private static int perfClampCount = 0;
        private static double perfSdkTotalMs = 0.0;
        private static double perfSdkMaxMs = 0.0;
        private static DateTime perfWindowStart = DateTime.UtcNow;

        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Console.Error.WriteLine("[FATAL] Unhandled exception: " + e.ExceptionObject);
            };

            Console.WriteLine("===========================================");
            Console.WriteLine("  MPSdkMiddleware - 汇鼎Mbox100运动平台中间件");
            Console.WriteLine("  替换 MPSdkMiddleware.exe 的纯.NET实现");
            Console.WriteLine("  启动时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Console.WriteLine("===========================================");

            // 1. Load config
            string configPath = (args.Length > 0) ? args[0] : "Config.cfg";
            config = Config.Load(configPath);
            PrintConfig();

            // 2. Setup C:\ProgramData\MP\
            SetupMpConfigDirectory();

            // 3. Initialize MpDll.dll
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            motionCtrl = new MotionController();
            motionCtrl.Initialize(appDir, config);
            Console.WriteLine();

            // 4. Safety startup: home the platform
            if (motionCtrl.IsInitialized)
            {
                Console.WriteLine("[Safety] 启动自检: 正在回零...");
                motionCtrl.SendZero();
                Thread.Sleep(500);
                Console.WriteLine("[Safety] 启动自检完成");
                Console.WriteLine();
            }

            // 5. Start heartbeat watchdog timer
            heartbeatTimer = new Timer(HeartbeatCheck, null, 500, 200);

            // 6. Start TCP server
            StartTcpServer();

            // 7. Graceful shutdown on Ctrl+C
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                running = false;
                Console.WriteLine("\n[Program] 收到退出信号...");
            };

            // 8. Keep alive
            Console.WriteLine("[Program] 中间件运行中. 按 Ctrl+C 停止.");
            Console.WriteLine();

            while (running)
            {
                Thread.Sleep(200);
            }

            // 9. Cleanup
            Shutdown();
        }

        static void PrintConfig()
        {
            Console.WriteLine("[配置]");
            Console.WriteLine("  网络:");
            Console.WriteLine("    ReadDeviceType = " + config.ReadDeviceType);
            Console.WriteLine("    本地端口       = " + config.LocalPort);
            Console.WriteLine("    控制器地址     = " + config.ServerIP + ":" + config.ServerPort);
            Console.WriteLine("    TCP监听端口    = " + config.ListenPort);
            Console.WriteLine("  安全保护:");
            Console.WriteLine("    Roll 限位      = \u00B1" + config.MaxRoll + "\u00B0");
            Console.WriteLine("    Pitch 限位     = " + config.MinPitch + "\u00B0 ~ " + config.MaxPitch + "\u00B0");
            Console.WriteLine("    Yaw 限位       = \u00B1" + config.MaxYaw + "\u00B0");
            Console.WriteLine("    最大平移量     = " + config.MaxTranslation + "mm");
            Console.WriteLine("    看门狗超时     = " + config.HeartbeatTimeoutMs + "ms");
            Console.WriteLine("    启动自回归零   = 已启用");
            Console.WriteLine("    断连自动回零   = 已启用");
            Console.WriteLine("    心跳丢失回零   = 已启用");
            Console.WriteLine();
        }

        // ============================================================
        //  SAFETY: Heartbeat Watchdog
        // ============================================================
        static void HeartbeatCheck(object state)
        {
            if (!running) return;
            if (!clientConnected) return;
            if (!motionCtrl.IsInitialized) return;

            lock (safetyLock)
            {
                long now = DateTime.Now.Ticks;
                long elapsedMs = (now - lastCommandTicks) / TimeSpan.TicksPerMillisecond;
                if (elapsedMs >= config.HeartbeatTimeoutMs)
                {
                    if (!watchdogZeroSent)
                    {
                        Console.Error.WriteLine("[Safety] ⚠ 看门狗触发: " + elapsedMs + "ms 未收到指令, 自动回零!");
                        motionCtrl.SendZero();
                        watchdogZeroSent = true;
                    }
                }
            }
        }

        // ============================================================
        //  SAFETY: Input Limiting
        // ============================================================
        static float ClampAxis(float value, float min, float max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        static float ClampTranslation(float value)
        {
            float max = config.MaxTranslation;
            if (value > max)
            {
                Console.Error.WriteLine("[Safety] ⚠ X/Y/Z 超限: " + value.ToString("F2") + " -> 钳位到 " + max.ToString("F2"));
                return max;
            }
            if (value < -max)
            {
                Console.Error.WriteLine("[Safety] ⚠ X/Y/Z 超限: " + value.ToString("F2") + " -> 钳位到 " + (-max).ToString("F2"));
                return -max;
            }
            return value;
        }

        // ============================================================
        //  C:\ProgramData\MP\ config directory
        // ============================================================
        static void SetupMpConfigDirectory()
        {
            string mpDir = @"C:\ProgramData\MP";
            try
            {
                if (!Directory.Exists(mpDir))
                {
                    Directory.CreateDirectory(mpDir);
                    Console.WriteLine("[Config] 创建目录: " + mpDir);
                }

                string platPath = Path.Combine(mpDir, "plat.xml");
                if (!File.Exists(platPath))
                {
                    WriteDefaultPlatXml(platPath);
                    Console.WriteLine("[Config] 创建默认配置: plat.xml");
                }

                string appsetPath = Path.Combine(mpDir, "appset.xml");
                if (!File.Exists(appsetPath))
                {
                    WriteDefaultAppsetXml(appsetPath);
                    Console.WriteLine("[Config] 创建默认配置: appset.xml");
                }

                Console.WriteLine("[Config] MP 配置目录准备就绪");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[Config] 无法创建 MP 配置: " + ex.Message);
            }
            Console.WriteLine();
        }

        static void WriteDefaultPlatXml(string path)
        {
            string xml = "<?xml version=\"1.0\" encoding=\"utf-16\"?>\r\n";
            xml += "<PlatParameter xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">\r\n";
            xml += "  <PlatDriverType>Vegafu_CAN</PlatDriverType>\r\n";
            xml += "  <DriverEx>192.168.15.201</DriverEx>\r\n";
            xml += "  <DOF>6</DOF>\r\n";
            xml += "  <legtype>cylinder</legtype>\r\n";
            xml += "  <PlatSpacedef1>870</PlatSpacedef1>\r\n";
            xml += "  <PlatSpacedef2>100</PlatSpacedef2>\r\n";
            xml += "  <PlatSpacedef3>870</PlatSpacedef3>\r\n";
            xml += "  <PlatSpacedef4>100</PlatSpacedef4>\r\n";
            xml += "  <PlatoffsetX>0</PlatoffsetX>\r\n";
            xml += "  <PlatoffsetY>0</PlatoffsetY>\r\n";
            xml += "  <PlatBaseHeight>515</PlatBaseHeight>\r\n";
            xml += "  <CylinderMaxlen>290</CylinderMaxlen>\r\n";
            xml += "  <CylinFixLen>620</CylinFixLen>\r\n";
            xml += "  <PulseUnit>1500</PulseUnit>\r\n";
            xml += "  <HasTurnMoto>false</HasTurnMoto>\r\n";
            xml += "  <PulsePerAngle>1</PulsePerAngle>\r\n";
            xml += "  <PulsePerAngle_crank>1</PulsePerAngle_crank>\r\n";
            xml += "</PlatParameter>";
            File.WriteAllText(path, xml);
        }

        static void WriteDefaultAppsetXml(string path)
        {
            string xml = "<?xml version=\"1.0\"?>\r\n";
            xml += "<ClassAppset xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">\r\n";
            xml += "  <\u6570\u636E\u5305\u95F4\u9694>100</\u6570\u636E\u5305\u95F4\u9694>\r\n";
            xml += "  <\u65CB\u8F6C\u8F74\u590D\u4F4D\u89D2\u5EA6>0</\u65CB\u8F6C\u8F74\u590D\u4F4D\u89D2\u5EA6>\r\n";
            xml += "  <\u6700\u5927\u65CB\u8F6C\u89D2>180</\u6700\u5927\u65CB\u8F6C\u89D2>\r\n";
            xml += "  <\u6700\u5C0F\u65CB\u8F6C\u89D2>0</\u6700\u5C0F\u65CB\u8F6C\u89D2>\r\n";
            xml += "  <udp\u6570\u636E\u53CD\u9988>true</udp\u6570\u636E\u53CD\u9988>\r\n";
            xml += "</ClassAppset>";
            File.WriteAllText(path, xml);
        }

        // ============================================================
        //  TCP Server
        // ============================================================
        static void StartTcpServer()
        {
            try
            {
                tcpListener = new TcpListener(IPAddress.Loopback, config.ListenPort);
                tcpListener.Start();
                Console.WriteLine("[TCP] 监听 127.0.0.1:" + config.ListenPort);
                Console.WriteLine();

                Thread acceptThread = new Thread(AcceptLoop);
                acceptThread.IsBackground = true;
                acceptThread.Name = "TCP-Accept";
                acceptThread.Start();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[TCP] 启动失败: " + ex.Message);
            }
        }

        static void AcceptLoop()
        {
            while (running)
            {
                try
                {
                    TcpClient client = tcpListener.AcceptTcpClient();
                    client.NoDelay = true;
                    Console.WriteLine("[TCP] 客户端已连接: " + client.Client.RemoteEndPoint);

                    Thread clientThread = new Thread(HandleClient);
                    clientThread.IsBackground = true;
                    clientThread.Name = "TCP-Client";
                    clientThread.Start(client);
                }
                catch (SocketException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (running)
                        Console.Error.WriteLine("[TCP] Accept 错误: " + ex.Message);
                }
            }
        }

        static void HandleClient(object obj)
        {
            TcpClient client = (TcpClient)obj;

            // Mark connected and reset watchdog state
            clientConnected = true;
            lastCommandTicks = DateTime.Now.Ticks;
            watchdogZeroSent = false;

            try
            {
                using (client)
                using (NetworkStream stream = client.GetStream())
                using (StreamReader reader = new StreamReader(stream, Encoding.ASCII))
                {
                    string line;
                    int cmdCount = 0;
                    while (running && (line = reader.ReadLine()) != null)
                    {
                        string trimmed = line.Trim();
                        if (trimmed.Length == 0) continue;

                        ProcessCommand(trimmed);
                        cmdCount++;
                    }
                    Console.WriteLine("[TCP] 客户端已断开 (共处理 " + cmdCount + " 条指令)");
                }
            }
            catch (IOException)
            {
                Console.WriteLine("[TCP] 客户端连接已断开");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[TCP] 客户端处理错误: " + ex.Message);
            }
            finally
            {
                // SAFETY: Auto-zero on disconnect
                clientConnected = false;
                Console.WriteLine("[Safety] 客户端已断开, 正在回零保护...");
                if (motionCtrl != null && motionCtrl.IsInitialized)
                {
                    motionCtrl.SendZero();
                }
                Console.WriteLine("[TCP] 等待新的客户端连接...");
            }
        }

        // ============================================================
        //  Command Processing
        // ============================================================
        static void ProcessCommand(string command)
        {
            if (!command.EndsWith("#end"))
            {
                Console.WriteLine("[Parser] 指令缺少 #end 后缀: " + command);
                return;
            }

            string payload = command.Substring(0, command.Length - 4);
            string[] parts = payload.Split('#');

            if (parts.Length == 0) return;
            string cmdType = parts[0];

            switch (cmdType)
            {
                case "Runing":
                    HandleRuning(parts);
                    break;
                case "Zero":
                    HandleZero();
                    break;
                case "Reset":
                    HandleReset();
                    break;
                default:
                    Console.WriteLine("[Parser] 未知指令: " + cmdType);
                    break;
            }
        }

        static void HandleRuning(string[] parts)
        {
            if (parts.Length < 10)
            {
                Console.Error.WriteLine("[Parser] Runing: 参数不足 (" + parts.Length + "/10)");
                return;
            }

            try
            {
                float rx = -float.Parse(parts[1], CultureInfo.InvariantCulture); // 左右取反
                float ry = -float.Parse(parts[2], CultureInfo.InvariantCulture); // 前后取反
                float rz = float.Parse(parts[3], CultureInfo.InvariantCulture);
                float x  = float.Parse(parts[4], CultureInfo.InvariantCulture);
                float y  = float.Parse(parts[5], CultureInfo.InvariantCulture);
                float z  = float.Parse(parts[6], CultureInfo.InvariantCulture);
                byte eff1 = byte.Parse(parts[7]);
                byte eff2 = byte.Parse(parts[8]);
                int time  = int.Parse(parts[9]);

                // SAFETY: Clamp inputs
                float crx = ClampAxis(rx, -config.MaxRoll, config.MaxRoll);
                float cry = ClampAxis(ry, config.MinPitch, config.MaxPitch);
                float crz = ClampAxis(rz, -config.MaxYaw, config.MaxYaw);
                float cx  = ClampTranslation(x);
                float cy  = ClampTranslation(y);
                float cz  = ClampTranslation(z);
                bool wasClamped = crx != rx || cry != ry || crz != rz || cx != x || cy != y || cz != z;

                // Log with clamp indicators
                string log = string.Format("[指令] Runing Rx={0,7:F2} Ry={1,7:F2} Rz={2,7:F2}  X={3,7:F2} Y={4,7:F2} Z={5,7:F2}  E1={6} E2={7} T={8}ms",
                    crx, cry, crz, cx, cy, cz, eff1, eff2, time);
                if (config.LogMotionCommands)
                    Console.WriteLine(log);

                // Reset heartbeat watchdog
                lock (safetyLock)
                {
                    lastCommandTicks = DateTime.Now.Ticks;
                    watchdogZeroSent = false;
                }

                if (motionCtrl.IsInitialized)
                {
                    Stopwatch sdkTimer = Stopwatch.StartNew();
                    motionCtrl.SendMotion(crx, cry, crz, cx, cy, cz, eff1, eff2, time);
                    sdkTimer.Stop();
                    RecordPerformance(sdkTimer.Elapsed.TotalMilliseconds, wasClamped);
                }
                else
                {
                    Console.Error.WriteLine("[指令] MotionController 未初始化，跳过");
                }
            }
            catch (FormatException ex)
            {
                Console.Error.WriteLine("[Parser] Runing: 数字格式错误 - " + ex.Message);
            }
            catch (OverflowException ex)
            {
                Console.Error.WriteLine("[Parser] Runing: 数值超出范围 - " + ex.Message);
            }
        }

        static void RecordPerformance(double sdkMs, bool wasClamped)
        {
            lock (perfLock)
            {
                perfCommandCount++;
                if (wasClamped) perfClampCount++;
                perfSdkTotalMs += sdkMs;
                if (sdkMs > perfSdkMaxMs) perfSdkMaxMs = sdkMs;

                double windowSeconds = (DateTime.UtcNow - perfWindowStart).TotalSeconds;
                if (windowSeconds < 1.0) return;

                double hz = perfCommandCount / windowSeconds;
                double averageMs = perfCommandCount > 0 ? perfSdkTotalMs / perfCommandCount : 0.0;
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "[Perf] TCP={0:F1}Hz SDK avg={1:F2}ms max={2:F2}ms clamp={3}",
                    hz, averageMs, perfSdkMaxMs, perfClampCount));

                perfCommandCount = 0;
                perfClampCount = 0;
                perfSdkTotalMs = 0.0;
                perfSdkMaxMs = 0.0;
                perfWindowStart = DateTime.UtcNow;
            }
        }

        static void HandleZero()
        {
            Console.WriteLine("[指令] Zero - 回零复位");
            if (motionCtrl != null)
                motionCtrl.SendZero();
        }

        static void HandleReset()
        {
            Console.WriteLine("[指令] Reset - 设置当前位置为原点");
            if (motionCtrl != null)
                motionCtrl.SendReset();
        }

        // ============================================================
        //  Shutdown
        // ============================================================
        static void Shutdown()
        {
            Console.WriteLine();
            Console.WriteLine("[Program] 正在安全关闭...");

            // Stop watchdog first
            if (heartbeatTimer != null)
            {
                heartbeatTimer.Dispose();
                heartbeatTimer = null;
            }

            // SAFETY: Auto-zero before exit
            if (motionCtrl != null)
            {
                Console.WriteLine("[Safety] 退出回零...");
                motionCtrl.Shutdown();
            }

            try
            {
                if (tcpListener != null)
                    tcpListener.Stop();
            }
            catch { }

            Console.WriteLine("[Program] 已退出");
        }
    }
}
