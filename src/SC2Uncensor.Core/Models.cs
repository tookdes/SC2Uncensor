namespace SC2Uncensor.Core;

public enum OpResult
{
    Ok = 0,
    Fail = 2,
    Skipped = 3,
}

public sealed record NopSite(long Offset, byte[] Original, string Name);

public sealed record OffsetSet(
    string ExeVersion,
    string? Display,
    IReadOnlyList<NopSite> NopSites,
    long Store,
    long R);

public sealed record GameState(string Nop, int Store, int R);

public sealed record OcrLine(string Text, int X, int Y, int W, int H)
{
    public (int X, int Y) Center => (X + W / 2, Y + H / 2);
}

public sealed record ScreenRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

public sealed class SkipByDesign
{
    public static readonly SkipByDesign Instance = new();
    public override string ToString() => "<skipped>";
}

public sealed class CapturedImage
{
    public required byte[] Bgra { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public int Stride => Width * 4;

    public CapturedImage Crop(int x, int y, int w, int h)
    {
        x = Math.Clamp(x, 0, Width);
        y = Math.Clamp(y, 0, Height);
        w = Math.Clamp(w, 0, Width - x);
        h = Math.Clamp(h, 0, Height - y);
        var dst = new byte[w * h * 4];
        for (var row = 0; row < h; row++)
            Buffer.BlockCopy(Bgra, ((y + row) * Width + x) * 4, dst, row * w * 4, w * 4);
        return new CapturedImage { Bgra = dst, Width = w, Height = h };
    }

    public CapturedImage ScaleNearest(int scale)
    {
        if (scale <= 1) return this;
        var nw = Width * scale;
        var nh = Height * scale;
        var dst = new byte[nw * nh * 4];
        for (var y = 0; y < nh; y++)
        {
            var sy = y / scale;
            for (var x = 0; x < nw; x++)
            {
                var sx = x / scale;
                var si = (sy * Width + sx) * 4;
                var di = (y * nw + x) * 4;
                dst[di] = Bgra[si];
                dst[di + 1] = Bgra[si + 1];
                dst[di + 2] = Bgra[si + 2];
                dst[di + 3] = Bgra[si + 3];
            }
        }
        return new CapturedImage { Bgra = dst, Width = nw, Height = nh };
    }

    public byte[] ToRgba()
    {
        var rgba = new byte[Bgra.Length];
        for (var i = 0; i < Bgra.Length; i += 4)
        {
            rgba[i] = Bgra[i + 2];
            rgba[i + 1] = Bgra[i + 1];
            rgba[i + 2] = Bgra[i];
            rgba[i + 3] = Bgra[i + 3];
        }
        return rgba;
    }
}
