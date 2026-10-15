using System.Globalization;
using System.Resources;

namespace QrOffline.Localization;

/// <summary>
/// 集中管理所有面向用户的文案，通过 <see cref="ResourceManager"/> 按当前 UI 区域性解析。
/// 中性资源为英文（<c>Strings.resx</c>），简体中文位于 <c>Strings.zh-Hans.resx</c>。
/// </summary>
public static class Strings
{
    private static readonly ResourceManager Manager =
        new("QrOffline.Localization.Strings", typeof(Strings).Assembly);

    private static string Get(string name) =>
        Manager.GetString(name, CultureInfo.CurrentUICulture) ?? name;

    public static string AppTitle => Get(nameof(AppTitle));
    public static string AppSubtitle => Get(nameof(AppSubtitle));

    public static string InputHeader => Get(nameof(InputHeader));
    public static string InputHint => Get(nameof(InputHint));
    public static string InputPlaceholder => Get(nameof(InputPlaceholder));
    public static string FillSampleButton => Get(nameof(FillSampleButton));
    public static string EccLabel => Get(nameof(EccLabel));
    public static string ForegroundLabel => Get(nameof(ForegroundLabel));
    public static string BackgroundLabel => Get(nameof(BackgroundLabel));
    public static string OutputSizeLabel => Get(nameof(OutputSizeLabel));
    public static string GenerateButton => Get(nameof(GenerateButton));
    public static string ClearButton => Get(nameof(ClearButton));

    public static string PreviewHeader => Get(nameof(PreviewHeader));
    public static string PreviewEmpty => Get(nameof(PreviewEmpty));
    public static string CopyImageButton => Get(nameof(CopyImageButton));
    public static string SavePngButton => Get(nameof(SavePngButton));

    public static string EccLowName => Get(nameof(EccLowName));
    public static string EccLowDescription => Get(nameof(EccLowDescription));
    public static string EccMediumName => Get(nameof(EccMediumName));
    public static string EccMediumDescription => Get(nameof(EccMediumDescription));
    public static string EccQuartileName => Get(nameof(EccQuartileName));
    public static string EccQuartileDescription => Get(nameof(EccQuartileDescription));
    public static string EccHighName => Get(nameof(EccHighName));
    public static string EccHighDescription => Get(nameof(EccHighDescription));

    public static string ColorGraphite => Get(nameof(ColorGraphite));
    public static string ColorIndigo => Get(nameof(ColorIndigo));
    public static string ColorPine => Get(nameof(ColorPine));
    public static string ColorCarmine => Get(nameof(ColorCarmine));
    public static string ColorCoffee => Get(nameof(ColorCoffee));
    public static string ColorWhite => Get(nameof(ColorWhite));
    public static string ColorCream => Get(nameof(ColorCream));
    public static string ColorLightGray => Get(nameof(ColorLightGray));
    public static string ColorTransparent => Get(nameof(ColorTransparent));

    public static string SampleText => Get(nameof(SampleText));

    public static string StatusWaiting => Get(nameof(StatusWaiting));
    public static string StatusReady => Get(nameof(StatusReady));
    public static string StatusTooLong => Get(nameof(StatusTooLong));
    public static string StatusGenerationFailed => Get(nameof(StatusGenerationFailed));
    public static string StatusCopied => Get(nameof(StatusCopied));
    public static string StatusCopyFailed => Get(nameof(StatusCopyFailed));
    public static string StatusSaved => Get(nameof(StatusSaved));
    public static string StatusSaveFailed => Get(nameof(StatusSaveFailed));
    public static string StatusNothingToSave => Get(nameof(StatusNothingToSave));

    public static string CharCountFormat => Get(nameof(CharCountFormat));
    public static string PixelSizeFormat => Get(nameof(PixelSizeFormat));

    public static string SaveDialogTitle => Get(nameof(SaveDialogTitle));
    public static string PngFileTypeName => Get(nameof(PngFileTypeName));
    public static string SuggestedFileName => Get(nameof(SuggestedFileName));
}
