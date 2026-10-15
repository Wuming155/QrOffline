<img src="Assets/app-icon.png" width="96" height="96" alt="QrOffline 图标" align="right" />

# QrOffline · 离线二维码生成器

[English](./README.md) | **简体中文**

一个基于 **Avalonia 12** 的桌面二维码生成工具。所有编码与绘制都在本地完成，不发起任何网络请求，输入的内容不会离开你的电脑。

## 功能

- **输入即生成** —— 编辑文本时自动重新生成，带 260ms 防抖，不会卡手
- **容错级别** —— L / M / Q / H 四档可选，附带每档的容量与适用场景说明
- **自定义配色** —— 前景色、背景色各 5 种/4 种预设，支持透明背景，便于叠到设计稿上
- **输出尺寸** —— 180 ~ 1200 px 可调，按模块整数倍取整输出，避免缩放造成的边缘模糊
- **导出** —— 一键复制图片到剪贴板，或通过系统文件对话框保存为 PNG
- **友好容错** —— 内容超出二维码容量时给出中文提示，而不是直接抛异常

## 界面

窗口分左右两栏：

| 左栏 | 右栏 |
| --- | --- |
| 内容输入框、字符计数、填入示例 | 二维码预览（白底卡片，未生成时显示占位） |
| 容错级别、前景色、背景色、输出尺寸 | 复制图片 / 保存 PNG |

顶部是渐变标题栏，底部状态行会显示生成结果、复制或保存的反馈。

## 技术栈

| 组件 | 版本 | 用途 |
| --- | --- | --- |
| .NET | 10.0 | 目标框架 `net10.0` |
| Avalonia | 12.1.3 | 跨平台 UI 框架（Fluent 主题） |
| CommunityToolkit.Mvvm | 8.4.2 | `[ObservableProperty]` / `[RelayCommand]` 源生成器 |
| QRCoder | 1.8.0 | 二维码编码与 PNG 输出 |

## 环境要求

- [.NET 10 SDK](https://dotnet.microsoft.com/download) 或更高

## 运行

```bash
dotnet run
```

## 发布

打包成可独立分发的单文件程序（目标机器无需安装 .NET）：

```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

产物位于 `bin/Release/net10.0/win-x64/publish/`。

在 Release 发布时指定 `RuntimeIdentifier` 会自动启用裁剪（`TrimMode=partial`）、单文件压缩与嵌入符号设置，以缩小体积、加快启动；普通的 `dotnet build` / `dotnet run` 不受影响。

## 测试

```bash
dotnet test tests/QrOffline.Tests/QrOffline.Tests.csproj
```

测试覆盖模块栅格化（尺寸、静默区、透明背景、取消），验证 MainViewModel 依赖的 `DataTooLongException` 行为，并检查每一条界面文案都有对应的简体中文翻译。CI 会在每次 push 与 PR 时运行同一套测试（`.github/workflows/ci.yml`）。

## 项目结构

```
QrOffline/
├─ Program.cs                    # 入口，AppBuilder 配置
├─ App.axaml(.cs)                # 应用级样式与主窗口装配
├─ app.manifest                  # Windows 清单
├─ Assets/
│  ├─ app-icon.ico               # 多尺寸应用图标（窗口 + 可执行文件）
│  └─ app-icon.png               # 图标预览用位图
├─ Localization/
│  ├─ Strings.resx               # 界面文案，中性资源（英文）
│  ├─ Strings.zh-Hans.resx       # 界面文案，简体中文
│  └─ Strings.cs                 # 强类型访问入口
├─ Rendering/
│  └─ QrRaster.cs                # 模块矩阵 -> BGRA 像素（纯逻辑，可测试）
├─ ViewModels/
│  ├─ MainViewModel.cs           # 异步二维码生成、参数与状态
│  └─ ViewModelBase.cs
├─ Views/
│  ├─ MainWindow.axaml           # 界面布局与样式
│  └─ MainWindow.axaml.cs        # 剪贴板复制、保存 PNG
├─ tests/
│  └─ QrOffline.Tests/           # 栅格化与本地化的 xUnit 测试
└─ .github/workflows/ci.yml      # push / PR 时构建并测试
```

## 实现要点

**纯托管绘制，不依赖 `System.Drawing`，且不再有 PNG 往返。** 预览直接从 QRCoder 的模块矩阵栅格化为 `WriteableBitmap`（`Rendering/QrRaster.cs`），因此实时路径上不再有「先编码 PNG 再解码成位图」的双重开销。PNG 压缩只在你真正保存时才进行：

```csharp
// 实时预览：模块矩阵 -> BGRA 像素
var pixels = QrRaster.BuildBgraPixels(data, pixelsPerModule, dark, light, token);
QrImage = CreateBitmap(pixels, size);

// 仅在保存时
using var png = new PngByteQRCode(data);
return png.GetGraphic(pixelsPerModule, darkRgba, lightRgba, drawQuietZones: true);
```

**在线程池上执行，且可取消。** 编码与栅格化通过 `Task.Run` 放到线程池，配合 `CancellationTokenSource`：新的编辑会取消上一轮尚未完成的工作，只有最终的位图赋值回到 UI 线程；260ms 防抖依然负责收敛连续输入。

**静默区与模块对齐。** `QRCodeData.ModuleMatrix` 已包含 4 个模块宽的静默区，`pixelsPerModule` 由目标尺寸除以模块总数取整得到，因此输出分辨率始终是模块尺寸的整数倍，放大后边缘依然锐利。预览使用 `HighQuality` 插值，缩放到卡片尺寸时不会产生锯齿。

**按异常类型判断。** 内容超出容量时按 `QRCoder.Exceptions.DataTooLongException` 捕获并在状态栏提示，而不是靠异常类型名字符串匹配。

**位图回收时机。** 替换预览位图时，旧实例通过 `Dispatcher.UIThread.Post(..., DispatcherPriority.Background)` 延后一帧释放，避免渲染管线仍在使用时被提前销毁。视图模型实现了 `IDisposable`，窗口关闭时释放。

**本地化。** 所有面向用户的文案集中在 `Localization/Strings.resx`（英文，中性资源）与 `Strings.zh-Hans.resx`（简体中文）。`Strings` 提供强类型访问入口，代码与 XAML（`{x:Static loc:Strings.*}`）共用，语言跟随系统 UI 区域性。

**Avalonia 12 适配。** 剪贴板使用新的 `ClipboardExtensions.SetBitmapAsync`；`TextBox.Watermark` 已更名为 `PlaceholderText`；文件对话框统一走 `IStorageProvider`；编译绑定默认开启，XAML 中显式声明 `x:DataType`。

## 许可证

本项目基于 [MIT 许可证](./LICENSE) 发布。
