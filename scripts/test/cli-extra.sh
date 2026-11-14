#!/usr/bin/env bash
# L2 补充用例：命令面里剩下的一批（不依赖真实网络）。
# 覆盖：--shell-plan --url-plan --companion-plan --companion-run --prefix-ensure
#       --prefix-template --runner-check --convert-screenshots
#       --install-plan --patch-plan --install-fetch --update-run（后四个走本地假 Sophon）
#
# 需要：python3、zstd（假 Sophon 与 PNG 夹具用）；缺了就 skip 相关用例。
# 用法：LW_BIN=/path/to/Lunarward bash scripts/test/cli-extra.sh
set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
: "${LW_BIN:?需要 LW_BIN=/path/to/Lunarward}"
BIN="$LW_BIN"
ROOT="${LW_ROOT:-$(mktemp -d /tmp/lw-l2-extra-XXXXXX)}"
export ROOT BIN

source "$HERE/lib.sh"
bash "$HERE/fixtures.sh" >/dev/null

out="$ROOT/logs/out.txt"
text() { cat "$out" 2>/dev/null; }

PORT="${LW_SOPHON_PORT:-18322}"
BASE="http://127.0.0.1:$PORT"
HAVE_SOPHON=0
if command -v python3 >/dev/null && command -v zstd >/dev/null; then
    mkdir -p "$ROOT/sophon"
    python3 "$HERE/fake-sophon.py" --port "$PORT" >"$ROOT/sophon/server.log" 2>&1 &
    SERVER_PID=$!
    trap 'kill "$SERVER_PID" 2>/dev/null; wait "$SERVER_PID" 2>/dev/null' EXIT
    for _ in $(seq 1 40); do
        curl -sf "$BASE/health" >/dev/null 2>&1 && HAVE_SOPHON=1 && break
        sleep 0.1
    done
fi
sophon_env() {
    env LUNARWARD_SOPHON_BASE="$BASE" LUNARWARD_SOPHON_DIRECT=1 \
        LUNARWARD_SOPHON_PACKAGE=fake-package LUNARWARD_SOPHON_TAG=9.9.9 \
        XDG_CONFIG_HOME="$ROOT/cfg" XDG_DATA_HOME="$ROOT/data" XDG_CACHE_HOME="$ROOT/cache" \
        XDG_STATE_HOME="$ROOT/state" HOME="$ROOT/home" \
        PATH="$ROOT/bin:$ROOT/fake-wine/bin:$PATH" \
        "$BIN" "$@"
}

case_start "T-CL-21" "--shell-plan 列出方案与调试 Shell 命令"
lw --shell-plan >"$out" 2>&1; rc=$?
if expect_rc 0 "$rc" && expect_contains "$(text)" "测试方案"; then pass_case; fi

case_start "T-CL-22" "--url-plan 把深链展开成参数"
lw --url-plan "lunarward://startgame?biz=hk4e_cn" >"$out" 2>&1; rc=$?
if expect_rc 0 "$rc" && expect_contains "$(text)" "hk4e_cn"; then pass_case; fi

case_start "T-CL-23" "--companion-plan 无条目时不报错"
lw --companion-plan hk4e_cn >"$out" 2>&1; rc=$?
if expect_rc 0 "$rc"; then pass_case; fi

case_start "T-CL-24" "--companion-run 缺参数时报未知命令，不再静默进 GUI"
lw --companion-run hk4e_cn >"$out" 2>&1; rc=$?
if expect_rc 1 "$rc" && expect_contains "$(text)" "未知命令或不完整"; then pass_case; fi

case_start "T-CL-24b" "--companion-run 条目不存在时明确报错"
lw --companion-run hk4e_cn no-such-id >"$out" 2>&1; rc=$?
if expect_rc 1 "$rc" && expect_contains "$(text)" "找不到条目"; then pass_case; fi

case_start "T-CL-25" "--prefix-ensure 解析出 external 前缀"
lw --prefix-ensure hk4e_cn fakeprfl >"$out" 2>&1; rc=$?
if expect_rc 0 "$rc" && expect_contains "$(text)" "prefix  :" && expect_contains "$(printf %s "$(text)" | tr 'A-Z' 'a-z')" "true (drive_c)"; then pass_case; fi

case_start "T-CL-26" "--prefix-template 把现有前缀克隆成模板"
lw --prefix-template "$ROOT/prefix" extra-tpl >"$out" 2>&1; rc=$?
if expect_rc 0 "$rc" && expect_contains "$(text)" "模板已创建" && expect_file "$ROOT/data/lunarward/prefix-templates/extra-tpl/drive_c"; then pass_case; fi

