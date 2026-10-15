using System;
using System.Threading;
using Avalonia.Media;
using QRCoder;

namespace QrOffline.Rendering;

/// <summary>
/// 把 QRCoder 的模块矩阵直接栅格化为 BGRA 像素缓冲区。
/// 与平台无关、无 UI 依赖，便于单元测试，也避免「编码 PNG 再解码」的双重开销。
/// </summary>
public static class QrRaster
{
    /// <summary>
    /// 由目标边长和模块总数推导每个模块的像素边长，向下取整到整数模块并保证至少 2 像素。
    /// </summary>
    public static int GetPixelsPerModule(int moduleCount, double targetSize)
    {
        if (moduleCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(moduleCount));
        }

        return Math.Max(2, (int)Math.Round(targetSize / moduleCount));
    }

    /// <summary>
    /// 生成预乘 alpha 的 BGRA 像素数据。<paramref name="data"/> 的模块矩阵已包含静默区，
    /// 因此输出边长恒为 <c>pixelsPerModule * 模块数</c>。
    /// </summary>
    public static byte[] BuildBgraPixels(
        QRCodeData data,
        int pixelsPerModule,
        Color dark,
        Color light,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (pixelsPerModule <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelsPerModule));
        }

        var matrix = data.ModuleMatrix;
        var modules = matrix.Count;
        var size = modules * pixelsPerModule;
        var pixels = new byte[size * size * 4];

        var darkPixel = ToPremultipliedBgra(dark);
        var lightPixel = ToPremultipliedBgra(light);

        for (var moduleY = 0; moduleY < modules; moduleY++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var row = matrix[moduleY];
            var y0 = moduleY * pixelsPerModule;

            for (var moduleX = 0; moduleX < modules; moduleX++)
            {
                var pixel = row[moduleX] ? darkPixel : lightPixel;
                var x0 = moduleX * pixelsPerModule;

                for (var dy = 0; dy < pixelsPerModule; dy++)
                {
                    var offset = ((y0 + dy) * size + x0) * 4;

                    for (var dx = 0; dx < pixelsPerModule; dx++)
                    {
                        pixels[offset + 0] = pixel.B;
                        pixels[offset + 1] = pixel.G;
                        pixels[offset + 2] = pixel.R;
                        pixels[offset + 3] = pixel.A;
                        offset += 4;
                    }
                }
            }
        }

        return pixels;
    }

    private static (byte B, byte G, byte R, byte A) ToPremultipliedBgra(Color color)
    {
        var a = color.A;
        return ((byte)(color.B * a / 255), (byte)(color.G * a / 255), (byte)(color.R * a / 255), a);
    }
}
