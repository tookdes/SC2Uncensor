using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace SC2Uncensor.Core.Memory;

public static class ExeVersionReader
{
    private static readonly Regex Quad = new(@"^\d+\.\d+\.\d+\.\d+$", RegexOptions.Compiled);

    public static string? Read(string exePath)
    {
        var size = Native.GetFileVersionInfoSizeW(exePath, out _);
        if (size <= 0) return null;
        var data = new byte[size];
        if (!Native.GetFileVersionInfoW(exePath, 0, size, data)) return null;
        return ReadString(data) ?? ReadPacked(data);
    }

    private static string? ReadString(byte[] data)
    {
        var langs = new List<(ushort Lang, ushort Cp)>();
        if (Native.VerQueryValueW(data, @"\VarFileInfo\Translation", out var p, out var len) && len >= 4 && p != 0)
        {
            var n = (int)(len / 4);
            for (var i = 0; i < n; i++)
            {
                var lang = (ushort)Marshal.ReadInt16(p, i * 4);
                var cp = (ushort)Marshal.ReadInt16(p, i * 4 + 2);
                langs.Add((lang, cp));
            }
        }
        foreach (var extra in new (ushort, ushort)[] { (0, 0x04B0), (0x0804, 0x04B0), (0x0409, 0x04B0), (0x0409, 0x04E4) })
        {
            if (!langs.Contains(extra)) langs.Add(extra);
        }
        foreach (var (lang, cp) in langs)
        {
            var sub = $@"\StringFileInfo\{lang:X4}{cp:X4}\FileVersion";
            if (!Native.VerQueryValueW(data, sub, out var sp, out _) || sp == 0) continue;
            var s = Marshal.PtrToStringUni(sp)?.Trim();
            if (string.IsNullOrEmpty(s)) continue;
            foreach (var tok in s.Replace(',', '.').Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (Quad.IsMatch(tok)) return tok;
            }
        }
        return null;
    }

    private static string? ReadPacked(byte[] data)
    {
        if (!Native.VerQueryValueW(data, @"\", out var p, out var len) || len < 52 || p == 0)
            return null;
        var ms = (uint)Marshal.ReadInt32(p, 8);
        var ls = (uint)Marshal.ReadInt32(p, 12);
        return $"{ms >> 16}.{ms & 0xFFFF}.{ls >> 16}.{ls & 0xFFFF}";
    }

    public static string? ProcessImagePath(nint process)
    {
        var sb = new StringBuilder(32768);
        var size = (uint)sb.Capacity;
        return Native.QueryFullProcessImageNameW(process, 0, sb, ref size) != 0 ? sb.ToString() : null;
    }
}
