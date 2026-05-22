#!/usr/bin/env python3

import argparse
import os
import sys
from pathlib import Path
from typing import Dict, List, Set, Tuple

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from fluent.syntax import ast, FluentParser
from fluentast import FluentAstAbstract
from project import Project

ENTRY_TYPES = (ast.Message, ast.Term)

MODE_STRUCTURE = 'structure'
MODE_KEYS = 'keys'
MODES = (MODE_STRUCTURE, MODE_KEYS)


def collect_relative_paths(root: Path, suffix: str = '.ftl') -> Tuple[Set[str], Set[str]]:
    files: Set[str] = set()
    dirs: Set[str] = set()
    if not root.is_dir():
        return files, dirs

    for dirpath, _, filenames in os.walk(root):
        rel_dir = Path(dirpath).relative_to(root).as_posix()
        if rel_dir != '.':
            dirs.add(rel_dir)

        for name in filenames:
            if name.endswith(suffix):
                rel = Path(dirpath, name).relative_to(root).as_posix()
                files.add(rel)

    return files, dirs


def collect_fluent_keys(file_path: Path) -> Set[str]:
    keys: Set[str] = set()
    try:
        content = file_path.read_text(encoding='utf-8')
    except (OSError, UnicodeDecodeError):
        return keys

    if not content.strip():
        return keys

    try:
        parsed = FluentParser().parse(content)
    except Exception:
        return keys

    for element in parsed.body:
        if not isinstance(element, ENTRY_TYPES):
            continue
        key_name = FluentAstAbstract.get_id_name(element)
        if not key_name:
            continue
        keys.add(key_name)
        for attr in getattr(element, 'attributes', None) or []:
            keys.add(f'{key_name}.{attr.id.name}')

    return keys


def print_section(title: str, items: List[str], limit: int) -> None:
    print(f'\n{title} ({len(items)}):')
    if not items:
        print('  (brak)')
        return
    for item in items[:limit]:
        print(f'  {item}')
    if len(items) > limit:
        print(f'  ... i jeszcze {len(items) - limit}')


def run_structure_report(
    en_root: Path,
    pl_root: Path,
    en_files: Set[str],
    pl_files: Set[str],
    en_dirs: Set[str],
    pl_dirs: Set[str],
    limit: int,
) -> None:
    only_en_files = sorted(en_files - pl_files)
    only_pl_files = sorted(pl_files - en_files)
    common_files = sorted(en_files & pl_files)
    only_en_dirs = sorted(en_dirs - pl_dirs)
    only_pl_dirs = sorted(pl_dirs - en_dirs)
    common_dirs = sorted(en_dirs & pl_dirs)

    print('=== prototypes/generated: struktura (en-US vs pl-PL) ===')
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
    print(f'  Wspólne katalogi:       {len(common_dirs)}')
    print(f'  Tylko w en-US (kat.):   {len(only_en_dirs)}')
    print(f'  Tylko w pl-PL (kat.):   {len(only_pl_dirs)}')

    print_section('Pliki .ftl tylko w en-US', only_en_files, limit)
    print_section('Pliki .ftl tylko w pl-PL', only_pl_files, limit)
    print_section('Katalogi tylko w en-US', only_en_dirs, limit)
    print_section('Katalogi tylko w pl-PL', only_pl_dirs, limit)

    if not en_files and pl_files:
        print('\nUwaga: en-US/prototypes/generated nie zawiera plików .ftl,')
        print('       a pl-PL ma pełną strukturę — uruchom yamlextractor --mode en-only lub both.')


