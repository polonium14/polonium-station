#!/usr/bin/env python3

# Taken from Corvax - https://github.com/space-syndicate/space-station-14/tree/master/Tools/ss14_ru

# generuje pliki do folderu Resources/Locale/en-US/prototypes/generated i Resources/Locale/pl-PL/prototypes/generated

import os
import re
import typing

from fluent.syntax import ast
from fluent.syntax.parser import FluentParser
from fluent.syntax.serializer import FluentSerializer

from file import YAMLFile, FluentFile
from fluentast import FluentSerializedMessage, FluentAstAttributeFactory, FluentAstAbstract
from fluentformatter import FluentFormatter
from project import Project
import logging

ENTRY_TYPES = (ast.Message, ast.Term)
PRESERVED_ATTRIBUTE_NAMES = frozenset({'gender'})
_MESSAGE_KEY_RE = re.compile(r'^(ent-[^\s=]+|-[^\s=]+)\s*=', re.MULTILINE)


def _collect_keys_by_id(parsed: ast.Resource) -> typing.Dict[str, typing.Union[ast.Message, ast.Term]]:
    keys: typing.Dict[str, typing.Union[ast.Message, ast.Term]] = {}
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


def preserve_existing_attributes(new_text: str, old_text: str, attribute_names: typing.AbstractSet[str]) -> str:
    """Zachowuje wskazane atrybuty (np. .gender) z istniejącego pliku FTL przy regeneracji z YAML."""
    if not old_text.strip() or not new_text.strip():
        return new_text

    old_parsed = parser.parse(old_text)
    new_parsed = parser.parse(new_text)
    old_keys = _collect_keys_by_id(old_parsed)
    new_keys = _collect_keys_by_id(new_parsed)

    insertions: typing.List[typing.Tuple[int, str]] = []
    for key_name, new_entry in new_keys.items():
        old_entry = old_keys.get(key_name)
        if not old_entry:
            continue

        new_attr_names = {attr.id.name for attr in (getattr(new_entry, 'attributes', None) or [])}
        missing_attrs = [
            attr for attr in (getattr(old_entry, 'attributes', None) or [])
            if attr.id.name in attribute_names and attr.id.name not in new_attr_names
        ]
        if not missing_attrs or not new_entry.span:
            continue

        attr_snippets = [
            _indent_attribute_snippet(_extract_span_text(old_text, attr))
            for attr in missing_attrs
        ]
        insertions.append((new_entry.span.end, '\n' + '\n'.join(attr_snippets)))

    if not insertions:
        return new_text

    merged_text = new_text
    for position, text in sorted(insertions, key=lambda item: item[0], reverse=True):
        merged_text = merged_text[:position] + text + merged_text[position:]

    return merged_text


def _collect_message_keys_fast(content: str) -> typing.Set[str]:
    if not content.strip():
        return set()
    return set(_MESSAGE_KEY_RE.findall(content))


def _cut_span(text: str, start: int, end: int) -> str:
    after = text[end:]
    if after.startswith('\n'):
        after = after[1:]
    return text[:start] + after


def _remove_spans(content: str, spans: typing.List[typing.Tuple[int, int]]) -> str:
    for start, end in sorted(spans, key=lambda item: item[0], reverse=True):
        content = _cut_span(content, start, end)
    return content


def _remove_keys_from_content(content: str, keys: typing.AbstractSet[str]) -> typing.Tuple[str, int]:
    if not keys or not content.strip():
        return content, 0

    parsed = parser.parse(content)
    spans = []
    for element in parsed.body:
        if not isinstance(element, ENTRY_TYPES):
            continue
        key_name = FluentAstAbstract.get_id_name(element)
        if key_name in keys and element.span:
            spans.append((element.span.start, element.span.end))

    if not spans:
        return content, 0

    return _remove_spans(content, spans), len(spans)


