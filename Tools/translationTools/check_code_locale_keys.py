#!/usr/bin/env python3

from __future__ import annotations

import argparse
import os
import re
import sys
from collections import defaultdict
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Set, Tuple

_SCRIPT_DIR = Path(__file__).resolve().parent
if str(_SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(_SCRIPT_DIR))

from check_locales import (  # noqa: E402
    IGNORED_LOCALE_DIRS,
    MAX_ANNOTATIONS,
    WALK_SKIP_DIR_NAMES,
    Issue,
    find_repo_root,
    gh_error,
    parse_ftl,
    repo_rel,
)

SCAN_PROJECTS = (
    'Content.Client',
    'Content.Server',
    'Content.Shared',
    'Content.Replay',
)

CONTENT_LOCALE_ROOT = Path('Resources') / 'Locale'
ENGINE_FTL = Path('RobustToolbox') / 'Resources' / 'Locale' / 'en-US'

FLUENT_ID_RE = re.compile(r'^-?[A-Za-z][A-Za-z0-9_-]*$')
GETSTRING_RE = re.compile(
    r'\.(?:GetString|TryGetString)\(\s*(?:\$|@)?"([^"\n]+)"'
)
LOCID_NEW_RE = re.compile(r'new\s+LocId\s*\(\s*(?:\$|@)?"([^"\n]+)"')
LOCID_ASSIGN_RE = re.compile(
    r'\bLocId\b(?:\s+\w+)?(?:\s*\{[^}]*\})?\s*=\s*(?:\$|@)?"([^"\n]+)"'
)
SHOW_RE = re.compile(r'Show(?:Internal|External)\(\s*(?:\$|@)?"([^"\n]+)"')
XAML_LOC_RE = re.compile(
    r'\{Loc\s+(?:\'([^\']+)\'|"([^"]+)"|([A-Za-z][A-Za-z0-9_-]*))'
)
XML_COMMENT_RE = re.compile(r'<!--.*?-->', re.DOTALL)

Hit = Tuple[str, int, str]


def should_check(key: str) -> bool:
    if not key or '{' in key or '\\' in key:
        return False
    # "armor-damage-type-" + proto, interpolacje oraz GetString("On")
    if key.endswith('-') or not FLUENT_ID_RE.fullmatch(key) or '-' not in key:
        return False
    return True


def strip_cs_comments(text: str) -> str:
    out: List[str] = []
    i = 0
    n = len(text)
    while i < n:
        ch = text[i]
        nxt = text[i + 1] if i + 1 < n else ''

        if ch == '@' and nxt == '"':
            out.append(ch)
            out.append(nxt)
            i += 2
            while i < n:
                out.append(text[i])
                if text[i] == '"':
                    if i + 1 < n and text[i + 1] == '"':
                        out.append(text[i + 1])
                        i += 2
                        continue
                    i += 1
                    break
                i += 1
            continue

        if ch == '$' and nxt == '"':
            out.append(ch)
            i += 1
            continue

        if ch == '"':
            out.append(ch)
            i += 1
            while i < n:
                c = text[i]
                out.append(c)
                if c == '\\' and i + 1 < n:
                    out.append(text[i + 1])
                    i += 2
                    continue
                if c == '"':
                    i += 1
                    break
                i += 1
            continue

        if ch == "'":
            out.append(ch)
            i += 1
            if i < n and text[i] == '\\':
                out.append(text[i])
                i += 1
                if i < n:
                    out.append(text[i])
                    i += 1
            elif i < n:
                out.append(text[i])
                i += 1
            if i < n and text[i] == "'":
                out.append(text[i])
                i += 1
            continue

        if ch == '/' and nxt == '/':
            while i < n and text[i] != '\n':
                out.append(' ')
                i += 1
            continue

        if ch == '/' and nxt == '*':
            out.append(' ')
            out.append(' ')
            i += 2
            while i < n:
                if text[i] == '*' and i + 1 < n and text[i + 1] == '/':
                    out.append(' ')
                    out.append(' ')
                    i += 2
                    break
                out.append('\n' if text[i] == '\n' else ' ')
                i += 1
            continue

        out.append(ch)
        i += 1
    return ''.join(out)


def strip_xml_comments(text: str) -> str:
    def blank(match: re.Match[str]) -> str:
        return re.sub(r'[^\n]', ' ', match.group(0))

    return XML_COMMENT_RE.sub(blank, text)


def iter_source_files(root: Path) -> Iterable[Path]:
    if not root.is_dir():
        return
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [name for name in dirnames if name not in WALK_SKIP_DIR_NAMES]
        for name in filenames:
            if name.endswith(('.cs', '.xaml')):
                yield Path(dirpath) / name


def load_ftl_keys(locale_root: Path) -> Set[str]:
    keys: Set[str] = set()
    if not locale_root.is_dir():
        return keys
    for dirpath, dirnames, filenames in os.walk(locale_root):
        dirnames[:] = [name for name in dirnames if name not in WALK_SKIP_DIR_NAMES]
        for name in filenames:
            if not name.endswith('.ftl'):
                continue
            parsed, _dupes, empty = parse_ftl(Path(dirpath) / name)
            if empty:
                continue
            for key_name, _line, _has_body in parsed:
                keys.add(key_name)
    return keys


