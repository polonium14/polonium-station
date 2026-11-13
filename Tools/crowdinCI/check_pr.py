#!/usr/bin/env python3
"""Finds regressions that a Crowdin sync PR would bring into pl-PL.
"""

from __future__ import annotations

import argparse
import sys
from typing import Dict, List, Optional, Set, Tuple

from ftl_values import PL_ROOT, Value, changed_ftl, en_path, inspect, list_ftl, parse, read_file, read_files

MAX_LISTED = 100


def all_values(rev: str) -> Dict[str, Value]:
    values: Dict[str, Value] = {}
    for path, source in sorted(read_files(rev, list_ftl(rev, PL_ROOT)).items()):
        for key, value in parse(source).values.items():
            values.setdefault(key, value)
    return values


def find_cycle(start: str, values: Dict[str, Value]) -> Optional[List[str]]:
    stack: List[Tuple[str, List[str]]] = [(start, [start])]
    seen: Set[str] = set()
    while stack:
        key, path = stack.pop()
        value = values.get(key)
        if value is None:
            continue
        for ref in inspect(value)[1]:
            if ref == start:
                return path + [ref]
            if ref not in seen:
                seen.add(ref)
                stack.append((ref, path + [ref]))
    return None


def short(text: str, limit: int = 80) -> str:
    text = ' '.join(text.split())
    text = text if len(text) <= limit else text[:limit - 1] + '…'
    return '`' + text.replace('`', "'") + '`'


def check(base: str, head: str) -> Tuple[List[str], int]:
    problems: List[str] = []
    checked = 0
    head_values: Optional[Dict[str, Value]] = None
    for path in changed_ftl(base, head, PL_ROOT):
        name = path[len(PL_ROOT):]
        old = parse(read_file(base, path)).values
        new_file = parse(read_file(head, path))
        english = parse(read_file(head, en_path(path))).values
        old_english = parse(read_file(base, en_path(path))).values
        for junk in new_file.junk:
            problems.append(f'`{name}`: błąd składni Fluent w {short(junk)}')
        new = new_file.values
        for key in sorted(set(old) | set(new)):
            before, after = old.get(key), new.get(key)
            if before is not None and after is not None and before.text == after.text:
                continue
            checked += 1
            source = english.get(key)
            where = f'`{key}` w `{name}`'
            if after is None:
                if source is not None:
                    problems.append(f'{where}: klucz znika, choć jest w en-US')
                continue
            has_words, refs, variables = inspect(after)
            if source is None:
                continue
            source_words, source_refs, source_variables = inspect(source)
            old_source = old_english.get(key, source)
            if (has_words and after.text == source.text and before is not None
                    and before.text != old_source.text):
                problems.append(f'{where}: wraca angielski tekst {short(after.text)}, w master jest {short(before.text)}')
            if source_refs and not source_words and not has_words and after.text != source.text:
                problems.append(f'{where}: odwołanie {short(after.text)} zamiast {short(source.text)} jak w en-US')
            extra = variables - source_variables
            if extra:
                listed = ', '.join(f'`${v}`' for v in sorted(extra))
                problems.append(f'{where}: zmienne {listed}, których en-US nie przekazuje')
            if refs:
                if head_values is None:
                    head_values = all_values(head)
                missing = sorted(ref for ref in refs if ref not in head_values)
                if missing:
                    listed = ', '.join(f'`{ref}`' for ref in missing)
                    problems.append(f'{where}: odwołanie do nieistniejących kluczy {listed}')
                cycle = find_cycle(key, head_values)
                if cycle:
                    problems.append(f'{where}: zapętlone odwołania {" → ".join(f"`{ref}`" for ref in cycle)}')
    return problems, checked


def report(problems: List[str], checked: int) -> str:
    lines = ['## Kontrola tłumaczeń z Crowdina', '']
    if not problems:
        lines.append(f'Nie znaleziono regresji w {checked} zmienionych kluczach pl-PL.')
        return '\n'.join(lines) + '\n'
    lines.append(f'Znaleziono {len(problems)} problemów w {checked} zmienionych kluczach pl-PL. '
                 'Popraw je na Crowdinie, zatwierdzając właściwe tłumaczenie, a potem uruchom ponownie workflow '
                 '"Crowdin Download", żeby odświeżyć ten PR.')
    lines.append('')
    lines.extend(f'- {problem}' for problem in problems[:MAX_LISTED])
    if len(problems) > MAX_LISTED:
        lines.append(f'- …i {len(problems) - MAX_LISTED} kolejnych, pełna lista jest w logu workflow.')
    return '\n'.join(lines) + '\n'


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.split('\n', 1)[0])
    parser.add_argument('--base', required=True, help='revision or tree of master')
    parser.add_argument('--head', required=True, help='revision or tree after merging the sync PR')
    parser.add_argument('--output', help='file for the Markdown report')
    args = parser.parse_args()

    problems, checked = check(args.base, args.head)
    for problem in problems:
        print(problem)
    print(f'{len(problems)} problems in {checked} changed keys')
    if args.output:
        with open(args.output, 'w', encoding='utf-8') as file:
            file.write(report(problems, checked))
    return 1 if problems else 0


if __name__ == '__main__':
    sys.exit(main())
