#!/usr/bin/env python3

# Taken from Corvax - https://github.com/space-syndicate/space-station-14/tree/master/Tools/ss14_ru

# generuje pliki do folderu Resources/Locale/en-US/prototypes/generated i Resources/Locale/pl-PL/prototypes/generated

import os
import re
import typing
from collections import defaultdict

from fluent.syntax import ast
from fluent.syntax.parser import FluentParser
from fluent.syntax.serializer import FluentSerializer

from ftl_relocator import fingerprint_entry, messages_equivalent, same_path, upsert_entries as reloc_upsert_entries, remove_keys_from_content as reloc_remove_keys
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
    out = []
    for line in lines:
        stripped = line.lstrip(' ')
        if stripped.startswith('.') and not line.startswith('    .'):
            out.append('    ' + stripped)
        else:
            out.append(line)
    return '\n'.join(out)


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
    return reloc_remove_keys(content, keys)


def _pattern_value_text(value) -> str:
    if value is None:
        return ''
    return FluentSerializer(with_junk=True).serialize(ast.Resource(body=[
        ast.Message(id=ast.Identifier('__tmp'), value=value)
    ])).replace('__tmp = ', '', 1).strip()


def _entry_fingerprint(entry: typing.Union[ast.Message, ast.Term]) -> str:
    parts = [_pattern_value_text(getattr(entry, 'value', None))]
    for attr in getattr(entry, 'attributes', None) or []:
        if attr.id.name == 'gender':
            continue
        parts.append(f'{attr.id.name}={_pattern_value_text(attr.value)}')
    return '\n'.join(parts)


def detect_key_renames(old_text: str, new_text: str) -> typing.Dict[str, str]:
    """Mapuje stary_klucz -> nowy_klucz gdy angielska treść (name/desc) jest identyczna."""
    if not old_text.strip() or not new_text.strip():
        return {}

    old_keys = _collect_keys_by_id(parser.parse(old_text))
    new_keys = _collect_keys_by_id(parser.parse(new_text))
    removed = set(old_keys) - set(new_keys)
    added = set(new_keys) - set(old_keys)
    if not removed or not added:
        return {}

    old_by_fp: typing.Dict[str, typing.List[str]] = {}
    for key in removed:
        fp = _entry_fingerprint(old_keys[key])
        if not fp.strip():
            continue
        old_by_fp.setdefault(fp, []).append(key)

    new_by_fp: typing.Dict[str, typing.List[str]] = {}
    for key in added:
        fp = _entry_fingerprint(new_keys[key])
        if not fp.strip():
            continue
        new_by_fp.setdefault(fp, []).append(key)

    renames: typing.Dict[str, str] = {}
    for fp, old_list in old_by_fp.items():
        new_list = new_by_fp.get(fp)
        if len(old_list) == 1 and new_list and len(new_list) == 1:
            renames[old_list[0]] = new_list[0]
    return renames


def _extract_entries_by_keys(
    content: str, keys: typing.AbstractSet[str]
) -> typing.Dict[str, str]:
    if not content.strip() or not keys:
        return {}
    parsed = parser.parse(content)
    result: typing.Dict[str, str] = {}
    for element in parsed.body:
        if not isinstance(element, ENTRY_TYPES):
            continue
        key_name = FluentAstAbstract.get_id_name(element)
        if key_name in keys:
            result[key_name] = _extract_span_text(content, element).strip('\n')
    return result


def upsert_entries(content: str, entries: typing.Dict[str, str]) -> typing.Tuple[str, int]:
    """Wstawia/nadpisuje bloki wiadomości; zwraca (nowa_treść, liczba_zmian)."""
    return reloc_upsert_entries(content, entries, overwrite=True)


