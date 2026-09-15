#!/usr/bin/env python3

from __future__ import annotations

import argparse
import os
from pathlib import Path
from typing import Iterable, List, Optional

BOM = b"\xef\xbb\xbf"
LOCALE_DIR = Path("Resources") / "Locale"
SKIP_DIR_NAMES = {".git", "bin", "obj"}


def find_repo_root(start: Optional[Path] = None) -> Path:
    cwd = Path.cwd()
    if (cwd / LOCALE_DIR).is_dir():
        return cwd
    current = (start or Path(__file__).resolve()).parent
    markers = {"SpaceStation14.sln", "SpaceStation14.slnx"}
    while True:
        if any((current / marker).exists() for marker in markers):
            return current
        if current.parent == current:
            raise SystemExit("Nie znaleziono katalogu repo (brak .sln/.slnx)")
        current = current.parent


def iter_locale_files(locale_root: Path) -> Iterable[Path]:
    for dirpath, dirnames, filenames in os.walk(locale_root):
        dirnames[:] = [name for name in dirnames if name not in SKIP_DIR_NAMES]
        for filename in filenames:
            yield Path(dirpath) / filename


def repo_rel(path: Path, repo_root: Path) -> str:
    return path.relative_to(repo_root).as_posix()


def has_bom(path: Path) -> bool:
    with path.open("rb") as handle:
        return handle.read(len(BOM)) == BOM


def strip_bom(path: Path) -> None:
    data = path.read_bytes()
    if data.startswith(BOM):
        path.write_bytes(data[len(BOM) :])


def collect_bom_files(repo_root: Path) -> List[Path]:
    locale_root = repo_root / LOCALE_DIR
    if not locale_root.is_dir():
        raise SystemExit(f"Brak katalogu {locale_root}")
    return [path for path in iter_locale_files(locale_root) if has_bom(path)]


def main(argv: Optional[List[str]] = None) -> int:
    parser = argparse.ArgumentParser(
        description="Sprawdza (albo wycina) UTF-8 BOM w Resources/Locale.",
    )
    parser.add_argument(
        "--fix",
        action="store_true",
        help="wytnij BOM z plików zamiast tylko zgłaszać błąd",
    )
    args = parser.parse_args(argv)

    repo_root = find_repo_root()
    bom_files = collect_bom_files(repo_root)

    if args.fix:
        for path in bom_files:
            strip_bom(path)
            print(f"stripped BOM: {repo_rel(path, repo_root)}")
        leftover = collect_bom_files(repo_root)
        if leftover:
            print(f"BOM nadal siedzi w {len(leftover)} plikach po --fix")
            return 1
        print(f"Locale BOM strip OK ({len(bom_files)} plików)")
        return 0

    if not bom_files:
        print("Locale BOM check OK")
        return 0

    print(f"Locale BOM check FAILED ({len(bom_files)}):")
    for path in bom_files:
        rel = repo_rel(path, repo_root)
        print(f"  {rel}")
        if os.environ.get("GITHUB_ACTIONS"):
            print(
                f"::error file={rel},title=UTF-8 BOM::"
                f"Plik '{rel}' ma UTF-8 BOM. Crowdin FTL wtedy gubi pierwszy klucz. "
                f"Zapisz jako UTF-8 bez BOM."
            )
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
