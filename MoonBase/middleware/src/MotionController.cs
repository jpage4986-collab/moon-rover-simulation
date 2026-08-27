using System;
using System.IO;
using System.Reflection;

namespace MPSdkMiddleware
{
    /// <summary>
    /// Bridges TCP commands to MpDll.dll (汇鼎 Mbox100 SDK).
    /// Uses MpDll.MoveDll class — the same initialization path as the
    /// official MPSdkMiddleware.exe. This ensures XML configs are read,
    /// SafeNet dongle is verified, and network settings are applied
    /// correctly before sending motion commands.
    /// </summary>
    class MotionController
    {
        private object moveDllInstance;
        private MethodInfo methodControlActions;
        private MethodInfo methodSendReset;
        private MethodInfo methodSendreadySta;
        private bool initialized;
        private string errorMessage;

        public bool IsInitialized
        {
            get { return initialized; }
        }

        public string ErrorMessage
        {
            get { return errorMessage ?? "OK"; }
        }

        /// <summary>
        /// Load MpDll.dll, create MoveDll instance with full initialization.
        /// MoveDll constructor handles:
        ///   - XML config loading (48.xml, plat.xml, appset.xml)
        ///   - SafeNet dongle check
        ///   - Network configuration (serverIP, ports)
        ///   - Device type setup
        /// </summary>
        public void Initialize(string dllDir, Config config)
        {
            initialized = false;

            try
            {
                string mpDllPath = Path.Combine(dllDir, "MpDll.dll");
                if (!File.Exists(mpDllPath))
                {
                    errorMessage = "MpDll.dll not found at: " + mpDllPath;
                    Console.Error.WriteLine("[MotionCtrl] " + errorMessage);
                    return;
                }

                Assembly mpAssembly = Assembly.LoadFrom(mpDllPath);

                // Step 1: Create MoveDll instance with full initialization.
                // This mirrors the official MPSdkMiddleware.Server constructor:
                //   new MoveDll(deviceIdentifier, localPort, targetIP, targetPort)
                Console.WriteLine("[MotionCtrl] Creating MoveDll instance...");
                Type moveDllType = mpAssembly.GetType("MpDll.MoveDll");
                if (moveDllType == null)
                {
                    errorMessage = "MoveDll type not found in MpDll.dll";
                    Console.Error.WriteLine("[MotionCtrl] " + errorMessage);
                    return;
                }

                moveDllInstance = Activator.CreateInstance(
                    moveDllType,
                    new object[] {
                        config.ReadDeviceType,
                        config.LocalPort,
                        config.ServerIP,
                        config.ServerPort
                    });
                Console.WriteLine("[MotionCtrl] MoveDll instance created successfully");

                // Step 2: Resolve methods
                methodControlActions = moveDllType.GetMethod("ControlActions");
                methodSendReset = moveDllType.GetMethod("SendReset");
                methodSendreadySta = moveDllType.GetMethod("SendreadySta");

                if (methodControlActions == null)
                    Console.Error.WriteLine("[MotionCtrl] ControlActions() not found");
                if (methodSendReset == null)
                    Console.Error.WriteLine("[MotionCtrl] SendReset() not found");
                if (methodSendreadySta == null)
                    Console.Error.WriteLine("[MotionCtrl] SendreadySta() not found");

                initialized = (methodControlActions != null);
                if (initialized)
                {
                    Console.WriteLine("[MotionCtrl] DLL initialized successfully via MoveDll");
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                Console.Error.WriteLine("[MotionCtrl] Failed: " + ex.GetType().Name + ": " + ex.Message);

                // Log inner exception details if available
                if (ex.InnerException != null)
                {
                    Console.Error.WriteLine("[MotionCtrl]   Inner: " + ex.InnerException.GetType().Name + ": " + ex.InnerException.Message);
                    if (ex.InnerException.InnerException != null)
                        Console.Error.WriteLine("[MotionCtrl]   Inner2: " + ex.InnerException.InnerException.GetType().Name + ": " + ex.InnerException.InnerException.Message);
                }

                if (ex is ReflectionTypeLoadException)
                {
                    ReflectionTypeLoadException rtle = (ReflectionTypeLoadException)ex;
                    foreach (Exception le in rtle.LoaderExceptions)
                    {
                        if (le != null)
                            Console.Error.WriteLine("  -> " + le.Message);
                    }
                }
            }
        }

        /// <summary>
        /// Send motion command via MoveDll.ControlActions()
        /// </summary>
        public bool SendMotion(float rx, float ry, float rz,
                               float x, float y, float z,
                               byte eff1, byte eff2, int time)
        {
            if (!initialized) return false;

            try
            {
                methodControlActions.Invoke(moveDllInstance,
                    new object[] { rx, ry, rz, x, y, z, eff1, eff2, time });
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[MotionCtrl] ControlActions error: " + ex.Message);
                if (ex.InnerException != null)
                    Console.Error.WriteLine("[MotionCtrl]   Inner: " + ex.InnerException.Message);
                return false;
            }
        }

        /// <summary>
        /// Send Zero command — platform returns to zero position.
        /// Uses MoveDll.SendreadySta().
        /// </summary>
        public bool SendZero()
        {
            if (!initialized) return false;

            try
            {
                if (methodSendreadySta != null)
                {
                    methodSendreadySta.Invoke(moveDllInstance, null);
                    Console.WriteLine("[MotionCtrl] Zero: SendreadySta() via MoveDll");
                    return true;
                }
                else
                {
                    Console.Error.WriteLine("[MotionCtrl] No method for Zero command");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[MotionCtrl] SendZero error: " + ex.Message);
                if (ex.InnerException != null)
                    Console.Error.WriteLine("[MotionCtrl]   Inner: " + ex.InnerException.Message);
                return false;
            }
        }

        /// <summary>
        /// Send Reset command — set current position as origin.
        /// </summary>
        public bool SendReset()
        {
            if (!initialized) return false;

            try
            {
                if (methodSendReset != null)
                {
                    methodSendReset.Invoke(moveDllInstance, null);
                    Console.WriteLine("[MotionCtrl] Reset: SendReset() via MoveDll");
                    return true;
                }
                else
                {
                    Console.Error.WriteLine("[MotionCtrl] SendReset() not available");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[MotionCtrl] SendReset error: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Graceful shutdown — send zero before exiting.
        /// </summary>
        public void Shutdown()
        {
            if (initialized)
            {
                try
                {
                    Console.WriteLine("[MotionCtrl] Shutdown: sending Zero...");
                    SendZero();
                }
                catch
                {
                    // best effort
                }
                initialized = false;
            }
        }
    }
}
