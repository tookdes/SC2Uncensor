using SC2Uncensor.Core.Capture;
using SC2Uncensor.Core.Input;
using SC2Uncensor.Core.Memory;
using SC2Uncensor.Core.Ocr;

namespace SC2Uncensor.Core.Navigation;

public sealed class DialogHit(OcrLine violence, OcrLine? accept, int clientW, int clientH)
{
    public OcrLine Violence { get; } = violence;
    public OcrLine? Accept { get; } = accept;
    public int ClientW { get; } = clientW;
    public int ClientH { get; } = clientH;
}

public sealed class DialogNavigator(AppConfig cfg, ScreenCapture capture, WinOcrEngine ocr)
{
    private static readonly string KeyViolence = "\u51cf\u5c11\u66b4\u529b\u8868\u73b0";
    private static readonly string KeyViolenceShort = "\u66b4\u529b";
    private static readonly string KeyGraphics = "\u753b\u9762";
    private static readonly string KeyOptions = "\u9009\u9879";
    private static readonly string KeyBack = "\u8fd4\u56de\u6e38\u620f";
    private static readonly string KeyMenu = "\u83dc\u5355";
    private static readonly string KeyAccept = "\u63a5\u53d7";
    private static readonly string KeyCancel = "\u53d6\u6d88";

    public (IReadOnlyList<OcrLine> Lines, ScreenRect Client) OcrCenter(int scale = 2)
    {
        var client = GameWindow.ClientRect(GameWindow.Find()) ?? GameWindow.Primary();
        var crop = GameWindow.CropCenter(client);
        var img = capture.Grab(crop).ScaleNearest(scale);
        var lines = ocr.Recognize(img, crop.Left, crop.Top, scale);
        Log.Write($"[OCR] {lines.Count} lines scale={scale}");
        return (lines, client);
    }

    public object? RefineCheckbox(OcrLine lab, int clientW)
    {
        var y = lab.Y + lab.H / 2;
        var xGuess = lab.X + lab.W + (int)(28 * (clientW / 1280.0));
        try
        {
            var bandRect = new ScreenRect(xGuess - 45, y - 14, xGuess + 45, y + 14);
            var band = capture.Grab(bandRect);
            var (_, p95, _) = ScreenCapture.PixelStats(band);
            var xs = ScreenCapture.BlueCols(band);
            var hits = xs.Count(xx => Math.Abs(xx - band.Width / 2) <= 25);
            if (hits < 10 && p95 <= ScreenCapture.DarkP95)
            {
                var alt = capture.GrabAlt(bandRect, "desktop");
                if (alt is not null)
                {
                    var (_, p95b, _) = ScreenCapture.PixelStats(alt);
                    var xs2 = ScreenCapture.BlueCols(alt);
                    var hits2 = xs2.Count(xx => Math.Abs(xx - alt.Width / 2) <= 25);
                    if (p95b > ScreenCapture.DarkP95)
                    {
                        band = alt; p95 = p95b; xs = xs2; hits = hits2;
                    }
                }
            }
            if (hits < 10)
            {
                if (p95 <= ScreenCapture.DarkP95)
                {
                    Log.Write($"[locate] frame unreadable, guess ({xGuess},{y})");
                    return (xGuess, y);
                }
                Log.Write("[locate] no blue border (greyed / in-match?), skip click");
                return SkipByDesign.Instance;
            }
            var keep = xs.Where(xx => Math.Abs(xx - band.Width / 2) <= 25).ToList();
            var x = xGuess - 45 + (keep.Min() + keep.Max()) / 2;
            Log.Write($"[locate] y={y} x={x}");
            return (x, y);
        }
        catch (Exception ex)
        {
            Log.Write($"[locate] refine failed ({ex.Message}), guess ({xGuess},{y})");
            return (xGuess, y);
        }
    }

    public object? FindCheckbox()
    {
        var (lines, client) = OcrCenter();
        var hits = OcrMatch.Match(lines, KeyViolence, KeyViolenceShort);
        var lab = hits.GetValueOrDefault(KeyViolence) ?? hits.GetValueOrDefault(KeyViolenceShort);
        if (lab is null)
        {
            var crop = GameWindow.CropCenter(client);
            var img = capture.Grab(crop);
            var ok = ScreenCapture.BlueClusters(img)
                .Where(p => p.W is >= 7 and <= 50 && p.H is >= 7 and <= 50)
                .Select(p => (crop.Left + p.Cx, crop.Top + p.Cy))
                .ToList();
            if (ok.Count == 0) return null;
            return ok.MinBy(p => p.Item2);
        }
        return RefineCheckbox(lab, client.Width);
    }

