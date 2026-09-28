<img src="Assets/app-icon.png" width="96" height="96" alt="QrOffline icon" align="right" />

# QrOffline · Offline QR Code Generator

**English** | [简体中文](./README.zh-CN.md)

A desktop QR code generator built with **Avalonia 12**. Everything — encoding and rasterizing — runs locally. The app makes no network requests, and whatever you type never leaves your machine.

## Features

- **Live generation** — the code is regenerated as you type, debounced by 260 ms so the UI stays responsive
- **Error correction levels** — L / M / Q / H, each annotated with its capacity trade-off and typical use case
- **Custom colours** — 5 foreground and 4 background presets, including a transparent background for dropping the code onto artwork
- **Output size** — 180 to 1200 px. Sizes are snapped to whole modules, so edges stay sharp at any resolution
- **Export** — copy the image straight to the clipboard, or save it as a PNG through the system file dialog
- **Graceful limits** — content that exceeds the capacity of a QR symbol produces a readable message instead of an exception

## Interface

The window is split into two columns:

| Left | Right |
| --- | --- |
| Text input, character count, sample text | Live preview on a white card (placeholder when empty) |
| Error correction level, foreground / background colour, output size | Copy image / Save PNG |

A gradient banner sits at the top; the status line at the bottom reports generation, copy and save results.

## Tech stack

| Component | Version | Purpose |
| --- | --- | --- |
| .NET | 10.0 | Target framework `net10.0` |
| Avalonia | 12.1.3 | Cross-platform UI framework (Fluent theme) |
| CommunityToolkit.Mvvm | 8.4.2 | `[ObservableProperty]` / `[RelayCommand]` source generators |
| QRCoder | 1.8.0 | QR encoding and PNG output |

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later

## Run

```bash
dotnet run
```

## Publish

Produce a self-contained single-file executable, so the target machine needs no .NET installation:

```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

The output lands in `bin/Release/net10.0/win-x64/publish/`.

## Project structure

```
QrOffline/
├─ Program.cs                 # Entry point, AppBuilder configuration
├─ App.axaml(.cs)             # Application styles and main window wiring
├─ ViewLocator.cs             # ViewModel -> View mapping (from the template)
├─ app.manifest               # Windows manifest
├─ Assets/
│  ├─ app-icon.ico            # Multi-size app icon (window + executable)
│  └─ app-icon.png            # Bitmap used for the icon preview
├─ ViewModels/
│  ├─ MainViewModel.cs        # QR generation logic, options and state
│  └─ ViewModelBase.cs
└─ Views/
   ├─ MainWindow.axaml        # Layout and styles
   └─ MainWindow.axaml.cs     # Clipboard copy, PNG export
```

## Implementation notes

**Managed rendering only, no `System.Drawing`.** QRCoder's `PngByteQRCode` emits a PNG byte array which Avalonia's `Bitmap` decodes, so behaviour is identical on Windows, Linux and macOS:

```csharp
using var pngCode = new PngByteQRCode(data);
var bytes = pngCode.GetGraphic(perModule, dark, light, drawQuietZones: true);
using var stream = new MemoryStream(bytes);
QrImage = new Bitmap(stream);
```

**Quiet zone and module alignment.** `QRCodeData.ModuleMatrix` already contains the 4-module quiet zone that scanners require. `pixelsPerModule` is derived by dividing the requested size by the total module count, so the rendered resolution is always an exact multiple of the module size and the code stays crisp when scaled up.

**Bitmap lifetime.** When the preview image is replaced, the previous instance is disposed one frame later via `Dispatcher.UIThread.Post(..., DispatcherPriority.Background)`, so the render pipeline is never left holding a disposed bitmap.

**Avalonia 12 adaptations.** Clipboard access goes through the new `ClipboardExtensions.SetBitmapAsync`; `TextBox.Watermark` is now `PlaceholderText`; file dialogs use `IStorageProvider`; compiled bindings are enabled by default, so `x:DataType` is declared explicitly in XAML.
