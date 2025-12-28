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

# Skrypt jest odpowiednikiem sync_locales.py

import typing
import logging

from pydash import py_

from file import FluentFile
from fluentast import FluentAstAbstract
from fluentformatter import FluentFormatter
from project import Project
from fluent.syntax import ast, FluentParser, FluentSerializer

######################################### Configuration ################################################################

# Lista folderów, które należy zignorować przy tworzeniu par plików
IGNORED_FOLDERS: typing.List[str] = ['robust-toolbox', 'datasets']

######################################### Description #####################################################################
# Przeprowadza aktualizację kluczy. Znajduje pliki z angielskim tłumaczeniem i sprawdza, czy istnieje polski odpowiednik
# Jeśli nie - tworzy plik z kopią tłumaczeń z angielskiego
# Następnie, plik po pliku sprawdzane są klucze. Jeśli w angielskim pliku jest więcej kluczy - tworzy brakujące w polskim, kopiując angielskie tłumaczenia
# Oznacza polskie pliki, które zawierają klucze nieobecne w odpowiadających im angielskich
# Oznacza polskie pliki, które nie mają angielskiego odpowiednika

######################################### Class defifitions ############################################################
class RelativeFile:
    def __init__(self, file: FluentFile, locale: typing.AnyStr, relative_path_from_locale: typing.AnyStr):
        self.file = file
        self.locale = locale
        self.relative_path_from_locale = relative_path_from_locale


class FilesFinder:
    def __init__(self, project: Project):
        self.project: Project = project
        self.created_files: typing.List[FluentFile] = []

    def get_relative_path_dict(self, file: FluentFile, locale):
        if locale == 'pl-PL':
            return RelativeFile(file=file, locale=locale,
                                relative_path_from_locale=file.get_relative_path(self.project.pl_locale_dir_path))
        elif locale == 'en-US':
            return RelativeFile(file=file, locale=locale,
                                relative_path_from_locale=file.get_relative_path(self.project.en_locale_dir_path))
        else:
            raise Exception(f'Język {locale} nie jest obsługiwany')

    def get_file_pair(self, en_file: FluentFile) -> typing.Tuple[FluentFile, FluentFile]:
        pl_file_path = en_file.full_path.replace('en-US', 'pl-PL')
        pl_file = FluentFile(pl_file_path)

        return en_file, pl_file

    def execute(self):
        self.created_files = []
        groups = self.get_files_pars()
        keys_without_pair = list(filter(lambda g: len(groups[g]) < 2, groups))

        for key_without_pair in keys_without_pair:
            relative_file: RelativeFile = groups.get(key_without_pair)[0]

            if relative_file.locale == 'en-US':
                pl_file = self.create_pl_analog(relative_file)
                self.created_files.append(pl_file)
            elif relative_file.locale == 'pl-PL':
                is_engine_files = "robust-toolbox" in (relative_file.file.full_path)
                if not is_engine_files:
                    self.warn_en_analog_not_exist(relative_file)
            else:
                raise Exception(f'Plik {relative_file.file.full_path} ma nieznany język "{relative_file.locale}"')
        return self.created_files

    def get_files_pars(self):
        en_fluent_files = self.project.get_fluent_files_by_dir(project.en_locale_dir_path)
        pl_fluent_files = self.project.get_fluent_files_by_dir(project.pl_locale_dir_path)

        if IGNORED_FOLDERS:
            en_fluent_files = [f for f in en_fluent_files if not any(ignored in f.full_path for ignored in IGNORED_FOLDERS)]
            pl_fluent_files = [f for f in pl_fluent_files if not any(ignored in f.full_path for ignored in IGNORED_FOLDERS)]

        en_fluent_relative_files = list(map(lambda f: self.get_relative_path_dict(f, 'en-US'), en_fluent_files))
        pl_fluent_relative_files = list(map(lambda f: self.get_relative_path_dict(f, 'pl-PL'), pl_fluent_files))
        relative_files = py_.flatten_depth(py_.concat(en_fluent_relative_files, pl_fluent_relative_files), depth=1)

        return py_.group_by(relative_files, 'relative_path_from_locale')

    def create_pl_analog(self, en_relative_file: RelativeFile) -> FluentFile:
        en_file: FluentFile = en_relative_file.file
        en_file_data = en_file.read_data()
        pl_file_path = en_file.full_path.replace('en-US', 'pl-PL')
        pl_file = FluentFile(pl_file_path)
        pl_file.save_data(en_file_data)

        logging.info(f'Utworzono plik {pl_file_path} z tłumaczeniami z angielskiego pliku')

        return pl_file

    def warn_en_analog_not_exist(self, pl_relative_file: RelativeFile):
        file: FluentFile = pl_relative_file.file
        en_file_path = file.full_path.replace('pl-PL', 'en-US')

        logging.warning(f'Plik {file.full_path} nie ma angielskiego odpowiednika pod ścieżką {en_file_path}')


