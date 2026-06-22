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
import logging
from datetime import datetime


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


def setup_logging() -> str:
    log_filename = f"cleanup_{datetime.now().strftime('%Y%m%d_%H%M%S')}.log"
    logging.basicConfig(filename=log_filename, level=logging.INFO,
                        format='%(asctime)s - %(levelname)s - %(message)s')
    console = logging.StreamHandler()
    console.setLevel(logging.INFO)
    if hasattr(console.stream, 'reconfigure'):
        console.stream.reconfigure(encoding='utf-8')
    logging.getLogger('').addHandler(console)
    return log_filename


def is_empty_file(file_path: str) -> bool:
    """Plik pusty (0 B) lub zawiera wyłącznie białe znaki (spacje, tabulatory, przejścia linii)."""
    try:
        with open(file_path, 'rb') as file:
            data = file.read()
    except OSError:
        return False

    if not data:
        return True

    return not data.strip()


def remove_empty_files_and_folders(path: str) -> tuple[int, int]:
    removed_files = 0
    removed_folders = 0

    for root, _, files in os.walk(path, topdown=False):
        for file in files:
            file_path = os.path.join(root, file)
            if not is_empty_file(file_path):
                continue
            try:
                os.remove(file_path)
                logging.info(f"Usunięto pusty plik: {file_path}")
                removed_files += 1
            except Exception as e:
                logging.error(f"Błąd podczas usuwania pliku {file_path}: {str(e)}")

        if not os.listdir(root):
            try:
                os.rmdir(root)
                logging.info(f"Usunięto pusty folder: {root}")
                removed_folders += 1
            except Exception as e:
                logging.error(f"Błąd podczas usuwania folderu {root}: {str(e)}")

    return removed_files, removed_folders


def main():
    script_dir = os.path.dirname(os.path.abspath(__file__))
    main_folder = find_top_level_dir(script_dir)
    root_dir = os.path.join(main_folder, "Resources", "Locale")

    log_file = setup_logging()
    logging.info(f"Rozpoczęcie czyszczenia w katalogu: {root_dir}")
    files_removed, folders_removed = remove_empty_files_and_folders(root_dir)

    if files_removed or folders_removed:
        logging.info(
            f"Czyszczenie zakończone. Usunięto plików: {files_removed}, folderów: {folders_removed}"
        )
        print(f"Dziennik operacji zapisany w pliku: {log_file}")
    else:
        print("Puste pliki i foldery nie znaleziono — nic nie usunięto.")


if __name__ == "__main__":
    main()
