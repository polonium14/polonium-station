#!/usr/bin/env python3

from __future__ import annotations

import argparse
import re
import sys
from typing import List, Optional, Pattern

import yaml

from ftl_values import EN_ROOT, PL_ROOT, changed_ftl, en_path, git_text, list_ftl

CONFIG = 'crowdin.yml'
DIRS_GROUP = 'dirs'

_GLOB_TOKEN = re.compile(r'\*\*/|\*\*|\*|\?')


def glob_regex(pattern: str, capture_dirs: bool = False) -> Pattern[str]:
    pattern = re.sub('/+', '/', pattern).lstrip('/')
    regex = ''
    pos = 0
    for token in _GLOB_TOKEN.finditer(pattern):
        regex += re.escape(pattern[pos:token.start()])
        if token.group() == '**/':
            regex += f'(?P<{DIRS_GROUP}>(?:[^/]+/)*)' if capture_dirs else '(?:[^/]+/)*'
        elif token.group() == '**':
            regex += '.*'
        elif token.group() == '*':
            regex += '[^/]*'
        else:
            regex += '[^/]'
        pos = token.end()
    return re.compile(regex + re.escape(pattern[pos:]))


def ignored(path: str, ignores: List[Pattern[str]]) -> bool:
    parts = path.split('/')
    prefixes = ['/'.join(parts[:i]) for i in range(1, len(parts) + 1)]
    return any(rule.fullmatch(prefix) for rule in ignores for prefix in prefixes)


def split_group(group: dict, paths: List[str]) -> Optional[List[dict]]:
    source = group['source']
    translation = group['translation']
    if '%' in source or source.count('**') > 1 or ('**' in source and '**/' not in source):
        return None
    source_re = glob_regex(source, capture_dirs=True)
    ignores = [glob_regex(pattern) for pattern in group.get('ignore', [])]
    rest = {key: value for key, value in group.items() if key not in ('source', 'translation', 'ignore')}
    files = []
    for path in paths:
        match = source_re.fullmatch(path)
        if match is None or ignored(path, ignores):
            continue
        dirs = match.groupdict().get(DIRS_GROUP) or ''
        files.append({
            'source': '/' + path,
            'translation': re.sub('/+', '/', translation.replace('**', dirs)),
            **rest,
        })
    return files


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.split('\n', 1)[0])
    parser.add_argument('--base', required=True, help='revision that was uploaded last')
    parser.add_argument('--head', default='HEAD', help='revision to upload')
    parser.add_argument('--output', required=True, help='where to write the Crowdin config')
    args = parser.parse_args()

    if git_text('diff', '--name-only', args.base, args.head, '--', CONFIG).strip():
        print(f'{CONFIG} changed', file=sys.stderr)
        print('full')
        return 0

    changed = set(changed_ftl(args.base, args.head, EN_ROOT))
    changed.update(en_path(path) for path in changed_ftl(args.base, args.head, PL_ROOT))
    paths = sorted(changed & set(list_ftl(args.head, EN_ROOT)))

    config = yaml.safe_load(git_text('show', f'{args.head}:{CONFIG}'))
    files = []
    for group in config['files']:
        group_files = split_group(group, paths)
        if group_files is None:
            print(f'unsupported pattern {group["source"]}', file=sys.stderr)
            print('full')
            return 0
        files.extend(group_files)

    for file in files:
        print(file['source'], file=sys.stderr)
    if not files:
        print('none')
        return 0

    config['files'] = files
    with open(args.output, 'w', encoding='utf-8') as output:
        yaml.safe_dump(config, output, sort_keys=False, allow_unicode=True)
    print('changed')
    return 0


if __name__ == '__main__':
    sys.exit(main())