class KeyFinder:
    def __init__(self, files_dict):
        self.files_dict = files_dict
        self.changed_files: typing.List[FluentFile] = []

    def execute(self) -> typing.List[FluentFile]:
        self.changed_files = []
        for pair in self.files_dict:
            pl_relative_file = py_.find(self.files_dict[pair], {'locale': 'pl-PL'})
            en_relative_file = py_.find(self.files_dict[pair], {'locale': 'en-US'})

            if not en_relative_file or not pl_relative_file:
                continue

            pl_file: FluentFile = pl_relative_file.file
            en_file: FluentFile = en_relative_file.file

            self.compare_files(en_file, pl_file)

        return self.changed_files


    def compare_files(self, en_file, pl_file):
        pl_file_parsed: ast.Resource = pl_file.parse_data(pl_file.read_data())
        en_file_parsed: ast.Resource = en_file.parse_data(en_file.read_data())

        self.write_to_pl_files(pl_file, pl_file_parsed, en_file_parsed)
        self.log_not_exist_en_files(en_file, pl_file_parsed, en_file_parsed)


    def write_to_pl_files(self, pl_file, pl_file_parsed, en_file_parsed):
        for idx, en_message in enumerate(en_file_parsed.body):
            if isinstance(en_message, ast.ResourceComment) or isinstance(en_message, ast.GroupComment) or isinstance(en_message, ast.Comment):
                continue

            pl_message_analog_idx = py_.find_index(pl_file_parsed.body, lambda pl_message: self.find_duplicate_message_id_name(pl_message, en_message))
            have_changes = False

            # Attributes
            if getattr(en_message, 'attributes', None) and pl_message_analog_idx != -1:
                if not pl_file_parsed.body[pl_message_analog_idx].attributes:
                    pl_file_parsed.body[pl_message_analog_idx].attributes = en_message.attributes
                    have_changes = True
                else:
                    for en_attr in en_message.attributes:
                        pl_attr_analog = py_.find(pl_file_parsed.body[pl_message_analog_idx].attributes, lambda pl_attr: pl_attr.id.name == en_attr.id.name)
                        if not pl_attr_analog:
                            pl_file_parsed.body[pl_message_analog_idx].attributes.append(en_attr)
                            have_changes = True

            # New elements
            if pl_message_analog_idx == -1:
                pl_file_body = pl_file_parsed.body
                if (len(pl_file_body) >= idx + 1):
                    pl_file_parsed = self.append_message(pl_file_parsed, en_message, idx)
                else:
                    pl_file_parsed = self.push_message(pl_file_parsed, en_message)
                have_changes = True

            if have_changes:
                serialized = serializer.serialize(pl_file_parsed)
                self.save_and_log_file(pl_file, serialized, en_message)

    def log_not_exist_en_files(self, en_file, pl_file_parsed, en_file_parsed):
        for idx, pl_message in enumerate(pl_file_parsed.body):
            if isinstance(pl_message, ast.ResourceComment) or isinstance(pl_message, ast.GroupComment) or isinstance(pl_message, ast.Comment):
                continue

            en_message_analog = py_.find(en_file_parsed.body, lambda en_message: self.find_duplicate_message_id_name(pl_message, en_message))

            if not en_message_analog:
                logging.warning(f'Klucz "{FluentAstAbstract.get_id_name(pl_message)}" nie ma angielskiego odpowiednika pod ścieżką {en_file.full_path}"')

    def append_message(self, pl_file_parsed, en_message, en_message_idx):
        pl_message_part_1 = pl_file_parsed.body[0:en_message_idx]
        pl_message_part_middle = [en_message]
        pl_message_part_2 = pl_file_parsed.body[en_message_idx:]
        new_body = py_.flatten_depth([pl_message_part_1, pl_message_part_middle, pl_message_part_2], depth=1)
        pl_file_parsed.body = new_body

        return pl_file_parsed

    def push_message(self,  pl_file_parsed, en_message):
        pl_file_parsed.body.append(en_message)
        return pl_file_parsed

    def save_and_log_file(self, file, file_data, message):
        file.save_data(file_data)
        logging.info(f'Do pliku {file.full_path} dodano klucz "{FluentAstAbstract.get_id_name(message)}"')
        self.changed_files.append(file)

    def find_duplicate_message_id_name(self, pl_message, en_message):
        pl_element_id_name = FluentAstAbstract.get_id_name(pl_message)
        en_element_id_name = FluentAstAbstract.get_id_name(en_message)

        if not pl_element_id_name or not en_element_id_name:
            return False

        if pl_element_id_name == en_element_id_name:
            return pl_message
        else:
            return None

######################################## Var definitions ###############################################################

logging.basicConfig(level = logging.INFO)
project = Project()
parser = FluentParser()
serializer = FluentSerializer(with_junk=True)
files_finder = FilesFinder(project)
key_finder = KeyFinder(files_finder.get_files_pars())

########################################################################################################################

print('Sprawdzam aktualności plików ...')
created_files = files_finder.execute()
if len(created_files):
    print('Formatuję utworzone pliki ...')
    FluentFormatter.format(created_files)
print('Sprawdzam aktualność kluczy ...')
changed_files = key_finder.execute()
if len(changed_files):
    print('Formatuję zmienione pliki ...')
    FluentFormatter.format(changed_files)
