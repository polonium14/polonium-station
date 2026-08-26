#!/usr/bin/env python3
"""Przenosi istniejące klucze Fluent na kanoniczną ścieżkę zamiast kopiować z drugiej locale."""

from __future__ import annotations

import os
import re
from collections import defaultdict
from typing import Dict, Iterable, List, Optional, Set, Tuple, Union

from fluent.syntax import ast, FluentParser, FluentSerializer

from file import FluentFile
from fluentast import FluentAstAbstract

ENTRY_TYPES = (ast.Message, ast.Term)
PARSER = FluentParser()
SERIALIZER = FluentSerializer(with_junk=True)
SIMPLE_KEY_RE = re.compile(r'^(-?[A-Za-z][A-Za-z0-9_-]*)\s*=', re.MULTILINE)
SKIP_DIR_NAMES = {'datasets', '.git', 'bin', 'obj'}


def same_path(left: str, right: str) -> bool:
    # na windows _RMC14 i _rmc14 to ten sam plik
    return os.path.normcase(os.path.normpath(left)) == os.path.normcase(os.path.normpath(right))


def _cut_span(text: str, start: int, end: int) -> str:
    after = text[end:]
    if after.startswith('\n'):
        after = after[1:]
    return text[:start] + after


def extract_span_text(source: str, element) -> str:
    if element.span:
        return source[element.span.start:element.span.end]
    return SERIALIZER.serialize(ast.Resource(body=[element]))


def collect_keys_by_id(parsed: ast.Resource) -> Dict[str, Union[ast.Message, ast.Term]]:
    keys: Dict[str, Union[ast.Message, ast.Term]] = {}
    for element in parsed.body:
        if not isinstance(element, ENTRY_TYPES):
            continue
        name = FluentAstAbstract.get_id_name(element)
        if name:
            keys[name] = element
    return keys


def collect_message_keys_fast(content: str) -> Set[str]:
    if not content.strip():
        return set()
    return set(SIMPLE_KEY_RE.findall(content))


def _pattern_fingerprint(value) -> Optional[str]:
    if value is None:
        return None
    dummy = ast.Message(id=ast.Identifier('_fp'), value=value)
    return SERIALIZER.serialize(ast.Resource(body=[dummy])).strip()


def fingerprint_entry(entry: Union[ast.Message, ast.Term]) -> Tuple:
    name = FluentAstAbstract.get_id_name(entry)
    attrs = tuple(sorted(
        (attr.id.name, _pattern_fingerprint(attr.value))
        for attr in (getattr(entry, 'attributes', None) or [])
    ))
    return (name, _pattern_fingerprint(getattr(entry, 'value', None)), attrs)


def messages_equivalent(left: str, right: str) -> bool:
    if left == right:
        return True
    left_keys = collect_keys_by_id(PARSER.parse(left)) if left.strip() else {}
    right_keys = collect_keys_by_id(PARSER.parse(right)) if right.strip() else {}
    if left_keys.keys() != right_keys.keys():
        return False
    for key, entry in left_keys.items():
        if fingerprint_entry(entry) != fingerprint_entry(right_keys[key]):
            return False
    return True


def _snippet_fingerprint(snippet: str, key: str):
    if not snippet.strip():
        return None
    parsed = PARSER.parse(snippet)
    entry = collect_keys_by_id(parsed).get(key)
    if entry is None:
        return None
    return fingerprint_entry(entry)


def _append_entries(content: str, entries: Dict[str, str]) -> str:
    extra = '\n\n'.join(entries[key].strip('\n') for key in sorted(entries))
    if not content.strip():
        return extra + '\n'
    if not content.endswith('\n'):
        content += '\n'
    if not content.endswith('\n\n'):
        content += '\n'
    return content + extra + '\n'


def extract_entries_by_keys(content: str, keys: Iterable[str]) -> Dict[str, str]:
    wanted = set(keys)
    if not content.strip() or not wanted:
        return {}
    parsed = PARSER.parse(content)
    result: Dict[str, str] = {}
    for element in parsed.body:
        if not isinstance(element, ENTRY_TYPES):
            continue
        name = FluentAstAbstract.get_id_name(element)
        if name in wanted:
            result[name] = extract_span_text(content, element).strip('\n')
    return result


