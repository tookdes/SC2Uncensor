using System.Runtime.InteropServices;

namespace SC2Uncensor.Core.Capture;

public sealed class ScreenCapture(AppConfig cfg)
{
    public const int DarkP95 = 24;
    private readonly AppConfig _cfg = cfg;

    public CapturedImage Grab(ScreenRect? bbox = null)
    {
        var method = _cfg.Screenshot.ToLowerInvariant();
        var hwnd = method == "printwindow" ? Input.GameWindow.Find() : 0;
        if (hwnd != 0)
        {
            var win = GrabWindow(hwnd);
            if (win is not null) return CropToBbox(win, hwnd, bbox);
        }
        var img = GrabDesktop(bbox);
        if (method == "bitblt" || !LooksBlank(img)) return img;
        hwnd = Input.GameWindow.Find();
        var win2 = hwnd == 0 ? null : GrabWindow(hwnd);
        if (win2 is null)
        {
            Log.Write($"[capture] desktop looks black (p95={PixelStats(img).P95}), PrintWindow failed");
            return img;
        }
        Log.Write("[capture] desktop black, switched to PrintWindow");
        return CropToBbox(win2, hwnd, bbox);
    }

    public CapturedImage? GrabAlt(ScreenRect bbox, string badMethod)
    {
        if (badMethod != "window")
        {
            var hwnd = Input.GameWindow.Find();
            var win = hwnd == 0 ? null : GrabWindow(hwnd);
            if (win is not null) return CropToBbox(win, hwnd, bbox);
        }
        return GrabDesktop(bbox);
    }

    public static (int Count, int P95, double Dark) PixelStats(CapturedImage img)
    {
        var n = img.Width * img.Height;
        if (n <= 0) return (0, 0, 1);
        var hist = new int[256];
        var px = img.Bgra;
        for (var i = 0; i < px.Length; i += 4)
        {
            var y = (px[i + 2] * 299 + px[i + 1] * 587 + px[i] * 114) / 1000;
            hist[Math.Clamp(y, 0, 255)]++;
        }
        var acc = 0;
        var p95 = 255;
        var target = n * 0.95;
        for (var v = 0; v < 256; v++)
        {
            acc += hist[v];
            if (acc >= target) { p95 = v; break; }
        }
        var dark = 0;
        for (var v = 0; v < DarkP95; v++) dark += hist[v];
        return (n, p95, dark / (double)n);
    }

    public static bool LooksBlank(CapturedImage img)
    {
        var max = 0;
        var px = img.Bgra;
        for (var i = 0; i < px.Length; i += 4)
            max = Math.Max(max, (px[i] + px[i + 1] + px[i + 2]) / 3);
        return max <= 6;
    }

