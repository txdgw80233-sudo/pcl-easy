# 构建说明（无需 Visual Studio）

本目录提供一套用 `dotnet msbuild` 编译经典 .NET Framework WPF 工程的脚本，目的是让没有安装 Visual Studio 的机器（以及 CI）也能编出单文件 exe。

## 用法

```bash
bash build/build-pcl.sh Debug     # 或 Release
```

产物：`Plain Craft Launcher 2/bin/Plain Craft Launcher 2.exe`

## 依赖

- .NET SDK 9（含 WindowsDesktop 组件，提供 net9.0 版 `PresentationBuildTasks.dll`）
- .NET Framework 4.8 引用程序集（`C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8`）
- `MeloongCore` 子模块：`git submodule update --init --recursive`
- 首次构建需要联网还原 NuGet 包

## 为什么需要 winfx-import.targets

经典 WPF 工程依赖 `Microsoft.WinFX.targets`（XAML 标记编译），它随 Visual Studio 提供。SDK 自带的 `Microsoft.NETFramework.CurrentVersion.targets` 会尝试从 `$(MSBuildToolsPath)` 导入，但 dotnet SDK 目录里没有该文件，且 .NET Framework 自带的版本依赖 GAC 中的 `PresentationBuildTasks`，在 CoreCLR 上加载失败（`MSB4062`）。

`winfx-import.targets` 通过 `CustomAfterMicrosoftCommonTargets` 注入：

1. 指向 4.8 引用程序集目录，并显式引用 `mscorlib` / `Microsoft.VisualBasic`；
2. 关闭 NuGet 相关的临时工程复制（经典工程没有 `obj\*.nuget.g.*`，否则 `MSB3030`）；
3. 导入 SDK 自带的 net9.0 版 `Microsoft.WinFX.targets`。

注意：脚本会为本工程设置 `BuildProjectReferences=false`，子工程（MeloongCore / PCLCS）由脚本先单独构建，避免全局属性污染 netstandard 工程（否则出现 `CS0433 HttpRequestMessage` 同时存在于 System.Net.Http 与 netstandard）。

路径必须是 Windows 风格（`C:/...`）：MSYS 的 `/c/...` 会让 MSBuild 的 `Exists()` 判定失败，从而静默跳过 WinFX 导入，最终表现为大量 `BC30506 / BC30590`（找不到 `WithEvents` 变量与事件）。
