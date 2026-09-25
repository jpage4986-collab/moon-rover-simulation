using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace MoonBaseButtonDetector
{
    internal static class Program
    {
        private const uint JoyReturnButtons = 0x00000080;
        private const uint JoyError = 160;
        private const uint JoyNoDriver = 6;

        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [StructLayout(LayoutKind.Sequential)]
        private struct JoyInfoEx
        {
            public uint dwSize;
            public uint dwFlags;
            public uint dwXpos;
            public uint dwYpos;
            public uint dwZpos;
            public uint dwRpos;
            public uint dwUpos;
            public uint dwVpos;
            public uint dwButtons;
            public uint dwButtonNumber;
            public uint dwPOV;
            public uint dwReserved1;
            public uint dwReserved2;
        }

        [DllImport("winmm.dll")]
        private static extern uint joyGetNumDevs();

        [DllImport("winmm.dll")]
        private static extern uint joyGetPosEx(uint uJoyID, ref JoyInfoEx pji);

        private static void Main()
        {
            if (GetConsoleWindow() == IntPtr.Zero)
            {
                AllocConsole();
            }

            Console.OutputEncoding = Encoding.UTF8;
            Console.Title = "MoonBase - Base Button Detector";

            Console.WriteLine("MoonBase base button detector");
            Console.WriteLine("This program only reads button states. It sends no motion command.");
            Console.WriteLine("Press a base button: BUTTON DOWN will appear.");
            Console.WriteLine("Release it: BUTTON UP will appear.");
            Console.WriteLine("Press Ctrl+C to exit.");
            Console.WriteLine();

            var devices = DiscoverDevices();
            if (devices.Count == 0)
            {
                Console.WriteLine("NO JOYSTICK FOUND.");
                Console.WriteLine("Reconnect the controller and run this program again.");
                WaitForever();
                return;
            }

            var previous = new Dictionary<uint, uint>();
            foreach (uint id in devices)
            {
                uint buttons;
                if (TryReadButtons(id, out buttons))
                {
                    previous[id] = buttons;
                    Console.WriteLine("ONLINE: device #{0}, initial buttons = 0x{1:X8}", id, buttons);
                }
            }

            Console.WriteLine();
            Console.WriteLine("LISTENING NOW...");
            Console.WriteLine();

            while (true)
            {
                foreach (uint id in devices)
                {
                    uint current;
                    if (!TryReadButtons(id, out current))
                    {
                        continue;
                    }

                    uint old;
                    if (!previous.TryGetValue(id, out old))
                    {
                        previous[id] = current;
                        continue;
                    }

                    uint changed = old ^ current;
                    for (int bit = 0; bit < 32; bit++)
                    {
                        uint mask = 1u << bit;
                        if ((changed & mask) == 0)
                        {
                            continue;
                        }

                        string action = (current & mask) != 0
                            ? "BUTTON DOWN"
                            : "BUTTON UP";
                        Console.WriteLine("[{0}] {1:HH:mm:ss.fff} device #{2}, button {3}",
                            action, DateTime.Now, id, bit + 1);
                    }

                    previous[id] = current;
                }

                Thread.Sleep(10);
            }
        }

        private static List<uint> DiscoverDevices()
        {
            var devices = new List<uint>();
            uint count = joyGetNumDevs();
            for (uint id = 0; id < count; id++)
            {
                uint buttons;
                if (TryReadButtons(id, out buttons))
                {
                    devices.Add(id);
                }
            }
            return devices;
        }

        private static bool TryReadButtons(uint id, out uint buttons)
        {
            var info = new JoyInfoEx
            {
                dwSize = (uint)Marshal.SizeOf(typeof(JoyInfoEx)),
                dwFlags = JoyReturnButtons
            };

            uint result = joyGetPosEx(id, ref info);
            if (result == 0)
            {
                buttons = info.dwButtons;
                return true;
            }

            buttons = 0;
            return result != JoyError && result != JoyNoDriver;
        }

        private static void WaitForever()
        {
            while (true)
            {
                Thread.Sleep(1000);
            }
        }
    }
}
