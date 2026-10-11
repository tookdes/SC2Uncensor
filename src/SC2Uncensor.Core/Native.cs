using System.Runtime.InteropServices;
using System.Text;

namespace SC2Uncensor.Core;

public static class Native
{
    public const uint ProcessQueryInformation = 0x0400;
    public const uint ProcessVmRead = 0x0010;
    public const uint ProcessVmWrite = 0x0020;
    public const uint ProcessVmOperation = 0x0008;
    public const uint ProcessAllNeeded = ProcessQueryInformation | ProcessVmRead | ProcessVmWrite | ProcessVmOperation;

    public const uint Th32CsSnapProcess = 0x00000002;
    public const uint ListModulesAll = 0x03;

    public const uint KeyeventfKeyup = 0x0002;
    public const uint KeyeventfExtendedkey = 0x0001;
    public const uint KeyeventfScancode = 0x0008;
    public const uint MouseeventfLeftdown = 0x0002;
    public const uint MouseeventfLeftup = 0x0004;

    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint ModWin = 0x0008;
    public const uint ModNorepeat = 0x4000;

    public const uint WmHotkey = 0x0312;
    public const uint WmQuit = 0x0012;
    public const uint WmAppForward = 0x8001;
    public const uint WmAppUiCall = 0x8002;

    public const int SwRestore = 9;
    public const int MonitorDefaultToNearest = 2;
    public const uint PwClientOnly = 0x1;
    public const uint PwRenderFullContent = 0x2;
    public const int BiRgb = 0;
    public const int DibRgbColors = 0;
    public const int ErrorAlreadyExists = 183;
    public const int SmCxScreen = 0;
    public const int SmCyScreen = 1;
    public const uint VkMenu = 0x12;
    public const uint VkEscape = 0x1B;
    public const uint VkF10 = 0x79;

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left, Top, Right, Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public int X, Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct ProcessEntry32
    {
        public uint Size;
        public uint CntUsage;
        public uint ProcessId;
        public nuint DefaultHeapId;
        public uint ModuleId;
        public uint CntThreads;
        public uint ParentProcessId;
        public int PriClassBase;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ExeFile;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ModuleInfo
    {
        public nint BaseOfDll;
        public uint SizeOfImage;
        public nint EntryPoint;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ClrUsed;
        public uint ClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Msg
    {
        public nint Hwnd;
        public uint Message;
        public nuint WParam;
        public nint LParam;
        public uint Time;
        public Point Pt;
        public uint LPrivate;
    }

    public delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern nint OpenProcess(uint access, bool inherit, uint pid);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(nint handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ReadProcessMemory(nint process, nint address, byte[] buffer, nint size, out nint read);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool WriteProcessMemory(nint process, nint address, byte[] buffer, nint size, out nint written);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern nint CreateToolhelp32Snapshot(uint flags, uint pid);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool Process32FirstW(nint snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool Process32NextW(nint snapshot, ref ProcessEntry32 entry);

    [DllImport("psapi.dll", SetLastError = true)]
    public static extern bool EnumProcessModulesEx(nint process, [Out] nint[] modules, uint cb, out uint needed, uint filter);

    [DllImport("psapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern uint GetModuleBaseNameW(nint process, nint module, StringBuilder name, uint size);

    [DllImport("psapi.dll", SetLastError = true)]
    public static extern bool GetModuleInformation(nint process, nint module, out ModuleInfo info, uint size);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern nint CreateMutexW(nint attr, bool initialOwner, string name);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll")]
    public static extern int GetWindowTextLengthW(nint hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowTextW(nint hWnd, StringBuilder text, int max);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(nint hWnd, out uint pid);

    [DllImport("user32.dll")]
    public static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(nint hWnd, out Rect rect);

    [DllImport("user32.dll")]
    public static extern bool GetClientRect(nint hWnd, out Rect rect);

    [DllImport("user32.dll")]
    public static extern bool ClientToScreen(nint hWnd, ref Point point);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(nint hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(nint hWnd, int cmd);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    public static extern bool AttachThreadInput(uint attach, uint attachTo, bool attachFlag);

    [DllImport("user32.dll")]
    public static extern nint MonitorFromWindow(nint hWnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool GetMonitorInfoW(nint monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    public static extern void keybd_event(byte vk, byte scan, uint flags, nuint extra);

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, nuint extra);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(nint hWnd, int id);

    [DllImport("user32.dll")]
    public static extern int GetMessageW(out Msg msg, nint hWnd, uint min, uint max);

    [DllImport("user32.dll")]
    public static extern bool PeekMessageW(out Msg msg, nint hWnd, uint min, uint max, uint remove);

    [DllImport("user32.dll")]
    public static extern bool PostThreadMessageW(uint threadId, uint msg, nuint wParam, nint lParam);

    [DllImport("user32.dll")]
    public static extern nint GetDC(nint hWnd);

    [DllImport("user32.dll")]
    public static extern int ReleaseDC(nint hWnd, nint dc);

    [DllImport("user32.dll")]
    public static extern bool PrintWindow(nint hWnd, nint hdcBlt, uint flags);

    [DllImport("user32.dll")]
    public static extern bool SetProcessDPIAware();

    [DllImport("gdi32.dll")]
    public static extern nint CreateCompatibleDC(nint hdc);

    [DllImport("gdi32.dll")]
    public static extern nint CreateCompatibleBitmap(nint hdc, int width, int height);

    [DllImport("gdi32.dll")]
    public static extern nint SelectObject(nint hdc, nint obj);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteDC(nint hdc);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteObject(nint obj);

    [DllImport("gdi32.dll")]
    public static extern int GetDIBits(nint hdc, nint bitmap, uint start, uint lines, byte[] bits, ref BitmapInfoHeader info, uint usage);

    [DllImport("gdi32.dll")]
    public static extern nint CreateDC(string driver, string? device, string? output, nint init);

    [DllImport("gdi32.dll")]
    public static extern bool BitBlt(nint dest, int x, int y, int w, int h, nint src, int sx, int sy, uint rop);

    public const uint Srccopy = 0x00CC0020;

    [DllImport("version.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int GetFileVersionInfoSizeW(string filename, out uint handle);

    [DllImport("version.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool GetFileVersionInfoW(string filename, uint handle, int len, byte[] data);

    [DllImport("version.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool VerQueryValueW(byte[] block, string subBlock, out nint buffer, out uint len);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern uint QueryFullProcessImageNameW(nint process, uint flags, StringBuilder name, ref uint size);

    [DllImport("kernel32.dll")]
    public static extern void Beep(uint freq, uint duration);
}
