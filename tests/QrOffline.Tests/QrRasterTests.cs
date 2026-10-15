using Avalonia.Media;
using QRCoder;
using QrOffline.Rendering;
using Xunit;

namespace QrOffline.Tests;

public class QrRasterTests
{
    private static QRCodeData CreateData(string text, QRCodeGenerator.ECCLevel ecc = QRCodeGenerator.ECCLevel.M)
    {
        using var generator = new QRCodeGenerator();
        return generator.CreateQrCode(text, ecc, forceUtf8: true, utf8BOM: false);
    }

    [Theory]
    [InlineData(1, 10.0, 10)]
    [InlineData(100, 190.0, 2)]
    [InlineData(50, 180.0, 4)]
    [InlineData(45, 1200.0, 27)]
    public void GetPixelsPerModule_ComputesExpectedValue(int moduleCount, double targetSize, int expected)
    {
        Assert.Equal(expected, QrRaster.GetPixelsPerModule(moduleCount, targetSize));
    }

    [Fact]
    public void GetPixelsPerModule_ClampsToMinimumTwo()
    {
        // 目标尺寸远小于模块数时，仍至少 2 像素，避免输出退化。
        Assert.Equal(2, QrRaster.GetPixelsPerModule(500, 100));
    }

    [Fact]
    public void GetPixelsPerModule_RejectsNonPositiveModuleCount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => QrRaster.GetPixelsPerModule(0, 100));
    }

    [Fact]
    public void ModuleMatrix_IncludesQuietZone()
    {
        using var data = CreateData("hello");

        // 第一行整行应与静默区一致（全部为浅色模块）。
        Assert.All(Enumerable.Range(0, data.ModuleMatrix.Count), x => Assert.False(data.ModuleMatrix[0][x]));
    }

    [Fact]
    public void BuildBgraPixels_ProducesSizeTimesSizeTimesFourBytes()
    {
        using var data = CreateData("https://example.com");
        const int perModule = 6;

        var pixels = QrRaster.BuildBgraPixels(data, perModule, Colors.Black, Colors.White);

        var size = data.ModuleMatrix.Count * perModule;
        Assert.Equal(size * size * 4, pixels.Length);
    }

    [Fact]
    public void BuildBgraPixels_UsesLightColorForQuietZone()
    {
        using var data = CreateData("hello");
        const int perModule = 4;

        var pixels = QrRaster.BuildBgraPixels(data, perModule, Colors.Black, Colors.White);

        // 左上角落在静默区，应为浅色。
        Assert.Equal(0xFF, pixels[0]); // B
        Assert.Equal(0xFF, pixels[1]); // G
        Assert.Equal(0xFF, pixels[2]); // R
        Assert.Equal(0xFF, pixels[3]); // A
    }

    [Fact]
    public void BuildBgraPixels_ContainsBothDarkAndLightModules()
    {
        using var data = CreateData("hello");
        const int perModule = 4;

        var pixels = QrRaster.BuildBgraPixels(data, perModule, Colors.Black, Colors.White);

        var dark = 0;
        var light = 0;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            if (pixels[i] == 0x00)
            {
                dark++;
            }
            else if (pixels[i] == 0xFF)
            {
                light++;
            }
        }

        Assert.True(dark > 0);
        Assert.True(light > 0);
    }

    [Fact]
    public void BuildBgraPixels_TransparentBackgroundStaysTransparent()
    {
        using var data = CreateData("hello");
        const int perModule = 4;

        var pixels = QrRaster.BuildBgraPixels(data, perModule, Colors.Black, Color.Parse("#00FFFFFF"));

        // 静默区应为完全透明的预乘像素。
        Assert.Equal(0, pixels[3]); // A
        Assert.Equal(0, pixels[0]); // B
        Assert.Equal(0, pixels[1]); // G
        Assert.Equal(0, pixels[2]); // R
    }

    [Fact]
    public void BuildBgraPixels_RejectsNonPositivePixelsPerModule()
    {
        using var data = CreateData("hello");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            QrRaster.BuildBgraPixels(data, 0, Colors.Black, Colors.White));
    }

    [Fact]
    public void BuildBgraPixels_ObservesCancellation()
    {
        using var data = CreateData("hello");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            QrRaster.BuildBgraPixels(data, 4, Colors.Black, Colors.White, cts.Token));
    }
}
