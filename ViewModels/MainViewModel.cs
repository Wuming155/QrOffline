using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRCoder;
using QRCoder.Exceptions;
using QrOffline.Localization;
using QrOffline.Rendering;

namespace QrOffline.ViewModels;

/// <summary>容错级别选项。</summary>
public sealed record EccOption(string Name, string Description, QRCodeGenerator.ECCLevel Level);

/// <summary>颜色选项，用于前景色 / 背景色下拉框。</summary>
public sealed record ColorOption(string Name, Color Color)
{
    public IBrush Brush { get; } = new SolidColorBrush(Color);
}

public partial class MainViewModel : ViewModelBase, IDisposable
{
    // 缓存的画刷，避免每次绑定时重新分配。
    private static readonly IBrush SuccessBrush = new SolidColorBrush(Color.Parse("#FF16A34A"));
    private static readonly IBrush ErrorBrush = new SolidColorBrush(Color.Parse("#FFE5484D"));

    private readonly DispatcherTimer _debounceTimer;
    private CancellationTokenSource? _generationCts;
    private Bitmap? _qrImage;
    private bool _ready;
    private bool _suppressSchedule;
    private bool _disposed;

    // 最近一次成功生成的重编码参数，用于按需导出 PNG。
    private bool _hasRender;
    private string _renderText = string.Empty;
    private QRCodeGenerator.ECCLevel _renderEcc;
    private int _renderPixelsPerModule;
    private byte[] _renderDark = [];
    private byte[] _renderLight = [];

    public MainViewModel()
    {
        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(260) };
        _debounceTimer.Tick += (_, _) =>
        {
            _debounceTimer.Stop();
            _ = GenerateAsync();
        };

        EccOptions =
        [
            new(Strings.EccLowName, Strings.EccLowDescription, QRCodeGenerator.ECCLevel.L),
            new(Strings.EccMediumName, Strings.EccMediumDescription, QRCodeGenerator.ECCLevel.M),
            new(Strings.EccQuartileName, Strings.EccQuartileDescription, QRCodeGenerator.ECCLevel.Q),
            new(Strings.EccHighName, Strings.EccHighDescription, QRCodeGenerator.ECCLevel.H),
        ];

        ForegroundOptions =
        [
            new(Strings.ColorGraphite, Color.Parse("#FF111827")),
            new(Strings.ColorIndigo, Color.Parse("#FF4338CA")),
            new(Strings.ColorPine, Color.Parse("#FF047857")),
            new(Strings.ColorCarmine, Color.Parse("#FFBE123C")),
            new(Strings.ColorCoffee, Color.Parse("#FF78350F")),
        ];

        BackgroundOptions =
        [
            new(Strings.ColorWhite, Colors.White),
            new(Strings.ColorCream, Color.Parse("#FFFDF6E3")),
            new(Strings.ColorLightGray, Color.Parse("#FFF1F5F9")),
            new(Strings.ColorTransparent, Color.Parse("#00FFFFFF")),
        ];

        SelectedEcc = EccOptions[1];
        SelectedForeground = ForegroundOptions[0];
        SelectedBackground = BackgroundOptions[0];

