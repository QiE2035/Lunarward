#!/usr/bin/env bash
# 性能基线（M-T4）：采集 3 个可脚本化的指标，和基线比，超过阈值就失败。
#
#   bash scripts/test/perf.sh                 # 采集并与基线比较（回归 → 退出码 1）
#   bash scripts/test/perf.sh --update        # 采集成新基线
#
# 指标：
#   cli_median_ms   `--version` 冷启动中位数（进程启动 + 依赖加载，不含 UI）
#   selftest_ms     `--selftest-ui` 从启动到自检汇总的耗时（含创建窗口、导航两个页面）
#   peak_rss_kb     `--selftest-ui` 进程峰值 RSS
#
# 为什么是这三个：它们完全确定性、不需要游戏与网络，能在 CI 里跑；
# 真机观感类的指标（切页 150ms、视频 CPU）需要人工操作，见 19-test-plan.md 的 L3 清单。
set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
: "${LW_BIN:?需要 LW_BIN=/path/to/Lunarward}"
BIN="$LW_BIN"
BASELINE="${PERF_BASELINE:-$HERE/../../docs/linux-port/perf-baseline.json}"
THRESHOLD="${PERF_THRESHOLD:-20}"   # 允许变差 20%

UPDATE=0
[ "${1:-}" = "--update" ] && UPDATE=1

ROOT="$(mktemp -d /tmp/lw-perf-XXXXXX)"
trap 'rm -rf "$ROOT"' EXIT
mkdir -p "$ROOT/cfg" "$ROOT/data" "$ROOT/cache" "$ROOT/state" "$ROOT/home"

perf_env() {
    env -u DISPLAY -u WAYLAND_DISPLAY \
        XDG_CONFIG_HOME="$ROOT/cfg" \
        XDG_DATA_HOME="$ROOT/data" \
        XDG_CACHE_HOME="$ROOT/cache" \
        XDG_STATE_HOME="$ROOT/state" \
        HOME="$ROOT/home" \
        "$@"
}

median() { sort -n | awk '{a[NR]=$1} END{print (NR%2)?a[(NR+1)/2]:int((a[NR/2]+a[NR/2+1])/2)}'; }

echo "采集 CLI 冷启动（5 次）…"
times=""
for _ in 1 2 3 4 5; do
    start=$(date +%s%N)
    perf_env "$BIN" --version >/dev/null 2>&1
    end=$(date +%s%N)
    times="$times$(( (end - start) / 1000000 ))\n"
done
cli_ms=$(printf "$times" | median)

# UI 运行器：优先 xvfb（CI/无头一致），本机没有 xvfb 但开着 DISPLAY 时直接跑真实显示
HAVE_XVFB=0
command -v xvfb-run >/dev/null && HAVE_XVFB=1
HAVE_DISPLAY=0
[ -n "${DISPLAY:-}" ] && HAVE_DISPLAY=1

ui_run() {   # ui_run <logfile> <command...>
    local log="$1"; shift
    if [ "$HAVE_XVFB" = "1" ]; then
        xvfb-run -a -s "-screen 0 1280x800x24" "$@" >"$log" 2>&1
    else
        "$@" >"$log" 2>&1
    fi
}