class LocaleKeyRegistry:
    """Indeks kluczy ent-* w generated — unika pełnego skanowania przy każdym pliku YAML."""

    def __init__(self, locale_generated_root: str):
        self.locale_generated_root = locale_generated_root
        self.key_to_file: typing.Dict[str, str] = {}
        self._pending_removals: typing.Dict[str, typing.Set[str]] = {}
        self._build_index()

    def _build_index(self) -> None:
        if not os.path.isdir(self.locale_generated_root):
            return

        for dirpath, _, filenames in os.walk(self.locale_generated_root):
            for filename in filenames:
                if not filename.endswith('.ftl'):
                    continue

                file_path = os.path.normpath(os.path.join(dirpath, filename))
                try:
                    content = FluentFile(file_path).read_data()
                except OSError:
                    continue

                for key in _collect_message_keys_fast(content):
                    self.key_to_file[key] = file_path

    def claim_keys(self, canonical_path: str, keys: typing.AbstractSet[str]) -> None:
        if not keys:
            return

        canonical_path = os.path.normpath(canonical_path)
        for key in keys:
            stale_path = self.key_to_file.get(key)
            if stale_path and stale_path != canonical_path:
                self._pending_removals.setdefault(stale_path, set()).add(key)
            self.key_to_file[key] = canonical_path

    def flush(self) -> int:
        removed_total = 0

        for file_path, keys in self._pending_removals.items():
            if not keys or not os.path.isfile(file_path):
                continue

            fluent_file = FluentFile(file_path)
            content = fluent_file.read_data()
            new_content, removed = _remove_keys_from_content(content, keys)
            if not removed:
                continue

            fluent_file.save_data(new_content)
            removed_total += removed

        self._pending_removals.clear()
        return removed_total


######################################### Class defifitions ############################################################
class YAMLExtractor:
    def __init__(self, yaml_files):
        self.yaml_files = yaml_files
        self.en_registry = LocaleKeyRegistry(project.en_locale_prototypes_dir_path)
        self.pl_registry = LocaleKeyRegistry(project.pl_locale_prototypes_dir_path)

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
            self.create_pl_fluent_file_from_en(en_fluent_file_path)

        removed_en = self.en_registry.flush()
        removed_pl = self.pl_registry.flush()
        if removed_en or removed_pl:
            logging.info(
                f'Usunięto zduplikowane klucze: en-US={removed_en}, pl-PL={removed_pl}'
            )

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

    def _publish_canonical_keys(self, en_canonical_path: str, file_data: str) -> None:
        keys = _collect_message_keys_fast(file_data)
        if not keys:
            return

        self.en_registry.claim_keys(en_canonical_path, keys)
        pl_canonical_path = en_canonical_path.replace(
            project.en_locale_dir_path, project.pl_locale_dir_path,
        )
        self.pl_registry.claim_keys(pl_canonical_path, keys)

    def create_en_fluent_file(self, relative_parent_dir, file_name, file_data):
        en_new_dir_path = os.path.join(project.en_locale_prototypes_dir_path, relative_parent_dir)
        en_fluent_file = FluentFile(os.path.join(en_new_dir_path, f'{file_name}.ftl'))

        if os.path.isfile(en_fluent_file.full_path):
            existing_data = en_fluent_file.read_data()
            file_data = preserve_existing_attributes(file_data, existing_data, PRESERVED_ATTRIBUTE_NAMES)
            if file_data == existing_data:
                self._publish_canonical_keys(en_fluent_file.full_path, file_data)
                return en_fluent_file.full_path

        en_fluent_file.save_data(file_data)
        self._publish_canonical_keys(en_fluent_file.full_path, file_data)

        return en_fluent_file.full_path

    @staticmethod
    def _is_missing_or_empty_ftl(file_path: str) -> bool:
        if not os.path.isfile(file_path):
            return True
        return not FluentFile(file_path).read_data().strip()

    def create_pl_fluent_file_from_en(self, en_analog_file_path):
        pl_file_full_path = en_analog_file_path.replace('en-US', 'pl-PL')

        if not self._is_missing_or_empty_ftl(pl_file_full_path):
            return

        en_file = FluentFile(f'{en_analog_file_path}')
        pl_data = en_file.read_data()
        if not pl_data.strip():
            return

        file = FluentFile(f'{pl_file_full_path}')
        file.save_data(pl_data)
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

if __name__ == '__main__':
    logging.info('Szukam plików YAML ...')
    YAMLExtractor(yaml_files).execute()
