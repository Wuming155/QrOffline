using System;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using QrOffline.ViewModels;

namespace QrOffline.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    /// <summary>把当前二维码位图写入系统剪贴板。</summary>
    private async void OnCopyImageClick(object? sender, RoutedEventArgs e)
    {
        var viewModel = ViewModel;
        var bitmap = viewModel?.QrImage;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;

        if (viewModel is null || bitmap is null || clipboard is null)
        {
            return;
        }

        try
        {
            await clipboard.SetBitmapAsync(bitmap);
            viewModel.StatusMessage = "二维码图片已复制到剪贴板";
            viewModel.StatusIsError = false;
        }
        catch (Exception ex)
        {
            viewModel.StatusMessage = $"复制失败：{ex.Message}";
            viewModel.StatusIsError = true;
        }
    }

    /// <summary>通过系统文件对话框把二维码保存为 PNG。</summary>
    private async void OnSavePngClick(object? sender, RoutedEventArgs e)
    {
        var viewModel = ViewModel;
        var bytes = viewModel?.PngBytes;
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;

        if (viewModel is null || bytes is null || storage is null)
        {
            return;
        }

        try
        {
            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "保存二维码",
                SuggestedFileName = "qrcode.png",
                DefaultExtension = "png",
                ShowOverwritePrompt = true,
                FileTypeChoices =
                [
                    new FilePickerFileType("PNG 图片") { Patterns = ["*.png"] },
                ],
            });

            if (file is null)
            {
                return;
            }

            await using (var stream = await file.OpenWriteAsync())
            {
                await stream.WriteAsync(bytes);
            }

            viewModel.StatusMessage = $"已保存：{file.Name}";
            viewModel.StatusIsError = false;
        }
        catch (Exception ex)
        {
            viewModel.StatusMessage = $"保存失败：{ex.Message}";
            viewModel.StatusIsError = true;
        }
    }
}
