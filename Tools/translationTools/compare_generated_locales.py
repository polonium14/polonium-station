#!/usr/bin/env python3

import argparse
import os
import sys
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from project import Project


def collect_relative_paths(root: Path, suffix: str = '.ftl') -> tuple[set[str], set[str]]:
    files: set[str] = set()
    dirs: set[str] = set()
    if not root.is_dir():
        return files, dirs

    for dirpath, dirnames, filenames in os.walk(root):
        rel_dir = Path(dirpath).relative_to(root).as_posix()
        if rel_dir != '.':
            dirs.add(rel_dir)

        for name in filenames:
            if name.endswith(suffix):
                rel = Path(dirpath, name).relative_to(root).as_posix()
                files.add(rel)

    return files, dirs


def print_section(title: str, items: list[str], limit: int) -> None:
    print(f'\n{title} ({len(items)}):')
    if not items:
        print('  (brak)')
        return
    shown = items[:limit]
    for item in shown:
        print(f'  {item}')
    if len(items) > limit:
        print(f'  ... i jeszcze {len(items) - limit}')


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(
        description='Strukturalne różnice prototypes/generated: en-US vs pl-PL',
    )
    parser.add_argument(
        '--limit',
        type=int,
        default=40,
        help='maks. liczba ścieżek wyświetlanych w każdej sekcji (domyślnie 40)',
    )
    args = parser.parse_args(argv)

    project = Project()
    en_root = Path(project.en_locale_prototypes_dir_path)
    pl_root = Path(project.pl_locale_prototypes_dir_path)

    en_files, en_dirs = collect_relative_paths(en_root)
    pl_files, pl_dirs = collect_relative_paths(pl_root)

    only_en_files = sorted(en_files - pl_files)
    only_pl_files = sorted(pl_files - en_files)
    common_files = sorted(en_files & pl_files)

    only_en_dirs = sorted(en_dirs - pl_dirs)
    only_pl_dirs = sorted(pl_dirs - en_dirs)
    common_dirs = sorted(en_dirs & pl_dirs)

    print('=== prototypes/generated: en-US vs pl-PL ===')
    print(f'en-US: {en_root}')
    print(f'pl-PL: {pl_root}')
    print()
    print('Podsumowanie:')
    print(f'  Pliki .ftl w en-US:     {len(en_files)}')
    print(f'  Pliki .ftl w pl-PL:     {len(pl_files)}')
    print(f'  Wspólne pliki .ftl:     {len(common_files)}')
    print(f'  Tylko w en-US (pliki):  {len(only_en_files)}')
    print(f'  Tylko w pl-PL (pliki):  {len(only_pl_files)}')
    print(f'  Katalogi w en-US:       {len(en_dirs)}')
    print(f'  Katalogi w pl-PL:       {len(pl_dirs)}')
    print(f'  Wspólne katalogi:     {len(common_dirs)}')
    print(f'  Tylko w en-US (kat.):   {len(only_en_dirs)}')
    print(f'  Tylko w pl-PL (kat.):   {len(only_pl_dirs)}')

    print_section('Pliki .ftl tylko w en-US', only_en_files, args.limit)
    print_section('Pliki .ftl tylko w pl-PL', only_pl_files, args.limit)
    print_section('Katalogi tylko w en-US', only_en_dirs, args.limit)
    print_section('Katalogi tylko w pl-PL', only_pl_dirs, args.limit)

    if not en_files and pl_files:
        print('\nUwaga: en-US/prototypes/generated nie zawiera plików .ftl,')
        print('       a pl-PL ma pełną strukturę — typowe przed pierwszym uruchomieniem yamlextractor --mode en-only/both.')

    return 0


if __name__ == '__main__':
    raise SystemExit(main())
