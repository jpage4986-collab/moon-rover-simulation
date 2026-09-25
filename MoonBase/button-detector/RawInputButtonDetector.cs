using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace MoonBaseButtonDetector
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("月球车底座实体按钮检测器");
            Console.WriteLine("只监听输入，不发送任何运动、回零或复位指令。\n");

            DetectorForm form = new DetectorForm();
            Console.CancelKeyPress += delegate(object sender, ConsoleCancelEventArgs args)
            {
                args.Cancel = true;
                form.BeginInvoke(new Action(form.Close));
            };

            Application.Run(form);
        }
    }

    internal sealed class DetectorForm : Form
    {
        private const int WM_INPUT = 0x00FF;
        private const uint RID_INPUT = 0x10000003;
        private const uint RIDI_PREPARSEDDATA = 0x20000005;
        private const uint RIDI_DEVICENAME = 0x20000007;
        private const uint RIDEV_INPUTSINK = 0x00000100;
        private const uint RIM_TYPEHID = 2;

        private readonly Dictionary<IntPtr, string> deviceNames = new Dictionary<IntPtr, string>();
        private readonly Dictionary<IntPtr, IntPtr> preparsedData = new Dictionary<IntPtr, IntPtr>();
        private readonly Dictionary<IntPtr, HashSet<ushort>> pressedButtons = new Dictionary<IntPtr, HashSet<ushort>>();
        private readonly TextBox output;

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
            public ushort UsagePage;
            public ushort Usage;
            public uint Flags;
            public IntPtr Target;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICELIST
        {
            public IntPtr Device;
            public uint Type;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTHEADER
        {
            public uint Type;
            public uint Size;
            public IntPtr Device;
            public IntPtr WParam;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterRawInputDevices(
            RAWINPUTDEVICE[] devices, uint count, uint deviceSize);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRawInputDeviceList(
            [In, Out] RAWINPUTDEVICELIST[] devices, ref uint count, uint deviceSize);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern uint GetRawInputDeviceInfo(
            IntPtr device, uint command, IntPtr data, ref uint size);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRawInputData(
            IntPtr rawInput, uint command, IntPtr data, ref uint size, uint headerSize);

        private enum HidPReportType
        {
            Input = 0,
            Output = 1,
            Feature = 2
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HidpCaps
        {
            public ushort Usage;
            public ushort UsagePage;
            public ushort InputReportByteLength;
            public ushort OutputReportByteLength;
            public ushort FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
            public ushort[] Reserved;
        }

        [DllImport("hid.dll")]
        private static extern int HidP_GetCaps(IntPtr preparsedData, ref HidpCaps capabilities);

        [DllImport("hid.dll")]
        private static extern int HidP_GetUsages(
            HidPReportType reportType,
            ushort usagePage,
            ushort linkCollection,
            [Out] ushort[] usageList,
            ref uint usageLength,
            IntPtr preparsedData,
            byte[] report,
            uint reportLength);

        private const int HidpStatusSuccess = 0x00110000;

        public DetectorForm()
        {
            Text = "MoonBase - Base Button Detector";
            ShowInTaskbar = true;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            Width = 900;
            Height = 600;
            BackColor = System.Drawing.Color.FromArgb(18, 24, 32);

            output = new TextBox();
            output.Dock = DockStyle.Fill;
            output.Multiline = true;
            output.ReadOnly = true;
            output.ScrollBars = ScrollBars.Both;
            output.WordWrap = false;
            output.Font = new System.Drawing.Font("Consolas", 11f);
            output.ForeColor = System.Drawing.Color.FromArgb(190, 245, 255);
            output.BackColor = System.Drawing.Color.FromArgb(8, 14, 20);
            output.BorderStyle = BorderStyle.None;
            Controls.Add(output);
            Console.SetOut(new TextBoxWriter(output));
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            EnumerateDevices();
            RegisterInput();
            Console.WriteLine("\n请依次按下底座上的每个实体按钮。按 Ctrl+C 退出。\n");
        }

        private void EnumerateDevices()
        {
            Console.WriteLine("--- 系统输入设备 ---");
            Console.WriteLine("串口设备:");
            string[] ports = System.IO.Ports.SerialPort.GetPortNames();
            if (ports.Length == 0)
            {
                Console.WriteLine("  未发现串口（如果按钮走 USB-HID，这属于正常情况）。");
            }
            else
            {
                foreach (string port in ports)
                {
                    Console.WriteLine("  " + port);
                }
            }

            uint count = 0;
            uint result = GetRawInputDeviceList(null, ref count,
                (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICELIST)));
            if (result == unchecked((uint)-1) || count == 0)
            {
                Console.WriteLine("\nHID 设备枚举失败: " + new Win32Exception(Marshal.GetLastWin32Error()).Message);
                return;
            }

            RAWINPUTDEVICELIST[] devices = new RAWINPUTDEVICELIST[count];
            result = GetRawInputDeviceList(devices, ref count,
                (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICELIST)));
            if (result == unchecked((uint)-1))
            {
                Console.WriteLine("\nHID 设备枚举失败: " + new Win32Exception(Marshal.GetLastWin32Error()).Message);
                return;
            }

            foreach (RAWINPUTDEVICELIST item in devices)
            {
                if (item.Device == IntPtr.Zero || item.Type != RIM_TYPEHID)
                {
                    continue;
                }

                string name = GetDeviceName(item.Device);
                deviceNames[item.Device] = name;
                if (PrepareHidDevice(item.Device))
                {
                    Console.WriteLine("HID: " + name + "  [button reports parsed]");
                }
                else
                {
                    Console.WriteLine("HID: " + name + "  [no standard button page]");
                }
            }
        }

        private bool PrepareHidDevice(IntPtr device)
        {
            uint size = 0;
            if (GetRawInputDeviceInfo(device, RIDI_PREPARSEDDATA, IntPtr.Zero, ref size) == unchecked((uint)-1)
                || size == 0)
            {
                return false;
            }

            IntPtr data = Marshal.AllocHGlobal((int)size);
            uint capacity = size;
            if (GetRawInputDeviceInfo(device, RIDI_PREPARSEDDATA, data, ref capacity) == unchecked((uint)-1))
            {
                Marshal.FreeHGlobal(data);
                return false;
            }

            HidpCaps caps = new HidpCaps { Reserved = new ushort[17] };
            if (HidP_GetCaps(data, ref caps) != HidpStatusSuccess
                || caps.InputReportByteLength == 0)
            {
                Marshal.FreeHGlobal(data);
                return false;
            }

            preparsedData[device] = data;
            pressedButtons[device] = new HashSet<ushort>();
            return true;
        }

        private void RegisterInput()
        {
            RAWINPUTDEVICE[] devices = new RAWINPUTDEVICE[]
            {
                // 常见游戏手柄/按钮盒：Joystick、Gamepad。
                new RAWINPUTDEVICE { UsagePage = 0x01, Usage = 0x04, Flags = RIDEV_INPUTSINK, Target = Handle },
                new RAWINPUTDEVICE { UsagePage = 0x01, Usage = 0x05, Flags = RIDEV_INPUTSINK, Target = Handle },
                // 有些按钮盒会伪装成键盘。
                new RAWINPUTDEVICE { UsagePage = 0x01, Usage = 0x06, Flags = RIDEV_INPUTSINK, Target = Handle }
            };

            if (!RegisterRawInputDevices(devices, (uint)devices.Length,
                (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE))))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "注册 Raw Input 失败");
            }
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == WM_INPUT)
            {
                HandleRawInput(message.LParam);
            }
            base.WndProc(ref message);
        }

        private void HandleRawInput(IntPtr rawInput)
        {
            uint size = 0;
            uint headerSize = (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER));
            if (GetRawInputData(rawInput, RID_INPUT, IntPtr.Zero, ref size, headerSize) == unchecked((uint)-1)
                || size == 0)
            {
                return;
            }

            IntPtr buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                if (GetRawInputData(rawInput, RID_INPUT, buffer, ref size, headerSize) == unchecked((uint)-1))
                {
                    return;
                }

                RAWINPUTHEADER header = (RAWINPUTHEADER)Marshal.PtrToStructure(
                    buffer, typeof(RAWINPUTHEADER));
                string name;
                if (!deviceNames.TryGetValue(header.Device, out name))
                {
                    name = GetDeviceName(header.Device);
                    deviceNames[header.Device] = name;
                }

                if (header.Type == RIM_TYPEHID)
                {
                    int offset = (int)headerSize;
                    byte[] hidData = new byte[Math.Max(0, (int)size - offset)];
                    if (hidData.Length > 0)
                    {
                        Marshal.Copy(IntPtr.Add(buffer, offset), hidData, 0, hidData.Length);
                    }
                    ReportButtonChanges(header.Device, hidData);
                }
                // 键盘类 Raw Input 直接忽略，不显示、不参与按钮检测。
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private void ReportButtonChanges(IntPtr device, byte[] hidData)
        {
            IntPtr ppd;
            if (!preparsedData.TryGetValue(device, out ppd) || hidData.Length < 8)
            {
                return;
            }

            int reportSize = BitConverter.ToInt32(hidData, 0);
            if (reportSize <= 0 || hidData.Length < 8 + reportSize)
            {
                return;
            }

            byte[] report = new byte[reportSize];
            Buffer.BlockCopy(hidData, 8, report, 0, reportSize);
            ushort[] usages = new ushort[128];
            uint usageLength = (uint)usages.Length;
            int status = HidP_GetUsages(
                HidPReportType.Input, 0x09, 0, usages, ref usageLength,
                ppd, report, (uint)report.Length);
            if (status != HidpStatusSuccess)
            {
                return;
            }

            var current = new HashSet<ushort>();
            for (int i = 0; i < usageLength && i < usages.Length; i++)
            {
                current.Add(usages[i]);
            }

            HashSet<ushort> previous;
            if (!pressedButtons.TryGetValue(device, out previous))
            {
                previous = new HashSet<ushort>();
                pressedButtons[device] = previous;
            }

            foreach (ushort usage in current)
            {
                if (!previous.Contains(usage))
                {
                    Console.WriteLine("[BUTTON DOWN] " + DateTime.Now.ToString("HH:mm:ss.fff")
                        + "  设备: " + deviceNames[device] + "  button " + usage);
                }
            }
            foreach (ushort usage in previous)
            {
                if (!current.Contains(usage))
                {
                    Console.WriteLine("[BUTTON UP]   " + DateTime.Now.ToString("HH:mm:ss.fff")
                        + "  设备: " + deviceNames[device] + "  button " + usage);
                }
            }

            pressedButtons[device] = current;
        }

        private static string GetDeviceName(IntPtr device)
        {
            uint size = 0;
            GetRawInputDeviceInfo(device, RIDI_DEVICENAME, IntPtr.Zero, ref size);
            if (size == 0)
            {
                return "未知设备";
            }

            IntPtr buffer = Marshal.AllocHGlobal((int)((size + 1) * 2));
            try
            {
                uint capacity = size;
                if (GetRawInputDeviceInfo(device, RIDI_DEVICENAME, buffer, ref capacity) == unchecked((uint)-1))
                {
                    return "未知设备";
                }
                return Marshal.PtrToStringUni(buffer) ?? "未知设备";
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static string ToHex(byte[] bytes)
        {
            if (bytes.Length == 0)
            {
                return "<空>";
            }
            return BitConverter.ToString(bytes);
        }

        private sealed class TextBoxWriter : TextWriter
        {
            private readonly TextBox box;

            public TextBoxWriter(TextBox box)
            {
                this.box = box;
            }

            public override Encoding Encoding { get { return Encoding.UTF8; } }

            public override void Write(char value)
            {
                Write(value.ToString());
            }

            public override void Write(string value)
            {
                if (box.IsDisposed) return;
                if (box.InvokeRequired)
                {
                    box.BeginInvoke(new Action<string>(Write), value);
                    return;
                }
                box.AppendText(value);
            }
        }
    }
}
