param([switch]$Package)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$buildDir = Join-Path $root '.build'
$outputDir = Join-Path $root 'dist\ScrPhone'
New-Item -ItemType Directory -Force $buildDir, $outputDir | Out-Null

function Get-VerifiedArchive([string]$Url, [string]$Path, [string]$ExpectedHash) {
    if (-not (Test-Path -LiteralPath $Path)) { Invoke-WebRequest -Uri $Url -OutFile $Path }
    $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    if ($actual -ne $ExpectedHash) { throw "SHA-256 不匹配：$Path。删除该缓存文件后可重新下载。" }
}

$webviewVersion = '1.0.4314.35'
$webviewArchive = Join-Path $buildDir "webview2-$webviewVersion.zip"
Get-VerifiedArchive "https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/$webviewVersion/microsoft.web.webview2.$webviewVersion.nupkg" $webviewArchive '90778DEB1B77E36292828B4B5833C9255C27533F40E0DDD55891AE718944EDC4'
$sdk = Join-Path $buildDir "webview2-$webviewVersion"
if (-not (Test-Path (Join-Path $sdk 'lib\net462\Microsoft.Web.WebView2.Core.dll'))) { Expand-Archive -LiteralPath $webviewArchive -DestinationPath $sdk -Force }

$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '未找到 .NET Framework C# 编译器。' }
$core = Join-Path $sdk 'lib\net462\Microsoft.Web.WebView2.Core.dll'
$forms = Join-Path $sdk 'lib\net462\Microsoft.Web.WebView2.WinForms.dll'
$sources = Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object { $_.FullName }
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Xml.dll /reference:System.Web.Extensions.dll /reference:Accessibility.dll "/reference:$core" "/reference:$forms" "/win32manifest:$root\src\app.manifest" "/out:$outputDir\ScrPhone.exe" $sources
if ($LASTEXITCODE -ne 0) { throw "编译失败：$LASTEXITCODE" }
Copy-Item -LiteralPath $core, $forms -Destination $outputDir -Force
foreach ($arch in 'win-x86', 'win-x64', 'win-arm64') {
    $target = Join-Path $outputDir "runtimes\$arch\native"
    New-Item -ItemType Directory -Force $target | Out-Null
    Copy-Item -LiteralPath (Join-Path $sdk "runtimes\$arch\native\WebView2Loader.dll") -Destination $target -Force
}
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination $outputDir -Force
Copy-Item -LiteralPath (Join-Path $root 'licenses') -Destination $outputDir -Recurse -Force

if ($Package) {
    $scrcpyArchive = Join-Path $buildDir 'scrcpy-win64-v5.0.1.zip'
    Get-VerifiedArchive 'https://github.com/Genymobile/scrcpy/releases/download/v5.0.1/scrcpy-win64-v5.0.1.zip' $scrcpyArchive 'B12A2C4EE8BE317422451FC7DCF8EE20A71B5EA7EF9AD73DDD825A25316227A5'
    $scrcpyExtract = Join-Path $buildDir 'scrcpy-v5.0.1'
    if (-not (Test-Path (Join-Path $scrcpyExtract 'scrcpy-win64-v5.0.1\scrcpy.exe'))) { Expand-Archive -LiteralPath $scrcpyArchive -DestinationPath $scrcpyExtract -Force }
    $scrcpyTarget = Join-Path $outputDir 'tools\scrcpy'
    New-Item -ItemType Directory -Force $scrcpyTarget | Out-Null
    Copy-Item -Path (Join-Path $scrcpyExtract 'scrcpy-win64-v5.0.1\*') -Destination $scrcpyTarget -Recurse -Force
    $archive = Join-Path $root 'dist\ScrPhone-v0.1.0-win64.zip'
    $files = @('ScrPhone.exe', 'Microsoft.Web.WebView2.Core.dll', 'Microsoft.Web.WebView2.WinForms.dll', 'runtimes', 'licenses', 'tools', 'README.md') | ForEach-Object { Join-Path $outputDir $_ }
    Compress-Archive -Path $files -DestinationPath $archive -CompressionLevel Optimal -Force
    $hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -LiteralPath "$archive.sha256" -Value "$hash  ScrPhone-v0.1.0-win64.zip" -Encoding ascii
    Write-Host "发布包：$archive"
    Write-Host "SHA-256：$hash"
} else {
    Write-Host "构建完成：$outputDir\ScrPhone.exe。运行前请将 scrcpy 和 adb 放入 tools\scrcpy，或在启动参数中设置路径。"
}