def _patch_existing_with_generated(existing: str, generated: str) -> str:
    # nie przepisuj calego pliku - tylko klucze ktore sie faktycznie zmienily
    gen_parsed = parser.parse(generated)
    exist_keys = _collect_keys_by_id(parser.parse(existing)) if existing.strip() else {}
    existing_text_keys = _collect_message_keys_fast(existing)
    to_upsert: typing.Dict[str, str] = {}
    for element in gen_parsed.body:
        if not isinstance(element, ENTRY_TYPES):
            continue
        name = FluentAstAbstract.get_id_name(element)
        if not name:
            continue
        old = exist_keys.get(name)
        if old is None:
            if name in existing_text_keys:
                continue
            to_upsert[name] = _extract_span_text(generated, element).strip('\n')
            continue
        if fingerprint_entry(element) != fingerprint_entry(old):
            to_upsert[name] = _extract_span_text(generated, element).strip('\n')
    if not to_upsert:
        return existing
    patched, _ = upsert_entries(existing, to_upsert)
    return patched


def apply_key_renames_to_content(content: str, renames: typing.Dict[str, str]) -> typing.Tuple[str, int]:
    """Zmienia identyfikatory wiadomości old->new, zachowując polską treść."""
    if not renames or not content.strip():
        return content, 0

    parsed = parser.parse(content)
    keys = _collect_keys_by_id(parsed)
    applied = 0
    # Najpierw zbierz treści starych kluczy, potem usuń stare i upsert pod nowymi ID
    migrate: typing.Dict[str, str] = {}
    remove_keys: typing.Set[str] = set()

    for old_key, new_key in renames.items():
        old_entry = keys.get(old_key)
        if not old_entry or not old_entry.span:
            continue
        if new_key in keys:
            # Nowy klucz już jest — jeśli wygląda na angielski stub, nadpisz polskim
            # Zawsze preferuj istniejącą polską treść ze starego klucza
            pass
        snippet = _extract_span_text(content, old_entry).strip('\n')
        # Podmień identyfikator w pierwszej linii bloku
        lines = snippet.split('\n')
        if not lines:
            continue
        if old_key.startswith('-'):
            # term: -name
            lines[0] = re.sub(
                rf'^-{re.escape(old_key[1:])}\b',
                f'-{new_key[1:]}' if new_key.startswith('-') else f'-{new_key}',
                lines[0],
                count=1,
            )
        else:
            lines[0] = re.sub(
                rf'^{re.escape(old_key)}\b',
                new_key,
                lines[0],
                count=1,
            )
        migrate[new_key] = '\n'.join(lines)
        remove_keys.add(old_key)
        applied += 1

    if not applied:
        return content, 0

    content, _ = _remove_keys_from_content(content, remove_keys)
    content, _ = upsert_entries(content, migrate)
    return content, applied


