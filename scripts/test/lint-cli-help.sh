#!/usr/bin/env bash
# Lunarward：命令行帮助的防漂移 lint（不启动 GUI、不建前缀，秒级）。
#
# 背景（D79 / L-151）：`--help` 曾经是一段手写字符串，和 `Program.Linux.cs` 里的
# `args[0] is "--x"` 分支各写一遍，于是反复出现「加了开关忘了写帮助」
# （`--profile-ensure` 是事后补的）和「描述停在旧版本」（`--preflight` 一直写「六项」）。
# 现在帮助只有一份来源 `src/Lunarward/Helpers/CliSpec.cs`，这里卡三条：
#   A. `Program.Linux.cs` 里每个 `args[0] is "--x"` 都必须在 CliSpec 表里
#   B. CliSpec 表里每条命令都必须真的出现在 `--help` 输出里（给了 LW_BIN 才跑）
#   C. 拼错的开关必须 exit 2 并给出最接近的候选（给了 LW_BIN 才跑）
set -uo pipefail
cd "$(dirname "$0")/../.." || exit 1
SRC=src/Lunarward
SPEC=$SRC/Helpers/CliSpec.cs
fail=0
say() { printf '%s\n' "$*"; }

say "── A. 源码里的开关 ⊆ CliSpec 表 ─────────────────────"
# 只看第一 token 的分支：`args[0] is "--x"` / `args[0] is "--x" or "-y"`
tokens=$(grep -n 'args\[0\]' "$SRC/Program.Linux.cs" \
  | grep -oE '"--[a-z0-9-]+"' | tr -d '"' | sort -u)
missing=""
for t in $tokens; do
  grep -q "new(\"$t\"" "$SPEC" || missing="$missing $t"
done
if [ -n "$missing" ]; then
  say "源码里有、CliSpec 表里没有：$missing"
  say "FAIL  A: 命令行开关没有登记进帮助表（在 Helpers/CliSpec.cs 里补一行）"
  fail=1
else
  say "PASS  A: $(printf '%s\n' "$tokens" | wc -l) 个第一 token 开关都在表里"
fi

if [ -z "${LW_BIN:-}" ] || [ ! -x "${LW_BIN:-}" ]; then
  say "SKIP  B/C: 未提供可执行文件（LW_BIN=… bash scripts/test/lint-cli-help.sh 可跑）"
  exit $fail
fi

say "── B. --help 输出覆盖全表 ───────────────────────────"
help=$("$LW_BIN" --help 2>&1)
all=$(grep -oE 'new\("--[a-z0-9-]+"' "$SPEC" | sed 's/new("//;s/"//' | sort -u)
absent=""
for t in $all; do
  case "$help" in *"$t"*) ;; *) absent="$absent $t";; esac
done
if [ -n "$absent" ]; then
  say "--help 里没出现：$absent"
  say "FAIL  B: 表里有、帮助里没有（检查 RenderHelp 的分组过滤）"
  fail=1
else
  say "PASS  B: $(printf '%s\n' "$all" | wc -l) 条命令全部出现在 --help 里"
fi

say "── C. 未知开关 exit 2 ───────────────────────────────"
out=$("$LW_BIN" --definitely-not-a-switch 2>&1)
code=$?
if [ "$code" -ne 2 ]; then
  say "退出码是 $code（应为 2）"
  say "FAIL  C: 未知开关没有以 2 退出"
  fail=1
elif ! printf '%s' "$out" | grep -q '未知开关'; then
  say "$out"
  say "FAIL  C: 未知开关没有给出提示"
  fail=1
else
  say "PASS  C: 未知开关 exit 2 且带提示"
fi

# 拼错一个字符也要能猜对：--componet-list → --component-list
out=$("$LW_BIN" --componet-list 2>&1)
if printf '%s' "$out" | grep -q -- '--component-list'; then
  say "PASS  C2: 拼错时给出 --component-list"
else
  say "$out"
  say "FAIL  C2: 拼错时没有给出最接近的开关"
  fail=1
fi

exit $fail
