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

import argparse
import typing
import logging

from pydash import py_

from file import FluentFile
from fluentast import FluentAstAbstract
from fluentformatter import FluentFormatter
from project import Project
from fluent.syntax import ast, FluentSerializer

ENTRY_TYPES = (ast.Message, ast.Term)

SYNC_MODE_BOTH = 'both'
SYNC_MODE_PL_FROM_EN = 'pl-from-en'
SYNC_MODE_EN_FROM_PL = 'en-from-pl'
SYNC_MODES = (SYNC_MODE_BOTH, SYNC_MODE_PL_FROM_EN, SYNC_MODE_EN_FROM_PL)


def parse_args(argv=None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description='Synchronizacja kluczy Fluent między en-US i pl-PL.',
    )
    parser.add_argument(
        '--mode',
        choices=SYNC_MODES,
        default=SYNC_MODE_BOTH,
        help=(
            'both: dwustronna synchronizacja (domyślnie); '
            'pl-from-en: uzupełnia tylko pl-PL z en-US; '
            'en-from-pl: uzupełnia tylko en-US z pl-PL'
        ),
    )
    parser.add_argument(
        '--add-missing-en',
        action='store_true',
        help='przestarzałe: równoważne --mode both',
    )
    args = parser.parse_args(argv)
    if args.add_missing_en:
        args.mode = SYNC_MODE_BOTH
    return args


def syncs_pl_from_en(mode: str) -> bool:
    return mode in (SYNC_MODE_BOTH, SYNC_MODE_PL_FROM_EN)


def syncs_en_from_pl(mode: str) -> bool:
    return mode in (SYNC_MODE_BOTH, SYNC_MODE_EN_FROM_PL)


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
    def __init__(self, project: Project, sync_mode: str = SYNC_MODE_BOTH):
        self.project: Project = project
        self.sync_mode = sync_mode
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
                    if syncs_en_from_pl(self.sync_mode):
                        en_file = self.create_en_analog(relative_file)
                        self.created_files.append(en_file)
                    elif syncs_pl_from_en(self.sync_mode):
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

    def create_en_analog(self, pl_relative_file: RelativeFile) -> FluentFile:
        pl_file: FluentFile = pl_relative_file.file
        pl_file_data = pl_file.read_data()
        en_file_path = pl_file.full_path.replace('pl-PL', 'en-US')
        en_file = FluentFile(en_file_path)
        en_file.save_data(pl_file_data)

        logging.info(f'Utworzono plik {en_file_path} z tłumaczeniami z polskiego pliku')

        return en_file

    def warn_en_analog_not_exist(self, pl_relative_file: RelativeFile):
        file: FluentFile = pl_relative_file.file
        en_file_path = file.full_path.replace('pl-PL', 'en-US')

        logging.warning(f'Plik {file.full_path} nie ma angielskiego odpowiednika pod ścieżką {en_file_path}')