case_start "T-CL-27" "--runner-check 探测运行器版本"
lw --runner-check "$ROOT/fake-wine/bin/wine" >"$out" 2>&1; rc=$?
if expect_rc 0 "$rc" && expect_contains "$(text)" "wine-9.0-fake"; then pass_case; fi

case_start "T-CL-28" "--convert-screenshots 批量转 JPG"
if ! command -v python3 >/dev/null; then
    skip_case "缺少 python3"
else
    mkdir -p "$ROOT/shots"
    python3 - "$ROOT/shots/a.png" <<'PY'
import struct,zlib,sys
w,h=16,16
raw=b"".join(b"\x00"+bytes([10,20,30])*w for _ in range(h))
def chunk(t,d):
    c=t+d
    return struct.pack(">I",len(d))+c+struct.pack(">I",zlib.crc32(c)&0xffffffff)
png=b"\x89PNG\r\n\x1a\n"+chunk(b"IHDR",struct.pack(">IIBBBBB",w,h,8,2,0,0,0))+chunk(b"IDAT",zlib.compress(raw))+chunk(b"IEND",b"")
open(sys.argv[1],"wb").write(png)
PY
    lw --convert-screenshots "$ROOT/shots" >"$out" 2>&1; rc=$?
    if expect_rc 0 "$rc" && expect_file "$ROOT/shots/a.jpg"; then pass_case; fi
fi

if [ "$HAVE_SOPHON" = "1" ]; then
    case_start "T-CL-29" "--install-plan 走假 Sophon 清单"
    sophon_env --install-plan hk4e_cn >"$out" 2>&1; rc=$?
    if expect_rc 0 "$rc" && expect_contains "$(text)" "Game.exe"; then pass_case; fi

    case_start "T-CL-30" "--patch-plan 无补丁时明确说明"
    sophon_env --patch-plan hk4e_cn --local 9.9.8 >"$out" 2>&1; rc=$?
    if expect_rc 0 "$rc" && expect_contains "$(text)" "补丁"; then pass_case; fi

    case_start "T-CL-31" "--install-fetch 下载单个文件到 target"
    rm -rf "$ROOT/fetch"
    sophon_env --install-fetch hk4e_cn Game.exe --target "$ROOT/fetch" --install-dir "$ROOT/game" >"$out" 2>&1; rc=$?
    if expect_rc 0 "$rc" && expect_file "$ROOT/fetch/Game.exe"; then pass_case; fi

    case_start "T-CL-32" "--update-run 无补丁时回落到块复用"
    rm -rf "$ROOT/upd"; mkdir -p "$ROOT/upd"
    printf 'A%.0s' $(seq 1 100) > "$ROOT/upd/Game.exe"
    sophon_env --update-run hk4e_cn --install-dir "$ROOT/upd" --local 9.9.8 --limit-mb 1 >"$out" 2>&1; rc=$?
    if expect_rc 0 "$rc" && expect_contains "$(text)" "复用"; then pass_case; fi
else
    case_start "T-CL-29" "--install-plan（缺少 python3/zstd）"; skip_case "缺少 python3 或 zstd"
    case_start "T-CL-30" "--patch-plan（缺少 python3/zstd）"; skip_case "缺少 python3 或 zstd"
    case_start "T-CL-31" "--install-fetch（缺少 python3/zstd）"; skip_case "缺少 python3 或 zstd"
    case_start "T-CL-32" "--update-run（缺少 python3/zstd）"; skip_case "缺少 python3 或 zstd"
fi

# —— RG-07 数据库备份不得自我嵌套（2026-11-14 修过：把整个 DatabaseBackup 目录打进 zip）——
if command -v unzip >/dev/null; then
    BK="$ROOT/backup"
    rm -rf "$BK"; mkdir -p "$BK"

    case_start "T-CL-33" "--database-backup 生成只含一个 .db 的归档"
    lw --database-backup "$BK/one.zip" >"$out" 2>&1; rc=$?
    entries="$(unzip -Z1 "$BK/one.zip" 2>/dev/null)"
    count="$(printf '%s\n' "$entries" | grep -c . || true)"
    if expect_rc 0 "$rc" && expect_file "$BK/one.zip" \
        && [ "$count" = "1" ] && printf '%s' "$entries" | grep -qE '^LunarwardDatabase_[0-9]{8}_[0-9]{6}\.db$'; then
        pass_case
    fi

    case_start "T-CL-34" "连续备份不把上一次的 .zip 套进新归档"
    lw --database-backup "$BK/two.zip" >"$out" 2>&1; rc=$?
    entries2="$(unzip -Z1 "$BK/two.zip" 2>/dev/null)"
    if expect_rc 0 "$rc" \
        && [ "$(printf '%s\n' "$entries2" | grep -c . || true)" = "1" ] \
        && ! printf '%s' "$entries2" | grep -qi '\.zip$' \
        && [ -z "$(find "$BK" -maxdepth 1 -name '.lwdb_*' -print -quit)" ]; then
        pass_case
    fi

    case_start "T-CL-35" "--database-backup 缺参数时报错并返回 1"
    lw --database-backup >"$out" 2>&1; rc=$?
    if expect_rc 1 "$rc" && expect_contains "$(text)" "用法"; then pass_case; fi