    public static List<(int Cx, int Cy, int W, int H)> BlueClusters(CapturedImage rgb)
    {
        var w = rgb.Width;
        var h = rgb.Height;
        var mask = new bool[w * h];
        var px = rgb.Bgra;
        var any = false;
        for (var i = 0; i < w * h; i++)
        {
            var o = i * 4;
            int b = px[o], g = px[o + 1], r = px[o + 2];
            if (b >= 140 && b - r >= 40 && b - g >= 30)
            {
                mask[i] = true;
                any = true;
            }
        }
        if (!any) return [];
        var rows = new List<int>();
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                if (mask[y * w + x]) { rows.Add(y); break; }
            }
        }
        if (rows.Count == 0) return [];
        var bands = new List<(int Y0, int Y1)>();
        var start = rows[0];
        for (var i = 1; i < rows.Count; i++)
        {
            if (rows[i] - rows[i - 1] > 4)
            {
                bands.Add((start, rows[i - 1]));
                start = rows[i];
            }
        }
        bands.Add((start, rows[^1]));
        var boxes = new List<(int Y0, int Y1, int X0, int X1)>();
        foreach (var (y0, y1) in bands)
        {
            var cols = new List<int>();
            for (var x = 0; x < w; x++)
            {
                for (var y = y0; y <= y1; y++)
                    if (mask[y * w + x]) { cols.Add(x); break; }
            }
            if (cols.Count == 0) continue;
            var cstart = cols[0];
            for (var i = 1; i < cols.Count; i++)
            {
                if (cols[i] - cols[i - 1] > 4)
                {
                    boxes.Add((y0, y1, cstart, cols[i - 1]));
                    cstart = cols[i];
                }
            }
            boxes.Add((y0, y1, cstart, cols[^1]));
        }
        var outList = new List<(int Cx, int Cy, int W, int H)>();
        foreach (var b in boxes)
            outList.Add((b.X0 + (b.X1 - b.X0 + 1) / 2, b.Y0 + (b.Y1 - b.Y0 + 1) / 2, b.X1 - b.X0 + 1, b.Y1 - b.Y0 + 1));
        return outList;
    }

    public static List<int> BlueCols(CapturedImage band)
    {
        var xs = new List<int>();
        var px = band.Bgra;
        for (var y = 0; y < band.Height; y++)
        for (var x = 0; x < band.Width; x++)
        {
            var o = (y * band.Width + x) * 4;
            int b = px[o], g = px[o + 1], r = px[o + 2];
            if (b >= 140 && b - r >= 40 && b - g >= 30) xs.Add(x);
        }
        return xs;
    }

    private static CapturedImage GrabDesktop(ScreenRect? bbox)
    {
        var r = bbox ?? Input.GameWindow.Primary();
        var w = Math.Max(1, r.Width);
        var h = Math.Max(1, r.Height);
        var screen = Native.CreateDC("DISPLAY", null, null, 0);
        var mem = Native.CreateCompatibleDC(screen);
        var bmp = Native.CreateCompatibleBitmap(screen, w, h);
        var old = Native.SelectObject(mem, bmp);
        try
        {
            Native.BitBlt(mem, 0, 0, w, h, screen, r.Left, r.Top, Native.Srccopy);
            return BitsFromBitmap(mem, bmp, w, h);
        }
        finally
        {
            Native.SelectObject(mem, old);
            Native.DeleteObject(bmp);
            Native.DeleteDC(mem);
            Native.DeleteDC(screen);
        }
    }

    private static CapturedImage? GrabWindow(nint hwnd)
    {
        if (!Native.GetClientRect(hwnd, out var cr)) return null;
        var w = cr.Width;
        var h = cr.Height;
        if (w <= 0 || h <= 0 || w * h > 16 * 1024 * 1024) return null;
        var hdc = Native.GetDC(hwnd);
        if (hdc == 0) return null;
        var mdc = Native.CreateCompatibleDC(hdc);
        var bmp = Native.CreateCompatibleBitmap(hdc, w, h);
        var old = Native.SelectObject(mdc, bmp);
        try
        {
            if (!Native.PrintWindow(hwnd, mdc, Native.PwClientOnly | Native.PwRenderFullContent))
                return null;
            return BitsFromBitmap(mdc, bmp, w, h);
        }
        finally
        {
            Native.SelectObject(mdc, old);
            Native.DeleteObject(bmp);
            Native.DeleteDC(mdc);
            Native.ReleaseDC(hwnd, hdc);
        }
    }

    private static CapturedImage BitsFromBitmap(nint dc, nint bmp, int w, int h)
    {
        var bi = new Native.BitmapInfoHeader
        {
            Size = (uint)Marshal.SizeOf<Native.BitmapInfoHeader>(),
            Width = w,
            Height = -h,
            Planes = 1,
            BitCount = 32,
            Compression = Native.BiRgb,
        };
        var buf = new byte[w * h * 4];
        if (Native.GetDIBits(dc, bmp, 0, (uint)h, buf, ref bi, Native.DibRgbColors) == 0)
            throw new InvalidOperationException("GetDIBits failed");
        return new CapturedImage { Bgra = buf, Width = w, Height = h };
    }

    private static CapturedImage CropToBbox(CapturedImage win, nint hwnd, ScreenRect? bbox)
    {
        if (bbox is null) return win;
        var client = Input.GameWindow.ClientRect(hwnd) ?? Input.GameWindow.Primary();
        var x0 = Math.Max(0, bbox.Left - client.Left);
        var y0 = Math.Max(0, bbox.Top - client.Top);
        return win.Crop(x0, y0, bbox.Width, bbox.Height);
    }
}
