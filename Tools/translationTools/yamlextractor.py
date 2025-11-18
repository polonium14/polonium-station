#!/usr/bin/env python3

# Taken from Corvax - https://github.com/space-syndicate/space-station-14/tree/master/Tools/ss14_ru

# generuje pliki do folderu Resources/Locale/en-US/prototypes/generated i Resources/Locale/pl-PL/prototypes/generated

import os

from fluent.syntax.parser import FluentParser
from fluent.syntax.serializer import FluentSerializer

from file import YAMLFile, FluentFile
from fluentast import FluentSerializedMessage, FluentAstAttributeFactory
from fluentformatter import FluentFormatter
from project import Project
import logging

######################################### Class defifitions ############################################################
class YAMLExtractor:
    def __init__(self, yaml_files):
        self.yaml_files = yaml_files

    def execute(self):
        for yaml_file in self.yaml_files:
            yaml_elements = yaml_file.get_elements(yaml_file.parse_data(yaml_file.read_data()))

            if not len(yaml_elements):
                continue

            fluent_file_serialized = self.get_serialized_fluent_from_yaml_elements(yaml_elements)

            if not fluent_file_serialized:
                continue

            pretty_fluent_file_serialized = formatter.format_serialized_file_data(fluent_file_serialized)

            relative_parent_dir = yaml_file.get_relative_parent_dir(project.prototypes_dir_path).lower()
            file_name = yaml_file.get_name()

            en_fluent_file_path = self.create_en_fluent_file(relative_parent_dir, file_name, pretty_fluent_file_serialized)
            pl_fluent_file_path = self.create_pl_fluent_file_from_en(en_fluent_file_path)

    @classmethod
    def serialize_yaml_element(cls, element):
        parent_id = element.parent_id
        if isinstance(parent_id, list):
            parent_id = parent_id[0] if parent_id else 'None'

        message = FluentSerializedMessage.from_yaml_element(
            element.id, element.name,
            FluentAstAttributeFactory.from_yaml_element(element),
            parent_id
        )

        return message


    def get_serialized_fluent_from_yaml_elements(self, yaml_elements):
        fluent_serialized_messages = list(map(YAMLExtractor.serialize_yaml_element, yaml_elements))
        fluent_exist_serialized_messages = list(filter(lambda m: m, fluent_serialized_messages))

        if not len(fluent_exist_serialized_messages):
            return None

        return '\n'.join(fluent_exist_serialized_messages)

    def create_en_fluent_file(self, relative_parent_dir, file_name, file_data):
        en_new_dir_path = os.path.join(project.en_locale_prototypes_dir_path, relative_parent_dir)
        en_fluent_file = FluentFile(os.path.join(en_new_dir_path, f'{file_name}.ftl'))
        en_fluent_file.save_data(file_data)

        return en_fluent_file.full_path

    def create_pl_fluent_file_from_en(self, en_analog_file_path):
        pl_file_full_path = en_analog_file_path.replace('en-US', 'pl-PL')

        if os.path.isfile(pl_file_full_path):
            return
        else:
            en_file = FluentFile(f'{en_analog_file_path}')
            file = FluentFile(f'{pl_file_full_path}')
            file.save_data(en_file.read_data())
            logging.info(f'Utworzono plik z polskim tłumaczeniem: {pl_file_full_path}')

        return pl_file_full_path



######################################## Var definitions ###############################################################

logging.basicConfig(level = logging.INFO)
project = Project()
serializer = FluentSerializer()
parser = FluentParser()
formatter = FluentFormatter()

yaml_files_paths = project.get_files_paths_by_dir(project.prototypes_dir_path, 'yml')
yaml_files = list(map(lambda yaml_file_path: YAMLFile(yaml_file_path), yaml_files_paths))

########################################################################################################################

logging.info(f'Szukam plików YAML ...')
YAMLExtractor(yaml_files).execute()
