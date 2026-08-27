using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        private const uint RIM_TYPEKEYBOARD = 1;
        private const uint RIM_TYPEHID = 2;

        private readonly Dictionary<IntPtr, string> deviceNames = new Dictionary<IntPtr, string>();

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

        public DetectorForm()
        {
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            WindowState = FormWindowState.Minimized;
            Opacity = 0;
            Width = 1;
            Height = 1;
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
                Console.WriteLine("HID: " + name);
            }
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
                    byte[] data = new byte[Math.Max(0, (int)size - offset)];
                    if (data.Length > 0)
                    {
                        Marshal.Copy(IntPtr.Add(buffer, offset), data, 0, data.Length);
                    }
                    Console.WriteLine("[HID BUTTON INPUT] " + DateTime.Now.ToString("HH:mm:ss.fff")
                        + "\n  设备: " + name + "\n  原始数据: " + ToHex(data));
                }
                else if (header.Type == RIM_TYPEKEYBOARD)
                {
                    // 只提示设备，不把普通键盘按键当成底座按钮显示。
                    Console.WriteLine("[KEYBOARD INPUT] " + DateTime.Now.ToString("HH:mm:ss.fff")
                        + "  设备: " + name);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
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
    }
}
