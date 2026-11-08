# ScrPhone

「ScrPhone」是一款适用于 Windows 的 Android 设备连接与投屏启动工具。

通过 ADB 和 scrcpy，可以连接 Android 设备、浏览应用列表，并使用虚拟屏幕或手机主屏幕镜像启动投屏。

## 一、下载与安装

前往 [Releases](https://github.com/FrRay/scrphone/releases/latest) 下载最新的 Windows x64 压缩包，解压后运行 `ScrPhone.exe`。

发布包包含 scrcpy 和 ADB。

※如需使用其他版本，可在「启动参数」中修改工具路径。

运行环境：

- Windows x64。
- .NET Framework 4.8.1 或更高版本。
- Microsoft Edge WebView2 Runtime。

※应用列表使用 WebView2 Runtime。未安装时，将切换至原生控件。

※在支持的 Windows 11 系统上，窗口将使用系统云母背景。

## 二、连接设备

「ScrPhone」支持 USB 和无线调试两种连接方式。

首次使用无线调试时，请按照以下步骤操作：

1. 在手机上开启开发者选项和无线调试。
2. 进入「设备与连接」页面，完成设备配对。
3. 选择「搜索并连接」，连接已配对的设备。

※配对认证由 ADB 管理，配对码不会保存。

启动时，如未检测到已连接设备，将进入「设备与连接」页面；检测到设备时，将进入「应用」页面。

## 三、启动投屏

设备连接后，「应用」页面将读取手机应用列表。

选择应用并点击「开始」，即可启动投屏。可在「启动参数」中选择虚拟屏幕或手机主屏幕镜像，并调整相关设置。

运行中的投屏会话可在「启动参数」中结束。

※关闭启动器时，程序会请求关闭由其管理的 scrcpy 窗口。

应用列表支持鼠标滚轮和键盘方向键浏览。

※使用 WebView2 列表时，还支持鼠标中键自动滚动。

※虚拟屏幕、输入法和屏幕电源命令的实际效果取决于手机系统和应用。

※使用关闭主屏幕选项后，可通过「恢复手机屏幕」发送恢复请求。

## 四、设置与数据

程序运行时产生的设置和数据保存在程序目录下：

- `settings.xml`：程序设置和工具路径。
- `cache`：应用缓存。
- `history.xml`：最近使用记录。

※上述文件不包含在发布包中。

## 五、构建项目

在 Windows PowerShell 中运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

构建脚本使用系统 .NET Framework 编译器，下载指定版本的 WebView2 SDK，并验证 SHA-256。构建结果输出至 `dist\ScrPhone`。

※普通构建不包含 ADB 和 scrcpy。可将工具放入 `dist\ScrPhone\tools\scrcpy`，或在启动后设置工具路径。

### 制作发布包

在 Windows PowerShell 中运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Package
```

打包脚本将下载并校验官方 scrcpy Windows x64 发行包，并按照 `VERSION` 中的版本号生成 ZIP 和 SHA-256 文件。

※构建缓存和用户数据不纳入源码仓库及发布包。

## 六、项目结构

- `src`：窗口、设备连接、投屏会话及应用列表代码。
- `assets`：应用图标的 SVG 源文件和多尺寸 Windows ICO。
- `build.ps1`：依赖校验、构建与打包脚本。
- `licenses`：WebView2 SDK 许可及声明。

※发布包中的 scrcpy 许可位于 `tools\scrcpy\LICENSE.txt`。

## 七、依赖项目

- [scrcpy](https://github.com/Genymobile/scrcpy)
- [Android Debug Bridge](https://developer.android.com/tools/adb)
- [Microsoft Edge WebView2](https://learn.microsoft.com/microsoft-edge/webview2/)

项目使用 WebView2 SDK，WebView2 Runtime 由本机提供。