    public bool ClickCheckbox(ProcessMemory mem, (int X, int Y)? pos = null)
    {
        var b = mem.Read(mem.Offsets.Store, 1);
        if (b is null) return false;
        var before = b[0];
        var hwnd = GameWindow.Find();
        var why = GameWindow.ClickGate(hwnd);
        if (why is not null)
        {
            Log.Write($"[click] blocked: {why}");
            return false;
        }
        if (pos is null)
        {
            var found = FindCheckbox();
            if (found is SkipByDesign) return false;
            if (found is not ValueTuple<int, int> xy) return false;
            pos = xy;
        }
        GameWindow.Click(pos.Value.X, pos.Value.Y);
        var hit = mem.WaitStoreChange(before, cfg.SafeVerifyClickMs);
        Thread.Sleep(50);
        var after = mem.Read(mem.Offsets.Store, 1)?[0] ?? -1;
        Log.Write($"[click] ({pos.Value.X},{pos.Value.Y}) store {before}->{after} {(hit ? "OK" : "MISS")}");
        return hit;
    }

    public object? OpenDialog(nint hwnd, int attempts = 3)
    {
        var acted = false;
        void EnsureAct()
        {
            if (acted) return;
            GameWindow.Activate(hwnd);
            acted = true;
        }

        DialogHit? OptThenSettle((int X, int Y) xy)
        {
            EnsureAct();
            GameWindow.Click(xy.X, xy.Y);
            var got = PollLabel(0.15, 1.8);
            if (got is not null)
            {
                var lab = got.Hits.GetValueOrDefault(KeyViolence) ?? got.Hits.GetValueOrDefault(KeyViolenceShort);
                if (lab is not null) return new DialogHit(lab, got.Hits.GetValueOrDefault(KeyAccept), got.W, got.H);
            }
            EnsureAct();
            var tab = got?.Hits.GetValueOrDefault(KeyGraphics);
            tab ??= OcrLeftBand().FirstOrDefault(c => OcrMatch.Norm(c.Text).Contains(KeyGraphics, StringComparison.Ordinal));
            if (tab is null) return null;
            GameWindow.Click(tab.Center.X, tab.Center.Y);
            var got2 = PollLabel(0.15, 1.6);
            if (got2 is not null)
            {
                var lab3 = got2.Hits.GetValueOrDefault(KeyViolence) ?? got2.Hits.GetValueOrDefault(KeyViolenceShort);
                if (lab3 is not null) return new DialogHit(lab3, got2.Hits.GetValueOrDefault(KeyAccept), got2.W, got2.H);
            }
            return null;
        }

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            var (lines, client) = OcrCenter();
            var hits = OcrMatch.Match(lines, KeyViolence, KeyViolenceShort, KeyGraphics, KeyOptions, KeyBack, KeyMenu, KeyAccept, KeyCancel);
            var lab = hits.GetValueOrDefault(KeyViolence) ?? hits.GetValueOrDefault(KeyViolenceShort);
            var tabs = lines.Where(ln => OcrMatch.Norm(ln.Text).Contains(KeyGraphics, StringComparison.Ordinal)).ToList();
            if (tabs.Count == 0 && (hits.ContainsKey(KeyAccept) || hits.ContainsKey(KeyCancel)))
                tabs = OcrLeftBand();
            var tab = tabs.FirstOrDefault();
            var opt = hits.GetValueOrDefault(KeyOptions);
            var menu = hits.GetValueOrDefault(KeyBack) ?? hits.GetValueOrDefault(KeyMenu);
            var where = lab is not null ? "graphics-page"
                : tab is not null ? "other-tab"
                : opt is not null ? "esc-menu"
                : menu is not null ? "menu"
                : "main/other";
            Log.Write($"[nav] round {attempt} {where}");
            if (lab is not null) return new DialogHit(lab, hits.GetValueOrDefault(KeyAccept), client.Width, client.Height);
            if (tab is not null)
            {
                EnsureAct();
                tab = tabs.MinBy(ln => ln.X)!;
                GameWindow.Click(tab.Center.X, tab.Center.Y);
                var got = PollLabel(0.15, 1.6);
                if (got is not null)
                {
                    var lab2 = got.Hits.GetValueOrDefault(KeyViolence) ?? got.Hits.GetValueOrDefault(KeyViolenceShort);
                    if (lab2 is not null) return new DialogHit(lab2, got.Hits.GetValueOrDefault(KeyAccept), got.W, got.H);
                }
                continue;
            }
            if (opt is not null)
            {
                var back = hits.GetValueOrDefault(KeyBack);
                if (back is not null && back.Y < opt.Y)
                {
                    Log.Write("[nav] in-match menu, skip");
                    return SkipByDesign.Instance;
                }
                var res = OptThenSettle(opt.Center);
                if (res is not null) return res;
                continue;
            }
            if (menu is not null)
            {
                var res = OptThenSettle(GameWindow.Frac(client, 0.500, 0.3319));
                if (res is not null) return res;
                continue;
            }
            if (InMatchHud())
            {
                Log.Write("[nav] in-match HUD, skip");
                return SkipByDesign.Instance;
            }
            EnsureAct();
            GameWindow.SendF10();
            var (gotMenu, back2) = PollMenu(0.15, 1.2);
            if (gotMenu is null)
            {
                GameWindow.SendEsc();
                (gotMenu, back2) = PollMenu(0.15, 1.6);
            }
            if (gotMenu is null) continue;
            if (gotMenu is string s && s == "dialog") continue;
            if (gotMenu is OcrLine optLine && back2 is not null && back2.Y < optLine.Y)
            {
                GameWindow.SendEsc();
                Log.Write("[nav] in-match pause menu, skip");
                return SkipByDesign.Instance;
            }
            var xy = gotMenu is OcrLine ol ? ol.Center : GameWindow.Frac(client, 0.500, 0.3319);
            var settled = OptThenSettle(xy);
            if (settled is not null) return settled;
        }
        return null;
    }

    public static void CloseDialogAndMenu()
    {
        GameWindow.SendEsc();
        Thread.Sleep(150);
        GameWindow.SendEsc();
        Thread.Sleep(200);
    }

    private PollResult? PollLabel(double firstDelay, double timeout)
    {
        Thread.Sleep((int)(firstDelay * 1000));
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(timeout);
        while (true)
        {
            var (lines, client) = OcrCenter();
            var hits = OcrMatch.Match(lines, KeyViolence, KeyViolenceShort, KeyAccept, KeyCancel, KeyGraphics);
            if (hits.Count > 0) return new PollResult(hits, client.Width, client.Height);
            if (DateTime.UtcNow >= deadline) return null;
            Thread.Sleep(150);
        }
    }

    private (object? Got, OcrLine? Back) PollMenu(double firstDelay, double timeout)
    {
        Thread.Sleep((int)(firstDelay * 1000));
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(timeout);
        while (true)
        {
            var (lines, _) = OcrCenter();
            var hits = OcrMatch.Match(lines, KeyOptions, KeyBack, KeyMenu, KeyAccept, KeyCancel);
            if (hits.GetValueOrDefault(KeyOptions) is { } opt) return (opt, hits.GetValueOrDefault(KeyBack));
            if (hits.ContainsKey(KeyBack) || hits.ContainsKey(KeyMenu)) return ("menu", null);
            if (hits.ContainsKey(KeyAccept) || hits.ContainsKey(KeyCancel)) return ("dialog", null);
            if (DateTime.UtcNow >= deadline) return (null, null);
            Thread.Sleep(150);
        }
    }

    private bool InMatchHud()
    {
        try
        {
            var client = GameWindow.ClientRect(GameWindow.Find()) ?? GameWindow.Primary();
            var box = new ScreenRect(
                client.Left + (int)(client.Width * 0.62),
                client.Top + (int)(client.Height * 0.72),
                client.Right, client.Bottom);
            var img = capture.Grab(box).ScaleNearest(2);
            var lines = ocr.Recognize(img, box.Left, box.Top, 2);
            return lines.Any(ln => OcrMatch.Norm(ln.Text).Contains(KeyMenu, StringComparison.Ordinal));
        }
        catch { return false; }
    }

    private List<OcrLine> OcrLeftBand()
    {
        try
        {
            var client = GameWindow.ClientRect(GameWindow.Find()) ?? GameWindow.Primary();
            var x0 = client.Left + (int)(client.Width * 0.16);
            var y0 = client.Top + (int)(client.Height * 0.05);
            var box = new ScreenRect(x0, y0,
                client.Left + (int)(client.Width * 0.40),
                client.Top + (int)(client.Height * 0.30));
            var img = capture.Grab(box).ScaleNearest(2);
            return ocr.Recognize(img, x0, y0, 2)
                .Where(ln => OcrMatch.Norm(ln.Text).Contains(KeyGraphics, StringComparison.Ordinal))
                .ToList();
        }
        catch { return []; }
    }

    private sealed record PollResult(Dictionary<string, OcrLine> Hits, int W, int H);
}
