#!/usr/bin/env bash
# L2 测试脚手架公共函数（被 cli-smoke.sh 等 source）。
# 设计原则：所有用例都在临时 XDG 根里跑，不碰用户真实配置/数据；失败要能指出是哪条用例。
set -uo pipefail

: "${ROOT:?ROOT 未设置}"
: "${BIN:?BIN 未设置（Lunarward 可执行文件路径）}"

PASS_COUNT=0
FAIL_COUNT=0
SKIP_COUNT=0
FAILED_CASES=()

# 用例记录目录（失败证据）
CASE_LOG_DIR="$ROOT/logs"
mkdir -p "$CASE_LOG_DIR"

# 统一环境：CLI 全部走临时 XDG；PATH 前置假外部命令目录
test_env() {
    env \
        XDG_CONFIG_HOME="$ROOT/cfg" \
        XDG_DATA_HOME="$ROOT/data" \
        XDG_CACHE_HOME="$ROOT/cache" \
        XDG_STATE_HOME="$ROOT/state" \
        HOME="$ROOT/home" \
        PATH="$ROOT/bin:$ROOT/fake-wine/bin:$PATH" \
        DISPLAY="${DISPLAY:-}" \
        "$@"
}

# 无 DISPLAY 的环境（验证 headless 行为）
test_env_headless() {
    env -u DISPLAY -u WAYLAND_DISPLAY \
        XDG_CONFIG_HOME="$ROOT/cfg" \
        XDG_DATA_HOME="$ROOT/data" \
        XDG_CACHE_HOME="$ROOT/cache" \
        XDG_STATE_HOME="$ROOT/state" \
        HOME="$ROOT/home" \
        PATH="$ROOT/bin:$ROOT/fake-wine/bin:$PATH" \
        "$@"
}

lw() { test_env "$BIN" "$@"; }

case_start() {
    CASE_ID="$1"
    CASE_DESC="$2"
}

fail_case() {
    local reason="$1"
    FAIL_COUNT=$((FAIL_COUNT + 1))
    FAILED_CASES+=("$CASE_ID: $reason")
    echo "FAIL  $CASE_ID  $CASE_DESC"
    echo "      ↳ $reason"
    local log="$CASE_LOG_DIR/$CASE_ID.log"
    if [ -f "$CASE_LOG_DIR/$CASE_ID.out" ]; then
        echo "      ↳ 输出见 $CASE_LOG_DIR/$CASE_ID.out"
    fi
}

pass_case() {
    PASS_COUNT=$((PASS_COUNT + 1))
    echo "ok    $CASE_ID  $CASE_DESC"
}

skip_case() {
    SKIP_COUNT=$((SKIP_COUNT + 1))
    echo "skip  $CASE_ID  $CASE_DESC（$1）"
}

# 断言：退出码等于 N
expect_rc() {
    local expected="$1" actual="$2"
    if [ "$actual" -ne "$expected" ]; then
        fail_case "退出码 $actual != $expected"
        return 1
    fi
    return 0
}

# 断言：退出码非 0
expect_rc_nonzero() {
    local actual="$1"
    if [ "$actual" -eq 0 ]; then
        fail_case "退出码为 0，但期望失败"
        return 1
    fi
    return 0
}

# 断言：输出包含子串
expect_contains() {
    local haystack="$1" needle="$2"
    case "$haystack" in
        *"$needle"*) return 0 ;;
        *) fail_case "输出里没有「$needle」"; return 1 ;;
    esac
}

# 断言：输出不包含子串
expect_not_contains() {
    local haystack="$1" needle="$2"
    case "$haystack" in
        *"$needle"*) fail_case "输出里不该出现「$needle」"; return 1 ;;
        *) return 0 ;;
    esac
}

# 断言：文件存在
expect_file() {
    local path="$1"
    [ -e "$path" ] && return 0
    fail_case "文件不存在：$path"
    return 1
}

# 断言：JSON 可解析且字段等于期望（需要 jq）
expect_json() {
    local file="$1" expr="$2" expected="$3"
    local actual
    actual=$(jq -r "$expr" "$file" 2>/dev/null) || { fail_case "JSON 解析失败：$file"; return 1; }
    if [ "$actual" != "$expected" ]; then
        fail_case "JSON $expr = 「$actual」，期望「$expected」"
        return 1
    fi
    return 0
}

summary() {
    echo
    echo "──────── L2 汇总 ────────"
    echo "通过 $PASS_COUNT / 失败 $FAIL_COUNT / 跳过 $SKIP_COUNT"
    if [ "$FAIL_COUNT" -gt 0 ]; then
        echo "失败用例："
        printf '  - %s\n' "${FAILED_CASES[@]}"
        return 1
    fi
    return 0
}