class LocaleKeyRegistry:
    """Indeks kluczy w całej locale — generated to tylko kanoniczna ścieżka po YAML."""

    def __init__(self, locale_generated_root: str, locale_root: typing.Optional[str] = None):
        self.locale_generated_root = locale_generated_root
        self.locale_root = locale_root or locale_generated_root
        self.key_to_files: typing.Dict[str, typing.Set[str]] = defaultdict(set)
        self.key_to_file: typing.Dict[str, str] = {}
        self.claimed_keys: typing.Set[str] = set()
        self._pending_removals: typing.Dict[str, typing.Set[str]] = {}
        self._build_index()

    def _build_index(self) -> None:
        if not os.path.isdir(self.locale_root):
            return

        skip = {'datasets', '.git', 'bin', 'obj'}
        for dirpath, dirnames, filenames in os.walk(self.locale_root):
            dirnames[:] = [d for d in dirnames if d not in skip]
            for filename in filenames:
                if not filename.endswith('.ftl'):
                    continue

                file_path = os.path.normpath(os.path.join(dirpath, filename))
                try:
                    content = FluentFile(file_path).read_data()
                except OSError:
                    continue

                for key in _collect_message_keys_fast(content):
                    self.key_to_files[key].add(file_path)
                    self.key_to_file[key] = file_path

    def claim_keys(self, canonical_path: str, keys: typing.AbstractSet[str]) -> None:
        if not keys:
            return

        canonical_path = os.path.normpath(canonical_path)
        for key in keys:
            for stale_path in self.key_to_files.get(key, set()):
                if not same_path(stale_path, canonical_path):
                    self._pending_removals.setdefault(stale_path, set()).add(key)
            self.key_to_files[key] = {canonical_path}
            self.key_to_file[key] = canonical_path
            self.claimed_keys.add(key)

    def flush(self, migrate: bool = False) -> int:
        removed_total = 0
        migrated_total = 0

        for file_path, keys in self._pending_removals.items():
            if not keys or not os.path.isfile(file_path):
                continue

            fluent_file = FluentFile(file_path)
            content = fluent_file.read_data()

            if migrate:
                extracted = _extract_entries_by_keys(content, keys)
                by_canonical: typing.Dict[str, typing.Dict[str, str]] = {}
                for key, snippet in extracted.items():
                    canonical = self.key_to_file.get(key)
                    if not canonical or same_path(canonical, file_path):
                        continue
                    by_canonical.setdefault(canonical, {})[key] = snippet

                for canonical, entries in by_canonical.items():
                    canonical_file = FluentFile(canonical)
                    if os.path.isfile(canonical):
                        canonical_content = canonical_file.read_data()
                    else:
                        canonical_content = ''
                    new_canonical, changed = upsert_entries(canonical_content, entries)
                    if changed:
                        canonical_file.save_data(new_canonical)
                        migrated_total += changed

            keys_to_remove = {
                key for key in keys
                if not same_path(self.key_to_file.get(key, ''), file_path)
            }
            if not keys_to_remove:
                continue

            new_content, removed = _remove_keys_from_content(content, keys_to_remove)
            if not removed:
                continue

            fluent_file.save_data(new_content)
            if not new_content.strip() and os.path.isfile(file_path):
                os.remove(file_path)
            removed_total += removed

        self._pending_removals.clear()
        if migrate and migrated_total:
            logging.info(f'Zmigrowano {migrated_total} tłumaczeń do kanonicznych ścieżek')
        return removed_total

    def prune_keys_not_in(self, keep_keys: typing.AbstractSet[str]) -> int:
        """Usuwa z generated klucze spoza keep_keys (nieużywane / usunięte prototypy)."""
        if not os.path.isdir(self.locale_generated_root):
            return 0

        removed_total = 0
        for dirpath, _, filenames in os.walk(self.locale_generated_root):
            for filename in filenames:
                if not filename.endswith('.ftl'):
                    continue
                file_path = os.path.normpath(os.path.join(dirpath, filename))
                fluent_file = FluentFile(file_path)
                try:
                    content = fluent_file.read_data()
                except OSError:
                    continue
                present = _collect_message_keys_fast(content)
                obsolete = present - keep_keys
                if not obsolete:
                    continue
                new_content, removed = _remove_keys_from_content(content, obsolete)
                if removed:
                    fluent_file.save_data(new_content)
                    removed_total += removed
                    for key in obsolete:
                        if self.key_to_file.get(key) == file_path:
                            self.key_to_file.pop(key, None)
        return removed_total


