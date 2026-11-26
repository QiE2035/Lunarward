#!/usr/bin/env python3
"""生成 docs/linux-port/18-upstream-table.md（同名差异文件表）。

数据来源：
  1. `bash scripts/list-deviations.sh` 的同名差异文件清单（只取 `^  ` 且不带 `→` 的行）；
  2. 每个文件的变更行 = difflib.unified_diff 里 `+`/`-` 行数之和；
  3. 分类 = `docs/linux-port/25-deviation-register.md` §3 里该文件条目的类别词频（取前两位），
     §3 未收录则标「仅格式或轻微」。

用法（仓库根）：
    uv run --no-project python scripts/gen-upstream-table.py            # 打印到 stdout
    uv run --no-project python scripts/gen-upstream-table.py --write    # 直接覆盖 18-upstream-table.md
"""

from __future__ import annotations

import argparse
import difflib
import pathlib
import re
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
UP = ROOT / "src/Starward"
LO = ROOT / "src/Lunarward"
REGISTER = ROOT / "docs/linux-port/25-deviation-register.md"
TARGET = ROOT / "docs/linux-port/18-upstream-table.md"

CATEGORIES = [
    "linux 专属实现",
    "api 平台 API",
    "cut 裁剪",
    "engine 引擎能力",
    "cosmetic 外观文案",
    "eng 工程性",
    "bugfix 修上游缺陷",
]
SHORT = {
    "linux 专属实现": "linux",
    "api 平台 API": "api",
    "cut 裁剪": "cut",
    "engine 引擎能力": "engine",
    "cosmetic 外观文案": "cosmetic",
    "eng 工程性": "eng",
    "bugfix 修上游缺陷": "bugfix",
}


def changed_files() -> list[str]:
    out = subprocess.run(
        ["bash", str(ROOT / "scripts/list-deviations.sh")],
        cwd=ROOT,
        capture_output=True,
        text=True,
        check=True,
    ).stdout
    files = []
    for line in out.splitlines():
        if not line.startswith("  ") or "→" in line:
            continue
        rel = line.strip()
        if (UP / rel).exists() and (LO / rel).exists():
            files.append(rel)
    return sorted(set(files))


def change_lines(rel: str) -> int:
    a = (UP / rel).read_text(encoding="utf-8-sig", errors="replace").splitlines()
    b = (LO / rel).read_text(encoding="utf-8-sig", errors="replace").splitlines()
    diff = difflib.unified_diff(a, b, n=0)
    return sum(1 for line in diff if line.startswith(("+", "-")) and not line.startswith(("+++", "---")))


def category_map() -> dict[str, str]:
    text = REGISTER.read_text(encoding="utf-8")
    result: dict[str, str] = {}
    blocks = re.split(r"^#### ", text, flags=re.M)[1:]
    for block in blocks:
        head = block.splitlines()[0].strip()
        rel = head.strip("`").split("（")[0].strip()
        if not rel:
            continue
        counts = {c: block.count(c) for c in CATEGORIES}
        top = [c for c, n in sorted(counts.items(), key=lambda kv: kv[1], reverse=True) if n > 0][:2]
        if top:
            result[rel] = " / ".join(SHORT[c] for c in top)
    return result


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--write", action="store_true")
    parser.add_argument("--upstream-head", default="", help="上游 HEAD 短哈希，仅用于表头")
    parser.add_argument("--merge-base", default="", help="merge base 短哈希，仅用于表头")
    args = parser.parse_args()

    files = changed_files()
    cats = category_map()
    rows = sorted(((change_lines(rel), rel, cats.get(rel, "仅格式或轻微")) for rel in files), reverse=True)
    small = sum(1 for total, _, _ in rows if total <= 40)
    big = sum(1 for total, _, _ in rows if total >= 174)

    header = [
        "# 上游同名差异文件表（脚本生成）",
        "",
        f"> 本表由 `uv run --no-project python scripts/gen-upstream-table.py --write` 生成，不要手改。",
        f"> 来源：`bash scripts/list-deviations.sh` 的同名差异文件清单（**{len(rows)} 个**）；"
        "「变更行」= 两侧内容做 `difflib.unified_diff` 后 `+`/`-` 行数之和（去掉文件头），增删都计入，"
        "整段重排/挪位会让数字偏大，只作量级参考。",
        "> 「分类」＝该文件在 `25-deviation-register.md` §3 里的主导类别（取前两位）；"
        "§3 未收录说明差异只是格式或轻微，标 `仅格式或轻微`。逐点判定与必要性见 `25`，偏差原则见 `18-upstream-deviations.md`。",
        f"> 实测：{small} 个 ≤40 行，{big} 个 ≥174 行。",
        "",
        "| 变更行 | 文件 | 分类 |",
        "|---:|---|---|",
    ]
    body = [f"| {total} | `{rel}` | {cat} |" for total, rel, cat in rows]
    text = "\n".join(header + body) + "\n"

    if args.write:
        TARGET.write_text(text, encoding="utf-8")
        print(f"wrote {TARGET.relative_to(ROOT)} ({len(rows)} files)")
    else:
        sys.stdout.write(text)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