        _ready = true;
        _ = GenerateAsync();
    }

    public EccOption[] EccOptions { get; }

    public ColorOption[] ForegroundOptions { get; }

    public ColorOption[] BackgroundOptions { get; }

    /// <summary>容错级别。</summary>
    [ObservableProperty]
    public partial EccOption SelectedEcc { get; set; }

    /// <summary>前景色（二维码深色模块）。</summary>
    [ObservableProperty]
    public partial ColorOption SelectedForeground { get; set; }

    /// <summary>背景色（二维码浅色模块）。</summary>
    [ObservableProperty]
    public partial ColorOption SelectedBackground { get; set; }

    /// <summary>待编码的原始文本。</summary>
    [ObservableProperty]
    public partial string InputText { get; set; } = "www.baidu.com";

    /// <summary>输出图片边长（像素）。</summary>
    [ObservableProperty]
    public partial double PixelSize { get; set; } = 640;

    /// <summary>状态栏文字。</summary>
    [ObservableProperty]
    public partial string StatusMessage { get; set; } = Strings.StatusWaiting;

    /// <summary>状态是否为错误（用于变色）。</summary>
    [ObservableProperty]
    public partial bool StatusIsError { get; set; }

    /// <summary>当前二维码位图。</summary>
    public Bitmap? QrImage
    {
        get => _qrImage;
        private set
        {
            if (ReferenceEquals(_qrImage, value))
            {
                return;
            }

            var previous = _qrImage;
            _qrImage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasQrImage));

            // 延后一帧回收旧位图，避免渲染管线仍在使用它。
            if (previous is not null)
            {
                Dispatcher.UIThread.Post(previous.Dispose, DispatcherPriority.Background);
            }
        }
    }

    public bool HasQrImage => _qrImage is not null;

    public string CharCount => string.Format(CultureInfo.CurrentCulture, Strings.CharCountFormat, InputText?.Length ?? 0);

    public string PixelSizeLabel => string.Format(CultureInfo.CurrentCulture, Strings.PixelSizeFormat, (int)PixelSize);

    public IBrush StatusBrush => StatusIsError ? ErrorBrush : SuccessBrush;

    partial void OnInputTextChanged(string value)
    {
        OnPropertyChanged(nameof(CharCount));
        Schedule();
    }

    partial void OnPixelSizeChanged(double value)
    {
        OnPropertyChanged(nameof(PixelSizeLabel));
        Schedule();
    }

    partial void OnSelectedEccChanged(EccOption value) => Schedule();

    partial void OnSelectedForegroundChanged(ColorOption value) => Schedule();

    partial void OnSelectedBackgroundChanged(ColorOption value) => Schedule();

    partial void OnStatusIsErrorChanged(bool value) => OnPropertyChanged(nameof(StatusBrush));

    /// <summary>按钮：立即生成。</summary>
    [RelayCommand]
    private void Regenerate()
    {
        _debounceTimer.Stop();
        _ = GenerateAsync();
    }

    /// <summary>按钮：填充一段示例文本。</summary>
    [RelayCommand]
    private void FillSample() => InputText = Strings.SampleText;

    /// <summary>按钮：清空输入与预览。</summary>
    [RelayCommand]
    private void Clear()
    {
        _debounceTimer.Stop();
        _suppressSchedule = true;
        try
        {
            InputText = string.Empty;
        }
        finally
        {
            _suppressSchedule = false;
        }

        _ = GenerateAsync();
    }

    /// <summary>防抖：文本编辑时延迟生成，避免频繁重算。</summary>
    private void Schedule()
    {
        if (!_ready || _suppressSchedule || _disposed)
        {
            return;
        }

        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    /// <summary>
    /// 根据当前设置生成二维码位图。重活（编码 + 栅格化）在线程池上执行，
    /// 只有写入 <see cref="QrImage"/> 回到 UI 线程；上一轮请求会被取消。
    /// </summary>
    private async Task GenerateAsync()
    {
        // 取消上一轮尚未完成的工作（此时它一定还未被释放）。
        _generationCts?.Cancel();
        var cts = new CancellationTokenSource();
        _generationCts = cts;

        try
        {
            var text = InputText ?? string.Empty;

            if (text.Length == 0)
            {
                _hasRender = false;
                QrImage = null;
                StatusMessage = Strings.StatusWaiting;
                StatusIsError = false;
                return;
            }

            var ecc = SelectedEcc?.Level ?? QRCodeGenerator.ECCLevel.M;
            var targetSize = PixelSize;
            var darkColor = SelectedForeground?.Color ?? Colors.Black;
            var lightColor = SelectedBackground?.Color ?? Colors.White;

            try
            {
                var render = await Task.Run(
                    () => BuildRender(text, ecc, targetSize, darkColor, lightColor, cts.Token),
                    cts.Token);

                if (_disposed || cts.IsCancellationRequested)
                {
                    return;
                }

                QrImage = CreateBitmap(render.Pixels, render.Size);

                _renderText = text;
                _renderEcc = ecc;
                _renderPixelsPerModule = render.PixelsPerModule;
                _renderDark = ToRgba(darkColor);
                _renderLight = ToRgba(lightColor);
                _hasRender = true;

                StatusMessage = Strings.StatusReady;
                StatusIsError = false;
            }
            catch (OperationCanceledException)
            {
                // 已被更新的一次生成取代，忽略。
            }
            catch (DataTooLongException)
            {
                ClearRender();
                StatusIsError = true;
                StatusMessage = Strings.StatusTooLong;
            }
            catch (Exception ex)
            {
                ClearRender();
                StatusIsError = true;
                StatusMessage = string.Format(CultureInfo.CurrentCulture, Strings.StatusGenerationFailed, ex.Message);
            }
        }
        finally
        {
            // 只有仍是「当前」的请求才需要清空引用，避免误清掉新一轮的 cts。
            if (ReferenceEquals(_generationCts, cts))
            {
                _generationCts = null;
            }

            cts.Dispose();
        }
    }

    /// <summary>
    /// 按需把当前二维码编码为 PNG 字节（用于保存）。与预览共用同一套参数，
    /// 因此只在真正导出时才付出 PNG 压缩的代价。
    /// </summary>
    public Task<byte[]?> EncodePngAsync()
    {
        if (!_hasRender)
        {
            return Task.FromResult<byte[]?>(null);
        }

        var text = _renderText;
        var ecc = _renderEcc;
        var pixelsPerModule = _renderPixelsPerModule;
        var dark = _renderDark;
        var light = _renderLight;

        return Task.Run<byte[]?>(() =>
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(text, ecc, forceUtf8: true, utf8BOM: false);
            using var png = new PngByteQRCode(data);
            return png.GetGraphic(pixelsPerModule, dark, light, drawQuietZones: true);
        });
    }

    /// <summary>停止后台工作并释放当前位图。</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _debounceTimer.Stop();

        var cts = _generationCts;
        _generationCts = null;
        cts?.Cancel();
        cts?.Dispose();

        var image = _qrImage;
        _qrImage = null;
        image?.Dispose();
    }

    private void ClearRender()
    {
        _hasRender = false;
        QrImage = null;
    }

    private static RenderResult BuildRender(
        string text,
        QRCodeGenerator.ECCLevel ecc,
        double targetSize,
        Color darkColor,
        Color lightColor,
        CancellationToken cancellationToken)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, ecc, forceUtf8: true, utf8BOM: false);

        cancellationToken.ThrowIfCancellationRequested();

        // ModuleMatrix 已包含 4 个模块宽的静默区，保证扫码器可识别。
        var pixelsPerModule = QrRaster.GetPixelsPerModule(data.ModuleMatrix.Count, targetSize);
        var pixels = QrRaster.BuildBgraPixels(data, pixelsPerModule, darkColor, lightColor, cancellationToken);

        return new RenderResult(pixels, pixelsPerModule * data.ModuleMatrix.Count, pixelsPerModule);
    }

    private static Bitmap CreateBitmap(byte[] pixels, int size)
    {
        var bitmap = new WriteableBitmap(
            new PixelSize(size, size),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using (var buffer = bitmap.Lock())
        {
            var rowBytes = size * 4;

            for (var y = 0; y < size; y++)
            {
                Marshal.Copy(pixels, y * rowBytes, buffer.Address + (y * buffer.RowBytes), rowBytes);
            }
        }

        return bitmap;
    }

    /// <summary>把 Avalonia 颜色转换为 QRCoder 需要的 RGBA 字节数组。</summary>
    private static byte[] ToRgba(Color color) => [color.R, color.G, color.B, color.A];

    private readonly record struct RenderResult(byte[] Pixels, int Size, int PixelsPerModule);
}
