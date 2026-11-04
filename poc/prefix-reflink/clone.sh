#!/usr/bin/env bash
# G0-5：验证前缀模板克隆在用户实际文件系统上的耗时与空间占用。
# 用法：./clone.sh [源前缀] [目标前缀]
# 注意：源与目标必须同一文件系统，跨文件系统 reflink 会退化为真拷贝（见 GATES.md）。
set -euo pipefail

src="${1:-$HOME/.wine}"
dst="${2:-$HOME/.local/share/lunarward-poc/clone-test}"

if [ ! -d "$src" ]; then echo "源前缀不存在: $src" >&2; exit 1; fi
rm -rf "$dst"
mkdir -p "$(dirname "$dst")"

echo "源    : $src"
echo "目标  : $dst"
echo "文件系统: $(df --output=fstype "$src" | tail -1)"

free_before=$(df --output=used -B1 "$(dirname "$dst")" | tail -1)
start=$(date +%s.%N)
cp -a --reflink=auto "$src" "$dst"
end=$(date +%s.%N)
awk -v s="$start" -v e="$end" 'BEGIN { printf "耗时  : %.3f s\n", e-s }'

free_after=$(df --output=used -B1 "$(dirname "$dst")" | tail -1)
echo "表观大小: $(du -sh --apparent-size "$dst" | cut -f1)"
echo "du 报告 : $(du -sh "$dst" | cut -f1)（reflink 下 du 仍按块统计，不代表真实占用）"
echo "真实增量: $(awk -v a="$free_before" -v b="$free_after" 'BEGIN { printf "%.1f MB", (b-a)/1048576 }')（df 已用空间变化，越接近 0 说明越省）"
echo "对照源  : $(du -sh "$src" | cut -f1)"
