namespace SC2Uncensor.Core.Input;

public sealed class HotkeyLoop : IDisposable
{
    public static readonly string[] Names =
        ["master", "uncensor", "uncensorAuto", "clickToggle", "status", "restore", "restoreAuto"];

    private readonly AppConfig _cfg;
    private readonly UncensorService _svc;
    private readonly Dictionary<int, string> _byId = [];
    private readonly List<(int Id, uint Mods, uint Vk, string Name, string Key)> _reg = [];
    private Thread? _thread;
    private uint _tid;
    private bool _othersEnabled = true;
    private readonly CancellationTokenSource _cts = new();

    public event Action<string, OpResult>? ActionDone;
    public bool OthersEnabled => _othersEnabled;

    public HotkeyLoop(AppConfig cfg, UncensorService svc)
    {
        _cfg = cfg;
        _svc = svc;
    }

    public void Start()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "hotkeys" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public void Stop()
    {
        _cts.Cancel();
        if (_tid != 0) Native.PostThreadMessageW(_tid, Native.WmQuit, 0, 0);
        _thread?.Join(1000);
    }

    public void Request(string name) => Dispatch(name);

    private void Run()
    {
        _tid = Native.GetCurrentThreadId();
        Native.PeekMessageW(out _, 0, 0, 0, 0);
        RegisterAll();
        Log.Write("ready");
        while (!_cts.IsCancellationRequested && Native.GetMessageW(out var msg, 0, 0, 0) > 0)
        {
            if (msg.Message == Native.WmHotkey)
            {
                var id = (int)msg.WParam;
                if (_byId.TryGetValue(id, out var name))
                    Dispatch(name);
            }
        }
        foreach (var r in _reg)
            Native.UnregisterHotKey(0, r.Id);
    }

    private void RegisterAll()
    {
        _reg.Clear();
        _byId.Clear();
        var id = 1;
        foreach (var name in Names)
        {
            if (!_othersEnabled && name != "master") { id++; continue; }
            if (!_cfg.Hotkeys.TryGetValue(name, out var spec)) continue;
            var parsed = KeyParser.Parse(spec);
            if (parsed is null) continue;
            var (mods, vk) = parsed.Value;
            if (Native.RegisterHotKey(0, id, mods, vk))
            {
                _reg.Add((id, mods, vk, name, spec));
                _byId[id] = name;
            }
            else
            {
                var alt = Native.ModControl | Native.ModAlt | Native.ModNorepeat;
                if (Native.RegisterHotKey(0, id, alt, vk))
                {
                    var shown = "Ctrl+Alt+" + spec.Replace("Ctrl+", "").Replace("Alt+", "").Replace("Shift+", "");
                    _reg.Add((id, alt, vk, name, shown));
                    _byId[id] = name;
                    Log.Write($"[hotkey] {spec} busy, {name} -> {shown}");
                }
                else
                    Log.Write($"[hotkey] failed {name}={spec}");
            }
            id++;
        }
        Log.Write("hotkeys: " + string.Join(", ", _reg.Select(r => $"{r.Key}={r.Name}")));
    }

    private void Dispatch(string name)
    {
        try
        {
            Log.Write($"[key] {name}");
            var result = name switch
            {
                "master" => ToggleMaster(),
                "uncensor" => _svc.UncensorManual(),
                "uncensorAuto" => _svc.UncensorAuto(),
                "clickToggle" => _svc.ClickToggle(),
                "status" => _svc.PrintStatus(),
                "restore" => _svc.RestoreManual(),
                "restoreAuto" => _svc.RestoreAuto(),
                _ => OpResult.Fail,
            };
            ActionDone?.Invoke(name, result);
        }
        catch (Exception ex)
        {
            Log.Write($"[error] {name}: {ex.Message}");
            ActionDone?.Invoke(name, OpResult.Fail);
        }
    }

    private OpResult ToggleMaster()
    {
        _othersEnabled = !_othersEnabled;
        foreach (var r in _reg.ToList())
        {
            if (r.Name == "master") continue;
            Native.UnregisterHotKey(0, r.Id);
        }
        _reg.RemoveAll(r => r.Name != "master");
        foreach (var k in _byId.Keys.Where(k => _byId[k] != "master").ToList())
            _byId.Remove(k);
        if (_othersEnabled) RegisterAll();
        Log.Write(_othersEnabled ? "[master] other hotkeys on" : "[master] other hotkeys off");
        UncensorService.Beep(true, _othersEnabled ? "on" : "off");
        return OpResult.Ok;
    }

    public void Dispose() => Stop();
}
