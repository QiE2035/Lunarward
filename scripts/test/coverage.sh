#!/usr/bin/env bash
# L1 覆盖率：跑单测并解析 cobertura 报告。
# 被测逻辑以「链接源码」方式编译在测试程序集内，因此必须带 tests/coverlet.runsettings
# （其中 IncludeTestAssembly=true），否则 coverlet 会忽略测试程序集、覆盖率恒为 0。
#
# 用法：bash scripts/test/coverage.sh [阈值，默认 85]
# 环境：LW_COVERAGE_OUT 指定结果目录（默认 /tmp/lw-coverage）
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

python3 - "$REPORT" "$THRESHOLD" <<'PY'
import re, sys
report, threshold = sys.argv[1], float(sys.argv[2])
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
print("PASS")
PY