def remove_keys_from_content(content: str, keys: Iterable[str]) -> Tuple[str, int]:
    wanted = set(keys)
    if not wanted or not content.strip():
        return content, 0
    parsed = PARSER.parse(content)
    spans: List[Tuple[int, int]] = []
    for element in parsed.body:
        if not isinstance(element, ENTRY_TYPES) or not element.span:
            continue
        name = FluentAstAbstract.get_id_name(element)
        if name in wanted:
            spans.append((element.span.start, element.span.end))
    if not spans:
        return content, 0
    removed_from_start = any(start == 0 for start, _end in spans)
    for start, end in sorted(spans, key=lambda item: item[0], reverse=True):
        content = _cut_span(content, start, end)
    if removed_from_start:
        content = content.lstrip('\n')
    return content, len(spans)


def upsert_entries(content: str, entries: Dict[str, str], overwrite: bool = True) -> Tuple[str, int]:
    if not entries:
        return content, 0

    changed = 0
    existing = collect_keys_by_id(PARSER.parse(content)) if content.strip() else {}
    text_keys = collect_message_keys_fast(content)
    to_replace = {k: v for k, v in entries.items() if k in existing} if overwrite else {}
    to_append = {
        k: v for k, v in entries.items()
        if k not in existing and k not in text_keys
    }

    if to_replace:
        parsed = PARSER.parse(content)
        spans_and_text: List[Tuple[int, int, str]] = []
        for element in parsed.body:
            if not isinstance(element, ENTRY_TYPES) or not element.span:
                continue
            name = FluentAstAbstract.get_id_name(element)
            if name not in to_replace:
                continue
            new_text = to_replace[name]
            new_fp = _snippet_fingerprint(new_text, name)
            if new_fp is not None and new_fp == fingerprint_entry(element):
                continue
            spans_and_text.append((element.span.start, element.span.end, new_text))
            changed += 1
        for start, end, text in sorted(spans_and_text, key=lambda item: item[0], reverse=True):
            content = content[:start] + text + content[end:]

    if to_append:
        content = _append_entries(content, to_append)
        changed += len(to_append)

    return content, changed


def _delete_if_empty(path: str) -> None:
    if not os.path.isfile(path):
        return
    try:
        content = FluentFile(path).read_data()
    except OSError:
        return
    if content.strip():
        return
    os.remove(path)


class LocaleKeyIndex:
    """Gdzie w locale siedzi który klucz. datasets omijamy bo to 300k imion."""

    def __init__(self, locale_root: str):
        self.locale_root = os.path.normpath(locale_root)
        self.key_to_files: Dict[str, Set[str]] = defaultdict(set)
        self._build()

    def _build(self) -> None:
        if not os.path.isdir(self.locale_root):
            return
        for dirpath, dirnames, filenames in os.walk(self.locale_root):
            dirnames[:] = [d for d in dirnames if d not in SKIP_DIR_NAMES]
            for filename in filenames:
                if not filename.endswith('.ftl'):
                    continue
                file_path = os.path.normpath(os.path.join(dirpath, filename))
                try:
                    content = FluentFile(file_path).read_data()
                except OSError:
                    continue
                for key in collect_message_keys_fast(content):
                    self.key_to_files[key].add(file_path)

    def locations(self, key: str) -> Set[str]:
        return set(self.key_to_files.get(key, ()))

    def has(self, key: str) -> bool:
        return bool(self.key_to_files.get(key))

    def move_keys_to(
        self,
        canonical_path: str,
        keys: Iterable[str],
        overwrite: bool = True,
    ) -> int:
        """Wycina klucze z cudzych plików i wsadza je do canonical_path."""
        canonical_path = os.path.normpath(canonical_path)
        wanted = {key for key in keys if key}
        if not wanted:
            return 0

        by_source: Dict[str, Set[str]] = defaultdict(set)
        for key in wanted:
            for src in self.locations(key):
                if not same_path(src, canonical_path):
                    by_source[src].add(key)

        if not by_source:
            return 0

        if os.path.isfile(canonical_path):
            canonical_content = FluentFile(canonical_path).read_data()
        else:
            canonical_content = ''

        moved = 0
        wrote_canonical = False
        for src, src_keys in by_source.items():
            try:
                src_content = FluentFile(src).read_data()
            except OSError:
                continue
            extracted = extract_entries_by_keys(src_content, src_keys)
            if extracted:
                canonical_content, changed = upsert_entries(
                    canonical_content, extracted, overwrite=overwrite,
                )
                moved += changed
                if changed:
                    wrote_canonical = True
            new_src, removed = remove_keys_from_content(src_content, src_keys)
            if removed:
                FluentFile(src).save_data(new_src)
                _delete_if_empty(src)
            for key in src_keys:
                self.key_to_files[key].discard(src)
                self.key_to_files[key].add(canonical_path)

        if wrote_canonical:
            FluentFile(canonical_path).save_data(canonical_content)

        return moved
