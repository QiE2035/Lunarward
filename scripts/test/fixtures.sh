#!/usr/bin/env bash
# 生成 L2 夹具：假运行器（wine/wineboot/wineserver）、假前缀、假游戏 exe、假 DXVK、假外部命令、假配置。
# 一切都在 $ROOT 下，不碰真实环境。
set -euo pipefail

: "${ROOT:?ROOT 未设置}"

mkdir -p "$ROOT"/{bin,fake-wine/bin,prefix/drive_c,prefix/.lunarward,game,fake-dxvk/x64,fake-dxvk/x86,cfg/lunarward/profiles,data,cache,state,home,logs,games,out}

# ── 假运行器：wine 记录每次调用，--version 返回固定串 ───────────────────────────
cat > "$ROOT/fake-wine/bin/wine" <<'EOS'
#!/usr/bin/env bash
echo "wine $*" >> "${LUNARWARD_FAKE_LOG:-/tmp/lw-fake-wine.log}"
case "$1" in
    --version) echo "wine-9.0-fake"; exit 0 ;;
    wineboot)  echo "wineboot done"; exit 0 ;;
    reg)       echo "reg ok"; exit 0 ;;
    cmd)       echo "cmd ok"; exit 0 ;;
esac
exit 0
EOS
chmod +x "$ROOT/fake-wine/bin/wine"

cat > "$ROOT/fake-wine/bin/wineboot" <<'EOS'
#!/usr/bin/env bash
echo "wineboot $*" >> "${LUNARWARD_FAKE_LOG:-/tmp/lw-fake-wine.log}"
# 模拟真实行为：建出最小前缀结构
if [ -n "${WINEPREFIX:-}" ] && [ ! -d "$WINEPREFIX" ]; then
    mkdir -p "$WINEPREFIX/drive_c/windows/system32" "$WINEPREFIX/drive_c/windows/syswow64" "$WINEPREFIX/.lunarward"
    : > "$WINEPREFIX/system.reg"
    : > "$WINEPREFIX/user.reg"
fi
exit 0
EOS
chmod +x "$ROOT/fake-wine/bin/wineboot"

cat > "$ROOT/fake-wine/bin/wineserver" <<'EOS'
#!/usr/bin/env bash
echo "wineserver $*" >> "${LUNARWARD_FAKE_LOG:-/tmp/lw-fake-wine.log}"
case "$1" in
    -w) sleep 0.2; exit 0 ;;
    -k) exit 0 ;;
esac
exit 0
EOS
chmod +x "$ROOT/fake-wine/bin/wineserver"
ln -sf wine "$ROOT/fake-wine/bin/wine64"

# ── 假前缀（external 用）────────────────────────────────────────────────────
: > "$ROOT/prefix/system.reg"
: > "$ROOT/prefix/user.reg"
cat > "$ROOT/prefix/.lunarward/prefix.json" <<'EOS'
{"GameKey":"hk4e_cn","ProfileId":"fakeprfl","Mode":"external","TemplatePath":null,"CreatedAt":"2026-01-01T00:00:00Z"}
EOS

# ── 假游戏 exe ─────────────────────────────────────────────────────────────
printf '#!/bin/sh\necho fake-game\n' > "$ROOT/game/YuanShen.exe"
chmod +x "$ROOT/game/YuanShen.exe"

# ── 假 DXVK（组件安装/校验/移除用）─────────────────────────────────────────
head -c 2048 /dev/urandom > "$ROOT/fake-dxvk/x64/d3d11.dll"
head -c 1024 /dev/urandom > "$ROOT/fake-dxvk/x64/dxgi.dll"
head -c 1024 /dev/urandom > "$ROOT/fake-dxvk/x86/d3d10core.dll"
cat > "$ROOT/fake-dxvk/fake.version" <<'EOS'
9.9-fake
EOS

# 第二个图形组件（VKD3D 形态），用于验证「换组件时留 .bak」
mkdir -p "$ROOT/fake-vkd3d/x64"
head -c 1536 /dev/urandom > "$ROOT/fake-vkd3d/x64/d3d12.dll"

# ── 假外部命令：记录参数，方便断言「被调用且参数正确」 ──────────────────────
make_fake() {  # name, body
    cat > "$ROOT/bin/$1" <<EOS
#!/usr/bin/env bash
echo "$1 \$*" >> "\$ROOT/logs/ext.log" 2>/dev/null || true
$2
EOS
    # $ROOT 不透传，改写成字面量
    sed -i "s|\$ROOT|$ROOT|g" "$ROOT/bin/$1"
    chmod +x "$ROOT/bin/$1"
}

make_fake spectacle 'out=""; while [ $# -gt 0 ]; do case "$1" in -o|-O) out="$2"; shift 2;; *) shift;; esac; done; [ -n "$out" ] && python3 -c "
import struct,zlib,sys
w,h=8,8
raw=b\"\".join(b\"\x00\"+b\"\xff\x00\x00\"*w for _ in range(h))
def chunk(t,d):
    c=t+d
    return struct.pack(\">I\",len(d))+c+struct.pack(\">I\",zlib.crc32(c)&0xffffffff)
png=b\"\x89PNG\r\n\x1a\n\"+chunk(b\"IHDR\",struct.pack(\">IIBBBBB\",w,h,8,2,0,0,0))+chunk(b\"IDAT\",zlib.compress(raw))+chunk(b\"IEND\",b\"\")
open(sys.argv[1],\"wb\").write(png)
" "$out"; echo "$out"'
make_fake grim 'out=""; while [ $# -gt 0 ]; do case "$1" in -g) shift 2;; *) out="$1"; shift;; esac; done; [ -n "$out" ] && cp /dev/null "$out"; echo "$out"'
make_fake xdg-open 'exit 0'
make_fake gio 'exit 0'
make_fake notify-send 'exit 0'
make_fake kdialog 'exit 0'
make_fake zenity 'exit 0'
make_fake fuser 'exit 1'
make_fake lsof 'exit 1'

# ── 假配置：一个 external 方案 + 游戏安装路径 ────────────────────────────────
cat > "$ROOT/cfg/lunarward/profiles/fakeprfl.json" <<EOS
{
  "Id": "fakeprfl",
  "Name": "测试方案",
  "RunnerType": "wine",
  "RunnerPath": "$ROOT/fake-wine/bin/wine",
  "PrefixMode": "external",
  "PrefixPath": "$ROOT/prefix",
  "Environment": {},
  "Shell": "",
  "GameOutput": "silent",
  "GraphicsId": null
}
EOS

cat > "$ROOT/cfg/lunarward/config.json" <<EOS
{
  "UserDataFolder": "$ROOT/data/lunarward",
  "install_path_hk4e_cn": "$ROOT/game"
}
EOS

echo "夹具就绪：$ROOT"
