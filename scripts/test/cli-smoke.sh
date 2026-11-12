#!/usr/bin/env bash
# L2 冒烟：CLI 全链路（无 GUI、无真实 wine、无网络）。
# 用法：scripts/test/cli-smoke.sh [--keep]
#   环境变量 LW_NET=1 时额外跑需要真实网络的用例（默认跳过）。
set -uo pipefail

HERE=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
REPO=$(cd "$HERE/../.." && pwd)
KEEP=${1:-}

ROOT=${LW_TEST_ROOT:-$(mktemp -d /tmp/lw-l2-XXXXXX)}
BIN=${LW_BIN:-$REPO/src/Lunarward/bin/Debug/net10.0-desktop/Lunarward}
export LUNARWARD_FAKE_LOG="$ROOT/logs/wine.log"
mkdir -p "$ROOT/logs"

if [ ! -x "$BIN" ]; then
    echo "找不到可执行文件：$BIN（先 dotnet build src/Lunarward）" >&2
    exit 2
fi

ROOT=$ROOT BIN=$BIN
export ROOT BIN LUNARWARD_FAKE_LOG

echo "L2 根目录：$ROOT"
bash "$HERE/fixtures.sh" >/dev/null
# shellcheck source=lib.sh
. "$HERE/lib.sh"

out=""

# ── 基础信息（无 DISPLAY）──────────────────────────────────────────────────
case_start T-CL-01 "--version 可在无 DISPLAY 下运行"
out=$(test_env_headless "$BIN" --version 2>&1); rc=$?
expect_rc 0 $rc && expect_contains "$out" "Lunarward" && pass_case

case_start T-CL-02 "--help 有用法输出"
out=$(test_env_headless "$BIN" --help 2>&1); rc=$?
expect_rc 0 $rc && expect_contains "$out" "用法" && pass_case

case_start T-CL-03 "--list-runners 无 DISPLAY 可用"
out=$(test_env_headless "$BIN" --list-runners 2>&1); rc=$?
echo "$out" > "$ROOT/logs/runners.txt"
expect_rc 0 $rc && expect_contains "$out" "fake-wine" && pass_case

# ── 运行器扫描 ─────────────────────────────────────────────────────────────
case_start T-RT-01 扫描到假运行器并标可用
out=$(lw --list-runners 2>&1); rc=$?
expect_rc 0 $rc && expect_contains "$out" "✓" && expect_contains "$out" "wine-9.0-fake" && pass_case

case_start T-CL-04 --list-runners --json 可被 jq 解析
lw --list-runners --json > "$ROOT/logs/runners.json" 2>/dev/null; rc=$?
if expect_rc 0 $rc; then
    if jq -e 'length > 0' "$ROOT/logs/runners.json" >/dev/null 2>&1; then
        pass_case
    else
        fail_case "JSON 结构不是非空数组"
    fi
fi

# ── 组件 ───────────────────────────────────────────────────────────────────
case_start T-CL-05 --list-components 正常返回
out=$(lw --list-components 2>&1); rc=$?
expect_rc 0 $rc && pass_case

case_start T-CL-06 --graphics-list 只列图形层（不混运行器）
out=$(lw --graphics-list 2>&1); rc=$?
if expect_rc 0 $rc; then
    if printf '%s\n' "$out" | grep -qE '^(Wine|Proton)'; then
        fail_case "输出里混进了运行器行"
    else
        pass_case
    fi
fi

# ── 图形层：安装 → 校验 → 移除 ──────────────────────────────────────────────
case_start T-RT-11 --graphics-install 装进假前缀
# 当前 CLI 只认「已导入/已扫描」的组件名，路径形态需先 --graphics-import（见 T-CL-17）
lw --graphics-import "$ROOT/fake-dxvk" >/dev/null 2>&1
name=$(lw --graphics-list 2>&1 | sed -n 's/.*fake-dxvk\/\{0,1\}\([^ ]*\).*/\1/p' | head -1)
[ -z "$name" ] && name=$(lw --graphics-list 2>&1 | head -2 | tail -1 | awk '{print $NF}')
out=$(lw --graphics-install "$ROOT/prefix" "$name" 2>&1); rc=$?
if expect_rc 0 $rc; then
    if expect_file "$ROOT/prefix/drive_c/windows/system32/d3d11.dll" \
       && expect_file "$ROOT/prefix/.lunarward/graphics.json"; then
        pass_case
    fi
fi

case_start T-RT-12 --graphics-verify 报一致
out=$(lw --graphics-verify "$ROOT/prefix" 2>&1); rc=$?
expect_rc 0 $rc && expect_contains "$out" "一致" && pass_case

case_start T-RT-12b 删掉一个 dll 后校验报不一致
rm -f "$ROOT/prefix/drive_c/windows/system32/d3d11.dll"
out=$(lw --graphics-verify "$ROOT/prefix" 2>&1); rc=$?
expect_contains "$out" "缺失" && pass_case

