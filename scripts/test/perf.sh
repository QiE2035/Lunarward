#!/usr/bin/env bash
# Lunarward 性能基线（L2 性能子集，对应测试方案 §性能预算）。
#
# 思路：挑几个「无网络、可重复」的入口，量它们的墙钟耗时与峰值内存；与基线 JSON 比较，
# 超过阈值（默认 20%）判回归。它抓不到渲染帧率那类指标（那要靠 L3/L4 在人操作时用 cu_* + 截图），
# 但足以拦住「组件扫描变慢」「启动路径退化」「改崩了内存」这类常见问题。
#
# 用法：
#   bash scripts/test/perf.sh                # 比对基线，超阈值退出 1
#   bash scripts/test/perf.sh --record       # 记录当前值为新基线
#   LW_BIN=/path/to/Lunarward bash scripts/test/perf.sh
set -uo pipefail

HERE=$(cd "$(dirname "$0")" && pwd)
ROOT=$HERE/../..
LW_BIN=${LW_BIN:-$ROOT/src/Lunarward/bin/Release/net10.0-desktop/Lunarward}
BASELINE=${PERF_BASELINE:-$HERE/perf-baseline.json}
THRESHOLD_PCT=${PERF_THRESHOLD_PCT:-20}
RECORD=0
[ "${1:-}" = "--record" ] && RECORD=1

if [ ! -x "$LW_BIN" ]; then
    echo "找不到可执行文件：$LW_BIN（先 dotnet build -c Release）" >&2
    exit 2
fi

# 隔离环境：不碰真实配置/游戏目录/缓存
WORK=$(mktemp -d /tmp/lw-perf-XXXXXX)
export XDG_CONFIG_HOME=$WORK/cfg
export XDG_DATA_HOME=$WORK/data
export XDG_CACHE_HOME=$WORK/cache
export XDG_STATE_HOME=$WORK/state
export HOME=$WORK/home
mkdir -p "$XDG_CONFIG_HOME" "$XDG_DATA_HOME" "$XDG_CACHE_HOME" "$XDG_STATE_HOME" "$HOME"

now_ms() { date +%s%3N; }

# $1 = 标签；其余 = 命令。结果写进 results 关联数组（bash 4+）
declare -A RESULTS

measure() {
    local label=$1; shift
    local s e
    s=$(now_ms)
    "$@" >/dev/null 2>&1
    e=$(now_ms)
    RESULTS["$label"]=$((e - s))
    printf '%-28s %6d ms\n' "$label" "${RESULTS[$label]}"
}

echo "== 冷路径（隔离环境）=="
measure "list-runners"  "$LW_BIN" --list-runners
measure "list-components" "$LW_BIN" --list-components
measure "preflight-missing" "$LW_BIN" --preflight hk4e_cn || true

echo "== 热路径（第二次，进程已起过、页缓存已建）=="
measure "list-runners-hot" "$LW_BIN" --list-runners

# 启动 + UI 自检：包含 Uno 起来、首屏渲染、8 条断言，是「启动退化」最直接的代理指标
if command -v xvfb-run >/dev/null 2>&1; then
    echo "== 启动 + L1.5 自检（xvfb）=="
    local_start=$(now_ms)
    xvfb-run -a -s "-screen 0 1280x800x24" "$LW_BIN" --selftest-ui >"$WORK/selftest.log" 2>&1
    local_end=$(now_ms)
    RESULTS["startup-selftest"]=$((local_end - local_start))
    printf '%-28s %6d ms\n' "startup-selftest" "${RESULTS[startup-selftest]}"
    if ! grep -q "失败 0$" "$WORK/selftest.log"; then
        echo "自检未全通过，日志：$WORK/selftest.log" >&2
        FAILED=1
    fi

    # 峰值 RSS（KB）
    peak_rss=$(/usr/bin/time -f '%M' xvfb-run -a -s "-screen 0 1280x800x24" "$LW_BIN" --selftest-ui 2>&1 >/dev/null | tail -1)
    if [[ "$peak_rss" =~ ^[0-9]+$ ]]; then
        RESULTS["peak-rss-kb"]=$peak_rss
        printf '%-28s %6d KB\n' "peak-rss-kb" "$peak_rss"
    fi
else
    echo "（没有 xvfb-run，跳过启动指标）"
fi

json=$(
    printf '{'
    first=1
    for k in "${!RESULTS[@]}"; do
        [ $first -eq 1 ] || printf ','
        first=0
        printf '"%s":%d' "$k" "${RESULTS[$k]}"
    done
    printf '}'
)

if [ "$RECORD" = "1" ]; then
    printf '%s\n' "$json" > "$BASELINE"
    echo "已写入基线：$BASELINE"
    echo "$json"
    exit 0
fi

if [ ! -f "$BASELINE" ]; then
    echo "还没有基线（先跑一次 --record）：$BASELINE" >&2
    exit 2
fi

echo "== 与基线比较（阈值 ${THRESHOLD_PCT}%）=="
regressions=0
for k in "${!RESULTS[@]}"; do
    base=$(python3 - "$BASELINE" "$k" <<'PY'
import json,sys
data=json.load(open(sys.argv[1]))
print(data.get(sys.argv[2], ""))
PY
)
    cur=${RESULTS[$k]}
    if [ -z "$base" ] || [ "$base" -eq 0 ]; then
        printf '%-28s base=-(新指标) cur=%d ms\n' "$k" "$cur"
        continue
    fi
    pct=$(python3 -c "print(round(($cur-$base)*100.0/$base,1))")
    status=ok
    if python3 -c "import sys;sys.exit(0 if $pct > $THRESHOLD_PCT else 1)"; then
        status=REGRESSION
        regressions=$((regressions+1))
    fi
    printf '%-28s base=%6d cur=%6d  %+6s%%  %s\n' "$k" "$base" "$cur" "$pct" "$status"
done

rm -rf "$WORK"
if [ "$regressions" -gt 0 ]; then
    echo "有 $regressions 项超过阈值" >&2
    exit 1
fi
echo "全部在预算内"
