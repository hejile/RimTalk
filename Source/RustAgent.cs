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
    public delegate void UpdateSettingsDelegate([MarshalAs(UnmanagedType.LPStr)] string jsonData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr RustTickDelegate(IntPtr lastResponse);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void RustStartDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void RustExitDelegate();

    private static GetMagicNumberDelegate _getMagicNumber;
    private static UpdateGameInfoDelegate _updateGameInfo;
    private static UpdateSettingsDelegate _updateSettings;
    private static RustTickDelegate _rustTick;
    private static RustStartDelegate _rustStart;
    private static RustExitDelegate _rustExit;
    private static IntPtr _lastRustResponsePtr = IntPtr.Zero;
    private static IntPtr _hModule = IntPtr.Zero;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeLibrary(IntPtr hModule);

    public static void Initialize()
    {
        if (_hModule != IntPtr.Zero) return;

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
                    _hModule = LoadLibrary(dllPath);
                    if (_hModule != IntPtr.Zero)
                    {
                        IntPtr pFunc = GetProcAddress(_hModule, "get_rust_magic_number");
                        if (pFunc != IntPtr.Zero)
                        {
                            _getMagicNumber = (GetMagicNumberDelegate)Marshal.GetDelegateForFunctionPointer(pFunc, typeof(GetMagicNumberDelegate));
                            Log.Message($"[RimAgent] Successfully bound 'get_rust_magic_number' from {dllPath}");
                        }
                        
                        IntPtr pUpdateFunc = GetProcAddress(_hModule, "update_game_info");
                        if (pUpdateFunc != IntPtr.Zero)
                        {
                            _updateGameInfo = (UpdateGameInfoDelegate)Marshal.GetDelegateForFunctionPointer(pUpdateFunc, typeof(UpdateGameInfoDelegate));
                            Log.Message($"[RimAgent] Successfully bound 'update_game_info' from {dllPath}");
                        }
                        else
                        {
                            Log.Error($"[RimAgent] Symbol 'update_game_info' not found in {dllPath}");
                        }

                        IntPtr pSettingsFunc = GetProcAddress(_hModule, "update_settings");
                        if (pSettingsFunc != IntPtr.Zero)
                        {
                            _updateSettings = (UpdateSettingsDelegate)Marshal.GetDelegateForFunctionPointer(pSettingsFunc, typeof(UpdateSettingsDelegate));
                            Log.Message($"[RimAgent] Successfully bound 'update_settings' from {dllPath}");
                        }
                        else
                        {
                            Log.Error($"[RimAgent] Symbol 'update_settings' not found in {dllPath}");
                        }

                        IntPtr pTickFunc = GetProcAddress(_hModule, "rust_tick");
                        if (pTickFunc != IntPtr.Zero)
                        {
                            _rustTick = (RustTickDelegate)Marshal.GetDelegateForFunctionPointer(pTickFunc, typeof(RustTickDelegate));
                            Log.Message($"[RimAgent] Successfully bound 'rust_tick' from {dllPath}");
                        }
                        else
                        {
                            Log.Error($"[RimAgent] Symbol 'rust_tick' not found in {dllPath}");
                        }

                        IntPtr pStartFunc = GetProcAddress(_hModule, "rust_start");
                        if (pStartFunc != IntPtr.Zero)
                        {
                            _rustStart = (RustStartDelegate)Marshal.GetDelegateForFunctionPointer(pStartFunc, typeof(RustStartDelegate));
                            Log.Message($"[RimAgent] Successfully bound 'rust_start' from {dllPath}");
                        }
                        else
                        {
                            Log.Error($"[RimAgent] Symbol 'rust_start' not found in {dllPath}");
                        }

                        IntPtr pExitFunc = GetProcAddress(_hModule, "rust_exit");
                        if (pExitFunc != IntPtr.Zero)
                        {
                            _rustExit = (RustExitDelegate)Marshal.GetDelegateForFunctionPointer(pExitFunc, typeof(RustExitDelegate));
                            Log.Message($"[RimAgent] Successfully bound 'rust_exit' from {dllPath}");
                        }
                        else
                        {
                            Log.Error($"[RimAgent] Symbol 'rust_exit' not found in {dllPath}");
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

    public static void StopRust()
    {
        if (_rustExit != null)
        {
            try
            {
                _rustExit();
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAgent] Error calling rust_exit: {ex}");
            }
        }

        _getMagicNumber = null;
        _updateGameInfo = null;
        _updateSettings = null;
        _rustTick = null;
        _rustStart = null;
        _rustExit = null;
        _lastRustResponsePtr = IntPtr.Zero;

        if (_hModule != IntPtr.Zero)
        {
            if (FreeLibrary(_hModule))
            {
                Log.Message("[RimAgent] Successfully unloaded rust_agent.dll");
                _hModule = IntPtr.Zero;
            }
            else
            {
                Log.Error($"[RimAgent] Failed to unload rust_agent.dll. Error: {Marshal.GetLastWin32Error()}");
            }
        }
    }

    public static void StartRust()
    {
        Initialize();
        _rustStart?.Invoke();
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

    public static void UpdateSettings(string jsonData)
    {
        if (_updateSettings == null)
        {
            return;
        }
        _updateSettings(jsonData);
    }

    public static string RustTick()
    {
        if (_rustTick == null) return null;

        _lastRustResponsePtr = _rustTick(_lastRustResponsePtr);
        if (_lastRustResponsePtr == IntPtr.Zero) return null;

        return Marshal.PtrToStringAnsi(_lastRustResponsePtr);
    }
}
