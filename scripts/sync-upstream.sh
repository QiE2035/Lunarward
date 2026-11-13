#!/usr/bin/env bash
# 同步上游 Starward（upstream → 本仓 main）。
#
# 流程：
#   1. git fetch upstream（远端名约定为 upstream，见 AGENTS.md）
#   2. 跑 scripts/list-deviations.sh，提示本次合并会碰到的「同名但已偏差」文件
#   3. git merge upstream/main
#   4. 冲突里若属于「已登记偏差」的文件，默认保留本仓版本（--ours），其余冲突交给人看
#
# 用法：bash scripts/sync-upstream.sh [--dry-run]
set -uo pipefail
REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO"
DRY=0
[ "${1:-}" = "--dry-run" ] && DRY=1

if ! git remote get-url upstream >/dev/null 2>&1; then
    echo "没有名为 upstream 的远端。先执行："
    echo "  git remote add upstream https://github.com/Scighost/Starward.git"
    exit 1
fi

echo "== fetch upstream"
git fetch upstream --tags || exit 1

echo
echo "== 当前偏差清单（合并前先看一眼）"
bash scripts/list-deviations.sh | head -40

if [ "$DRY" = "1" ]; then
    echo
    echo "（dry-run）将要合并的提交："
    git log --oneline HEAD..upstream/main | head -20
    exit 0
fi

echo
echo "== merge upstream/main"
if ! git merge upstream/main --no-edit; then
    echo
    echo "有冲突。对「已偏差文件」保留本仓版本，其它需要人工判断："
    git diff --name-only --diff-filter=U | while read -r f; do
        if grep -q "^  $f$" <(bash scripts/list-deviations.sh 2>/dev/null); then
            echo "  保留本仓：$f"
            git checkout --ours -- "$f" && git add -- "$f"
        else
            echo "  需人工处理：$f"
        fi
    done
    echo
    echo "处理完剩下的冲突后执行 git merge --continue；有疑问先看 docs/linux-port/18-upstream-deviations.md。"
    exit 2
fi

echo "== 合并完成，跑一遍验证："
echo "  dotnet build Starward.slnx -c Debug && dotnet test tests/Lunarward.Tests/Lunarward.Tests.csproj"
