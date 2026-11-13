"""FTL values in the form Crowdin stores them, read from any git revision or tree."""

from __future__ import annotations

import os
import re
import subprocess
from dataclasses import dataclass
from typing import Dict, Iterable, List, Optional, Set, Tuple

from fluent.syntax import FluentParser, ast
from fluent.syntax.serializer import serialize_element
from fluent.syntax.visitor import Visitor

PL_ROOT = 'Resources/Locale/pl-PL/'
EN_ROOT = 'Resources/Locale/en-US/'

_MARKUP_RE = re.compile(r'\[/?[A-Za-z][^\]]*\]')
_LETTER_RE = re.compile(r'[^\W\d_]')


@dataclass(frozen=True)
class Value:
    text: str
    pattern: ast.Pattern


@dataclass
class ParsedFile:
    values: Dict[str, Value]
    junk: List[str]


REPO_ROOT = subprocess.run(['git', 'rev-parse', '--show-toplevel'], check=True, capture_output=True,
                           cwd=os.path.dirname(os.path.abspath(__file__))).stdout.decode('utf-8').strip()


def git_text(*args: str) -> str:
    return subprocess.run(['git', *args], check=True, capture_output=True, cwd=REPO_ROOT).stdout.decode('utf-8')


def read_file(rev: str, path: str) -> Optional[str]:
    result = subprocess.run(['git', 'show', f'{rev}:{path}'], capture_output=True, cwd=REPO_ROOT)
    if result.returncode != 0:
        return None
    return result.stdout.decode('utf-8-sig').replace('\r\n', '\n')


def read_files(rev: str, paths: Iterable[str]) -> Dict[str, str]:
    paths = list(paths)
    if not paths:
        return {}
    request = ''.join(f'{rev}:{path}\n' for path in paths).encode('utf-8')
    output = subprocess.run(['git', 'cat-file', '--batch'], input=request, check=True, capture_output=True,
                            cwd=REPO_ROOT).stdout
    files: Dict[str, str] = {}
    pos = 0
    for path in paths:
        header_end = output.index(b'\n', pos)
        header = output[pos:header_end].split()
        pos = header_end + 1
        if len(header) < 3 or header[1] != b'blob':
            continue
        size = int(header[2])
        files[path] = output[pos:pos + size].decode('utf-8-sig').replace('\r\n', '\n')
        pos += size + 1
    return files


def list_ftl(rev: str, root: str) -> List[str]:
    names = git_text('ls-tree', '-r', '--name-only', rev, '--', root).splitlines()
    return [name for name in names if name.endswith('.ftl')]


def changed_ftl(base: str, head: str, root: str) -> List[str]:
    names = git_text('diff', '--name-only', '--no-renames', base, head, '--', root).splitlines()
    return [name for name in names if name.endswith('.ftl')]


def en_path(pl_path: str) -> str:
    return EN_ROOT + pl_path[len(PL_ROOT):]


def _pattern_text(pattern: ast.Pattern) -> str:
    return ''.join(serialize_element(element) for element in pattern.elements)


def parse(source: Optional[str]) -> ParsedFile:
    parsed = ParsedFile({}, [])
    if source is None:
        return parsed
    resource = FluentParser(with_spans=False).parse(source)
    for entry in resource.body:
        if isinstance(entry, ast.Junk):
            parsed.junk.append(entry.content.strip().split('\n', 1)[0])
            continue
        if isinstance(entry, ast.Message):
            key = entry.id.name
        elif isinstance(entry, ast.Term):
            key = '-' + entry.id.name
        else:
            continue
        if entry.value is not None:
            parsed.values[key] = Value(_pattern_text(entry.value), entry.value)
        for attribute in entry.attributes:
            parsed.values[f'{key}.{attribute.id.name}'] = Value(_pattern_text(attribute.value), attribute.value)
    return parsed


class _Collector(Visitor):
    def __init__(self) -> None:
        self.text: List[str] = []
        self.refs: Set[str] = set()
        self.variables: Set[str] = set()

    def visit_TextElement(self, node: ast.TextElement) -> None:
        self.text.append(node.value)

    def visit_MessageReference(self, node: ast.MessageReference) -> None:
        self.refs.add(node.id.name + (f'.{node.attribute.name}' if node.attribute else ''))

    def visit_TermReference(self, node: ast.TermReference) -> None:
        self.refs.add('-' + node.id.name + (f'.{node.attribute.name}' if node.attribute else ''))
        self.generic_visit(node)

    def visit_VariableReference(self, node: ast.VariableReference) -> None:
        self.variables.add(node.id.name)


def inspect(value: Value) -> Tuple[bool, Set[str], Set[str]]:
    """Returns whether the pattern has words outside placeables and markup, its references and its variables."""
    collector = _Collector()
    collector.visit(value.pattern)
    text = _MARKUP_RE.sub('', ''.join(collector.text))
    return bool(_LETTER_RE.search(text)), collector.refs, collector.variables
