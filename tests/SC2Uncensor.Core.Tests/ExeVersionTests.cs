using SC2Uncensor.Core.Memory;

namespace SC2Uncensor.Core.Tests;

public class ExeVersionTests
{
    [Fact]
    public void Reads_four_part_string_not_truncated_packed()
    {
        var path = Environment.GetEnvironmentVariable("SC2_EXE_PATH");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

        var ver = ExeVersionReader.Read(path);
        Assert.NotNull(ver);
        Assert.Matches(@"^\d+\.\d+\.\d+\.\d+$", ver);

        var table = OffsetTable.Load(Path.Combine(AppContext.BaseDirectory, "offsets.dat"));
        var resolved = OffsetTable.Resolve(ver, table);
        Assert.NotNull(resolved);

        var filename = Path.GetFileName(path);
        if (filename.Equals("SC2_x64.exe", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Equal("5.0.15.98370", ver);
            Assert.Equal(0x11DA00, resolved!.NopSites[0].Offset);
            Assert.Equal(0x11DA12, resolved.NopSites[2].Offset);
            Assert.Equal(0x3E2E381, resolved.Store);
            Assert.Equal(0x3E2B5A8, resolved.R);
        }
    }
}
