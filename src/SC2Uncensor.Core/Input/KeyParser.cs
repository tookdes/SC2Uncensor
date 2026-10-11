namespace SC2Uncensor.Core.Input;

public static class KeyParser
{
    private static readonly Dictionary<string, uint> Vk = new(StringComparer.OrdinalIgnoreCase)
    {
        ["F1"] = 0x70, ["F2"] = 0x71, ["F3"] = 0x72, ["F4"] = 0x73,
        ["F5"] = 0x74, ["F6"] = 0x75, ["F7"] = 0x76, ["F8"] = 0x77,
        ["F9"] = 0x78, ["F10"] = 0x79, ["F11"] = 0x7A, ["F12"] = 0x7B,
        ["TAB"] = 0x09, ["SPACE"] = 0x20, ["ENTER"] = 0x0D, ["ESC"] = 0x1B,
        ["INSERT"] = 0x2D, ["DELETE"] = 0x2E, ["HOME"] = 0x24, ["END"] = 0x23,
        ["PAGEUP"] = 0x21, ["PAGEDOWN"] = 0x22,
        ["UP"] = 0x26, ["DOWN"] = 0x28, ["LEFT"] = 0x25, ["RIGHT"] = 0x27,
    };

    public static (uint Mods, uint Vk)? Parse(string spec)
    {
        uint mods = 0;
        uint? vk = null;
        foreach (var raw in spec.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var p = raw;
            if (p.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || p.Equals("Control", StringComparison.OrdinalIgnoreCase))
                mods |= Native.ModControl;
            else if (p.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                mods |= Native.ModAlt;
            else if (p.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                mods |= Native.ModShift;
            else if (p.Equals("Win", StringComparison.OrdinalIgnoreCase) || p.Equals("Windows", StringComparison.OrdinalIgnoreCase))
                mods |= Native.ModWin;
            else if (Vk.TryGetValue(p, out var mapped))
                vk = mapped;
            else if (p.Length == 1 && char.IsLetterOrDigit(p[0]))
                vk = char.ToUpperInvariant(p[0]);
        }
        return vk is null ? null : (mods | Native.ModNorepeat, vk.Value);
    }
}
