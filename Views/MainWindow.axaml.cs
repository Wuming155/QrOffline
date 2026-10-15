using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using QrOffline.Localization;
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
            viewModel.StatusMessage = Strings.StatusCopied;
            viewModel.StatusIsError = false;
        }
        catch (Exception ex)
        {
            viewModel.StatusMessage = string.Format(CultureInfo.CurrentCulture, Strings.StatusCopyFailed, ex.Message);
            viewModel.StatusIsError = true;
        }
    }

    /// <summary>通过系统文件对话框把二维码保存为 PNG。</summary>
    private async void OnSavePngClick(object? sender, RoutedEventArgs e)
    {
        var viewModel = ViewModel;
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;

        if (viewModel is null || storage is null)
        {
            return;
        }

        try
        {
            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = Strings.SaveDialogTitle,
                SuggestedFileName = Strings.SuggestedFileName,
                DefaultExtension = "png",
                ShowOverwritePrompt = true,
                FileTypeChoices =
                [
                    new FilePickerFileType(Strings.PngFileTypeName) { Patterns = ["*.png"] },
                ],
            });

            if (file is null)
            {
                return;
            }

            var bytes = await viewModel.EncodePngAsync();
            if (bytes is null)
            {
                viewModel.StatusMessage = Strings.StatusNothingToSave;
                viewModel.StatusIsError = true;
                return;
            }

            await using (var stream = await file.OpenWriteAsync())
            {
                await stream.WriteAsync(bytes);
            }

            viewModel.StatusMessage = string.Format(CultureInfo.CurrentCulture, Strings.StatusSaved, file.Name);
            viewModel.StatusIsError = false;
        }
        catch (Exception ex)
        {
            viewModel.StatusMessage = string.Format(CultureInfo.CurrentCulture, Strings.StatusSaveFailed, ex.Message);
            viewModel.StatusIsError = true;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        ViewModel?.Dispose();
        base.OnClosed(e);
    }
}
