using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Verse;

public static class RustAgent
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetMagicNumberDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void UpdateGameInfoDelegate([MarshalAs(UnmanagedType.LPStr)] string jsonData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr RustTickDelegate(IntPtr lastResponse);

    private static GetMagicNumberDelegate _getMagicNumber;
    private static UpdateGameInfoDelegate _updateGameInfo;
    private static RustTickDelegate _rustTick;
    private static IntPtr _lastRustResponsePtr = IntPtr.Zero;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

    static RustAgent()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                string assemblyPath = Assembly.GetExecutingAssembly().Location;
                string assemblyDir = Path.GetDirectoryName(assemblyPath);
                string modRoot = Path.GetFullPath(Path.Combine(assemblyDir, "..", ".."));
                string dllPath = Path.Combine(modRoot, "Native", "Windows", "rust_agent.dll");

                if (File.Exists(dllPath))
                {
                    IntPtr hModule = LoadLibrary(dllPath);
                    if (hModule != IntPtr.Zero)
                    {
                        IntPtr pFunc = GetProcAddress(hModule, "get_rust_magic_number");
                        if (pFunc != IntPtr.Zero)
                        {
                            _getMagicNumber = (GetMagicNumberDelegate)Marshal.GetDelegateForFunctionPointer(pFunc, typeof(GetMagicNumberDelegate));
                            Log.Message($"[RimAgent] Successfully bound 'get_rust_magic_number' from {dllPath}");
                        }
                        
                        IntPtr pUpdateFunc = GetProcAddress(hModule, "update_game_info");
                        if (pUpdateFunc != IntPtr.Zero)
                        {
                            _updateGameInfo = (UpdateGameInfoDelegate)Marshal.GetDelegateForFunctionPointer(pUpdateFunc, typeof(UpdateGameInfoDelegate));
                            Log.Message($"[RimAgent] Successfully bound 'update_game_info' from {dllPath}");
                        }
                        else
                        {
                            Log.Error($"[RimAgent] Symbol 'update_game_info' not found in {dllPath}");
                        }

                        IntPtr pTickFunc = GetProcAddress(hModule, "rust_tick");
                        if (pTickFunc != IntPtr.Zero)
                        {
                            _rustTick = (RustTickDelegate)Marshal.GetDelegateForFunctionPointer(pTickFunc, typeof(RustTickDelegate));
                            Log.Message($"[RimAgent] Successfully bound 'rust_tick' from {dllPath}");
                        }
                        else
                        {
                            Log.Error($"[RimAgent] Symbol 'rust_tick' not found in {dllPath}");
                        }
                    }
                    else
                    {
                        Log.Error($"[RimAgent] LoadLibrary failed for {dllPath}. Error: {Marshal.GetLastWin32Error()}");
                    }
                }
                else
                {
                    Log.Error($"[RimAgent] Native library NOT found at {dllPath}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAgent] Error initializing manual binding: {ex}");
            }
        }
    }

    public static int GetRustMagicNumber()
    {
        if (_getMagicNumber == null)
        {
            Log.Error("[RimAgent] Rust function 'get_rust_magic_number' is not bound!");
            return -1;
        }
        return _getMagicNumber();
    }

    public static void UpdateGameInfo(string jsonData)
    {
        if (_updateGameInfo == null)
        {
            // Only log error once to avoid spam
            return;
        }
        _updateGameInfo(jsonData);
    }

    public static string RustTick()
    {
        if (_rustTick == null) return null;

        _lastRustResponsePtr = _rustTick(_lastRustResponsePtr);
        if (_lastRustResponsePtr == IntPtr.Zero) return null;

        return Marshal.PtrToStringAnsi(_lastRustResponsePtr);
    }
}
