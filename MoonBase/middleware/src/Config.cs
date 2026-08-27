using System;
using System.IO;
using System.Globalization;

namespace MPSdkMiddleware
{
    /// <summary>
    /// Configuration loaded from Config.cfg
    /// </summary>
    class Config
    {
        public int ReadDeviceType { get; set; }
        public int LocalPort { get; set; }
        public string ServerIP { get; set; }
        public int ServerPort { get; set; }
        public int ListenPort { get; set; }

        // Safety limits
        public float MaxRotation { get; set; }     // degrees
        public float MaxRoll { get; set; }
        public float MinPitch { get; set; }
        public float MaxPitch { get; set; }
        public float MaxYaw { get; set; }
        public float MaxTranslation { get; set; }  // mm
        public int HeartbeatTimeoutMs { get; set; } // watchdog timeout
        public bool LogMotionCommands { get; set; }

        public Config()
        {
            ReadDeviceType = 1;
            LocalPort = 8410;
            ServerIP = "192.168.15.201";
            ServerPort = 7408;
            ListenPort = 9999;

            MaxRotation = 15.0f;
            MaxRoll = 5.0f;
            MinPitch = -3.0f;
            MaxPitch = 3.0f;
            MaxYaw = 0.0f;
            MaxTranslation = 100.0f;
            HeartbeatTimeoutMs = 1000;
            LogMotionCommands = false;
        }

        public static Config Load(string path)
        {
            Config cfg = new Config();

            if (!File.Exists(path))
            {
                Console.Error.WriteLine("[Config] \u914D\u7F6E\u6587\u4EF6\u4E0D\u5B58\u5728: " + path);
                Console.Error.WriteLine("[Config] \u4F7F\u7528\u9ED8\u8BA4\u914D\u7F6E");
                return cfg;
            }

            string[] lines = File.ReadAllLines(path);
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("//"))
                    continue;

                string[] parts = line.Split(new char[] { '=' }, 2);
                if (parts.Length != 2) continue;

                string key = parts[0].Trim().ToLower();
                string val = parts[1].Trim();

                try
                {
                    switch (key)
                    {
                        case "readdevicetype":
                            cfg.ReadDeviceType = int.Parse(val);
                            break;
                        case "localport":
                            cfg.LocalPort = int.Parse(val);
                            break;
                        case "serverip":
                            cfg.ServerIP = val;
                            break;
                        case "serverport":
                            cfg.ServerPort = int.Parse(val);
                            break;
                        case "listenport":
                            cfg.ListenPort = int.Parse(val);
                            break;
                        case "maxrotation":
                            cfg.MaxRotation = float.Parse(val, CultureInfo.InvariantCulture);
                            break;
                        case "maxroll":
                            cfg.MaxRoll = float.Parse(val, CultureInfo.InvariantCulture);
                            break;
                        case "minpitch":
                            cfg.MinPitch = float.Parse(val, CultureInfo.InvariantCulture);
                            break;
                        case "maxpitch":
                            cfg.MaxPitch = float.Parse(val, CultureInfo.InvariantCulture);
                            break;
                        case "maxyaw":
                            cfg.MaxYaw = float.Parse(val, CultureInfo.InvariantCulture);
                            break;
                        case "maxtranslation":
                            cfg.MaxTranslation = float.Parse(val, CultureInfo.InvariantCulture);
                            break;
                        case "heartbeattimeoutms":
                            cfg.HeartbeatTimeoutMs = int.Parse(val);
                            break;
                        case "logmotioncommands":
                            cfg.LogMotionCommands = val.Equals("true", StringComparison.OrdinalIgnoreCase) || val == "1";
                            break;
                    }
                }
                catch (FormatException)
                {
                    Console.Error.WriteLine("[Config] \u914D\u7F6E\u503C\u683C\u5F0F\u9519\u8BEF: " + key + "=" + val);
                }
            }

            return cfg;
        }
    }
}