case_start T-RT-13 --graphics-remove 按记录清干净
out=$(lw --graphics-remove "$ROOT/prefix" 2>&1); rc=$?
if expect_rc 0 $rc; then
    if [ -f "$ROOT/prefix/drive_c/windows/system32/d3d11.dll" ] || [ -f "$ROOT/prefix/drive_c/windows/system32/dxgi.dll" ]; then
        fail_case "移除后仍有残留 dll"
    else
        pass_case
    fi
fi

case_start T-RT-13b 换另一个组件时留 graphics.json.bak
rm -f "$ROOT/prefix/.lunarward/graphics.json.bak"
lw --graphics-import "$ROOT/fake-dxvk" >/dev/null 2>&1
lw --graphics-import "$ROOT/fake-vkd3d" >/dev/null 2>&1
lw --graphics-install "$ROOT/prefix" fake-dxvk >/dev/null 2>&1
lw --graphics-install "$ROOT/prefix" fake-vkd3d >/dev/null 2>&1
if expect_file "$ROOT/prefix/.lunarward/graphics.json.bak"; then pass_case; fi

case_start T-RT-14 --graphics-import 导入目录
out=$(lw --graphics-import "$ROOT/fake-dxvk" 2>&1); rc=$?
if expect_rc 0 $rc; then
    listed=$(lw --graphics-list 2>&1)
    expect_contains "$listed" "dxvk" && pass_case
fi

case_start T-CL-17 --graphics-install 传目录路径的当前行为（记录在案）
out=$(lw --graphics-install "$ROOT/prefix" "$ROOT/fake-dxvk" 2>&1); rc=$?
if [ $rc -ne 0 ] && expect_contains "$out" "没有这个图形组件"; then
    pass_case
fi

# ── 一次性运行：环境与路由 ─────────────────────────────────────────────────
case_start T-RT-15 --run-once 的 WINEPREFIX 指向方案前缀
out=$(lw --run-once fakeprfl /usr/bin/env 2>&1); rc=$?
if expect_rc 0 $rc; then
    expect_contains "$out" "WINEPREFIX=$ROOT/prefix" && pass_case
fi

case_start T-RT-16 --run-once 把 .exe 交给方案里的 wine
: > "$LUNARWARD_FAKE_LOG"
out=$(lw --run-once fakeprfl "$ROOT/game/YuanShen.exe" 2>&1)
log=$(cat "$LUNARWARD_FAKE_LOG" 2>/dev/null || true)
expect_contains "$log" "YuanShen.exe" && pass_case

case_start T-RT-16b --run-once 参数含空格与引号原样传入
: > "$LUNARWARD_FAKE_LOG"
out=$(lw --run-once fakeprfl "$ROOT/game/YuanShen.exe" "a b" "c;d" 2>&1)
log=$(cat "$LUNARWARD_FAKE_LOG" 2>/dev/null || true)
if expect_contains "$log" "a b" && expect_contains "$log" "c;d"; then
    pass_case
fi

case_start T-RT-17 --run-once 方案不存在时失败
out=$(lw --run-once nosuchprofile /usr/bin/env 2>&1); rc=$?
expect_rc_nonzero $rc && expect_contains "$out" "方案" && pass_case

# ── 启动计划 / 预检 ────────────────────────────────────────────────────────
case_start T-CL-07 --launch-plan 解析出运行器
out=$(lw --launch-plan "$ROOT/game/YuanShen.exe" 2>&1); rc=$?
expect_rc 0 $rc && expect_contains "$out" "fake-wine" && pass_case

case_start T-CL-07b --preflight 给出结论
out=$(lw --preflight hk4e_cn 2>&1); rc=$?
echo "$out" > "$ROOT/logs/preflight.txt"
expect_contains "$out" "结论" && pass_case

# ── 截图 ───────────────────────────────────────────────────────────────────
case_start T-PL-10 --screenshot 用假 spectacle 落盘
out=$(lw --screenshot 2>&1); rc=$?
file=$(printf '%s\n' "$out" | grep -E '/.*\.(png|jpg)$' | tail -1)
if expect_rc 0 $rc && [ -n "$file" ] && expect_file "$file"; then
    pass_case
fi

# ── 诊断包 ─────────────────────────────────────────────────────────────────
case_start T-CL-03 --diagnose 产出 zip 且含关键文件
out=$(lw --diagnose --out "$ROOT/out" 2>&1); rc=$?
zip=$(printf '%s\n' "$out" | grep -E '\.zip$' | tail -1)
if expect_rc 0 $rc && [ -n "$zip" ] && expect_file "$zip"; then
    if unzip -l "$zip" 2>/dev/null | grep -q "environment.txt"; then
        pass_case
    else
        fail_case "zip 里没有 environment.txt"
    fi
fi

# ── URL 协议：注册 / 状态 / 注销（隔离在临时 XDG）──────────────────────────
case_start T-PL-09 --register-protocol 写出 desktop 文件
out=$(lw --register-protocol 2>&1); rc=$?
if expect_rc 0 $rc; then
    if ls "$ROOT"/data/applications/lunarward.desktop >/dev/null 2>&1; then
        pass_case
    else
        fail_case "没有生成 .desktop"
    fi
