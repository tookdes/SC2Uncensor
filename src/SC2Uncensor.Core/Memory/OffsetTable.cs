using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SC2Uncensor.Core.Memory;

public static class OffsetTable
{
    public const string BuiltinExeVersion = "5.0.15.32043";

    public static OffsetSet Builtin { get; } = new(
        BuiltinExeVersion,
        "5.0.16.97579",
        [
            new NopSite(0x11D9F0, [0x74, 0x2A], "CHN1"),
            new NopSite(0x11D9F9, [0x74, 0x21], "CHN2"),
            new NopSite(0x11DA02, [0x75, 0x18], "CHN3"),
        ],
        0x3E2E381,
        0x3E2B5A8);

    public static IReadOnlyDictionary<string, OffsetSet> Load(string? path = null)
    {
        path ??= AppPaths.Offsets;
        var map = new Dictionary<string, OffsetSet>(StringComparer.Ordinal);
        if (!File.Exists(path)) return map;
        var root = JsonNode.Parse(File.ReadAllText(path));
        var versions = root?["versions"]?.AsObject();
        if (versions is null) return map;
        foreach (var (ver, node) in versions)
        {
            if (node is not JsonObject obj) continue;
            try
            {
                map[ver] = Parse(ver, obj);
            }
            catch (Exception ex)
            {
                Log.Write($"[offsets] skip {ver}: {ex.Message}");
            }
        }
        return map;
    }

    public static OffsetSet? Resolve(string? exeVersion, IReadOnlyDictionary<string, OffsetSet> table)
    {
        if (!string.IsNullOrEmpty(exeVersion) && table.TryGetValue(exeVersion, out var hit))
            return hit;
        if (exeVersion == BuiltinExeVersion) return Builtin;
        return null;
    }

    private static OffsetSet Parse(string ver, JsonObject obj)
    {
        var sites = new List<NopSite>();
        var arr = obj["nop_sites"]?.AsArray() ?? throw new FormatException("nop_sites");
        var i = 0;
        foreach (var item in arr)
        {
            var pair = item!.AsArray();
            var off = ParseHex(pair[0]!.GetValue<string>());
            var bytes = Convert.FromHexString(pair[1]!.GetValue<string>());
            sites.Add(new NopSite(off, bytes, $"CHN{++i}"));
        }
        return new OffsetSet(
            ver,
            obj["display"]?.GetValue<string>(),
            sites,
            ParseHex(obj["store"]!.GetValue<string>()),
            ParseHex(obj["r"]!.GetValue<string>()));
    }

    private static long ParseHex(string s) =>
        long.Parse(s.Replace("0x", "", StringComparison.OrdinalIgnoreCase), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}
