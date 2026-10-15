# AGENTS.md

## Project

QrOffline — a fully offline Avalonia 12 desktop QR-code generator. Everything (encoding + rasterizing) runs locally; the app makes no network requests.

Single .NET 10 app project at the repo root (`QrOffline.csproj`) with the xUnit test project under `tests/`. There is **no solution file** — target projects explicitly.

## Commands

- Build: `dotnet build`
- Run: `dotnet run`
- All tests: `dotnet test tests/QrOffline.Tests/QrOffline.Tests.csproj`
- Single test class: `dotnet test tests/QrOffline.Tests/QrOffline.Tests.csproj --filter "FullyQualifiedName~QrRasterTests"`
- Publish (self-contained single file): `dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true`

There is no separate lint/format/typecheck step; the compiler + tests are the verification. CI (`.github/workflows/ci.yml`) restores, builds, and tests the test project in Release on `net10.0.x`, so run the test command above before finishing.

## Layout and boundaries

- `Program.cs` → `App.axaml(.cs)` → `Views/MainWindow.axaml(.cs)` — entry point and window wiring.
- `ViewModels/MainViewModel.cs` — all QR state, options, and async generation. Marked `IDisposable`; the window disposes it on close.
- `Views/MainWindow.axaml.cs` — clipboard copy and PNG save live in code-behind (need `TopLevel`), not in the VM.
- `Rendering/QrRaster.cs` — pure, UI-free module-matrix → BGRA rasterizer; the primary unit-tested unit. Keep it free of UI/`Bitmap` dependencies.
- `Localization/` — resx-based strings (`Strings.resx` = neutral/English, `Strings.zh-Hans.resx` = Simplified Chinese, `Strings.cs` = typed accessors).
- `tests/QrOffline.Tests/` — xUnit, references the app project.

## Gotchas

- **The tests folder lives inside the app project's tree.** `QrOffline.csproj` explicitly `Compile Remove`s `tests\**`. If you add another top-level folder containing `.cs`, add a matching `<Compile Remove>` (and `EmbeddedResource`/`None`) or the app build will compile it.
- **Localization parity is test-enforced.** Every user-facing string must be added in three places: `Localization/Strings.resx`, `Localization/Strings.zh-Hans.resx`, and a strongly-typed accessor property in `Localization/Strings.cs`. `LocalizationTests.EveryNeutralKeyHasSimplifiedChineseValue` fails if a neutral key lacks a zh-Hans value. XAML uses them via `{x:Static loc:Strings.*}`.
- **Avalonia 12 differs from older docs:** clipboard via `ClipboardExtensions.SetBitmapAsync`, `TextBox.PlaceholderText` (not `Watermark`), file dialogs via `IStorageProvider`, and `x:DataType` is required on compiled-binding roots.
- **CommunityToolkit.Mvvm 8.4 source generators:** `[ObservableProperty]` sits on partial properties (`public partial X Y { get; set; }`); use `[RelayCommand]` for commands and `partial void OnXChanged(...)` for change hooks.
- **Two rendering paths on purpose:** the live preview rasterizes the module matrix straight to BGRA → `WriteableBitmap` (no `System.Drawing`, no PNG round-trip); PNG compression only happens on save via `PngByteQRCode`. Don't reintroduce a PNG encode/decode in the preview path.
- **Generation is async, cancellable, and debounced:** encoding + rasterizing run on `Task.Run` with a `CancellationTokenSource`; a newer edit cancels in-flight work; a 260 ms `DispatcherTimer` debounces typing; only the final `Bitmap` assignment touches the UI thread. Replaced bitmaps are disposed one frame later via `Dispatcher.UIThread.Post(..., DispatcherPriority.Background)`.
- `QRCodeData.ModuleMatrix` already includes the 4-module quiet zone; `QrRaster.GetPixelsPerModule` floors to a whole number of pixels per module (min 2) so output stays crisp.
- **Publish trimming is conditional:** `PublishTrimmed`/`TrimMode=partial`, single-file compression, and embedded-symbol settings only apply in Release when a `RuntimeIdentifier` is supplied. Plain `build`/`run` are unaffected.

## Conventions

- Source comments and XML docs are in Chinese; commit messages use conventional prefixes (`feat:`, `perf:`, `docs:`) with Chinese descriptions.
- `README.md` and `README.zh-CN.md` are paired translations — keep both in sync when behavior changes.
