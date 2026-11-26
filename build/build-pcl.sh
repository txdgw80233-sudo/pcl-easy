#!/usr/bin/env bash
# 编译 PCL（经典 .NET Framework WPF 工程），无需 Visual Studio。
# 用法：./build/build-pcl.sh [Debug|Release]
# 依赖：.NET SDK 9（含 WindowsDesktop 组件）、.NET Framework 4.8 引用程序集、MeloongCore 子模块已拉取。
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# MSBuild 需要 Windows 风格路径（C:/...）；MSYS 的 /c/... 会让 Exists() 判定失败而静默跳过导入
if command -v cygpath >/dev/null 2>&1; then
  HERE_WIN="$(cygpath -m "$HERE")"
else
  HERE_WIN="$(printf '%s' "$HERE" | sed -E 's#^/([a-zA-Z])/#\1:/#')"
fi
SRC="$(cd "$HERE/.." && pwd)"
SRC_WIN="$HERE_WIN/.."
CFG="${1:-Debug}"

if [ ! -d "$SRC/MeloongCore/Shared" ]; then
  echo "缺少 MeloongCore 子模块：$SRC/MeloongCore（git submodule update --init）" >&2
  exit 1
fi

echo "== 1/4 MeloongCore (netstandard2.0) =="
dotnet build "$SRC/MeloongCore/Shared/MeloongCore.csproj" -c "$CFG" -v:q -nologo

echo "== 2/4 MeloongCore.Wpf (net48) =="
dotnet build "$SRC/MeloongCore/Wpf/MeloongCore.Wpf.csproj" -c "$CFG" -v:q -nologo

echo "== 3/4 PCLCS (net48) =="
dotnet build "$SRC/PCLCS/PCLCS.csproj" -c "$CFG" -v:q -nologo

echo "== 4/4 PCL 主工程（经典工程，注入 WPF 编译目标） =="
dotnet msbuild "$SRC_WIN/Plain Craft Launcher 2/Plain Craft Launcher 2.vbproj" \
  -p:Configuration="$CFG" \
  -p:BuildProjectReferences=false \
  -p:CustomAfterMicrosoftCommonTargets="$HERE_WIN/winfx-import.targets" \
  -v:m -nologo

echo "== 产物 =="
ls -la "$SRC/Plain Craft Launcher 2/bin/Plain Craft Launcher 2.exe"
