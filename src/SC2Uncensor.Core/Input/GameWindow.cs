using System.Text;

namespace SC2Uncensor.Core.Input;

public static class GameWindow
{
    public static nint Find()
    {
        var cands = new List<(nint Hwnd, uint Pid)>();
        Native.EnumWindows((hwnd, _) =>
        {
            if (Native.IsWindowVisible(hwnd) && Native.GetWindowTextLengthW(hwnd) > 0)
            {
                Native.GetWindowThreadProcessId(hwnd, out var pid);
                cands.Add((hwnd, pid));
            }
            return true;
        }, 0);

        var ours = new List<nint>();
        foreach (var (hwnd, pid) in cands)
        {
            var h = Native.OpenProcess(Native.ProcessQueryInformation, false, pid);
            if (h == 0) continue;
            try
            {
                var path = Memory.ExeVersionReader.ProcessImagePath(h);
                if (path is not null &&
                    string.Equals(Path.GetFileName(path), Memory.ProcessMemory.ProcessName, StringComparison.OrdinalIgnoreCase))
                    ours.Add(hwnd);
            }
            finally { Native.CloseHandle(h); }
        }
        if (ours.Count == 0) return 0;
        var fg = Native.GetForegroundWindow();
        if (ours.Contains(fg)) return fg;
        return ours.MaxBy(Area);
    }

    public static ScreenRect? ClientRect(nint hwnd)
    {
        if (hwnd == 0) return null;
        if (!Native.GetClientRect(hwnd, out var cr)) return null;
        var pt = new Native.Point();
        if (!Native.ClientToScreen(hwnd, ref pt)) return null;
        if (cr.Right <= 100 || cr.Bottom <= 100) return null;
        return new ScreenRect(pt.X, pt.Y, pt.X + cr.Right, pt.Y + cr.Bottom);
    }

    public static ScreenRect Primary() =>
        new(0, 0, Native.GetSystemMetrics(Native.SmCxScreen), Native.GetSystemMetrics(Native.SmCyScreen));

    public static ScreenRect CropCenter(ScreenRect r) =>
        new(r.Left + (int)(r.Width * 0.30), r.Top + (int)(r.Height * 0.20),
            r.Left + (int)(r.Width * 0.70), r.Top + (int)(r.Height * 0.96));

    public static (int X, int Y) Frac(ScreenRect r, double fx, double fy) =>
        (r.Left + (int)(r.Width * fx), r.Top + (int)(r.Height * fy));

    public static string Title(nint hwnd)
    {
        var sb = new StringBuilder(128);
        Native.GetWindowTextW(hwnd, sb, 128);
        return sb.ToString();
    }

    public static bool IsFullscreen(nint hwnd)
    {
        if (!Native.GetWindowRect(hwnd, out var r)) return false;
        var mon = Native.MonitorFromWindow(hwnd, Native.MonitorDefaultToNearest);
        if (mon == 0) return false;
        var mi = new Native.MonitorInfo { Size = System.Runtime.InteropServices.Marshal.SizeOf<Native.MonitorInfo>() };
        if (!Native.GetMonitorInfoW(mon, ref mi)) return false;
        var m = mi.Monitor;
        return r.Left <= m.Left && r.Top <= m.Top && r.Right >= m.Right && r.Bottom >= m.Bottom;
    }

    public static bool IsExclusiveFullscreen(nint hwnd)
    {
        if (!IsFullscreen(hwnd)) return false;
        var mon = Native.MonitorFromWindow(hwnd, Native.MonitorDefaultToNearest);
        var mi = new Native.MonitorInfo { Size = System.Runtime.InteropServices.Marshal.SizeOf<Native.MonitorInfo>() };
        if (mon == 0 || !Native.GetMonitorInfoW(mon, ref mi)) return true;
        var m = mi.Monitor;
        var sw = Native.GetSystemMetrics(Native.SmCxScreen);
        var sh = Native.GetSystemMetrics(Native.SmCyScreen);
        return (sw, sh) != (m.Width, m.Height);
    }

    public static string? ClickGate(nint hwnd)
    {
        if (hwnd == 0) return "no-window";
        if (Native.IsIconic(hwnd)) return "minimized";
        if (Native.GetForegroundWindow() != hwnd) return "not-foreground";
        return null;
    }

    public static bool Activate(nint hwnd)
    {
        if (Native.GetForegroundWindow() == hwnd) return true;
        var iconic = Native.IsIconic(hwnd);
        var full = IsFullscreen(hwnd);
        var exclusive = full && IsExclusiveFullscreen(hwnd);
        Log.Write($"[activate] '{Title(hwnd)}' min={iconic} full={full} exclusive={exclusive}");
        if (iconic)
        {
            Native.ShowWindow(hwnd, Native.SwRestore);
            Thread.Sleep(150);
        }
        if (exclusive)
        {
            Native.SetForegroundWindow(hwnd);
            Thread.Sleep(200);
        }
        else
        {
            try
            {
                Native.keybd_event((byte)Native.VkMenu, 0, 0, 0);
                Native.SetForegroundWindow(hwnd);
            }
            finally
            {
                Native.keybd_event((byte)Native.VkMenu, 0, Native.KeyeventfKeyup, 0);
            }
            Thread.Sleep(150);
            if (Native.GetForegroundWindow() != hwnd)
            {
                var fg = Native.GetForegroundWindow();
                var ft = Native.GetWindowThreadProcessId(fg, out _);
                var ct = Native.GetWindowThreadProcessId(hwnd, out _);
                Native.AttachThreadInput(ct, ft, true);
                try { Native.SetForegroundWindow(hwnd); }
                finally { Native.AttachThreadInput(ct, ft, false); }
            }
            Thread.Sleep(100);
        }
        return Native.GetForegroundWindow() == hwnd;
    }

    public static void SendVk(uint vk)
    {
        Native.keybd_event((byte)vk, 0, 0, 0);
        Thread.Sleep(30);
        Native.keybd_event((byte)vk, 0, Native.KeyeventfKeyup, 0);
    }

    public static void SendEsc() => SendVk(Native.VkEscape);
    public static void SendF10() => SendVk(Native.VkF10);

    public static void Click(int x, int y)
    {
        Native.SetCursorPos(x, y);
        Thread.Sleep(20);
        Native.mouse_event(Native.MouseeventfLeftdown, 0, 0, 0, 0);
        Thread.Sleep(30);
        Native.mouse_event(Native.MouseeventfLeftup, 0, 0, 0, 0);
    }

    private static int Area(nint hwnd) =>
        Native.GetWindowRect(hwnd, out var r) ? r.Width * r.Height : 0;
}
