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

# Formatator, dopasowujący pliki fluent (.ftl) do wytycznych stylu
# path - ścieżka do katalogu zawierającego pliki do formatowania. Aby sformatować cały projekt, zastąp tę wartość root_dir_path
import typing

from file import FluentFile
from project import Project
from fluent.syntax import ast, FluentParser, FluentSerializer


######################################### Class defifitions ############################################################

class FluentFormatter:
    @classmethod
    def format(cls, fluent_files: typing.List[FluentFile]):
        for file in fluent_files:
            file_data = file.read_data()
            parsed_file_data = file.parse_data(file_data)
            serialized_file_data = file.serialize_data(parsed_file_data)
            file.save_data(serialized_file_data)

    @classmethod
    def format_serialized_file_data(cls, file_data: typing.AnyStr):
        parsed_data = FluentParser().parse(file_data)

        return FluentSerializer(with_junk=True).serialize(parsed_data)



######################################## Var definitions ###############################################################
project = Project()
fluent_files = project.get_fluent_files_by_dir(project.pl_locale_dir_path)

########################################################################################################################

FluentFormatter.format(fluent_files)