def line_of(text: str, index: int) -> int:
    return text.count('\n', 0, index) + 1


def extract_cs(text: str, rel: str) -> List[Hit]:
    hits: List[Hit] = []
    for regex in (GETSTRING_RE, LOCID_NEW_RE, LOCID_ASSIGN_RE, SHOW_RE):
        for match in regex.finditer(text):
            key = match.group(1)
            if should_check(key):
                hits.append((rel, line_of(text, match.start()), key))
    return hits


def extract_xaml(text: str, rel: str) -> List[Hit]:
    hits: List[Hit] = []
    for match in XAML_LOC_RE.finditer(text):
        key = match.group(1) or match.group(2) or match.group(3)
        if should_check(key):
            hits.append((rel, line_of(text, match.start()), key))
    return hits


def scan_project(repo_root: Path, project: str) -> List[Hit]:
    hits: List[Hit] = []
    root = repo_root / project
    for path in iter_source_files(root):
        rel = repo_rel(path, repo_root)
        try:
            raw = path.read_text(encoding='utf-8-sig')
        except (OSError, UnicodeDecodeError):
            continue
        if path.suffix.lower() == '.xaml':
            hits.extend(extract_xaml(strip_xml_comments(raw), rel))
        else:
            hits.extend(extract_cs(strip_cs_comments(raw), rel))
    return hits


def print_missing(grouped: Dict[str, List[Hit]], limit: int) -> None:
    keys = sorted(grouped)
    print(f'\nBrakujące klucze ({len(keys)} unikalnych, {sum(len(v) for v in grouped.values())} użyć):')
    if not keys:
        print('  (brak)')
        return
    shown = 0
    for key in keys:
        if shown >= limit:
            rest = len(keys) - shown
            print(f'  ... i jeszcze {rest} kluczy')
            break
        usages = grouped[key]
        print(f'  {key}')
        for rel, line, _key in usages[:8]:
            print(f'    {rel}:{line}')
        if len(usages) > 8:
            print(f'    ... i jeszcze {len(usages) - 8}')
        shown += 1


def load_content_ftl_keys(repo_root: Path) -> Tuple[Set[str], List[str]]:
    root = repo_root / CONTENT_LOCALE_ROOT
    keys: Set[str] = set()
    locales: List[str] = []
    if not root.is_dir():
        return keys, locales
    for child in sorted(root.iterdir()):
        if not child.is_dir() or child.name in IGNORED_LOCALE_DIRS:
            continue
        locales.append(child.name)
        keys |= load_ftl_keys(child)
    return keys, locales


def check_code_locale_keys(repo_root: Path, limit: int) -> int:
    content_keys, locales = load_content_ftl_keys(repo_root)
    engine_root = repo_root / ENGINE_FTL
    engine_keys = load_ftl_keys(engine_root)
    known = content_keys | engine_keys

    print('=== Code locale keys ===')
    print(f'Repo: {repo_root}')
    print(f'Skan: {", ".join(SCAN_PROJECTS)}')
    print(f'FTL content ({", ".join(locales) or "brak"}): {len(content_keys)} kluczy')
    if engine_root.is_dir():
        print(f'FTL RobustToolbox en-US: {len(engine_keys)} kluczy')
    else:
        print('Uwaga: brak RobustToolbox/Resources/Locale/en-US — klucze silnika nie wejdą do zbioru')

    hits: List[Hit] = []
    for project in SCAN_PROJECTS:
        hits.extend(scan_project(repo_root, project))

    missing: Dict[str, List[Hit]] = defaultdict(list)
    for rel, line, key in hits:
        if key not in known:
            missing[key].append((rel, line, key))

    print(f'Literalów w kodzie: {len(hits)}')
    print_missing(missing, limit)

    unique = len(missing)
    usages = sum(len(v) for v in missing.values())
    print('\nPodsumowanie:')
    print(f'  Unikalne brakujące klucze: {unique}')
    print(f'  Użycia bez FTL: {usages}')

    if os.environ.get('GITHUB_ACTIONS'):
        emitted = 0
        for key in sorted(missing):
            if emitted >= MAX_ANNOTATIONS:
                break
            rel, line, _key = missing[key][0]
            extra = f' (+{len(missing[key]) - 1} użyć)' if len(missing[key]) > 1 else ''
            gh_error(Issue(
                'missing-ftl-key',
                rel,
                f'Klucz "{key}" jest w kodzie, ale nie ma go w FTL{extra}',
                line,
            ))
            emitted += 1
        if unique > MAX_ANNOTATIONS:
            print(f'::warning::Pokazano {MAX_ANNOTATIONS} z {unique} brakujących kluczy. Reszta w logu powyżej.')

    if unique:
        print('\nCode locale keys FAILED')
        return 1
    print('\nCode locale keys OK')
    return 0


def main(argv: Optional[List[str]] = None) -> int:
    parser = argparse.ArgumentParser(
        description='Skanuje kod gry (Content.*) i sprawdza, czy literalne klucze FTL istnieją.',
    )
    parser.add_argument(
        '--limit',
        type=int,
        default=40,
        help='maks. unikalnych brakujących kluczy w logu',
    )
    args = parser.parse_args(argv)
    return check_code_locale_keys(find_repo_root(), args.limit)


if __name__ == '__main__':
    raise SystemExit(main())