class KeyFinder:
    def __init__(self, files_dict, sync_mode: str = SYNC_MODE_BOTH):
        self.files_dict = files_dict
        self.sync_mode = sync_mode
        self.changed_files: typing.List[FluentFile] = []
        self.pl_global_keys = set()
        self.en_global_keys = set()
        self._collect_global_keys()

    @staticmethod
    def _collect_keys_by_id(parsed: ast.Resource) -> typing.Dict[str, typing.Union[ast.Message, ast.Term]]:
        keys: typing.Dict[str, typing.Union[ast.Message, ast.Term]] = {}
        for element in parsed.body:
            if not isinstance(element, ENTRY_TYPES):
                continue
            key_name = FluentAstAbstract.get_id_name(element)
            if key_name:
                keys[key_name] = element
        return keys

    @staticmethod
    def _indent_attribute_snippet(snippet: str) -> str:
        lines = snippet.split('\n')
        return '\n'.join(
            f'  {line}' if line.startswith('.') and not line.startswith('  ') else line
            for line in lines
        )

    @staticmethod
    def _extract_span_text(source: str, element) -> str:
        if element.span:
            return source[element.span.start:element.span.end]
        return FluentSerializer(with_junk=True).serialize(ast.Resource(body=[element]))

    def _collect_global_keys(self):
        for pair in self.files_dict:
            for relative_file in self.files_dict[pair]:
                if relative_file.locale not in ('pl-PL', 'en-US'):
                    continue
                try:
                    parsed = relative_file.file.parse_data(relative_file.file.read_data())
                except Exception:
                    continue
                target = self.pl_global_keys if relative_file.locale == 'pl-PL' else self.en_global_keys
                for element in parsed.body:
                    if not isinstance(element, ENTRY_TYPES):
                        continue
                    key_name = FluentAstAbstract.get_id_name(element)
                    if key_name:
                        target.add(key_name)

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
        en_text = en_file.read_data()
        pl_text = pl_file.read_data()
        en_parsed = en_file.parse_data(en_text)
        pl_parsed = pl_file.parse_data(pl_text)

        if syncs_pl_from_en(self.sync_mode):
            self._sync_missing_entries(
                source_text=en_text,
                source_parsed=en_parsed,
                target_file=pl_file,
                target_text=pl_text,
                target_keys=self._collect_keys_by_id(pl_parsed),
                global_keys=self.pl_global_keys,
            )

        if syncs_en_from_pl(self.sync_mode):
            self._sync_missing_entries(
                source_text=pl_text,
                source_parsed=pl_parsed,
                target_file=en_file,
                target_text=en_text,
                target_keys=self._collect_keys_by_id(en_parsed),
                global_keys=self.en_global_keys,
            )
        elif syncs_pl_from_en(self.sync_mode):
            self.log_not_exist_en_files(en_file, pl_parsed, en_parsed)

    def _sync_missing_entries(
        self,
        source_text: str,
        source_parsed: ast.Resource,
        target_file: FluentFile,
        target_text: str,
        target_keys: typing.Dict[str, typing.Union[ast.Message, ast.Term]],
        global_keys: typing.Set[str],
    ):
        append_snippets: typing.List[str] = []
        insertions: typing.List[typing.Tuple[int, str]] = []
        added_keys: typing.List[str] = []

        for source_entry in source_parsed.body:
            if not isinstance(source_entry, ENTRY_TYPES):
                continue

            key_name = FluentAstAbstract.get_id_name(source_entry)
            if not key_name:
                continue

            target_entry = target_keys.get(key_name)
            if target_entry is None:
                if key_name in global_keys:
                    continue
                append_snippets.append(self._extract_span_text(source_text, source_entry))
                target_keys[key_name] = source_entry
                global_keys.add(key_name)
                added_keys.append(key_name)
                continue

            source_attrs = getattr(source_entry, 'attributes', None) or []
            if not source_attrs:
                continue

            target_attr_names = {attr.id.name for attr in (getattr(target_entry, 'attributes', None) or [])}
            missing_attrs = [attr for attr in source_attrs if attr.id.name not in target_attr_names]
            if not missing_attrs:
                continue

            if not target_entry.span:
                continue
            attr_snippets = [
                self._indent_attribute_snippet(self._extract_span_text(source_text, attr))
                for attr in missing_attrs
            ]
            insertions.append((target_entry.span.end, '\n' + '\n'.join(attr_snippets)))
            added_keys.append(f'{key_name} ({", ".join(attr.id.name for attr in missing_attrs)})')

        if not append_snippets and not insertions:
            return

        new_target_text = target_text
        for position, text in sorted(insertions, key=lambda item: item[0], reverse=True):
            new_target_text = new_target_text[:position] + text + new_target_text[position:]

        if append_snippets:
            new_target_text = new_target_text.rstrip('\n')
            blocks = [new_target_text] + [snippet.strip('\n') for snippet in append_snippets]
            new_target_text = '\n\n'.join(block for block in blocks if block) + '\n'

        target_file.save_data(new_target_text)
        for key_label in added_keys:
            logging.info(f'Do pliku {target_file.full_path} dodano klucz "{key_label}"')
        if target_file not in self.changed_files:
            self.changed_files.append(target_file)

    def log_not_exist_en_files(self, en_file, pl_file_parsed, en_file_parsed):
        en_keys = self._collect_keys_by_id(en_file_parsed)
        for pl_entry in pl_file_parsed.body:
            if not isinstance(pl_entry, ENTRY_TYPES):
                continue
            key_name = FluentAstAbstract.get_id_name(pl_entry)
            if key_name and key_name not in en_keys:
                logging.warning(
                    f'Klucz "{key_name}" nie ma angielskiego odpowiednika pod ścieżką {en_file.full_path}"'
                )

######################################## Var definitions ###############################################################

logging.basicConfig(level = logging.INFO)
project = Project()

########################################################################################################################

def main(argv=None):
    args = parse_args(argv)
    sync_mode = args.mode
    print(f'Tryb synchronizacji: {sync_mode}')

    files_finder = FilesFinder(project, sync_mode=sync_mode)
    print('Sprawdzam aktualności plików ...')
    created_files = files_finder.execute()
    if len(created_files):
        print('Formatuję utworzone pliki ...')
        FluentFormatter.format(created_files)
    print('Sprawdzam aktualność kluczy ...')
    key_finder = KeyFinder(files_finder.get_files_pars(), sync_mode=sync_mode)
    changed_files = key_finder.execute()
    if len(changed_files):
        print(f'Zaktualizowano {len(changed_files)} plików (bez przeformatowania).')


if __name__ == '__main__':
    main()
