#!/usr/bin/env bash
# Lunarward：静态规则检查（L2 之外的「防回归 lint」，不启动应用、不建前缀）。
#
# 三条规则都是从真实缺陷反推出来的（见 docs/linux-port/19-test-plan.md §12）：
#   A. UI 代码（*.xaml.cs）里不许同步阻塞：WaitForExit / .GetAwaiter().GetResult() / .Wait()
#      → 来自 D72：`wine reg query` 在 UI 线程 fork，切游戏整窗静止 4 秒（L-127）。
#   B. *.Linux.cs 里不许留空的事件处理器 stub
#      → 来自 L-132：事件加了、处理器是空的，事件触发也没做事。
#   C. DevSwitches 的命令行参数必须都在 Program.Linux.cs 的 guiFlags 白名单里
#      → 来自 2026-11-14 回归：`--page` 曾被「未知命令」守卫拦掉。
set -uo pipefail
cd "$(dirname "$0")/../.." || exit 1
SRC=src/Lunarward
fail=0
say() { printf '%s\n' "$*"; }

say "── A. UI 代码同步阻塞 ────────────────────────────────"
# 被 <Compile Remove> 排除的文件不参与编译，里面的代码等于注释，不能算违规（L-41 家族）。
removed=$(grep -oP '<Compile Remove="\K[^"]+' "$SRC/Lunarward.csproj" || true)
hits_raw=$(grep -rn --include='*.xaml.cs' -E 'WaitForExit\(|GetAwaiter\(\)\.GetResult\(\)|\.Wait\(\)' "$SRC" || true)
hits=""
while IFS= read -r line; do
  [ -z "$line" ] && continue
  skip=0
  for r in $removed; do
    case "$line" in *"/$r:"*) skip=1;; esac
  done
  [ "$skip" -eq 0 ] && hits="${hits}${line}\n"
done <<< "$hits_raw"
hits=$(printf '%b' "$hits")
if [ -n "$hits" ]; then
  say "$hits"
  say "FAIL  A: UI code-behind 里出现同步阻塞（请改 async / Task.Run，见 D72）"
  fail=1
else
  say "PASS  A: 无"
fi

say "── B. *.Linux.cs 空事件处理器 stub ──────────────────"
# 匹配「private void Xxx(参数) { }」或换行后紧跟 '}' 的空体
hits=$(grep -rn --include='*.Linux.cs' -P 'private\s+void\s+\w+\([^)]*\)\s*\{\s*\}' "$SRC" || true)
if [ -n "$hits" ]; then
  say "$hits"
  say "FAIL  B: 事件处理器是空 stub，事件触发了也不会做事（见 L-132）"
  say "     故意不接管的处理器写成多行并在函数体里写 // LINT-ALLOW: 原因"
  fail=1
else
  say "PASS  B: 无"
fi

say "── C. DevSwitches 参数 ↔ guiFlags 白名单 ────────────"
dev_args=$(grep -oP 'Read\("--?\K[a-z-]+' "$SRC/Features/App/DevSwitches.cs" 2>/dev/null \
           || grep -oP 'Read\("\K[a-z-]+' "$SRC/Features/App/DevSwitches.cs" || true)
if [ -z "$dev_args" ]; then
  # DevSwitches.cs 里写的是 Read("page", "LUNARWARD_PAGE") 这种不带 -- 的形式
  dev_args=$(grep -oP 'Read\("\K[a-z-]+' "$SRC/Features/App/DevSwitches.cs" || true)
fi
missing=""
for a in $dev_args; do
  if ! grep -q -- "\"--$a\"" "$SRC/Program.Linux.cs"; then
    missing="$missing --$a"
  fi
done
if [ -n "$missing" ]; then
  say "FAIL  C: 这些 DevSwitches 参数没进 guiFlags 白名单：$missing"
  fail=1
else
  say "PASS  C: $(echo "$dev_args" | tr '\n' ' ')"
fi

say "────────────────────────────────────────────────────"
if [ "$fail" -ne 0 ]; then
  say "lint 失败：上面每条都要么修掉，要么在文档里登记例外。"
  exit 1
fi
say "lint 全部通过。"
