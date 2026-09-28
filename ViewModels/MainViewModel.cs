using System;
using System.IO;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRCoder;

namespace QrOffline.ViewModels;

/// <summary>容错级别选项。</summary>
public sealed record EccOption(string Name, string Description, QRCodeGenerator.ECCLevel Level);

/// <summary>颜色选项，用于前景色 / 背景色下拉框。</summary>
public sealed record ColorOption(string Name, Color Color)
{
    public IBrush Brush { get; } = new SolidColorBrush(Color);
}

public partial class MainViewModel : ViewModelBase
{
    private readonly DispatcherTimer _debounceTimer;
    private Bitmap? _qrImage;
    private bool _ready;

    public MainViewModel()
    {
        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(260) };
        _debounceTimer.Tick += (_, _) =>
        {
            _debounceTimer.Stop();
            Generate();
        };

        EccOptions =
        [
            new("L · 低", "约 7% 容错，可容纳数据最多", QRCodeGenerator.ECCLevel.L),
            new("M · 中", "约 15% 容错，日常推荐", QRCodeGenerator.ECCLevel.M),
            new("Q · 较高", "约 25% 容错，适合轻微污损场景", QRCodeGenerator.ECCLevel.Q),
            new("H · 高", "约 30% 容错，最抗损，适合印刷", QRCodeGenerator.ECCLevel.H),
        ];

        ForegroundOptions =
        [
            new("石墨黑", Color.Parse("#FF111827")),
            new("深靛蓝", Color.Parse("#FF4338CA")),
            new("松墨绿", Color.Parse("#FF047857")),
            new("胭脂红", Color.Parse("#FFBE123C")),
            new("咖褐", Color.Parse("#FF78350F")),
        ];

        BackgroundOptions =
        [
            new("纯白", Colors.White),
            new("米白", Color.Parse("#FFFDF6E3")),
            new("浅灰", Color.Parse("#FFF1F5F9")),
            new("透明", Color.Parse("#00FFFFFF")),
        ];

        SelectedEcc = EccOptions[1];
        SelectedForeground = ForegroundOptions[0];
        SelectedBackground = BackgroundOptions[0];

        _ready = true;
        Generate();
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
    public partial string StatusMessage { get; set; } = "等待输入内容";

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

    /// <summary>最近一次生成的 PNG 数据。</summary>
    public byte[]? PngBytes { get; private set; }

    public string CharCount => $"{(InputText?.Length ?? 0)} 个字符";

    public string PixelSizeLabel => $"{(int)PixelSize} px";

    public IBrush StatusBrush => StatusIsError
        ? new SolidColorBrush(Color.Parse("#FFE5484D"))
        : new SolidColorBrush(Color.Parse("#FF16A34A"));

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
        Generate();
    }

    /// <summary>按钮：填充一段示例文本。</summary>
    [RelayCommand]
    private void FillSample() => InputText = "离线二维码生成器 · QrOffline · 无需联网";

    /// <summary>按钮：清空输入与预览。</summary>
    [RelayCommand]
    private void Clear()
    {
        _debounceTimer.Stop();
        InputText = string.Empty;
        Generate();
    }

    /// <summary>防抖：文本编辑时延迟生成，避免频繁重算。</summary>
    private void Schedule()
    {
        if (!_ready)
        {
            return;
        }

        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    /// <summary>根据当前设置生成二维码位图与 PNG 数据。</summary>
    public void Generate()
    {
        var text = InputText ?? string.Empty;

        if (text.Length == 0)
        {
            PngBytes = null;
            QrImage = null;
            StatusMessage = "等待输入内容";
            StatusIsError = false;
            return;
        }

        try
        {
            var ecc = SelectedEcc?.Level ?? QRCodeGenerator.ECCLevel.M;

            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(text, ecc, forceUtf8: true, utf8BOM: false);

            // ModuleMatrix 已包含 4 个模块宽的静默区，保证扫码器可识别。
            var modules = data.ModuleMatrix.Count;
            var perModule = Math.Max(2, (int)Math.Round(PixelSize / modules));

            var dark = ToRgba(SelectedForeground?.Color ?? Colors.Black);
            var light = ToRgba(SelectedBackground?.Color ?? Colors.White);

            using var pngCode = new PngByteQRCode(data);
            var bytes = pngCode.GetGraphic(perModule, dark, light, drawQuietZones: true);
            PngBytes = bytes;

            using var stream = new MemoryStream(bytes);
            QrImage = new Bitmap(stream);

            StatusMessage = "生成成功，可复制或保存为 PNG";
            StatusIsError = false;
        }
        catch (Exception ex)
        {
            PngBytes = null;
            QrImage = null;
            StatusIsError = true;
            StatusMessage = ex.GetType().Name.Contains("TooLong", StringComparison.Ordinal)
                ? "内容过长，无法生成。请缩短文本或改用更低的容错级别。"
                : $"生成失败：{ex.Message}";
        }
    }

    /// <summary>把 Avalonia 颜色转换为 QRCoder 需要的 RGBA 字节数组。</summary>
    private static byte[] ToRgba(Color color) => [color.R, color.G, color.B, color.A];
}
