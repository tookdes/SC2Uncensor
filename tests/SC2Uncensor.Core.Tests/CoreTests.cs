using SC2Uncensor.Core;
using SC2Uncensor.Core.Capture;
using SC2Uncensor.Core.Input;
using SC2Uncensor.Core.Memory;
using SC2Uncensor.Core.Ocr;

namespace SC2Uncensor.Core.Tests;

public class OffsetTableTests
{
    [Fact]
    public void Loads_98370_and_32043()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "offsets.dat");
        Assert.True(File.Exists(path), path);
        var table = OffsetTable.Load(path);
        Assert.True(table.ContainsKey("5.0.15.32043"));
        Assert.True(table.ContainsKey("5.0.15.98370"));
        var v = table["5.0.15.98370"];
        Assert.Equal(0x11DA00, v.NopSites[0].Offset);
        Assert.Equal(0x11DA09, v.NopSites[1].Offset);
        Assert.Equal(0x11DA12, v.NopSites[2].Offset);
        Assert.Equal([0x74, 0x2A], v.NopSites[0].Original);
        Assert.Equal(0x3E2E381, v.Store);
        Assert.Equal("5.0.17.98370", v.Display);
    }

    [Fact]
    public void Resolve_unknown_is_null_when_not_builtin()
    {
        var table = OffsetTable.Load(Path.Combine(AppContext.BaseDirectory, "offsets.dat"));
        Assert.Null(OffsetTable.Resolve("5.0.15.32834", table));
        Assert.NotNull(OffsetTable.Resolve("5.0.15.98370", table));
        Assert.NotNull(OffsetTable.Resolve("5.0.15.32043", table));
    }
}

public class KeyParserTests
{
    [Fact]
    public void Parses_combos()
    {
        var f9 = KeyParser.Parse("F9");
        Assert.NotNull(f9);
        Assert.Equal(0x78u, f9.Value.Vk);
        var ca1 = KeyParser.Parse("Ctrl+Alt+1");
        Assert.NotNull(ca1);
        Assert.Equal((uint)'1', ca1.Value.Vk);
        Assert.True((ca1.Value.Mods & Native.ModControl) != 0);
        Assert.True((ca1.Value.Mods & Native.ModAlt) != 0);
    }
}

public class BlueClusterTests
{
    [Fact]
    public void Finds_one_blue_box()
    {
        var img = new CapturedImage { Width = 80, Height = 60, Bgra = new byte[80 * 60 * 4] };
        void Put(int x, int y, byte r, byte g, byte b)
        {
            var o = (y * 80 + x) * 4;
            img.Bgra[o] = b; img.Bgra[o + 1] = g; img.Bgra[o + 2] = r; img.Bgra[o + 3] = 255;
        }
        for (var x = 10; x <= 28; x++)
        {
            Put(x, 12, 80, 170, 255);
            Put(x, 30, 80, 170, 255);
        }
        for (var y = 12; y <= 30; y++)
        {
            Put(10, y, 80, 170, 255);
            Put(28, y, 80, 170, 255);
        }
        var got = ScreenCapture.BlueClusters(img);
        Assert.Single(got);
    }
}

public class OcrMatchTests
{
    [Fact]
    public void Matches_fuzzy_key()
    {
        var key = "\u51cf\u5c11\u66b4\u529b\u8868\u73b0";
        var shortk = "\u66b4\u529b";
        var lines = new[] { new OcrLine(key + "\u00b1", 10, 20, 100, 16) };
        var hits = OcrMatch.Match(lines, key, shortk);
        Assert.True(hits.ContainsKey(shortk) || hits.ContainsKey(key));
    }
}
