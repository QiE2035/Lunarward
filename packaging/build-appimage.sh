#!/usr/bin/env bash
# 生成 AppImage 用的 AppDir；装了 appimagetool 就直接产出 .AppImage。
#
# 用法：
#   ./build-appimage.sh              # 构建 AppDir，若有 appimagetool 再打成 AppImage
#   SKIP_APPIMAGE=1 ./build-appimage.sh   # 只构建 AppDir（便于调试）
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo="$(dirname "$here")"
appdir="${APPDIR:-$repo/build/Lunarward.AppDir}"
out="$repo/build"

command -v dotnet >/dev/null || { echo "需要 dotnet SDK 10" >&2; exit 1; }

echo "==> 清理 $appdir"
rm -rf "$appdir"
mkdir -p "$appdir/usr/lib/lunarward" "$appdir/usr/share/applications" \
         "$appdir/usr/share/icons/hicolor/256x256/apps"

echo "==> publish"
# 必须进项目目录：dotnet 从当前工作目录向上找 global.json，仓库根钉的是上游 SDK 10.0.400
( cd "$repo/src/Lunarward" && \
  dotnet publish Lunarward.csproj -c Release -r linux-x64 --self-contained false \
    -o "$appdir/usr/lib/lunarward" )

echo "==> 组装 AppDir"
install -m 755 "$repo/packaging/lunarward.desktop" "$appdir/lunarward.desktop"
install -m 644 "$repo/packaging/lunarward.png" "$appdir/lunarward.png"
install -m 644 "$repo/packaging/lunarward.png" "$appdir/usr/share/icons/hicolor/256x256/apps/lunarward.png"
install -m 644 "$repo/packaging/lunarward.desktop" "$appdir/usr/share/applications/lunarward.desktop"

cat > "$appdir/AppRun" <<'SH'
#!/bin/sh
# AppImage 入口：资源与 dll 都在 usr/lib/lunarward 下
HERE="$(dirname "$(readlink -f "$0")")"
exec "$HERE/usr/lib/lunarward/Lunarward" "$@"
SH
chmod 755 "$appdir/AppRun"

echo "==> AppDir 完成：$appdir"

if [[ "${SKIP_APPIMAGE:-0}" == "1" ]]; then
  echo "跳过 AppImage 打包（SKIP_APPIMAGE=1）"
  exit 0
fi

if command -v appimagetool >/dev/null; then
  echo "==> 打包 AppImage"
  ARCH=x86_64 appimagetool "$appdir" "$out/Lunarward-x86_64.AppImage"
else
  cat <<'TIP'
未找到 appimagetool，AppDir 已就绪，可自行打包：

  wget https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage
  chmod +x appimagetool-x86_64.AppImage
  ARCH=x86_64 ./appimagetool-x86_64.AppImage build/Lunarward.AppDir build/Lunarward-x86_64.AppImage
TIP
fi
