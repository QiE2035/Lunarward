#!/usr/bin/env bash
# L1 覆盖率：跑单测并解析 cobertura 报告。
# 被测逻辑以「链接源码」方式编译在测试程序集内，因此必须带 tests/coverlet.runsettings
# （其中 IncludeTestAssembly=true），否则 coverlet 会忽略测试程序集、覆盖率恒为 0。
#
# 用法：bash scripts/test/coverage.sh [阈值，默认 85]
# 环境：
#   LW_COVERAGE_OUT        结果目录（默认 /tmp/lw-coverage）
#   LW_COVERAGE_DIFF_BASE  启用「增量」判定：只算该提交..HEAD 在 src/ 下新增的行
#                          （没设/不是有效版本时只做总量判定）
#   LW_DIFF_THRESHOLD      增量阈值（默认与总量阈值相同）
set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
THRESHOLD="${1:-85}"
OUT="${LW_COVERAGE_OUT:-/tmp/lw-coverage}"

rm -rf "$OUT"
mkdir -p "$OUT"

dotnet test "$REPO/tests/Lunarward.Tests/Lunarward.Tests.csproj" \
    --settings "$REPO/tests/coverlet.runsettings" \
    --collect:"XPlat Code Coverage" \
    --results-directory "$OUT" >"$OUT/test.log" 2>&1
rc=$?

if [ $rc -ne 0 ]; then
    echo "单测失败，日志：$OUT/test.log"
    tail -20 "$OUT/test.log"
    exit $rc
fi

REPORT="$(find "$OUT" -name coverage.cobertura.xml | head -1)"
if [ -z "$REPORT" ]; then
    echo "没有生成覆盖率报告，日志：$OUT/test.log"
    exit 1
fi

python3 - "$REPORT" "$THRESHOLD" "${LW_COVERAGE_DIFF_BASE:-}" "${LW_DIFF_THRESHOLD:-}" "$REPO" <<'PY'
import os, re, subprocess, sys
report, threshold, diff_base, diff_threshold, repo = (
    sys.argv[1], float(sys.argv[2]), sys.argv[3], sys.argv[4], sys.argv[5])
xml = open(report, encoding="utf-8").read()

def attr(name, source):
    m = re.search(name + r'="([0-9.]+)"', source)
    return float(m.group(1)) if m else 0.0

line = attr("line-rate", xml)
branch = attr("branch-rate", xml)
covered = re.search(r'lines-covered="(\d+)"', xml)
valid = re.search(r'lines-valid="(\d+)"', xml)
cov = int(covered.group(1)) if covered else 0
tot = int(valid.group(1)) if valid else 0
classes = len(re.findall(r"<class ", xml))

print(f"行覆盖 {line*100:.2f}%（{cov}/{tot}）  分支覆盖 {branch*100:.2f}%  类 {classes}")
if tot == 0:
    print("FAIL: 覆盖行数为 0（检查 tests/coverlet.runsettings 的 IncludeTestAssembly）")
    sys.exit(1)
if line * 100 < threshold:
    print(f"FAIL: 行覆盖率低于阈值 {threshold:.0f}%")
    sys.exit(1)
def diff_check():
    """只算「相对 diff_base 新增的行」的覆盖率：总量对局部稀释不敏感，
    147 行新代码不测也只掉 2 个点；这里要求新增行本身达标（D125）。"""
    if not diff_base or set(diff_base) == {"0"}:
        print("增量：未指定 LW_COVERAGE_DIFF_BASE，跳过")
        return 0
    try:
        subprocess.run(["git", "-C", repo, "rev-parse", "--verify", diff_base + "^{commit}"],
                       check=True, capture_output=True)
    except Exception:
        print(f"增量：{diff_base} 不是有效提交，跳过")
        return 0
    text = subprocess.run(["git", "-C", repo, "diff", "--unified=0", diff_base + "...HEAD", "--", "src/"],
                          check=True, capture_output=True, text=True).stdout
    added = {}
    current = None
    for row in text.splitlines():
        if row.startswith("+++ b/"):
            current = row[6:]
        elif row.startswith("@@") and current:
            m = re.match(r"@@ -\S+ \+(\d+)(?:,(\d+))? @@", row)
            if not m:
                continue
            start = int(m.group(1))
            count = int(m.group(2) or 1)
            added.setdefault(current, set()).update(range(start, start + count))
    if not added:
        print("增量：本次改动没有 src/ 下的新增行")
        return 0
    # cobertura 里的 filename 是相对构建目录的路径，用后缀匹配回仓库路径；
    # 逐行命中统计：解析 <lines>…<line number="N" hits="H"/>
    file_lines = {}
    for block in re.finditer(r'filename="([^"]+)"[^>]*>(.*?)</class>', xml, re.S):
        name, body = block.group(1), block.group(2)
        rel = next((p for p in added if name.endswith(p)), None)
        if rel is None:
            continue
        target = file_lines.setdefault(rel, {})
        for line in re.finditer(r'<line number="(\d+)" hits="(\d+)"', body):
            number, count = int(line.group(1)), int(line.group(2))
            target[number] = target.get(number, 0) + count
    if not file_lines:
        print("增量：新增行都不在可测文件里（未链接进测试工程或非可执行行）")
        return 0
    limit = threshold if not diff_threshold else float(diff_threshold)
    total_hit = total_seen = 0
    rows = []
    for rel, lines in sorted(file_lines.items()):
        wanted = sorted(n for n in added[rel] if n in lines)
        if not wanted:
            continue
        hit = sum(1 for n in wanted if lines[n] > 0)
        total_hit += hit
        total_seen += len(wanted)
        rows.append((rel, hit, len(wanted)))
    if total_seen == 0:
        print("增量：新增行里没有带序列点的行（注释/空行/using），跳过")
        return 0
    rate = total_hit / total_seen * 100
    print(f"增量：新增行覆盖 {rate:.2f}%（{total_hit}/{total_seen}，阈值 {limit:.0f}%）")
    for rel, hit, seen in rows:
        print(f"  {hit}/{seen}\t{rel}")
    if rate < limit:
        print(f"FAIL: 本次新增行覆盖率低于阈值 {limit:.0f}%")
        return 1
    return 0

rc = diff_check()
if rc:
    sys.exit(rc)
print("PASS")
PY
