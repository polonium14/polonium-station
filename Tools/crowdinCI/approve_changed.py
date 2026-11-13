#!/usr/bin/env python3

from __future__ import annotations

import argparse
import json
import os
import sys
import time
from typing import Dict, Iterator, List, Optional, Tuple
from urllib.error import HTTPError
from urllib.parse import urlencode
from urllib.request import Request, urlopen

from ftl_values import PL_ROOT, Value, changed_ftl, en_path, inspect, parse, read_file

API_URL = 'https://api.crowdin.com/api/v2'
PAGE = 500
STRING_IDS_PER_REQUEST = 100


class CrowdinError(Exception):
    def __init__(self, status: int, body: str):
        super().__init__(f'HTTP {status}: {body}')
        self.status = status
        self.body = body


class Crowdin:
    def __init__(self, token: str, project_id: str):
        self._token = token
        self._project = f'{API_URL}/projects/{project_id}'

    def request(self, method: str, path: str, params: Optional[dict] = None, body: Optional[dict] = None) -> Optional[dict]:
        url = self._project + path + (f'?{urlencode(params)}' if params else '')
        data = json.dumps(body).encode('utf-8') if body is not None else None
        headers = {'Authorization': f'Bearer {self._token}', 'Content-Type': 'application/json'}
        for attempt in range(6):
            try:
                with urlopen(Request(url, data=data, method=method, headers=headers), timeout=60) as response:
                    payload = response.read()
                    return json.loads(payload) if payload else None
            except HTTPError as error:
                if error.code == 429 or error.code >= 500:
                    time.sleep(2 ** attempt)
                    continue
                raise CrowdinError(error.code, error.read().decode('utf-8', 'replace')) from None
        raise CrowdinError(429, f'gave up on {method} {path}')

    def paged(self, path: str, params: dict) -> Iterator[dict]:
        offset = 0
        while True:
            page = self.request('GET', path, {**params, 'limit': PAGE, 'offset': offset})
            items = [item['data'] for item in page['data']]
            yield from items
            if len(items) < PAGE:
                return
            offset += PAGE


def changed_values(base: str, head: str) -> Dict[str, Dict[str, Value]]:
    changed: Dict[str, Dict[str, Value]] = {}
    for path in changed_ftl(base, head, PL_ROOT):
        old = parse(read_file(base, path)).values
        new = parse(read_file(head, path))
        if new.junk:
            print(f'skip {path}: syntax errors')
            continue
        source = parse(read_file(head, en_path(path))).values
        keys: Dict[str, Value] = {}
        for key, value in new.values.items():
            if key in old and old[key].text == value.text:
                continue
            english = source.get(key)
            has_words = inspect(value)[0]
            if english is not None and english.text == value.text and has_words:
                print(f'skip {path} {key}: not translated')
                continue
            keys[key] = value
        if keys:
            changed[path] = keys
    return changed


def approve(crowdin: Crowdin, string_id: int, language: str, text: str, dry_run: bool) -> str:
    existing = next((t for t in crowdin.paged('/translations', {'stringId': string_id, 'languageId': language})
                     if t['text'] == text), None)
    if dry_run:
        return 'would approve' if existing else 'would add and approve'
    if existing is None:
        try:
            existing = crowdin.request('POST', '/translations', body={
                'stringId': string_id, 'languageId': language, 'text': text})['data']
        except CrowdinError as error:
            if error.status != 400 or 'Duplicate' not in error.body:
                raise
            existing = next(t for t in crowdin.paged('/translations', {'stringId': string_id, 'languageId': language})
                            if t['text'] == text)
    crowdin.request('POST', '/approvals', body={'translationId': existing['id']})
    return 'approved'


def approved_texts(crowdin: Crowdin, language: str, string_ids: List[int]) -> Dict[int, str]:
    texts: Dict[int, str] = {}
    for i in range(0, len(string_ids), STRING_IDS_PER_REQUEST):
        chunk = ','.join(map(str, string_ids[i:i + STRING_IDS_PER_REQUEST]))
        for item in crowdin.paged(f'/languages/{language}/translations', {'stringIds': chunk, 'approvedOnly': 1}):
            texts[item['stringId']] = item['text']
    return texts


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.split('\n', 1)[0])
    parser.add_argument('--base', required=True, help='revision before the change')
    parser.add_argument('--head', default='HEAD', help='revision after the change')
    parser.add_argument('--branch', default='master', help='Crowdin branch name')
    parser.add_argument('--language', default='pl', help='Crowdin language id')
    parser.add_argument('--dry-run', action='store_true', help='only print what would be approved')
    args = parser.parse_args()

    changed = changed_values(args.base, args.head)
    total = sum(len(keys) for keys in changed.values())
    print(f'{total} changed pl-PL values in {len(changed)} files')
    if not total:
        return 0

    token = os.environ.get('CROWDIN_PERSONAL_TOKEN')
    project = os.environ.get('CROWDIN_PROJECT_ID')
    if not token or not project:
        print('CROWDIN_PERSONAL_TOKEN and CROWDIN_PROJECT_ID must be set', file=sys.stderr)
        return 1
    crowdin = Crowdin(token, project)

    branches = crowdin.request('GET', '/branches', {'name': args.branch})['data']
    if not branches:
        print(f'Crowdin branch {args.branch} not found', file=sys.stderr)
        return 1
    branch_id = branches[0]['data']['id']
    prefix = f'/{args.branch}/'
    files = {f['path'][len(prefix):]: f['id'] for f in crowdin.paged('/files', {'branchId': branch_id})
             if f['path'].startswith(prefix)}

    failures = 0
    for path, keys in sorted(changed.items()):
        file_id = files.get(en_path(path))
        if file_id is None:
            continue
        strings = {s['identifier']: s['id'] for s in crowdin.paged('/strings', {'fileId': file_id})}
        targets: List[Tuple[str, int, str]] = []
        for key, value in sorted(keys.items()):
            if key not in strings:
                print(f'skip {path} {key}: no source string on Crowdin')
                continue
            targets.append((key, strings[key], value.text))
        approved = approved_texts(crowdin, args.language, [string_id for _, string_id, _ in targets])
        for key, string_id, text in targets:
            if approved.get(string_id) == text:
                continue
            try:
                print(f'{approve(crowdin, string_id, args.language, text, args.dry_run)} {path} {key}')
            except CrowdinError as error:
                failures += 1
                print(f'failed {path} {key}: {error}', file=sys.stderr)
    return 1 if failures else 0


if __name__ == '__main__':
    sys.exit(main())
