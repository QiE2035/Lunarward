#!/usr/bin/env bash
# 校验三份 PKGBUILD 的 depends 是否覆盖源码真正需要的原生依赖。
#
# 动机：2026-11-18 的 D88 之前，webkitgtk-6.0 只补进了 packaging/PKGBUILD，
# 两个 AUR 包从 11-11 起就没再动过 —— 因为依赖清单是三个地方各写一遍。
# 这个脚本只做静态检查（秒级、不联网、不构建）：
#   A  源码里 P/Invoke 的库名与 Process.Start 的命令 → 反查 depends 有没有漏
#   B  三份 PKGBUILD 的 depends 必须完全一致（防止再漂移）
#   C  source 与 sha256sums 的自洽（git 源才能 SKIP；tag 源要求 tag 存在）
set -uo pipefail

ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
cd "$ROOT" || exit 1

fail=0
warn=0
ok() { printf '  [ OK ] %s\n' "$*"; }
bad() { printf '  [FAIL] %s\n' "$*"; fail=$((fail + 1)); }
note() { printf '  [WARN] %s\n' "$*"; warn=$((warn + 1)); }

PKGBUILDS=(
  packaging/PKGBUILD
  packaging/aur/lunarward/PKGBUILD
  packaging/aur/lunarward-git/PKGBUILD
)

# ---------------------------------------------------------------- A 依赖覆盖
echo "== A 源码依赖 → depends =="

# 库名或外部命令 → Arch 包名。空串表示「不写进 depends」：glibc 属 base，
# CoreMessaging.dll 是 Windows 专有路径，libzstd 随产物自带（Lib/linux-x64）。
declare -A PKG_OF=(
  ["libxcb.so.1"]="libxcb"
  ["libX11.so.6"]="libx11"
  ["libfontconfig.so.1"]="fontconfig"
  ["libfreetype.so.6"]="fontconfig"
  ["libexpat.so.1"]="fontconfig"
  ["libwebkitgtk-6.0.so"]="webkitgtk-6.0"
  ["libwebkitgtk-6.0.so.4"]="webkitgtk-6.0"
  ["libwebkitgtk-6.0.so.4.19.5"]="webkitgtk-6.0"
  ["libjavascriptcoregtk-6.0.so"]="webkitgtk-6.0"
  ["libjavascriptcoregtk-6.0.so.1"]="webkitgtk-6.0"
  ["libc.so.6"]=""
  ["CoreMessaging.dll"]=""
  ["xdg-open"]="xdg-utils"
  ["xdg-mime"]="xdg-utils"
  # 只在 optdepends 里出现，不是硬依赖
  ["python3"]=""
  ["cp"]=""
  ["xdotool"]=""
)

libs=$(grep -rhoE '"(lib[A-Za-z0-9._+-]*\.so(\.[0-9]+)*|CoreMessaging\.dll)"' \
         --include=*.cs src/Lunarward 2>/dev/null | tr -d '"' | sort -u)
cmds=$(grep -rhoE 'ProcessStartInfo\("[a-z0-9][a-z0-9-]*"' \
         --include=*.cs src/Lunarward 2>/dev/null | sed -E 's/.*"([^"]+)"/\1/' | sort -u)

needed=()
unknown=()
while IFS= read -r token; do
  [ -z "$token" ] && continue
  if [[ -v "PKG_OF[$token]" ]]; then
    pkg=${PKG_OF[$token]}
    [ -n "$pkg" ] && needed+=("$pkg")
  else
    unknown+=("$token")
  fi
done < <(printf '%s\n%s\n' "$libs" "$cmds")

if [ ${#unknown[@]} -gt 0 ]; then
  note "源码里出现未登记的依赖，请确认是否需要写进 depends 并补进 PKG_OF：$(printf '%s ' "${unknown[@]}")"
fi

# 去重排序
if [ ${#needed[@]} -gt 0 ]; then
  mapfile -t needed < <(printf '%s\n' "${needed[@]}" | sort -u)
fi
echo "  需要声明的包：${needed[*]:-（无）}"

depends_block() {
  awk '/^depends=\(/{flag=1} flag{print} flag&&/\)/{flag=0}' "$1"
}

for f in "${PKGBUILDS[@]}"; do
  if [ ! -f "$f" ]; then
    bad "$f 不存在"
    continue
  fi
  block=$(depends_block "$f")
  missing=()
  for pkg in "${needed[@]}"; do
    grep -qE "'${pkg}([<>][^']*)?'" <<<"$block" || missing+=("$pkg")
  done
  if [ ${#missing[@]} -eq 0 ]; then
    ok "$f：depends 覆盖 ${#needed[@]} 项"
  else
    bad "$f：depends 缺 ${missing[*]}"
  fi
done

# ---------------------------------------------------------------- B 三份一致
echo "== B 三份 depends 一致 =="
first=""
for f in "${PKGBUILDS[@]}"; do
  [ -f "$f" ] || continue
  norm=$(depends_block "$f" | tr '\n' ' ' | sed -E 's/.*depends=\(//; s/\).*//')
  norm=$(tr -d "[:space:]'" <<<"$norm" | tr ',' '\n' | sed '/^$/d' | sort | tr '\n' ' ')
  if [ -z "$first" ]; then
    first=$norm
    ok "基准：$f"
  elif [ "$norm" = "$first" ]; then
    ok "$f：与基准一致"
  else
    bad "$f：depends 与基准不一致"
    printf '         基准：%s\n' "$first"
    printf '         实际：%s\n' "$norm"
  fi
done

# ---------------------------------------------------------------- C 源与哈希
echo "== C source 与 sha256sums =="
for f in "${PKGBUILDS[@]}"; do
  [ -f "$f" ] || continue
  src=$(grep -E '^source=' "$f")
  sums=$(grep -E '^sha256sums=' "$f")
  if grep -qE 'git\+' <<<"$src"; then
    if grep -q "SKIP" <<<"$sums"; then
      ok "$f：VCS 源用 SKIP（合规）"
    else
      note "$f：VCS 源却写了具体哈希（makepkg 会报错）"
    fi
  else
    if grep -q "SKIP" <<<"$sums"; then
      bad "$f：tarball 源不能用 SKIP（AUR 会拒收）"
    else
      ok "$f：tarball 源有真实哈希"
    fi
  fi
  # tag 源要求 tag 真实存在
  tag=$(sed -nE 's/.*#tag=(v[^"]+)".*/\1/p' <<<"$src")
  if [ -n "$tag" ]; then
    if git rev-parse -q --verify "refs/tags/$tag" >/dev/null; then
      ok "$f：tag $tag 已存在"
    else
      note "$f：tag $tag 还不存在（发布前先 git tag $tag && git push origin $tag）"
    fi
  fi
done

echo
if [ "$fail" -eq 0 ]; then
  printf 'PKGBUILD 检查通过（%s 条提醒）\n' "$warn"
  exit 0
fi
printf 'PKGBUILD 检查失败：%s 项不合格，%s 条提醒\n' "$fail" "$warn"
exit 1
