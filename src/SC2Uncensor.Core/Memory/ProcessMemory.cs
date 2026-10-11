using System.Text;

namespace SC2Uncensor.Core.Memory;

public sealed class ProcessMemory : IDisposable
{
    public const string ProcessName = "SC2_x64.exe";
    private static readonly byte[] NopPair = [0x90, 0x90];

    public nint Handle { get; }
    public nint BaseAddress { get; }
    public uint Pid { get; }
    public string? ExeVersion { get; }
    public OffsetSet Offsets { get; }

    private ProcessMemory(nint handle, nint baseAddress, uint pid, string? ver, OffsetSet offsets)
    {
        Handle = handle;
        BaseAddress = baseAddress;
        Pid = pid;
        ExeVersion = ver;
        Offsets = offsets;
    }

    public static ProcessMemory? Attach(AppConfig cfg, IReadOnlyDictionary<string, OffsetSet> table, out string? error)
    {
        error = null;
        if (!TryFindPid(out var pid))
        {
            error = "norun";
            return null;
        }
        var handle = Native.OpenProcess(Native.ProcessAllNeeded, false, pid);
        if (handle == 0)
        {
            error = "norun";
            return null;
        }
        try
        {
            if (!TryModuleBase(handle, out var baseAddr))
            {
                error = "norun";
                Native.CloseHandle(handle);
                return null;
            }
            var path = ExeVersionReader.ProcessImagePath(handle);
            var ver = path is null ? null : ExeVersionReader.Read(path);
            var offsets = OffsetTable.Resolve(ver, table);
            if (offsets is null)
            {
                if (cfg.CheckVersion)
                {
                    error = "version:" + (ver ?? "unknown");
                    Native.CloseHandle(handle);
                    return null;
                }
                offsets = OffsetTable.Builtin;
            }
            return new ProcessMemory(handle, baseAddr, pid, ver, offsets);
        }
        catch
        {
            Native.CloseHandle(handle);
            throw;
        }
    }

    public nint Addr(long offset) => BaseAddress + (nint)offset;

    public byte[]? Read(long offset, int n)
    {
        var buf = new byte[n];
        return Native.ReadProcessMemory(Handle, Addr(offset), buf, n, out var got) && got == n ? buf : null;
    }

    public bool Write(long offset, byte[] data) =>
        Native.WriteProcessMemory(Handle, Addr(offset), data, data.Length, out var w) && w == data.Length;

    public GameState ReadState()
    {
        var tags = new List<string>();
        foreach (var site in Offsets.NopSites)
        {
            var cur = Read(site.Offset, 2);
            if (cur is null) tags.Add("??");
            else if (cur.AsSpan().SequenceEqual(NopPair)) tags.Add("nop");
            else if (cur.AsSpan().SequenceEqual(site.Original)) tags.Add("orig");
            else tags.Add("??");
        }
        var nop = tags.Distinct().Count() > 1 ? "mixed" : tags[0];
        var store = Read(Offsets.Store, 1);
        var r = Read(Offsets.R, 4);
        return new GameState(
            nop,
            store is null ? -1 : store[0],
            r is null ? -1 : BitConverter.ToInt32(r, 0));
    }

    public IReadOnlyList<(NopSite Site, byte[]? Current)> MismatchedNopSites()
    {
        var bad = new List<(NopSite, byte[]?)>();
        foreach (var site in Offsets.NopSites)
        {
            var cur = Read(site.Offset, 2);
            if (cur is null ||
                !(cur.AsSpan().SequenceEqual(NopPair) || cur.AsSpan().SequenceEqual(site.Original)))
                bad.Add((site, cur));
        }
        return bad;
    }

    public bool EnsureNop()
    {
        var st = ReadState();
        if (st.Nop == "nop") return true;
        var mismatch = MismatchedNopSites();
        if (mismatch.Count > 0 || st.Nop != "orig")
        {
            var detail = mismatch.Count == 0
                ? $"state={st.Nop}"
                : string.Join("; ", mismatch.Select(m =>
                    $"0x{m.Site.Offset:X}({m.Site.Name})={(m.Current is null ? "read-fail" : Convert.ToHexString(m.Current))}"));
            Log.Write($"[NOP] original bytes mismatch, refuse write: {detail}");
            return false;
        }
        foreach (var site in Offsets.NopSites)
            Write(site.Offset, NopPair);
        var ok = Offsets.NopSites.All(s =>
        {
            var cur = Read(s.Offset, 2);
            return cur is not null && cur.AsSpan().SequenceEqual(NopPair);
        });
        Log.Write($"[NOP] patch {(ok ? "OK" : "FAIL")}");
        return ok;
    }

