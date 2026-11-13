#!/usr/bin/env bash
# 列出「与上游同名但内容不同」的文件，以及代码里登记过的 LUNARWARD-DEVIATION 标记。
# 用途：同步上游（scripts/sync-upstream.sh）前先看这份清单，避免把偏差当成脏数据覆盖掉。
set -uo pipefail
REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO"

echo "# 同名文件偏差（src/Lunarward 里存在，且 src/Starward 里有同名文件，内容不同）"
find src/Lunarward -name '*.cs' -o -name '*.xaml' | while read -r f; do
    rel="${f#src/Lunarward/}"
    up="src/Starward/$rel"
    [ -f "$up" ] || continue
    cmp -s "$f" "$up" || echo "  $rel"
done | sort

echo
echo "# 代码里登记的偏差标记（LUNARWARD-DEVIATION）"
grep -rn "LUNARWARD-DEVIATION" src/Lunarward --include=*.cs --include=*.xaml | sed 's/:.*LUNARWARD-DEVIATION/  →/' | sort
