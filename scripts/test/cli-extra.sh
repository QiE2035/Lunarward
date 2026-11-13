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

summary
