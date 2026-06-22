#!/usr/bin/env python3

import argparse
import os
import sys
from pathlib import Path
from typing import Dict, List, Optional, Set, Tuple, Union

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from fluent.syntax import ast, FluentParser, FluentSerializer
from fluentast import FluentAstAbstract
from file import FluentFile
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


def _collect_keys_by_id(parsed: ast.Resource) -> Dict[str, Union[ast.Message, ast.Term]]:
    keys: Dict[str, Union[ast.Message, ast.Term]] = {}
    for element in parsed.body:
        if not isinstance(element, ENTRY_TYPES):
            continue
        key_name = FluentAstAbstract.get_id_name(element)
        if key_name:
            keys[key_name] = element
    return keys


def _indent_attribute_snippet(snippet: str) -> str:
    lines = snippet.split('\n')
    return '\n'.join(
        f'  {line}' if line.startswith('.') and not line.startswith('  ') else line
        for line in lines
    )


def _extract_span_text(source: str, element) -> str:
    if element.span:
        return source[element.span.start:element.span.end]
    return FluentSerializer(with_junk=True).serialize(ast.Resource(body=[element]))


def collect_locale_message_keys(root: Path) -> Set[str]:
    keys: Set[str] = set()
    if not root.is_dir():
        return keys
    for file_path in root.rglob('*.ftl'):
        for key in collect_fluent_keys(file_path):
            if '.' not in key:
                keys.add(key)
    return keys


def sync_missing_keys_in_file(
    source_text: str,
    source_parsed: ast.Resource,
    target_text: str,
    target_keys: Dict[str, Union[ast.Message, ast.Term]],
    locale_message_keys: Optional[Set[str]] = None,
) -> Tuple[str, List[str]]:
    """Dopisuje brakujące klucze/atrybuty do target_text (per plik; opcjonalnie bez duplikatów w całej lokalizacji)."""
    append_snippets: List[str] = []
    insertions: List[Tuple[int, str]] = []
    added_keys: List[str] = []

    for source_entry in source_parsed.body:
        if not isinstance(source_entry, ENTRY_TYPES):
            continue

        key_name = FluentAstAbstract.get_id_name(source_entry)
        if not key_name:
            continue

        target_entry = target_keys.get(key_name)
        if target_entry is None:
            if locale_message_keys is not None and key_name in locale_message_keys:
                continue
            append_snippets.append(_extract_span_text(source_text, source_entry))
            target_keys[key_name] = source_entry
            if locale_message_keys is not None:
                locale_message_keys.add(key_name)
            added_keys.append(key_name)
            continue

        source_attrs = getattr(source_entry, 'attributes', None) or []
        if not source_attrs:
            continue

        target_attr_names = {attr.id.name for attr in (getattr(target_entry, 'attributes', None) or [])}
        missing_attrs = [attr for attr in source_attrs if attr.id.name not in target_attr_names]
        if not missing_attrs or not target_entry.span:
            continue

        attr_snippets = [
            _indent_attribute_snippet(_extract_span_text(source_text, attr))
            for attr in missing_attrs
        ]
        insertions.append((target_entry.span.end, '\n' + '\n'.join(attr_snippets)))
        added_keys.append(f'{key_name} ({", ".join(a.id.name for a in missing_attrs)})')

    if not append_snippets and not insertions:
        return target_text, []

    new_text = target_text
    for position, text in sorted(insertions, key=lambda item: item[0], reverse=True):
        new_text = new_text[:position] + text + new_text[position:]

    if append_snippets:
        new_text = new_text.rstrip('\n')
        blocks = [new_text] + [snippet.strip('\n') for snippet in append_snippets]
        new_text = '\n\n'.join(block for block in blocks if block) + '\n'

    return new_text, added_keys


def fix_key_differences(en_root: Path, pl_root: Path, common_files: Set[str]) -> int:
    files_changed = 0
    pl_locale_keys = collect_locale_message_keys(pl_root)
    en_locale_keys = collect_locale_message_keys(en_root)

    for rel_path in sorted(common_files):
        en_file = FluentFile(str(en_root / rel_path))
        pl_file = FluentFile(str(pl_root / rel_path))

        en_text = en_file.read_data()
        pl_text = pl_file.read_data()
        en_parsed = en_file.parse_data(en_text)
        pl_parsed = pl_file.parse_data(pl_text)

        pl_keys = _collect_keys_by_id(pl_parsed)
        en_keys = _collect_keys_by_id(en_parsed)

        new_pl, pl_added = sync_missing_keys_in_file(
            en_text, en_parsed, pl_text, dict(pl_keys), pl_locale_keys,
        )
        new_en, en_added = sync_missing_keys_in_file(
            pl_text, pl_parsed, en_text, dict(en_keys), en_locale_keys,
        )

        changed = False
        if new_pl != pl_text:
            pl_file.save_data(new_pl)
            changed = True
            if pl_added:
                print(f'  pl-PL {rel_path}: +{", ".join(pl_added[:5])}' + (
                    f' ... (+{len(pl_added) - 5})' if len(pl_added) > 5 else ''
                ))

        if new_en != en_text:
            en_file.save_data(new_en)
            changed = True
            if en_added:
                print(f'  en-US {rel_path}: +{", ".join(en_added[:5])}' + (
                    f' ... (+{len(en_added) - 5})' if len(en_added) > 5 else ''
                ))

        if changed:
            files_changed += 1

    return files_changed


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
    parser.add_argument(
        '--fix',
        action='store_true',
        help='(tylko --mode keys) uzupełnij brakujące klucze w parach plików (en↔pl, per plik)',
    )
    args = parser.parse_args(argv)

    if args.fix and args.mode != MODE_KEYS:
        parser.error('--fix wymaga --mode keys')

    project = Project()
    en_root = Path(project.en_locale_prototypes_dir_path)
    pl_root = Path(project.pl_locale_prototypes_dir_path)

    en_files, en_dirs = collect_relative_paths(en_root)
    pl_files, pl_dirs = collect_relative_paths(pl_root)

    if args.mode == MODE_STRUCTURE:
        run_structure_report(en_root, pl_root, en_files, pl_files, en_dirs, pl_dirs, args.limit)
    else:
        if args.fix:
            common_files = en_files & pl_files
            print('=== Uzupełnianie brakujących kluczy (prototypes/generated) ===')
            changed = fix_key_differences(en_root, pl_root, common_files)
            print(f'\nZmieniono plików: {changed}')
            print()

        run_keys_report(en_root, pl_root, en_files, pl_files, args.limit, args.show_equal)

    return 0


if __name__ == '__main__':
    raise SystemExit(main())