else
    case_start "T-CL-33" "--database-backup（缺少 unzip）"; skip_case "缺少 unzip"
    case_start "T-CL-34" "连续备份（缺少 unzip）"; skip_case "缺少 unzip"
    case_start "T-CL-35" "--database-backup 缺参数（缺少 unzip）"; skip_case "缺少 unzip"
fi

# ── 受管组件（D73 阶段 1）────────────────────────────────────────────
case_start "T-CL-36" "--component-scan 列出下载源"
lw --component-scan >"$out" 2>&1; rc=$?
if expect_rc 0 "$rc" && expect_contains "$(text)" "wine-staging" \
    && expect_contains "$(text)" "dxvk"; then pass_case; fi

case_start "T-CL-37" "--component-import 把归档导入为受管组件并写入 manifest"
IMP="$ROOT/imp"
rm -rf "$IMP"; mkdir -p "$IMP/wine-z-9.9/files/bin" "$IMP/wine-z-9.9/files/share"
printf '#!/bin/sh\necho "wine-z 9.9"\n' >"$IMP/wine-z-9.9/files/bin/wine"
chmod +x "$IMP/wine-z-9.9/files/bin/wine"
echo x >"$IMP/wine-z-9.9/files/share/x.dat"
tar -C "$IMP" -czf "$IMP/wine-z-9.9.tar.gz" wine-z-9.9
lw --component-import "$IMP/wine-z-9.9.tar.gz" --kind wine >"$out" 2>&1; rc=$?
if expect_rc 0 "$rc" && expect_contains "$(text)" "已导入" \
    && find "${LW_DATA:-$ROOT/data}/lunarward/components/wine" -name manifest.json -print -quit | grep -q .; then
    pass_case
fi

case_start "T-CL-38" "--component-health 对导入的组件给出原文与退出码"
lw --component-health wine-z >"$out" 2>&1; rc=$?
if expect_rc 0 "$rc" && expect_contains "$(text)" "wine-z 9.9" && expect_contains "$(text)" "exit: 0"; then pass_case; fi

case_start "T-CL-39" "--component-import 缺 --kind 时返回 1"
lw --component-import "$IMP/wine-z-9.9.tar.gz" >"$out" 2>&1; rc=$?
if expect_rc 1 "$rc" && expect_contains "$(text)" "--kind"; then pass_case; fi

# ── 运行环境（D73 阶段 2/3）：配方 → argv/env 预览 ─────────────────────
case_start "T-CL-40" "--launch-plan 打印配方/命令/env"
mkdir -p "$IMP"
printf 'MZ\0\0' >"$IMP/game.exe"
lw --launch-plan "$IMP/game.exe" >"$out" 2>&1; rc=$?
if expect_rc 0 "$rc" && expect_contains "$(text)" "配方" && expect_contains "$(text)" "命令" \
    && expect_contains "$(text)" "WINEPREFIX="; then pass_case; fi

case_start "T-CL-41" "--launch-plan --profile <不存在> 时明确说找不到配方"
lw --launch-plan "$IMP/game.exe" --profile 不存在的配方 >"$out" 2>&1; rc=$?
if expect_rc 0 "$rc" && expect_contains "$(text)" "找不到"; then pass_case; fi

# ── 受管组件下载入口（P1）：列版本的前置校验 ──────────────────────
case_start "T-CL-42" "--component-list 缺参数时返回 1 并给用法"
lw --component-list >"$out" 2>&1; rc=$?
if expect_rc 1 "$rc" && expect_contains "$(text)" "--component-list <源 id>"; then pass_case; fi

case_start "T-CL-43" "--component-list 未知源时返回 1"
lw --component-list no-such-source >"$out" 2>&1; rc=$?
if expect_rc 1 "$rc" && expect_contains "$(text)" "没有这个源"; then pass_case; fi

summary
