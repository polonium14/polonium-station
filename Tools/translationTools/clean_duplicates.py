#!/usr/bin/env python3

# Prawa autorskie (C) 2025 Polonium Statiom
#
# Ten program jest wolnym oprogramowaniem: można go rozpowszechniać i/lub modyfikować
# zgodnie z warunkami licencji GNU AGPL opublikowanej przez
# Free Software Foundation, w wersji 3 licencji lub
# w dowolnej późniejszej wersji.
#
# Ten program stworzony na podstawie kodu projektu Corvax,
# pierwotnie licencjonowanego na podstawie licencji MIT (patrz https://github.com/space-syndicate/space-station-14/blob/master/LICENSE.TXT).

import argparse
import os
import typing
from datetime import datetime

from fluent.syntax import ast, FluentParser

ENTRY_TYPES = (ast.Message,)


def find_top_level_dir(start_dir: str) -> str:
    marker_file = 'SpaceStation14.sln'
    current_dir = start_dir
    while True:
        if marker_file in os.listdir(current_dir):
            return current_dir
        parent_dir = os.path.dirname(current_dir)
        if parent_dir == current_dir:
            print(f"Nie udało się znaleźć {marker_file} zaczynając od {start_dir}")
            exit(-1)
        current_dir = parent_dir


def find_ftl_files(root_dir: str) -> typing.List[str]:
    ftl_files = []
    for root, _, files in os.walk(root_dir):
        for file in files:
            if file.endswith('.ftl'):
                ftl_files.append(os.path.join(root, file))
    return ftl_files


def read_file_text(file_path: str) -> typing.Optional[str]:
    """Odczyt .ftl — preferuje UTF-8 (chardet myli np. znak ⏏ z Windows-1254)."""
    try:
        raw = open(file_path, 'rb').read()
    except OSError:
        print(f"Nie można otworzyć pliku {file_path}. Pomijam.")
        return None

    for encoding in ('utf-8-sig', 'utf-8', 'cp1250', 'latin-1'):
        try:
            return raw.decode(encoding)
        except UnicodeDecodeError:
            continue

    print(f"Nie udało się odczytać {file_path} jako UTF-8 — pomijam.")
    return None


def write_file_text(file_path: str, content: str) -> None:
    with open(file_path, 'w', encoding='utf-8', newline='\n') as file:
        file.write(content)


def find_ent_occurrences(content: str) -> typing.List[typing.Tuple[str, int, int]]:
    occurrences: typing.List[typing.Tuple[str, int, int]] = []
    parsed = FluentParser().parse(content)
    for element in parsed.body:
        if not isinstance(element, ENTRY_TYPES):
            continue
        key = element.id.name
        if not element.span:
            continue
        occurrences.append((key, element.span.start, element.span.end))
    return occurrences


def cut_span(text: str, start: int, end: int) -> str:
    after = text[end:]
    if after.startswith('\n'):
        after = after[1:]
    return text[:start] + after


def remove_spans(content: str, spans: typing.List[typing.Tuple[int, int]]) -> str:
    for start, end in sorted(spans, key=lambda item: item[0], reverse=True):
        content = cut_span(content, start, end)
    return content


def remove_duplicate_attributes(content: str) -> typing.Tuple[str, int]:
    """Usuwa zduplikowane atrybuty (.desc, .gender, …) w obrębie jednej wiadomości."""
    parsed = FluentParser().parse(content)
    spans_to_remove: typing.List[typing.Tuple[int, int]] = []

    for element in parsed.body:
        if not isinstance(element, ENTRY_TYPES):
            continue
        attrs = getattr(element, 'attributes', None) or []
        seen_attr_names: typing.Set[str] = set()
        for attr in attrs:
            if not attr.span:
                continue
            if attr.id.name in seen_attr_names:
                spans_to_remove.append((attr.span.start, attr.span.end))
            else:
                seen_attr_names.add(attr.id.name)

    if not spans_to_remove:
        return content, 0

    return remove_spans(content, spans_to_remove), len(spans_to_remove)


