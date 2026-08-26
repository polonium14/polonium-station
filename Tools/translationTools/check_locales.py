#!/usr/bin/env python3

from __future__ import annotations

import argparse
import os
import re
import sys
from collections import defaultdict
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Set, Tuple

EXPECTED_LOCALES = ('en-US', 'pl-PL')
IGNORED_LOCALE_DIRS = frozenset({'nl-NL'})
WALK_SKIP_DIR_NAMES = {'.git', 'bin', 'obj'}
CROWDIN_YML = 'crowdin.yml'
CROWDIN_SOURCE_PREFIX = '/Resources/Locale/en-US/'
IGNORE_LINE_RE = re.compile(r'^\s*-\s+(/Resources/Locale/en-US/\S+)\s*$')
KEY_RE = re.compile(r'^(-?[A-Za-z][A-Za-z0-9_-]*)\s*=(.*)$')
ATTR_RE = re.compile(r'^\s+\.([A-Za-z][A-Za-z0-9_-]*)\s*=')
MAX_ANNOTATIONS = 60


class Issue:
    __slots__ = ('kind', 'path', 'line', 'message')

    def __init__(self, kind: str, path: str, message: str, line: int = 1):
        self.kind = kind
        self.path = path.replace('\\', '/')
        self.line = line
        self.message = message


def find_repo_root(start: Optional[Path] = None) -> Path:
    current = (start or Path(__file__).resolve()).parent
    markers = {'SpaceStation14.sln', 'SpaceStation14.slnx'}
    while True:
        if any((current / marker).exists() for marker in markers):
            return current
        if current.parent == current:
            raise SystemExit('Nie znaleziono katalogu repo (brak .sln/.slnx)')
        current = current.parent


def load_crowdin_ignore_patterns(repo_root: Path) -> List[str]:
    path = repo_root / CROWDIN_YML
    if not path.is_file():
        raise SystemExit(f'Brak {CROWDIN_YML} — nie da się zastosować tych samych ignore co Crowdin')

    patterns: List[str] = []
    in_ignore = False
    for raw in path.read_text(encoding='utf-8').splitlines():
        stripped = raw.strip()
        if stripped.startswith('ignore:'):
            in_ignore = True
            continue
        if not in_ignore:
            continue
        if stripped.startswith('translation:') or stripped.startswith('update_option:'):
            break
        match = IGNORE_LINE_RE.match(raw)
        if not match:
            continue
        rel = match.group(1)
        if rel.startswith(CROWDIN_SOURCE_PREFIX):
            rel = rel[len(CROWDIN_SOURCE_PREFIX):]
        patterns.append(rel.strip('/'))
    if not patterns:
        raise SystemExit(f'{CROWDIN_YML} nie ma wpisów ignore pod source en-US')
    return patterns


def rel_ignored(rel: str, patterns: List[str]) -> bool:
    rel = rel.replace('\\', '/').strip('/')
    if not rel:
        return False
    parts = rel.split('/')
    for pat in patterns:
        pat = pat.strip('/')
        if pat.startswith('**/'):
            if pat[3:] in parts:
                return True
        elif rel == pat or rel.startswith(pat + '/'):
            return True
    return False


def iter_ftl_files(locale_root: Path, ignore_patterns: List[str]) -> Iterable[Path]:
    if not locale_root.is_dir():
        return
    for dirpath, dirnames, filenames in os.walk(locale_root):
        rel_dir = Path(dirpath).relative_to(locale_root).as_posix()
        if rel_dir == '.':
            rel_dir = ''
        keep = []
        for name in dirnames:
            if name in WALK_SKIP_DIR_NAMES:
                continue
            child = f'{rel_dir}/{name}' if rel_dir else name
            if not rel_ignored(child, ignore_patterns):
                keep.append(name)
        dirnames[:] = keep
        for filename in filenames:
            if not filename.endswith('.ftl'):
                continue
            rel = f'{rel_dir}/{filename}' if rel_dir else filename
            if rel_ignored(rel, ignore_patterns):
                continue
            yield Path(dirpath) / filename


def rel_from_locale(path: Path, locale_root: Path) -> str:
    return path.relative_to(locale_root).as_posix()


def repo_rel(path: Path, repo_root: Path) -> str:
    return path.relative_to(repo_root).as_posix()