if [ "$HAVE_XVFB" = "1" ] || [ "$HAVE_DISPLAY" = "1" ]; then
    echo "采集 UI 自检耗时与峰值 RSS…"
    if [ -x /usr/bin/time ]; then
        /usr/bin/time -v -o "$ROOT/self.time" \
            env XDG_CONFIG_HOME="$ROOT/cfg" XDG_DATA_HOME="$ROOT/data" \
                XDG_CACHE_HOME="$ROOT/cache" XDG_STATE_HOME="$ROOT/state" HOME="$ROOT/home" \
                "$BIN" --selftest-ui >"$ROOT/self.log" 2>&1
    else
        # Arch 默认不装 `time` 包：没有 /usr/bin/time 就只测耗时，RSS 记 n/a
        env XDG_CONFIG_HOME="$ROOT/cfg" XDG_DATA_HOME="$ROOT/data" \
            XDG_CACHE_HOME="$ROOT/cache" XDG_STATE_HOME="$ROOT/state" HOME="$ROOT/home" \
            "$BIN" --selftest-ui >"$ROOT/self.log" 2>&1
        : >"$ROOT/self.time"
    fi
    selftest_ms=$(sed -n 's/.*自检完成.*耗时 \([0-9]*\) *ms.*/\1/p' "$ROOT/self.log" | tail -1)
    [ -z "${selftest_ms:-}" ] && selftest_ms=$(grep -oE '耗时 [0-9]+' "$ROOT/self.log" | tail -1 | grep -oE '[0-9]+')
    peak_rss=$(awk '/Maximum resident set size/{print $NF}' "$ROOT/self.time" 2>/dev/null)
    # 冷启动卡顿（RG-09）：LUNARWARD_UI_WATCHDOG=1 跑 9 秒再收，取日志里第一次 UI 卡顿毫秒数。
    # 这条钉的是「窗口激活 + 首帧布局光栅」那段一次性成本，回退到 4 秒级就是 D72 那种灾难复现。
    echo "采集冷启动卡顿（UI 看门狗）…"
    mkdir -p "$ROOT/data/lunarward/log"
    env $( [ "$HAVE_XVFB" = "1" ] && echo "" ) \
        LUNARWARD_UI_WATCHDOG=1 \
        XDG_CONFIG_HOME="$ROOT/cfg" XDG_DATA_HOME="$ROOT/data" \
        XDG_CACHE_HOME="$ROOT/cache" XDG_STATE_HOME="$ROOT/state" HOME="$ROOT/home" \
        timeout 9 "$BIN" --hide >"$ROOT/start.log" 2>&1
    startup_stall=$(grep -hoE 'UI 线程卡顿 [0-9]+ ms' "$ROOT"/data/lunarward/log/*.log 2>/dev/null | head -1 | grep -oE '[0-9]+')
    [ -z "${startup_stall:-}" ] && startup_stall=0
else
    echo "既没有 xvfb-run 也没有 DISPLAY：跳过 selftest_ms / peak_rss_kb / startup_stall_ms"
    selftest_ms=""
    peak_rss=""
    startup_stall=""
fi

now_cli="$cli_ms"
[ -n "${selftest_ms:-}" ] && now_selftest="$selftest_ms" || now_selftest=""
[ -n "${peak_rss:-}" ] && now_rss="$peak_rss" || now_rss=""
[ -n "${startup_stall:-}" ] && now_startup="$startup_stall" || now_startup=""

echo "结果：cli_median_ms=$now_cli selftest_ms=${now_selftest:-n/a} peak_rss_kb=${now_rss:-n/a} startup_stall_ms=${now_startup:-n/a}"

if [ "$UPDATE" = "1" ]; then
    mkdir -p "$(dirname "$BASELINE")"
    python3 - "$BASELINE" "$(uname -sr)" "$now_cli" "${now_selftest:-}" "${now_rss:-}" "${now_startup:-}" <<'PY'
import json, os, sys
path, host, cli, selftest, rss, startup = sys.argv[1:7]
data = {"envs": {}}
if os.path.exists(path):
    try:
        data = json.load(open(path))
    except Exception:
        data = {"envs": {}}
data.setdefault("envs", {})
data["envs"][host] = {
    "measured_at": __import__("datetime").datetime.now().astimezone().isoformat(timespec="seconds"),
    "cli_median_ms": int(cli),
    "selftest_ms": int(selftest) if selftest else None,
    "peak_rss_kb": int(rss) if rss else None,
    "startup_stall_ms": int(startup) if startup else None,
}
json.dump(data, open(path, "w"), ensure_ascii=False, indent=2)
print(f"已更新 {host} 的基线")
PY
    echo "基线文件：$BASELINE"
    exit 0
fi

if [ ! -f "$BASELINE" ]; then
    echo "没有基线文件（$BASELINE），先跑 --update"
    exit 0
fi

python3 - "$BASELINE" "$now_cli" "$now_selftest" "$now_rss" "$THRESHOLD" "$(uname -sr)" "${now_startup:-}" <<'PY'
import json, sys
baseline_path, cli, selftest, rss, threshold, host, startup = sys.argv[1:8]
data = json.load(open(baseline_path))
base = data.get("envs", {}).get(host)
if base is None and "cli_median_ms" in data:
    # 兼容 2026-11-14 之前的扁平基线（当时直接写在根上，且比较逻辑恒为「只记录」）
    base = {k: v for k, v in data.items() if isinstance(v, (int, float))}
    print("（使用扁平基线：%s）" % ", ".join(f"{k}={v}" for k, v in base.items()))
if base is None:
    print(f"基线里没有 {host} 的记录（只有 {', '.join(data.get('envs', {})) or '空'}）：本次只记录，不判定")
    print(f"  cli_median_ms={cli or 'n/a'} selftest_ms={selftest or 'n/a'} peak_rss_kb={rss or 'n/a'}")
    sys.exit(0)
threshold = float(threshold)
failed = []

def check(name, now, old):
    if now in ("", "None"):
        return
    if old is None:
        return
    now = float(now)
    if old <= 0:
        return
    delta = (now - old) / old * 100
    flag = "OK"
    if delta > threshold:
        flag = "REGRESSION"
        failed.append(f"{name}: {old} → {now:g} (+{delta:.0f}%)")
    print(f"  {name:16s} {old:>10} → {now:>10g}  {delta:+6.0f}%  {flag}")

print("与基线比较（阈值 +%.0f%%）：" % threshold)
check("cli_median_ms", cli, base.get("cli_median_ms"))
check("selftest_ms", selftest, base.get("selftest_ms"))
check("peak_rss_kb", rss, base.get("peak_rss_kb"))

ABSOLUTE = {"startup_stall_ms": 1500}
for name, limit in ABSOLUTE.items():
    value = {"startup_stall_ms": startup}.get(name, "")
    if value in ("", "None", None):
        continue
    value = float(value)
    if value > limit:
        failed.append(f"{name}: {value:g} > 绝对上限 {limit}")
        print(f"  {name:16s} {value:>10g}  绝对上限 {limit}  REGRESSION")
    else:
        print(f"  {name:16s} {value:>10g}  绝对上限 {limit}  OK")

if failed:
    print("回归：")
    for f in failed:
        print("  - " + f)
    sys.exit(1)
print("无回归")
PY