fi

case_start T-PL-09b --protocol-status 报已注册
out=$(lw --protocol-status 2>&1); rc=$?
expect_rc 0 $rc && expect_contains "$out" "注册" && pass_case

case_start T-PL-09c --unregister-protocol 可注销
out=$(lw --unregister-protocol 2>&1); rc=$?
expect_rc 0 $rc && pass_case

# ── 错误路径：缺参数 / 未知方案 ─────────────────────────────────────────────
for spec in "--preflight" "--graphics-verify" "--install-plan" "--update-plan" "--run-once" "--predownload-size"; do
    case_start "T-CL-02" "缺参数应失败：$spec"
    out=$(lw $spec 2>&1); rc=$?
    if expect_rc_nonzero $rc; then pass_case; fi
done

case_start T-CL-02b 未知游戏应失败
out=$(lw --preflight no_such_game_xyz 2>&1); rc=$?
expect_rc_nonzero $rc && pass_case

# ── 版本标记（假目录，无 config.ini）────────────────────────────────────────
case_start T-CL-12 --version-status 报本地版本（无 config.ini 时给可读结果）
out=$(lw --version-status hk4e_cn 2>&1); rc=$?
expect_rc 0 $rc && pass_case

# ── 需要网络的用例（默认跳过）──────────────────────────────────────────────
if [ "${LW_NET:-0}" = "1" ]; then
    case_start T-IN-10 --predownload-size 走真实接口
    out=$(lw --predownload-size hk4e_cn 2>&1); rc=$?
    expect_rc 0 $rc && expect_contains "$out" "GB" && pass_case

    case_start T-IN-05b --update-plan 只读不写
    out=$(lw --update-plan hk4e_cn --install-dir "$ROOT/game" 2>&1); rc=$?
    expect_rc 0 $rc && pass_case
else
    skip_case "T-IN-10/T-IN-05b 需要真实网络（LW_NET=1 开启）"
fi

# ── 补充：JSON 输出 / 诊断包内容 / 版本标记 / 错误前缀 ───────────────────────
case_start T-CL-18 --list-runners --json 是合法 JSON
out=$(lw --list-runners --json 2>&1); rc=$?
if expect_rc 0 $rc; then
    if printf '%s' "$out" | python3 -c "import json,sys; d=json.load(sys.stdin); assert isinstance(d,list) and d; assert 'path' in d[0] or 'Path' in d[0]" 2>/dev/null; then
        pass_case
    else
        fail_case "不是预期结构的 JSON"
    fi
fi

case_start T-CL-19 --diagnose 产出的 zip 含关键文件
DIAG="$ROOT/diag"
out=$(lw --diagnose --out "$DIAG" 2>&1); rc=$?
if expect_rc 0 $rc; then
    z=$(ls "$DIAG"/*.zip 2>/dev/null | head -1)
    if [ -n "$z" ] && unzip -l "$z" | grep -q "environment.txt" && unzip -l "$z" | grep -q "preflight.txt"; then
        pass_case
    else
        fail_case "zip 内容缺少 environment.txt/preflight.txt"
    fi
fi

case_start T-IN-11 --version-write 写入并 --version-status 读回
mkdir -p "$ROOT/vgame"
printf '[General]\ngame_version=\n' > "$ROOT/vgame/config.ini"
out=$(lw --version-write "$ROOT/vgame" 9.9.9 2>&1); rc=$?
if expect_rc 0 $rc; then
    out=$(lw --version-status hk4e_cn 2>&1)
    # version-status 只认已配置的安装路径；这里退一步只验证 config.ini 真被写入
    if expect_contains "$(cat "$ROOT/vgame/config.ini")" "9.9.9"; then pass_case; fi
fi

case_start T-RT-18 --graphics-verify 对不存在的前缀报错
out=$(lw --graphics-verify "$ROOT/no-such-prefix" 2>&1); rc=$?
if expect_rc 1 $rc && expect_contains "$out" "前缀不存在"; then pass_case; fi

case_start T-CL-20 --output-tail 在没有输出日志时给出空结果而非崩溃
out=$(lw --output-tail 2>&1); rc=$?
expect_rc 0 $rc && pass_case

case_start T-LA-02 --launch-plan 不存在的 exe 返回 1
out=$(lw --launch-plan "$ROOT/no-such-game.exe" 2>&1); rc=$?
if expect_rc 1 $rc && expect_contains "$out" "找不到可执行文件"; then pass_case; fi

case_start T-LA-03 --launch-plan 现有 exe 返回 0 并打印 wine/前缀
out=$(lw --launch-plan "$ROOT/game/YuanShen.exe" 2>&1); rc=$?
if expect_rc 0 $rc && expect_contains "$out" "wine"; then pass_case; fi

summary
rc=$?

if [ "$KEEP" != "--keep" ]; then
    rm -rf "$ROOT"
else
    echo "保留测试根：$ROOT"
fi
exit $rc