def run_keys_report(
    en_root: Path,
    pl_root: Path,
    en_files: Set[str],
    pl_files: Set[str],
    limit: int,
    show_equal: bool,
) -> None:
    only_en_files = sorted(en_files - pl_files)
    only_pl_files = sorted(pl_files - en_files)
    common_files = sorted(en_files & pl_files)

    all_only_en_keys: Set[str] = set()
    all_only_pl_keys: Set[str] = set()
    files_with_key_diffs: List[str] = []
    per_file_diffs: Dict[str, Tuple[List[str], List[str]]] = {}

    for rel_path in only_en_files:
        en_keys = collect_fluent_keys(en_root / rel_path)
        all_only_en_keys.update(en_keys)
        if en_keys:
            per_file_diffs[rel_path] = (sorted(en_keys), [])

    for rel_path in only_pl_files:
        pl_keys = collect_fluent_keys(pl_root / rel_path)
        all_only_pl_keys.update(pl_keys)
        if pl_keys:
            per_file_diffs[rel_path] = ([], sorted(pl_keys))

    for rel_path in common_files:
        en_keys = collect_fluent_keys(en_root / rel_path)
        pl_keys = collect_fluent_keys(pl_root / rel_path)
        only_en = sorted(en_keys - pl_keys)
        only_pl = sorted(pl_keys - en_keys)

        if only_en or only_pl:
            files_with_key_diffs.append(rel_path)
            per_file_diffs[rel_path] = (only_en, only_pl)
            all_only_en_keys.update(only_en)
            all_only_pl_keys.update(only_pl)
        elif show_equal:
            per_file_diffs[rel_path] = ([], [])

    print('=== prototypes/generated: klucze Fluent (en-US vs pl-PL) ===')
    print(f'en-US: {en_root}')
    print(f'pl-PL: {pl_root}')
    print('(porównywane są identyfikatory Message/Term i nazwy atrybutów, bez wartości)')
    print()
    print('Podsumowanie:')
    print(f'  Pliki tylko w en-US:              {len(only_en_files)}')
    print(f'  Pliki tylko w pl-PL:              {len(only_pl_files)}')
    print(f'  Wspólne pliki:                    {len(common_files)}')
    print(f'  Wspólne pliki z różnicą kluczy:   {len(files_with_key_diffs)}')
    print(f'  Wspólne pliki bez różnic kluczy:  {len(common_files) - len(files_with_key_diffs)}')
    print(f'  Unikalne klucze tylko w en-US:    {len(all_only_en_keys)}')
    print(f'  Unikalne klucze tylko w pl-PL:    {len(all_only_pl_keys)}')

    if only_en_files:
        print_section('Pliki tylko w en-US (wszystkie klucze brakują w pl-PL)', only_en_files, limit)

    if only_pl_files:
        print_section('Pliki tylko w pl-PL (wszystkie klucze brakują w en-US)', only_pl_files, limit)

    print_section('Wspólne pliki z różnicą zestawów kluczy', files_with_key_diffs, limit)

    detail_paths = sorted(per_file_diffs.keys())
    if not show_equal:
        detail_paths = [p for p in detail_paths if per_file_diffs[p] != ([], [])]

    print(f'\nSzczegóły per plik (max {limit} plików):')
    if not detail_paths:
        print('  (brak różnic kluczy)')
    for rel_path in detail_paths[:limit]:
        only_en, only_pl = per_file_diffs[rel_path]
        if not only_en and not only_pl and not show_equal:
            continue
        print(f'\n  [{rel_path}]')
        if only_en:
            en_preview = ', '.join(only_en[:8])
            suffix = f' ... (+{len(only_en) - 8})' if len(only_en) > 8 else ''
            print(f'    tylko en-US ({len(only_en)}): {en_preview}{suffix}')
        if only_pl:
            pl_preview = ', '.join(only_pl[:8])
            suffix = f' ... (+{len(only_pl) - 8})' if len(only_pl) > 8 else ''
            print(f'    tylko pl-PL ({len(only_pl)}): {pl_preview}{suffix}')
        if show_equal and not only_en and not only_pl:
            print('    (zestawy kluczy identyczne)')

    if len(detail_paths) > limit:
        print(f'\n  ... i jeszcze {len(detail_paths) - limit} plików (zwiększ --limit)')

    if only_pl_files and not en_files:
        print('\nUwaga: brak plików en-US — wszystkie klucze z pl-PL uznane za brakujące po stronie en-US.')


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(
        description='Porównanie prototypes/generated: en-US vs pl-PL',
    )
    parser.add_argument(
        '--mode',
        choices=MODES,
        default=MODE_STRUCTURE,
        help='structure: różnice ścieżek/katalogów; keys: obecność kluczy Fluent w parach plików',
    )
    parser.add_argument(
        '--limit',
        type=int,
        default=40,
        help='maks. pozycji w każdej sekcji listy (domyślnie 40)',
    )
    parser.add_argument(
        '--show-equal',
        action='store_true',
        help='(tylko --mode keys) wypisz też wspólne pliki bez różnic kluczy',
    )
    args = parser.parse_args(argv)

    project = Project()
    en_root = Path(project.en_locale_prototypes_dir_path)
    pl_root = Path(project.pl_locale_prototypes_dir_path)

    en_files, en_dirs = collect_relative_paths(en_root)
    pl_files, pl_dirs = collect_relative_paths(pl_root)

    if args.mode == MODE_STRUCTURE:
        run_structure_report(en_root, pl_root, en_files, pl_files, en_dirs, pl_dirs, args.limit)
    else:
        run_keys_report(en_root, pl_root, en_files, pl_files, args.limit, args.show_equal)

    return 0


if __name__ == '__main__':
    raise SystemExit(main())
