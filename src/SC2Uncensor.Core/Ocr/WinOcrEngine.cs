using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace SC2Uncensor.Core.Ocr;

public sealed class WinOcrEngine : IDisposable
{
    private readonly OcrEngine _engine;

    public WinOcrEngine()
    {
        _engine = OcrEngine.TryCreateFromLanguage(new Language("zh-Hans-CN"))
                  ?? OcrEngine.TryCreateFromUserProfileLanguages()
                  ?? throw new InvalidOperationException("Windows OCR engine unavailable (install zh-Hans language pack).");
    }

    public IReadOnlyList<OcrLine> Recognize(CapturedImage image, int originX = 0, int originY = 0, int scale = 1)
    {
        var rgba = image.ToRgba();
        var bitmap = SoftwareBitmap.CreateCopyFromBuffer(
            rgba.AsBuffer(),
            BitmapPixelFormat.Rgba8,
            image.Width,
            image.Height);
        var result = _engine.RecognizeAsync(bitmap).AsTask().GetAwaiter().GetResult();
        var lines = new List<OcrLine>();
        foreach (var line in result.Lines)
        {
            var words = line.Words;
            if (words.Count == 0) continue;
            var minX = words.Min(w => w.BoundingRect.X);
            var minY = words.Min(w => w.BoundingRect.Y);
            var maxX = words.Max(w => w.BoundingRect.X + w.BoundingRect.Width);
            var maxY = words.Max(w => w.BoundingRect.Y + w.BoundingRect.Height);
            lines.Add(new OcrLine(
                line.Text ?? "",
                originX + (int)(minX / scale),
                originY + (int)(minY / scale),
                Math.Max(1, (int)((maxX - minX) / scale)),
                Math.Max(1, (int)((maxY - minY) / scale))));
        }
        return lines;
    }

    public void Dispose() { }
}

public static class OcrMatch
{
    public static string Norm(string t) =>
        t.Replace(" ", "", StringComparison.Ordinal)
         .Replace(":", "", StringComparison.Ordinal)
         .Replace("\uFF1A", "", StringComparison.Ordinal);

    public static Dictionary<string, OcrLine> Match(IEnumerable<OcrLine> lines, params string[] keys)
    {
        var normed = lines.Select(l => (Norm(l.Text), l)).ToList();
        var hits = new Dictionary<string, OcrLine>();
        foreach (var k in keys)
        {
            foreach (var (t, ln) in normed)
                if (t == k) { hits[k] = ln; break; }
        }
        foreach (var k in keys)
        {
            if (hits.ContainsKey(k)) continue;
            foreach (var (t, ln) in normed)
                if (t.Contains(k, StringComparison.Ordinal) && t.Length <= k.Length + 2) { hits[k] = ln; break; }
        }
        foreach (var k in keys)
        {
            if (hits.ContainsKey(k)) continue;
            foreach (var (t, ln) in normed)
                if (t.Contains(k, StringComparison.Ordinal)) { hits[k] = ln; break; }
        }
        return hits;
    }
}
