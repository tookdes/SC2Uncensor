using SC2Uncensor.Core.Capture;
using SC2Uncensor.Core.Input;
using SC2Uncensor.Core.Memory;
using SC2Uncensor.Core.Navigation;
using SC2Uncensor.Core.Ocr;

namespace SC2Uncensor.Core;

public sealed class UncensorService : IDisposable
{
    public const string Version = "0.1.0";
    public AppConfig Config { get; }
    public IReadOnlyDictionary<string, OffsetSet> Offsets { get; }

    private readonly ScreenCapture _capture;
    private readonly WinOcrEngine _ocr;
    private readonly DialogNavigator _nav;
    private ProcessMemory? _mem;
    private string? _attachError;

    public UncensorService(AppConfig? cfg = null)
    {
        Config = cfg ?? AppConfig.Load();
        Offsets = OffsetTable.Load();
        _capture = new ScreenCapture(Config);
        _ocr = new WinOcrEngine();
        _nav = new DialogNavigator(Config, _capture, _ocr);
        try { Native.SetProcessDPIAware(); } catch { /* older OS */ }
    }

    public ProcessMemory? Memory => _mem;
    public string? AttachError => _attachError;

    public bool TryAttach()
    {
        _mem?.Dispose();
        _mem = ProcessMemory.Attach(Config, Offsets, out _attachError);
        if (_mem is null) return false;
        var disp = _mem.Offsets.Display;
        var note = _mem.ExeVersion == _mem.Offsets.ExeVersion && disp is not null && disp != _mem.ExeVersion
            ? $" (battle.net {disp})" : "";
        Log.Write($"[attach] {ProcessMemory.ProcessName} base=0x{_mem.BaseAddress:X} ver={_mem.ExeVersion}{note}");
        return true;
    }

    public string NoGameMessage()
    {
        if (_attachError is not null && _attachError.StartsWith("version:", StringComparison.Ordinal))
            return $"game version {_attachError[8..]} not in offsets.dat / builtin table";
        return "game not running";
    }

    public OpResult UncensorManual()
    {
        if (!EnsureMem()) return Fail($"[uncensor] {NoGameMessage()}");
        if (!_mem!.ApplyRecipe()) return Fail("[uncensor] memory write failed");
        Log.Write("[uncensor] memory done. Toggle Reduce Violence once in Options > Graphics.");
        Beep(true, "open");
        return OpResult.Ok;
    }

    public OpResult UncensorAuto()
    {
        if (!EnsureMem()) return Fail($"[auto] {NoGameMessage()}");
        Log.Write("[auto] 1/3 memory");
        if (!_mem!.ApplyRecipe()) return Fail("[auto] memory write failed");
        var hwnd = GameWindow.Find();
        if (hwnd == 0) return Fail("[auto] SC2 window not found");
        Log.Write("[auto] 2/3 navigate");
        var res = _nav.OpenDialog(hwnd);
        if (res is SkipByDesign)
            return Skip("[auto] in-match, skipped");
        if (res is not DialogHit hit)
            return Fail("[auto] did not reach graphics options");
        if (!GameWindow.Activate(hwnd))
            return Fail("[auto] cannot foreground game (use windowed / borderless)");
        var why = GameWindow.ClickGate(hwnd);
        if (why is not null) return Fail($"[auto] cannot click: {why}");
        Log.Write("[auto] 3/3 click on/off");
        var pos = _nav.RefineCheckbox(hit.Violence, hit.ClientW);
        if (pos is SkipByDesign) return Skip("[auto] checkbox greyed, skipped");
        if (pos is not ValueTuple<int, int> xy) return Fail("[auto] checkbox locate failed");
        if (!_nav.ClickCheckbox(_mem, xy)) return Fail("[auto] first click missed");
        if (!_nav.ClickCheckbox(_mem, xy)) return Fail("[auto] second click missed");
        DialogNavigator.CloseDialogAndMenu();
        Log.Write("[auto] done");
        Beep(true, "open");
        return OpResult.Ok;
    }

