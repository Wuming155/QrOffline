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

## 项目结构

```
QrOffline/
├─ Program.cs                 # 入口，AppBuilder 配置
├─ App.axaml(.cs)             # 应用级样式与主窗口装配
├─ ViewLocator.cs             # ViewModel -> View 映射（模板自带）
├─ app.manifest               # Windows 清单
├─ Assets/
│  ├─ app-icon.ico            # 多尺寸应用图标（窗口 + 可执行文件）
│  └─ app-icon.png            # 图标预览用位图
├─ ViewModels/
│  ├─ MainViewModel.cs        # 二维码生成逻辑、参数与状态
│  └─ ViewModelBase.cs
└─ Views/
   ├─ MainWindow.axaml        # 界面布局与样式
   └─ MainWindow.axaml.cs     # 剪贴板复制、保存 PNG
```

## 实现要点

**纯托管绘制，不依赖 `System.Drawing`。** 使用 QRCoder 的 `PngByteQRCode` 直接产出 PNG 字节流，再交给 Avalonia 的 `Bitmap` 解码显示，因此在 Windows / Linux / macOS 上行为一致：

```csharp
using var pngCode = new PngByteQRCode(data);
var bytes = pngCode.GetGraphic(perModule, dark, light, drawQuietZones: true);
using var stream = new MemoryStream(bytes);
QrImage = new Bitmap(stream);
```

**静默区与模块对齐。** `QRCodeData.ModuleMatrix` 已包含 4 个模块宽的静默区，`pixelsPerModule` 由目标尺寸除以模块总数取整得到，因此输出分辨率始终是模块尺寸的整数倍，放大后边缘依然锐利。

**位图回收时机。** 替换预览位图时，旧实例通过 `Dispatcher.UIThread.Post(..., DispatcherPriority.Background)` 延后一帧释放，避免渲染管线仍在使用时被提前销毁。

**Avalonia 12 适配。** 剪贴板使用新的 `ClipboardExtensions.SetBitmapAsync`；`TextBox.Watermark` 已更名为 `PlaceholderText`；文件对话框统一走 `IStorageProvider`；编译绑定默认开启，XAML 中显式声明 `x:DataType`。

## 许可证

本项目基于 [MIT 许可证](./LICENSE) 发布。