def parse_ftl(path: Path) -> Tuple[List[Tuple[str, int, bool]], List[Tuple[str, int]], bool]:
    try:
        text = path.read_text(encoding='utf-8-sig')
    except (OSError, UnicodeDecodeError):
        return [], [], True

    if not text.strip():
        return [], [], True

    keys: List[Tuple[str, int, bool]] = []
    seen: Dict[str, int] = {}
    dupes: List[Tuple[str, int]] = []
    current: Optional[int] = None
    current_has_body = False

    def close_current() -> None:
        nonlocal current, current_has_body
        if current is None:
            return
        name, line = keys[current][0], keys[current][1]
        keys[current] = (name, line, current_has_body)
        current = None
        current_has_body = False

    for index, raw_line in enumerate(text.splitlines(), start=1):
        line = raw_line.rstrip('\r')
        stripped = line.strip()
        if not stripped or stripped.startswith('#'):
            continue

        key_match = KEY_RE.match(line)
        if key_match and not line.startswith(' ') and not line.startswith('\t'):
            close_current()
            name = key_match.group(1)
            rest = key_match.group(2).strip()
            if name in seen:
                dupes.append((name, index))
            else:
                seen[name] = index
            keys.append((name, index, False))
            current = len(keys) - 1
            current_has_body = bool(rest)
            continue

        if current is None:
            continue
        if ATTR_RE.match(line) or stripped.startswith('.') or stripped.startswith('[') or stripped.startswith('{'):
            current_has_body = True
        elif line.startswith(' ') or line.startswith('\t'):
            current_has_body = True

    close_current()
    return keys, dupes, False


def gh_error(issue: Issue) -> None:
    print(
        f'::error file={issue.path},line={issue.line},title={issue.kind}::{issue.message}'
    )


def print_section(title: str, issues: List[Issue], limit: int) -> None:
    print(f'\n{title} ({len(issues)}):')
    if not issues:
        print('  (brak)')
        return
    for issue in issues[:limit]:
        loc = f'{issue.path}:{issue.line}'
        print(f'  {loc}  {issue.message}')
    if len(issues) > limit:
        print(f'  ... i jeszcze {len(issues) - limit}')


