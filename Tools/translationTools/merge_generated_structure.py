#!/usr/bin/env python3
from __future__ import annotations

import os
import sys
from typing import Dict, Optional, Union

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from fluent.syntax import ast, FluentParser, FluentSerializer
from fluentast import FluentAstAbstract
from file import FluentFile
from project import Project

ENTRY_TYPES = (ast.Message, ast.Term)
parser = FluentParser()
serializer = FluentSerializer(with_junk=True)


def collect_keys(parsed: ast.Resource) -> Dict[str, Union[ast.Message, ast.Term]]:
    keys: Dict[str, Union[ast.Message, ast.Term]] = {}
    for element in parsed.body:
        if isinstance(element, ENTRY_TYPES):
            name = FluentAstAbstract.get_id_name(element)
            if name:
                keys[name] = element
    return keys


def is_literal_pattern(value) -> bool:
    if value is None:
        return False
    if not isinstance(value, ast.Pattern):
        return False
    for el in value.elements:
        if isinstance(el, ast.Placeable):
            return False
    return True


def clone_entry(entry: Union[ast.Message, ast.Term]) -> Union[ast.Message, ast.Term]:
    # Round-trip through serialize/parse for a deep copy
    snippet = serializer.serialize(ast.Resource(body=[entry]))
    parsed = parser.parse(snippet)
    for element in parsed.body:
        if isinstance(element, ENTRY_TYPES):
            return element
    raise RuntimeError('clone_entry failed')


def merge_entry(
    en_entry: Union[ast.Message, ast.Term],
    pl_entry: Optional[Union[ast.Message, ast.Term]],
) -> Union[ast.Message, ast.Term]:
    """Bierz strukturę z en; literały name/desc/.suffix itd. z pl jeśli istnieją."""
    merged = clone_entry(en_entry)

    if pl_entry is None:
        return merged

    # Wartość główna: jeśli obie literały — preferuj pl; jeśli en to ref — zostaw en
    if is_literal_pattern(getattr(en_entry, 'value', None)) and is_literal_pattern(getattr(pl_entry, 'value', None)):
        merged.value = pl_entry.value
    elif getattr(en_entry, 'value', None) is None and is_literal_pattern(getattr(pl_entry, 'value', None)):
        merged.value = pl_entry.value

    pl_attrs = {a.id.name: a for a in (getattr(pl_entry, 'attributes', None) or [])}
    new_attrs = []
    for en_attr in (getattr(en_entry, 'attributes', None) or []):
        name = en_attr.id.name
        pl_attr = pl_attrs.get(name)
        if (
            pl_attr is not None
            and is_literal_pattern(en_attr.value)
            and is_literal_pattern(pl_attr.value)
        ):
            # Zachowaj polski literał
            new_attrs.append(ast.Attribute(id=en_attr.id, value=pl_attr.value))
        else:
            new_attrs.append(en_attr)
    # Zachowaj atrybuty tylko w pl (np. .gender), których nie ma w en
    en_attr_names = {a.id.name for a in (getattr(en_entry, 'attributes', None) or [])}
    for name, pl_attr in pl_attrs.items():
        if name not in en_attr_names:
            new_attrs.append(pl_attr)

    merged.attributes = new_attrs
    return merged


def merge_file(en_text: str, pl_text: str) -> str:
    en_parsed = parser.parse(en_text)
    pl_parsed = parser.parse(pl_text) if pl_text.strip() else ast.Resource(body=[])
    pl_keys = collect_keys(pl_parsed)

    new_body = []
    for element in en_parsed.body:
        if isinstance(element, ENTRY_TYPES):
            key = FluentAstAbstract.get_id_name(element)
            new_body.append(merge_entry(element, pl_keys.get(key) if key else None))
        else:
            new_body.append(element)

    # Zachowaj wpisy tylko w pl, które nie są w en? Nie — generated: en jest źródłem prawdy.
    return serializer.serialize(ast.Resource(body=new_body))


def main() -> None:
    project = Project()
    en_root = project.en_locale_prototypes_dir_path
    pl_root = project.pl_locale_prototypes_dir_path
    changed = 0

    for dirpath, _, filenames in os.walk(en_root):
        for filename in filenames:
            if not filename.endswith('.ftl'):
                continue
            en_path = os.path.join(dirpath, filename)
            rel = os.path.relpath(en_path, en_root)
            pl_path = os.path.join(pl_root, rel)

            en_file = FluentFile(en_path)
            en_text = en_file.read_data()
            if not en_text.strip():
                continue

            pl_file = FluentFile(pl_path)
            pl_text = pl_file.read_data() if os.path.isfile(pl_path) else ''

            merged = merge_file(en_text, pl_text)
            if merged != pl_text:
                pl_file.save_data(merged)
                changed += 1

    print(f'Zaktualizowano strukturę w {changed} plikach pl-PL/prototypes/generated')


if __name__ == '__main__':
    main()