    public OpResult ClickToggle()
    {
        if (!EnsureMem()) return Fail($"[toggle] {NoGameMessage()}");
        if (_mem!.ReadState().Nop != "nop" && !_mem.EnsureNop())
            return Fail("[toggle] NOP failed");
        var hwnd = GameWindow.Find();
        if (hwnd == 0) return Fail("[toggle] SC2 window not found");
        var res = _nav.OpenDialog(hwnd);
        if (res is SkipByDesign) return Skip("[toggle] in-match, skipped");
        if (res is not DialogHit hit) return Fail("[toggle] did not reach options");
        if (!GameWindow.Activate(hwnd)) return Fail("[toggle] cannot foreground game");
        var why = GameWindow.ClickGate(hwnd);
        if (why is not null) return Fail($"[toggle] cannot click: {why}");
        var pos = _nav.RefineCheckbox(hit.Violence, hit.ClientW);
        if (pos is SkipByDesign) return Skip("[toggle] checkbox greyed");
        if (pos is not ValueTuple<int, int> xy) return Fail("[toggle] locate failed");
        if (!_nav.ClickCheckbox(_mem, xy)) return Fail("[toggle] click missed");
        if (hit.Accept is not null)
            GameWindow.Click(hit.Accept.Center.X, hit.Accept.Center.Y);
        else
            GameWindow.Click(GameWindow.Frac(GameWindow.ClientRect(hwnd) ?? GameWindow.Primary(), 0.4336, 0.9278).X,
                             GameWindow.Frac(GameWindow.ClientRect(hwnd) ?? GameWindow.Primary(), 0.4336, 0.9278).Y);
        Thread.Sleep(150);
        GameWindow.SendEsc();
        var st = _mem.ReadState();
        Beep(true, st.Store == 0 ? "open" : "close");
        Log.Write($"[toggle] store={st.Store}");
        return OpResult.Ok;
    }

    public OpResult RestoreManual()
    {
        if (!EnsureMem()) return Fail($"[restore] {NoGameMessage()}");
        if (!_mem!.RestoreRecipe()) return Fail("[restore] memory restore failed");
        Log.Write("[restore] memory restored. Click Reduce Violence once in Options > Graphics.");
        Beep(true, "close");
        return OpResult.Ok;
    }

    public OpResult RestoreAuto()
    {
        if (!EnsureMem()) return Fail($"[restore-auto] {NoGameMessage()}");
        if (!_mem!.RestoreRecipe()) return Fail("[restore-auto] memory restore failed");
        var hwnd = GameWindow.Find();
        if (hwnd == 0) return Fail("[restore-auto] SC2 window not found");
        var res = _nav.OpenDialog(hwnd);
        if (res is SkipByDesign) return Skip("[restore-auto] in-match, skipped");
        if (res is not DialogHit hit) return Fail("[restore-auto] did not reach options");
        if (!GameWindow.Activate(hwnd)) return Fail("[restore-auto] cannot foreground game");
        var pos = _nav.RefineCheckbox(hit.Violence, hit.ClientW);
        if (pos is SkipByDesign) return Skip("[restore-auto] checkbox greyed");
        if (pos is not ValueTuple<int, int> xy) return Fail("[restore-auto] locate failed");
        if (!_nav.ClickCheckbox(_mem, xy)) return Fail("[restore-auto] click missed");
        if (hit.Accept is not null)
            GameWindow.Click(hit.Accept.Center.X, hit.Accept.Center.Y);
        Thread.Sleep(150);
        GameWindow.SendEsc();
        Log.Write("[restore-auto] done");
        Beep(true, "close");
        return OpResult.Ok;
    }

    public OpResult PrintStatus()
    {
        if (!EnsureMem())
        {
            Log.Write($"[status] {NoGameMessage()}");
            return OpResult.Fail;
        }
        var st = _mem!.ReadState();
        Log.Write($"[status] NOP={st.Nop} store={st.Store} R={st.R}");
        return OpResult.Ok;
    }

    public GameState? PeekState()
    {
        if (_mem is null && !TryAttach()) return null;
        return _mem?.ReadState();
    }

    private bool EnsureMem()
    {
        if (_mem is not null)
        {
            var probe = _mem.Read(0, 1);
            if (probe is not null) return true;
            _mem.Dispose();
            _mem = null;
        }
        return TryAttach();
    }

    private static OpResult Fail(string msg)
    {
        Log.Write("[error] " + msg);
        Beep(false, "fail");
        return OpResult.Fail;
    }

    private static OpResult Skip(string msg)
    {
        Log.Write("[skip] " + msg);
        Beep(true, "skip");
        return OpResult.Skipped;
    }

    public static void Beep(bool ok, string kind)
    {
        try
        {
            switch (kind)
            {
                case "open": Native.Beep(1047, 80); Native.Beep(1568, 120); break;
                case "close": Native.Beep(1568, 80); Native.Beep(1047, 120); break;
                case "on": Native.Beep(1047, 90); break;
                case "off": Native.Beep(784, 90); break;
                case "fail": Native.Beep(400, 320); break;
                case "skip": Native.Beep(660, 90); break;
            }
        }
        catch { /* no console beep device */ }
        _ = ok;
    }

    public static bool TakeSingleInstance()
    {
        Native.CreateMutexW(0, false, "SC2Uncensor_Net_SingleInstance_Mutex");
        return System.Runtime.InteropServices.Marshal.GetLastWin32Error() != Native.ErrorAlreadyExists;
    }

    public void Dispose()
    {
        _mem?.Dispose();
        _ocr.Dispose();
    }
}
