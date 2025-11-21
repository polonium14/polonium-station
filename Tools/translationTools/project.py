# Taken from Corvax - https://github.com/space-syndicate/space-station-14/tree/master/Tools/ss14_ru

import pathlib
import os
import glob
from file import FluentFile

class Project:
    def __init__(self): # Nie należy zmieniać tych ścieżek.
        self.base_dir_path = pathlib.Path(os.path.abspath(os.curdir)).parent.parent.resolve()
        self.resources_dir_path = os.path.join(self.base_dir_path, 'Resources')
        self.locales_dir_path = os.path.join(self.resources_dir_path, 'Locale')
        self.pl_locale_dir_path = os.path.join(self.locales_dir_path, 'pl-PL')
        self.en_locale_dir_path = os.path.join(self.locales_dir_path, 'en-US')
        self.prototypes_dir_path = os.path.join(self.resources_dir_path, "Prototypes")
        self.en_locale_prototypes_dir_path = os.path.join(self.en_locale_dir_path, 'prototypes', 'generated')
        self.pl_locale_prototypes_dir_path = os.path.join(self.pl_locale_dir_path, 'prototypes', 'generated')

    def get_files_paths_by_dir(self, dir_path, files_extenstion):
        return glob.glob(f'{dir_path}/**/*.{files_extenstion}', recursive=True)

    def get_fluent_files_by_dir(self, dir_path):
        files = []
        files_paths_list = glob.glob(f'{dir_path}/**/*.ftl', recursive=True)

        for file_path in files_paths_list:
            try:
                files.append(FluentFile(file_path))
            except:
                continue

        return files