def check_locales(repo_root: Path, limit: int) -> int:
    locale_root = repo_root / 'Resources' / 'Locale'
    if not locale_root.is_dir():
        print(f'Brak katalogu {locale_root}')
        return 1

    ignore_patterns = load_crowdin_ignore_patterns(repo_root)

    present_locales = sorted(
        path.name for path in locale_root.iterdir() if path.is_dir()
    )

    issues: Dict[str, List[Issue]] = defaultdict(list)

    extra_locales = [
        name for name in present_locales
        if name not in EXPECTED_LOCALES and name not in IGNORED_LOCALE_DIRS
    ]
    for name in extra_locales:
        extra_dir = locale_root / name
        ftl_count = sum(1 for _ in extra_dir.rglob('*.ftl'))
        issues['orphan_locale'].append(Issue(
            'orphan-locale',
            repo_rel(extra_dir, repo_root),
            f'Nieoczekiwana locale "{name}" ({ftl_count} plików .ftl). Oczekiwane: {", ".join(EXPECTED_LOCALES)}',
        ))

    per_locale_files: Dict[str, Dict[str, Path]] = {}
    per_locale_keys: Dict[str, Dict[str, List[Tuple[str, int]]]] = {}
    per_file_keyset: Dict[str, Dict[str, Dict[str, int]]] = {}

    for locale in EXPECTED_LOCALES:
        root = locale_root / locale
        files: Dict[str, Path] = {}
        keys: Dict[str, List[Tuple[str, int]]] = defaultdict(list)
        file_keyset: Dict[str, Dict[str, int]] = {}
        per_locale_files[locale] = files
        per_locale_keys[locale] = keys
        per_file_keyset[locale] = file_keyset
        if not root.is_dir():
            issues['unpaired_file'].append(Issue(
                'unpaired-file',
                repo_rel(locale_root, repo_root),
                f'Brak katalogu locale {locale}',
            ))
            continue

        for path in iter_ftl_files(root, ignore_patterns):
            rel = rel_from_locale(path, root)
            files[rel] = path
            parsed_keys, dupes, empty = parse_ftl(path)
            repo_path = repo_rel(path, repo_root)

            if empty:
                issues['empty'].append(Issue(
                    'empty-file',
                    repo_path,
                    'Pusty plik locale (brak treści albo same białe znaki)',
                ))
                continue

            for name, line in dupes:
                issues['duplicate'].append(Issue(
                    'duplicate-key',
                    repo_path,
                    f'Duplikat klucza "{name}" w tym samym pliku',
                    line,
                ))

            for name, line, has_body in parsed_keys:
                keys[name].append((repo_path, line))
                file_keyset.setdefault(rel, {})[name] = line
                if not has_body:
                    issues['empty'].append(Issue(
                        'empty-key',
                        repo_path,
                        f'Pusty klucz "{name}" (brak wartości i atrybutów)',
                        line,
                    ))

        for name, locations in keys.items():
            unique_files = {loc[0] for loc in locations}
            if len(unique_files) < 2:
                continue
            first_path, first_line = locations[0]
            others = ', '.join(sorted(unique_files - {first_path})[:4])
            issues['duplicate'].append(Issue(
                'duplicate-key',
                first_path,
                f'Duplikat klucza "{name}" też w: {others}',
                first_line,
            ))

    en_files = set(per_locale_files['en-US'])
    pl_files = set(per_locale_files['pl-PL'])

    for rel in sorted(en_files - pl_files):
        issues['unpaired_file'].append(Issue(
            'unpaired-file',
            repo_rel(per_locale_files['en-US'][rel], repo_root),
            'Plik nie ma odpowiednika w pl-PL',
        ))
    for rel in sorted(pl_files - en_files):
        issues['unpaired_file'].append(Issue(
            'unpaired-file',
            repo_rel(per_locale_files['pl-PL'][rel], repo_root),
            'Plik nie ma odpowiednika w en-US',
        ))

    en_keys = per_locale_keys['en-US']
    pl_keys = per_locale_keys['pl-PL']
    for name in sorted(set(en_keys) - set(pl_keys)):
        path, line = en_keys[name][0]
        issues['unpaired_key'].append(Issue(
            'unpaired-key',
            path,
            f'Klucz "{name}" istnieje tylko w en-US',
            line,
        ))
    for name in sorted(set(pl_keys) - set(en_keys)):
        path, line = pl_keys[name][0]
        issues['unpaired_key'].append(Issue(
            'unpaired-key',
            path,
            f'Klucz "{name}" istnieje tylko w pl-PL',
            line,
        ))

    common = en_files & pl_files
    for rel in sorted(common):
        en_names = per_file_keyset['en-US'].get(rel, {})
        pl_names = per_file_keyset['pl-PL'].get(rel, {})
        for name in sorted(set(en_names) - set(pl_names)):
            if name not in pl_keys:
                continue
            issues['unpaired_key'].append(Issue(
                'unpaired-key',
                repo_rel(per_locale_files['en-US'][rel], repo_root),
                f'Klucz "{name}" jest w en-US tutaj, a w pl-PL siedzi w innym pliku',
                en_names[name],
            ))
        for name in sorted(set(pl_names) - set(en_names)):
            if name not in en_keys:
                continue
            issues['unpaired_key'].append(Issue(
                'unpaired-key',
                repo_rel(per_locale_files['pl-PL'][rel], repo_root),
                f'Klucz "{name}" jest w pl-PL tutaj, a w en-US siedzi w innym pliku',
                pl_names[name],
            ))

    order = (
        ('orphan_locale', 'Osierocone locale'),
        ('empty', 'Puste pliki / puste klucze'),
        ('duplicate', 'Duplikaty kluczy'),
        ('unpaired_file', 'Pliki bez pary en-US/pl-PL'),
        ('unpaired_key', 'Klucze bez pary en-US/pl-PL'),
    )

    total = 0
    print('=== Locale check (en-US <-> pl-PL) ===')
    print(f'Repo: {repo_root}')
    print(f'Locale na dysku: {", ".join(present_locales) or "(brak)"}')
    if IGNORED_LOCALE_DIRS:
        ignored = sorted(name for name in present_locales if name in IGNORED_LOCALE_DIRS)
        if ignored:
            print(f'Ignorowane locale (poza checkiem): {", ".join(ignored)}')
    print(f'en-US plików: {len(en_files)}')
    print(f'pl-PL plików: {len(pl_files)}')
    print(f'Ignore Crowdin ({CROWDIN_YML}): {", ".join(ignore_patterns)}')
    print()

    for kind, title in order:
        bucket = issues[kind]
        total += len(bucket)
        print_section(title, bucket, limit)

    print('\nPodsumowanie:')
    for kind, title in order:
        print(f'  {title}: {len(issues[kind])}')
    print(f'  RAZEM: {total}')

    emitted = 0
    if os.environ.get('GITHUB_ACTIONS'):
        for kind, _title in order:
            for issue in issues[kind]:
                if emitted >= MAX_ANNOTATIONS:
                    break
                gh_error(issue)
                emitted += 1
            if emitted >= MAX_ANNOTATIONS:
                break
        if total > MAX_ANNOTATIONS:
            print(f'::warning::Pokazano {MAX_ANNOTATIONS} z {total} problemów. Reszta w logu powyżej.')

    if total:
        print('\nLocale check FAILED')
        return 1
    print('\nLocale check OK')
    return 0


def main(argv: Optional[List[str]] = None) -> int:
    parser = argparse.ArgumentParser(
        description='Sprawdza synchronizację locale en-US/pl-PL (puste, duplikaty, osierocone).',
    )
    parser.add_argument(
        '--limit',
        type=int,
        default=40,
        help='maks. pozycji w każdej sekcji logu',
    )
    args = parser.parse_args(argv)
    return check_locales(find_repo_root(), args.limit)


if __name__ == '__main__':
    raise SystemExit(main())
