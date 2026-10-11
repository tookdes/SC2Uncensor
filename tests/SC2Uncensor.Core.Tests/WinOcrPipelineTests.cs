using SC2Uncensor.Core;
using SC2Uncensor.Core.Ocr;

namespace SC2Uncensor.Core.Tests;

public class WinOcrPipelineTests
{
    [Fact]
    public void Recognizes_synthetic_image_without_throwing()
    {
        // Verifies the riskiest new interop: RGBA buffer -> SoftwareBitmap -> RecognizeAsync.
        const int w = 320, h = 160;
        var bgra = new byte[w * h * 4];
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            var o = (y * w + x) * 4;
            var ink = (x is > 10 and < 300) && (y is > 60 and < 100);
            var v = (byte)(ink ? 0 : 255);
            bgra[o] = v; bgra[o + 1] = v; bgra[o + 2] = v; bgra[o + 3] = 255;
        }
        var img = new CapturedImage { Bgra = bgra, Width = w, Height = h };

        using var engine = new WinOcrEngine();
        var lines = engine.Recognize(img);
        Assert.NotNull(lines);
    }

    [Fact]
    public void ScaleNearest_doubles_dimensions()
    {
        var img = new CapturedImage { Bgra = new byte[4 * 4 * 4], Width = 4, Height = 4 };
        var big = img.ScaleNearest(2);
        Assert.Equal(8, big.Width);
        Assert.Equal(8, big.Height);
        Assert.Equal(8 * 8 * 4, big.Bgra.Length);
    }

    [Fact]
    public void ToRgba_swaps_red_and_blue()
    {
        var img = new CapturedImage { Bgra = [1, 2, 3, 4], Width = 1, Height = 1 };
        Assert.Equal([3, 2, 1, 4], img.ToRgba());
    }
}
