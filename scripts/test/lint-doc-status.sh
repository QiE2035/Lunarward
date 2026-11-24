#!/usr/bin/env bash
# 文档状态一致性检查（秒级、不构建、不联网）。
#
# 目的：`docs/linux-port/` 里的状态表是判断「某个能力到底做了没有」的唯一依据，
# 但它靠人手维护，数字与代码脱节时没人发现（2026-11-24 复查踩到：排除表写着 55 条、
# csproj 里是 53 条；XAML 差异写着「共 7 处」、实际 37 处）。这个脚本把能机器判定的
# 几项固定下来，改代码后顺手跑一次。
#
# 用法：
#   bash scripts/test/lint-doc-status.sh            # 只报告，退出码恒 0
#   bash scripts/test/lint-doc-status.sh --strict   # 有 FAIL 时退出码 1（CI 用这个）
#
# 检查项：
#   1. `13-lessons.md` 的 L-NNN 编号唯一、连续无重号
#   2. `19-test-plan.md` 的 T-* / RG-* 编号唯一
#   3. `12-shims-and-gaps.md` 登记的 `*.Linux.cs` 集合 == 实际文件集合
#   4. `12-shims-and-gaps.md` 里写的 `<Compile Remove>` 条数 == csproj 实际条数
#   5. `18-upstream-deviations.md` 里写的同名文件数 == `scripts/list-deviations.sh` 输出
#   6. `docs/linux-port/` 文档份数与 `README.md` / `AGENTS.md` 里的声明一致
#
# 判不了的不硬判：涉及「历史快照」的数字（25 的 265 点、23 的设计条目）不在这里校验。

set -uo pipefail
REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO" || exit 1

STRICT=0
for arg in "$@"; do
    [ "$arg" = "--strict" ] && STRICT=1
done

DOCS="docs/linux-port"
fails=0
warns=0

pass() { printf 'PASS  %s\n' "$*"; }
warn() { printf 'WARN  %s\n' "$*"; warns=$((warns + 1)); }
fail() { printf 'FAIL  %s\n' "$*"; fails=$((fails + 1)); }

# ---------- 1. L-NNN 唯一 ----------
lessons="$DOCS/13-lessons.md"
if [ -f "$lessons" ]; then
    # 只看标题行：正文里「见 L-119」这类引用不算重号
    dup="$(grep -oE '^#+ *L-[0-9]{3}' "$lessons" | grep -oE 'L-[0-9]{3}' | sort | uniq -d | head -10)"
    if [ -n "$dup" ]; then
        fail "13-lessons.md 有重号：$(echo "$dup" | tr '\n' ' ')"
    else
        pass "13-lessons.md 的 L-NNN 编号唯一"
    fi
else
    warn "找不到 $lessons"
fi

# ---------- 2. T-* / RG-* 唯一 ----------
plan="$DOCS/19-test-plan.md"
if [ -f "$plan" ]; then
    # 只看表格首列的定义行，正文里的引用不算
    tdup="$(grep -oE '^ *\| *T-[A-Za-z0-9]+-[0-9]+[a-z]?' "$plan" | grep -oE 'T-[A-Za-z0-9]+-[0-9]+[a-z]?' | sort | uniq -d | head -10)"
    if [ -n "$tdup" ]; then
        fail "19-test-plan.md 有重号：$(echo "$tdup" | tr '\n' ' ')"
    else
        pass "19-test-plan.md 的 T-* 编号唯一"
    fi
    rows=$(grep -cE '^\| *T-' "$plan")
    uniqn=$(grep -oE '^ *\| *T-[A-Za-z0-9]+-[0-9]+[a-z]?' "$plan" | grep -oE 'T-[A-Za-z0-9]+-[0-9]+[a-z]?' | sort -u | wc -l)
    printf 'INFO  19-test-plan.md：T-* 表格行 %s 条，唯一编号 %s 个\n' "$rows" "$uniqn"
else
    warn "找不到 $plan"
fi