def remove_duplicates(root_dir: str) -> typing.Tuple[int, int, typing.List[typing.Tuple[str, str, str]]]:
    ftl_files = find_ftl_files(root_dir)
    canonical_file_by_ent: typing.Dict[str, str] = {}
    occurrences_by_file: typing.Dict[str, typing.List[typing.Tuple[str, int, int, str]]] = {}
    removed_duplicates: typing.List[typing.Tuple[str, str, str]] = []
    attr_fixes = 0
    files_changed = 0

    for file_path in ftl_files:
        content = read_file_text(file_path)
        if content is None:
            continue
        file_occurrences = []
        for key, start, end in find_ent_occurrences(content):
            if key not in canonical_file_by_ent:
                canonical_file_by_ent[key] = file_path
            file_occurrences.append((key, start, end, content[start:end]))
        occurrences_by_file[file_path] = file_occurrences

    for file_path, file_occurrences in occurrences_by_file.items():
        content = read_file_text(file_path)
        if content is None:
            continue

        spans_to_remove: typing.List[typing.Tuple[int, int]] = []
        seen_keys_in_file: typing.Set[str] = set()

        for key, start, end, block in file_occurrences:
            if canonical_file_by_ent.get(key) != file_path:
                spans_to_remove.append((start, end))
                removed_duplicates.append((key, file_path, block))
                continue
            if key in seen_keys_in_file:
                spans_to_remove.append((start, end))
                removed_duplicates.append((key, file_path, block))
                continue
            seen_keys_in_file.add(key)

        new_content = content
        if spans_to_remove:
            new_content = remove_spans(new_content, spans_to_remove)

        deduped_content, removed_attrs = remove_duplicate_attributes(new_content)
        if removed_attrs:
            attr_fixes += removed_attrs
            new_content = deduped_content

        if new_content == content:
            continue

        write_file_text(file_path, new_content)
        files_changed += 1

    return files_changed, len(removed_duplicates), removed_duplicates


def main(argv=None):
    parser = argparse.ArgumentParser(
        description='Usuwa zduplikowane wpisy ent-* w plikach Fluent (.ftl).',
    )
    parser.add_argument(
        '--locale',
        choices=('pl-PL', 'en-US', 'both'),
        default='pl-PL',
        help='którą lokalizację przetworzyć (domyślnie pl-PL)',
    )
    args = parser.parse_args(argv)

    script_dir = os.path.dirname(os.path.abspath(__file__))
    main_folder = find_top_level_dir(script_dir)
    locales = []
    if args.locale in ('pl-PL', 'both'):
        locales.append(os.path.join(main_folder, 'Resources', 'Locale', 'pl-PL'))
    if args.locale in ('en-US', 'both'):
        locales.append(os.path.join(main_folder, 'Resources', 'Locale', 'en-US'))

    total_files = 0
    total_removed = 0
    all_removed: typing.List[typing.Tuple[str, str, str]] = []

    for root_dir in locales:
        print(f'Przetwarzam: {root_dir}')
        files_changed, removed_count, removed = remove_duplicates(root_dir)
        total_files += files_changed
        total_removed += removed_count
        all_removed.extend(removed)

    print(f'Przetwarzanie zakończone. Zmieniono plików: {total_files}, usunięto zduplikowanych wiadomości: {total_removed}')

    if not all_removed:
        print('Duplikaty ent-* nie znaleziono — log nie został utworzony.')
        return

    log_filename = f"removed_duplicates_{datetime.now().strftime('%Y%m%d_%H%M%S')}.log"
    with open(log_filename, 'w', encoding='utf-8') as log_file:
        for ent, path, block in all_removed:
            log_file.write(f"Usunięto duplikat: {ent}\n")
            log_file.write(f"Plik: {path}\n")
            log_file.write("Zawartość:\n")
            log_file.write(block)
            log_file.write("\n\n")

    print(f'Log: {log_filename}')


if __name__ == '__main__':
    main()
