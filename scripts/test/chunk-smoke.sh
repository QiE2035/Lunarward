#!/usr/bin/env bash
# L2 安装链路离线用例（T-IN-*）：本地假 Sophon 服务 + zstd 分块。
# 不访问任何真实米哈游接口：LUNARWARD_SOPHON_BASE 指向 127.0.0.1，
# LUNARWARD_SOPHON_DIRECT=1 让 CLI 跳过 HoYoPlay 的 branch 查询。
#
# 需要：python3、zstd。
# 用法：LW_BIN=/path/to/Lunarward bash scripts/test/chunk-smoke.sh
set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
: "${LW_BIN:?需要 LW_BIN=/path/to/Lunarward}"
BIN="$LW_BIN"
ROOT="${LW_ROOT:-$(mktemp -d /tmp/lw-l2-chunk-XXXXXX)}"
mkdir -p "$ROOT/cfg" "$ROOT/data" "$ROOT/cache" "$ROOT/state" "$ROOT/home" "$ROOT/bin"
export ROOT BIN

# shellcheck source=./lib.sh
source "$HERE/lib.sh"

if ! command -v python3 >/dev/null; then
    echo "SKIP: 缺少 python3"
    exit 0
fi
if ! command -v zstd >/dev/null; then
    echo "SKIP: 缺少 zstd（Arch: pacman -S zstd / CI 已自带）"
    exit 0
fi

PORT="${LW_SOPHON_PORT:-18321}"
BASE="http://127.0.0.1:$PORT"
TONA_DIR="$ROOT/sophon"
mkdir -p "$TONA_DIR"
python3 "$HERE/fake-sophon.py" --port "$PORT" >"$TONA_DIR/server.log" 2>&1 &
SERVER_PID=$!
cleanup() {
    kill "$SERVER_PID" 2>/dev/null
    wait "$SERVER_PID" 2>/dev/null
}
trap cleanup EXIT

for _ in $(seq 1 50); do
    if curl -fsS --max-time 1 "$BASE/health" >/dev/null 2>&1; then
        break
    fi
    sleep 0.1
done
if ! curl -fsS --max-time 2 "$BASE/health" >/dev/null 2>&1; then
    echo "无法启动假 Sophon 服务，见 $TONA_DIR/server.log"
    exit 1
fi

# 安装链路相关环境（每个用例都用）
sophon_env() {
    env \
        XDG_CONFIG_HOME="$ROOT/cfg" \
        XDG_DATA_HOME="$ROOT/data" \
        XDG_CACHE_HOME="$ROOT/cache" \
        XDG_STATE_HOME="$ROOT/state" \
        HOME="$ROOT/home" \
        PATH="$ROOT/bin:$PATH" \
        LUNARWARD_SOPHON_BASE="$BASE" \
        LUNARWARD_SOPHON_DIRECT=1 \
        "$@"
}

sophon() { sophon_env "$BIN" "$@"; }

V1="$ROOT/v1"
V2="$ROOT/v2"
mkdir -p "$V1/Data"
python3 - "$V1" <<'PY'
import pathlib, sys
root = pathlib.Path(sys.argv[1])
(root / "Game.exe").write_bytes(b"A" * 100)
(root / "Data/a.bin").write_bytes(bytes(range(256)))
(root / "Data/old.bin").write_bytes(b"C" * 128)
PY

mtimes_before="$(find "$V1" -type f -printf '%p %s %T@\n' | sort)"

# ---------------------------------------------------------------- T-IN-20
case_start "T-IN-20" "--install-chunk-plan 报告可复用块与需下载量"
out="$CASE_LOG_DIR/T-IN-20.out"
sophon --install-chunk-plan hk4e_cn --install-dir "$V1" --local 9.9.8 >"$out" 2>&1
if expect_rc 0 $? && grep -q "可复用 2 块" "$out" && grep -q "需下载 22 B" "$out"; then
    pass_case
else
    fail_case "输出未同时包含「可复用 2 块」与「需下载 22 B」"
fi

# ---------------------------------------------------------------- T-IN-21
case_start "T-IN-21" "--install-chunk-run 只下载新增块，未变更文件链接进 target"
out="$CASE_LOG_DIR/T-IN-21.out"
sophon --install-chunk-run hk4e_cn --install-dir "$V1" --local 9.9.8 --target "$V2" >"$out" 2>&1
rc=$?
if expect_rc 0 "$rc" \
    && grep -q "链接 2 个未变更文件" "$out" \
    && grep -q "失败 0" "$out" \
    && grep -q "Data/new.bin" <<<"$(find "$V2" -type f -o -type l)"; then
    pass_case
else
    fail_case "未按预期链接 2 个未变更文件或未产出 new.bin"
fi

# ---------------------------------------------------------------- T-IN-22
case_start "T-IN-22" "target 树完整且三个文件内容正确"
sizes="$(cd "$V2" && for p in Game.exe Data/a.bin Data/new.bin; do wc -c <"$p" 2>/dev/null || echo missing; done | tr '\n' ' ')"
if [ "$sizes" = "100 256 300 " ]; then
    pass_case
else
    fail_case "文件尺寸不符：$sizes"
fi

# ---------------------------------------------------------------- T-IN-23
case_start "T-IN-23" "重复运行不重复下载（幂等）"
out="$CASE_LOG_DIR/T-IN-23.out"
sophon --install-chunk-run hk4e_cn --install-dir "$V2" --local 9.9.9 --target "$V2" >"$out" 2>&1
if expect_rc 0 $? && grep -q "待处理 0 个文件" "$out"; then
    pass_case
else
    fail_case "第二次运行仍处理了文件（应为 0）"
fi

# ---------------------------------------------------------------- T-IN-24
case_start "T-IN-24" "本地缺文件时会重新下载（清单说没变也不跳过）"
rm -f "$V2/Game.exe"
out="$CASE_LOG_DIR/T-IN-24.out"
sophon --install-chunk-run hk4e_cn --install-dir "$V2" --local 9.9.9 --target "$V2" >"$out" 2>&1
if expect_rc 0 $? && grep -q "待处理 1 个文件" "$out" && [ -f "$V2/Game.exe" ]; then
    pass_case
else
    fail_case "缺失文件没有被重新拉取"
fi

# ---------------------------------------------------------------- T-IN-25
case_start "T-IN-25" "测试不修改源安装目录（只读复用）"
mtimes_after="$(find "$V1" -type f -printf '%p %s %T@\n' | sort)"
if [ "$mtimes_before" = "$mtimes_after" ]; then
    pass_case
else
    fail_case "源目录被改写"
fi

summary