# ---------- 3. *.Linux.cs 集合 ----------
shims="$DOCS/12-shims-and-gaps.md"
actual_count=$(find src/Lunarward -name '*.Linux.cs' | wc -l)
if [ -f "$shims" ]; then
    # 文档里提到的每个 XXX.Linux.cs（可能带目录前缀），取 basename 去重
    # 只看 §3 的表格行：正文里的历史说明（如「该行曾登记为 GameNoticeWindow.Linux.cs，已作废」）不算登记
    sec3=$(awk '/^## 3 · Linux 实现文件/{f=1} /^## 4 ·/{f=0} f' "$shims")
    doc_names=$(printf '%s\n' "$sec3" | grep -E '^ *\|' | grep -oE '[A-Za-z0-9_.-]+\.Linux\.cs' | sort -u)
    real_names=$(find src/Lunarward -name '*.Linux.cs' -printf '%f\n' | sort -u)
    missing=$(comm -13 <(echo "$doc_names") <(echo "$real_names") | sed '/^$/d')
    extra=$(comm -23 <(echo "$doc_names") <(echo "$real_names") | sed '/^$/d')
    if [ -n "$missing" ]; then
        fail "12-shims §3 漏登 $(echo "$missing" | wc -l) 个实际存在的替身：$(echo "$missing" | tr '\n' ' ')"
    fi
    if [ -n "$extra" ]; then
        fail "12-shims §3 登记了 $(echo "$extra" | wc -l) 个不存在的文件：$(echo "$extra" | tr '\n' ' ')"
    fi
    [ -z "$missing" ] && [ -z "$extra" ] && pass "12-shims §3 的 *.Linux.cs 集合与实际一致（$actual_count 个）"
else
    warn "找不到 $shims"
fi

# ---------- 4. Compile Remove 条数 ----------
csproj="src/Lunarward/Lunarward.csproj"
if [ -f "$csproj" ] && [ -f "$shims" ]; then
    real=$(grep -c '<Compile Remove' "$csproj")
    # 文档里的写法通常是「共 53 条」「53 条排除」「（53）」
    declared=$(grep -oE '^## 6 · 排除编译的上游文件（[0-9]+ 条）' "$shims" | sed 's/.*（\([0-9]*\) 条）/\1/' | head -1)
    if [ -n "$declared" ]; then
        if [ "$declared" = "$real" ]; then
            pass "12-shims 声明的 Compile Remove 条数 $declared == csproj 实际 $real"
        else
            fail "12-shims 声明 Compile Remove $declared 条，csproj 实际 $real 条"
        fi
    else
        warn "12-shims 里没找到 Compile Remove 的条数声明（跳过）"
    fi
fi

# ---------- 5. 同名偏差文件数 ----------
dev="$DOCS/18-upstream-deviations.md"
if [ -f "$dev" ] && [ -x scripts/list-deviations.sh ] || [ -f scripts/list-deviations.sh ]; then
    real=$(bash scripts/list-deviations.sh 2>/dev/null | grep -c '^  [A-Za-z]')
    if [ -f "$dev" ]; then
        declared=$(grep -oE '[0-9]+ *个同名文件' "$dev" | grep -oE '[0-9]+' | head -1)
        if [ -n "$declared" ]; then
            if [ "$declared" = "$real" ]; then
                pass "18-upstream-deviations 声明的同名文件数 $declared == 脚本输出 $real"
            else
                warn "18-upstream-deviations 声明 $declared 个同名文件，脚本现在列出 $real 个（文档可能带日期快照，核对后更新）"
            fi
        else
            warn "18-upstream-deviations 里没找到同名文件数声明（跳过）"
        fi
    fi
fi

# ---------- 6. 文档份数 ----------
doc_count=$(ls "$DOCS"/*.md 2>/dev/null | wc -l)
for f in "$DOCS/README.md" AGENTS.md; do
    [ -f "$f" ] || continue
    declared=$(grep -oE '活跃文档[^0-9]*[0-9]+ *份|共 *[0-9]+ *份' "$f" | grep -oE '[0-9]+' | head -1)
    [ -z "$declared" ] && continue
    if [ "$declared" = "$doc_count" ]; then
        pass "$(basename "$f") 声明的文档份数 $declared == 实际 $doc_count"
    else
        fail "$(basename "$f") 声明 $declared 份文档，$DOCS 实际有 $doc_count 份"
    fi
done

echo
if [ "$fails" -gt 0 ]; then
    echo "lint-doc-status: $fails 项失败 / $warns 项警告"
    [ "$STRICT" = "1" ] && exit 1
    exit 0
fi
echo "lint-doc-status: 全部通过（$warns 项警告）"
exit 0
