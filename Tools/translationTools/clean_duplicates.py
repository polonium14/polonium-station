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
        if not key.startswith('ent-') or not element.span:
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


def remove_duplicates(root_dir: str):
    ftl_files = find_ftl_files(root_dir)
    canonical_file_by_ent: typing.Dict[str, str] = {}
    occurrences_by_file: typing.Dict[str, typing.List[typing.Tuple[str, int, int, str]]] = {}
    removed_duplicates: typing.List[typing.Tuple[str, str, str]] = []

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

    files_changed = 0
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

        if not spans_to_remove:
            continue

        new_content = remove_spans(content, spans_to_remove)
        if new_content == content:
            continue

        write_file_text(file_path, new_content)
        files_changed += 1

    print(f"Przetwarzanie zakończone. Sprawdzono plików: {len(ftl_files)}, zmieniono: {files_changed}")

    if not removed_duplicates:
        print("Duplikaty nie znaleziono — log nie został utworzony.")
        return

    log_filename = f"removed_duplicates_{datetime.now().strftime('%Y%m%d_%H%M%S')}.log"
    with open(log_filename, 'w', encoding='utf-8') as log_file:
        for ent, path, block in removed_duplicates:
            log_file.write(f"Usunięto duplikat: {ent}\n")
            log_file.write(f"Plik: {path}\n")
            log_file.write("Zawartość:\n")
            log_file.write(block)
            log_file.write("\n\n")

    print(f"Usunięto duplikatów: {len(removed_duplicates)}. Log: {log_filename}")


def main():
    script_dir = os.path.dirname(os.path.abspath(__file__))
    main_folder = find_top_level_dir(script_dir)
    root_dir = os.path.join(main_folder, "Resources", "Locale", "pl-PL")
    remove_duplicates(root_dir)


if __name__ == "__main__":
    main()
