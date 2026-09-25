using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

internal static class LogitechSdkProbe
{
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    private struct State
    {
        public int lX, lY, lZ, lRx, lRy, lRz;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)] public int[] sliders;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public uint[] pov;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)] public byte[] buttons;
        public int lVX, lVY, lVZ, lVRx, lVRy, lVRz;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)] public int[] vSliders;
        public int lAX, lAY, lAZ, lARx, lARy, lARz;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)] public int[] aSliders;
        public int lFX, lFY, lFZ, lFRx, lFRy, lFRz;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)] public int[] fSliders;
    }

    [DllImport("LogitechSteeringWheelEnginesWrapper", CallingConvention = CallingConvention.Cdecl)]
    private static extern bool LogiSteeringInitialize(bool ignoreXInputControllers);
    [DllImport("LogitechSteeringWheelEnginesWrapper", CallingConvention = CallingConvention.Cdecl)]
    private static extern bool LogiUpdate();
    [DllImport("LogitechSteeringWheelEnginesWrapper", CallingConvention = CallingConvention.Cdecl)]
    private static extern bool LogiIsConnected(int index);
    [DllImport("LogitechSteeringWheelEnginesWrapper", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool LogiGetFriendlyProductName(int index, StringBuilder value, int size);
    [DllImport("LogitechSteeringWheelEnginesWrapper", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool LogiGetDevicePath(int index, StringBuilder value, int size);
    [DllImport("LogitechSteeringWheelEnginesWrapper", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr LogiGetStateENGINES(int index);
    [DllImport("LogitechSteeringWheelEnginesWrapper", CallingConvention = CallingConvention.Cdecl)]
    private static extern void LogiSteeringShutdown();

    private static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        bool initialized = LogiSteeringInitialize(false);
        Console.WriteLine("Logitech SDK initialized=" + initialized);
        for (int sample = 0; sample < 8; sample++)
        {
            bool updated = LogiUpdate();
            Console.WriteLine("sample=" + sample + " updated=" + updated);
            for (int index = 0; index < 4; index++)
            {
                if (!LogiIsConnected(index))
                {
                    Console.WriteLine("  index " + index + ": disconnected");
                    continue;
                }

                var name = new StringBuilder(256);
                var path = new StringBuilder(512);
                LogiGetFriendlyProductName(index, name, name.Capacity);
                LogiGetDevicePath(index, path, path.Capacity);
                IntPtr statePtr = LogiGetStateENGINES(index);
                if (statePtr == IntPtr.Zero)
                {
                    Console.WriteLine("  index " + index + ": connected, state=null, name=" + name);
                    continue;
                }
                State state = (State)Marshal.PtrToStructure(statePtr, typeof(State));
                Console.WriteLine("  index {0}: X={1} Y={2} Rz={3} name={4}", index, state.lX, state.lY, state.lRz, name);
                if (sample == 0) Console.WriteLine("    path=" + path);
            }
            Thread.Sleep(350);
        }
        LogiSteeringShutdown();
    }
}