    public bool ApplyRecipe()
    {
        if (!EnsureNop()) return false;
        Write(Offsets.Store, [0]);
        Write(Offsets.R, BitConverter.GetBytes(1));
        var r = Read(Offsets.R, 4);
        var ok = r is not null && r[0] == 1;
        Log.Write($"[recipe] NOP+store0+R=1 {(ok ? "OK" : "FAIL")}");
        return ok;
    }

    public bool RestoreRecipe()
    {
        var mismatch = MismatchedNopSites();
        if (mismatch.Count > 0)
        {
            var detail = string.Join("; ", mismatch.Select(m =>
                $"0x{m.Site.Offset:X}({m.Site.Name})={(m.Current is null ? "read-fail" : Convert.ToHexString(m.Current))}"));
            Log.Write($"[restore] current bytes neither NOP nor expected original, refuse: {detail}");
            return false;
        }
        foreach (var site in Offsets.NopSites)
            Write(site.Offset, site.Original);
        Write(Offsets.Store, [0]);
        Write(Offsets.R, BitConverter.GetBytes(0));
        var chk = Offsets.NopSites.All(s =>
        {
            var cur = Read(s.Offset, 2);
            return cur is not null && cur.AsSpan().SequenceEqual(s.Original);
        });
        var r = Read(Offsets.R, 4);
        var ok = chk && r is not null && r[0] == 0;
        Log.Write($"[restore] original NOP+store0+R=0 {(ok ? "OK" : "FAIL")}");
        return ok;
    }

    public bool WaitStoreChange(int before, int timeoutMs)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            var cur = Read(Offsets.Store, 1);
            if (cur is not null && cur[0] != before) return true;
            Thread.Sleep(15);
        }
        return false;
    }

    public void Dispose()
    {
        if (Handle != 0) Native.CloseHandle(Handle);
    }

    private static bool TryFindPid(out uint pid)
    {
        pid = 0;
        var snap = Native.CreateToolhelp32Snapshot(Native.Th32CsSnapProcess, 0);
        if (snap == 0 || snap == nint.Zero || snap == -1) return false;
        try
        {
            var pe = new Native.ProcessEntry32 { Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Native.ProcessEntry32>() };
            if (!Native.Process32FirstW(snap, ref pe)) return false;
            do
            {
                if (string.Equals(pe.ExeFile, ProcessName, StringComparison.OrdinalIgnoreCase))
                {
                    pid = pe.ProcessId;
                    return true;
                }
            } while (Native.Process32NextW(snap, ref pe));
            return false;
        }
        finally
        {
            Native.CloseHandle(snap);
        }
    }

    private static bool TryModuleBase(nint handle, out nint baseAddr)
    {
        baseAddr = 0;
        var needed = 0u;
        Native.EnumProcessModulesEx(handle, Array.Empty<nint>(), 0, out needed, Native.ListModulesAll);
        var count = Math.Max(needed / (uint)nint.Size, 1);
        var mods = new nint[count];
        if (!Native.EnumProcessModulesEx(handle, mods, (uint)(mods.Length * nint.Size), out needed, Native.ListModulesAll))
            return false;
        var n = needed / (uint)nint.Size;
        var sb = new StringBuilder(260);
        for (var i = 0; i < n; i++)
        {
            sb.Clear();
            Native.GetModuleBaseNameW(handle, mods[i], sb, 260);
            if (!string.Equals(sb.ToString(), ProcessName, StringComparison.OrdinalIgnoreCase)) continue;
            if (!Native.GetModuleInformation(handle, mods[i], out var info, (uint)System.Runtime.InteropServices.Marshal.SizeOf<Native.ModuleInfo>()))
                return false;
            baseAddr = info.BaseOfDll;
            return true;
        }
        return false;
    }
}