######################################### Class defifitions ############################################################
class YAMLExtractor:
    def __init__(self, yaml_files):
        self.yaml_files = yaml_files
        self.en_registry = LocaleKeyRegistry(
            project.en_locale_prototypes_dir_path, project.en_locale_dir_path,
        )
        self.pl_registry = LocaleKeyRegistry(
            project.pl_locale_prototypes_dir_path, project.pl_locale_dir_path,
        )
        self._pending_pl_renames: typing.List[typing.Tuple[str, typing.Dict[str, str]]] = []

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

        self._apply_pending_pl_renames()

        removed_en = self.en_registry.flush(migrate=False)
        removed_pl = self.pl_registry.flush(migrate=True)
        if removed_en or removed_pl:
            logging.info(
                f'Usunięto zduplikowane/przeniesione klucze: en-US={removed_en}, pl-PL={removed_pl}'
            )

        # Po ekstrakcji en-US jest źródłem prawdy dla generated — usuń nieużywane klucze z pl-PL
        keep_keys = set(self.en_registry.claimed_keys)
        pruned_pl = self.pl_registry.prune_keys_not_in(keep_keys)
        if pruned_pl:
            logging.info(f'Usunięto nieużywane klucze z pl-PL/prototypes/generated: {pruned_pl}')
        pruned_en = self.en_registry.prune_keys_not_in(keep_keys)
        if pruned_en:
            logging.info(f'Usunięto nieużywane klucze z en-US/prototypes/generated: {pruned_en}')

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
            renames = detect_key_renames(existing_data, file_data)
            if renames:
                pl_path = en_fluent_file.full_path.replace('en-US', 'pl-PL')
                self._pending_pl_renames.append((pl_path, renames))
                logging.info(
                    f'Wykryto {len(renames)} zmian kluczy w {en_fluent_file.full_path}: '
                    + ', '.join(f'{o}->{n}' for o, n in list(renames.items())[:5])
                )
            file_data = preserve_existing_attributes(file_data, existing_data, PRESERVED_ATTRIBUTE_NAMES)
            if messages_equivalent(file_data, existing_data):
                self._publish_canonical_keys(en_fluent_file.full_path, file_data)
                return en_fluent_file.full_path
            patched = _patch_existing_with_generated(existing_data, file_data)
            self._publish_canonical_keys(en_fluent_file.full_path, file_data)
            if patched != existing_data:
                en_fluent_file.save_data(patched)
            return en_fluent_file.full_path

        en_fluent_file.save_data(file_data)
        self._publish_canonical_keys(en_fluent_file.full_path, file_data)

        return en_fluent_file.full_path

    def _apply_pending_pl_renames(self) -> None:
        total = 0
        for pl_path, renames in self._pending_pl_renames:
            # Szukaj starych kluczy też w innych plikach pl (gdy ścieżka się rozjechała)
            for old_key, new_key in list(renames.items()):
                stale_path = self.pl_registry.key_to_file.get(old_key)
                if stale_path and os.path.normpath(stale_path) != os.path.normpath(pl_path):
                    if not os.path.isfile(stale_path):
                        continue
                    stale_file = FluentFile(stale_path)
                    stale_content = stale_file.read_data()
                    extracted = _extract_entries_by_keys(stale_content, {old_key})
                    if old_key not in extracted:
                        continue
                    snippet = extracted[old_key]
                    lines = snippet.split('\n')
                    if old_key.startswith('-'):
                        lines[0] = re.sub(
                            rf'^-{re.escape(old_key[1:])}\b',
                            f'-{new_key[1:]}' if new_key.startswith('-') else f'-{new_key}',
                            lines[0],
                            count=1,
                        )
                    else:
                        lines[0] = re.sub(rf'^{re.escape(old_key)}\b', new_key, lines[0], count=1)
                    target = FluentFile(pl_path)
                    target_content = target.read_data() if os.path.isfile(pl_path) else ''
                    target_content, _ = upsert_entries(target_content, {new_key: '\n'.join(lines)})
                    target.save_data(target_content)
                    stale_content, _ = _remove_keys_from_content(stale_content, {old_key})
                    stale_file.save_data(stale_content)
                    self.pl_registry.key_to_file[new_key] = os.path.normpath(pl_path)
                    self.pl_registry.key_to_file.pop(old_key, None)
                    total += 1
                    continue

            if not os.path.isfile(pl_path):
                continue
            pl_file = FluentFile(pl_path)
            content = pl_file.read_data()
            new_content, applied = apply_key_renames_to_content(content, renames)
            if applied:
                pl_file.save_data(new_content)
                for old_key, new_key in renames.items():
                    self.pl_registry.key_to_file.pop(old_key, None)
                    self.pl_registry.key_to_file[new_key] = os.path.normpath(pl_path)
                total += applied
        self._pending_pl_renames.clear()
        if total:
            logging.info(f'Zastosowano {total} zmian kluczy w pl-PL (zachowano tłumaczenia)')

    @staticmethod
    def _is_missing_or_empty_ftl(file_path: str) -> bool:
        if not os.path.isfile(file_path):
            return True
        return not FluentFile(file_path).read_data().strip()

    def create_pl_fluent_file_from_en(self, en_analog_file_path):
        pl_file_full_path = en_analog_file_path.replace('en-US', 'pl-PL')

        if not self._is_missing_or_empty_ftl(pl_file_full_path):
            return pl_file_full_path

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
