# ScrPhone

ScrPhone 是 Windows 上的 Android 设备连接与应用投屏启动器。它使用 ADB 和 scrcpy 连接设备、读取应用列表，并支持虚拟屏幕和手机主屏幕镜像。

## 下载与使用

从仓库的 Releases 下载 `ScrPhone-v0.1.0-win64.zip`，解压后运行 `ScrPhone.exe`。发布包包含 Windows x64 版 scrcpy 5.0.1 和 ADB；使用其他版本时可在“启动参数”中修改工具路径。

运行环境：Windows x64、.NET Framework 4.8.1，以及用于应用列表的 Microsoft Edge WebView2 Runtime。Windows 11 支持时使用系统云母背景；未安装 WebView2 Runtime 时，应用列表回退到原生控件。

1. 在手机开启开发者选项和无线调试，也可使用 USB 连接。首次无线连接时，进入“设备与连接”并完成配对，然后选择“搜索并连接”。
2. 首页会读取应用列表。选择应用后点击“开始”；可在“启动参数”中切换虚拟屏幕或手机镜像并调整设置。
3. 应用列表支持滚轮和浏览器中键自动滚动；选择后可通过键盘方向键浏览。运行中的会话可在“启动参数”结束。

工具路径和设置存放在程序目录的 `settings.xml`，应用缓存位于 `cache`，最近使用记录位于 `history.xml`。这些文件不会随发布包附带。配对认证由 ADB 管理，配对码不会保存。关闭启动器时会请求关闭其管理的 scrcpy 窗口。

虚拟屏幕、输入法和屏幕电源命令的效果取决于手机系统和应用。使用关闭主屏幕选项后，可通过“恢复手机屏幕”发送恢复请求。

## 构建

在 Windows PowerShell 中运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

脚本使用系统 .NET Framework 编译器，下载固定版本的 WebView2 SDK 并验证 SHA-256，输出到 `dist\ScrPhone`。普通构建不包含 ADB 和 scrcpy：可将工具放在 `dist\ScrPhone\tools\scrcpy`，或启动后设置工具路径。

制作 Windows x64 发布包：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Package
```

打包脚本下载并校验官方 scrcpy 5.0.1 Windows x64 发行包，生成 ZIP 和 SHA-256 文件。构建缓存和用户数据不纳入源码仓库或发布包。

## 项目结构

- `src`：窗口、设备连接、会话和应用列表代码。
- `build.ps1`：依赖校验、构建及打包。
- `licenses`：WebView2 SDK 的许可和声明；随发布包附带的 scrcpy 许可位于 `tools\scrcpy\LICENSE.txt`。

依赖：[scrcpy](https://github.com/Genymobile/scrcpy)、[Android Debug Bridge](https://developer.android.com/tools/adb)、[Microsoft Edge WebView2](https://learn.microsoft.com/microsoft-edge/webview2/)。WebView2 SDK 版本为 1.0.4314.35，运行时由本机提供。
