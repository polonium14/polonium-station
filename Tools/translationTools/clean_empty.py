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

def find_top_level_dir(start_dir):
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
def setup_logging():
    log_filename = f"cleanup_{datetime.now().strftime('%Y%m%d_%H%M%S')}.log"
    logging.basicConfig(filename=log_filename, level=logging.INFO,
                        format='%(asctime)s - %(levelname)s - %(message)s')
    console = logging.StreamHandler()
    console.setLevel(logging.INFO)
    console.stream.reconfigure(encoding='utf-8')
    logging.getLogger('').addHandler(console)
    return log_filename

def remove_empty_files_and_folders(path):
    removed_files = 0
    removed_folders = 0

    for root, dirs, files in os.walk(path, topdown=False):
        # Usuwanie pustych plików
        for file in files:
            file_path = os.path.join(root, file)
            if os.path.getsize(file_path) == 0:
                try:
                    os.remove(file_path)
                    logging.info(f"Usunięto pusty plik: {file_path}")
                    removed_files += 1
                except Exception as e:
                    logging.error(f"Błąd podczas usuwania pliku {file_path}: {str(e)}")

        # Usuwanie pustych folderów
        if not os.listdir(root):
            try:
                os.rmdir(root)
                logging.info(f"Usunięto pusty folder: {root}")
                removed_folders += 1
            except Exception as e:
                logging.error(f"Błąd podczas usuwania folderu {root}: {str(e)}")

    return removed_files, removed_folders

if __name__ == "__main__":
    script_dir = os.path.dirname(os.path.abspath(__file__))
    main_folder = find_top_level_dir(script_dir)
    root_dir = os.path.join(main_folder, "Resources\\Locale")
    log_file = setup_logging()

    logging.info(f"Rozpoczęcie czyszczenia w katalogu: {root_dir}")
    files_removed, folders_removed = remove_empty_files_and_folders(root_dir)
    logging.info(f"Czyszczenie zakończone. Usunięto plików: {files_removed}, usunięto folderów: {folders_removed}")
    print(f"Dziennik operacji zapisany w pliku: {log_file}")
