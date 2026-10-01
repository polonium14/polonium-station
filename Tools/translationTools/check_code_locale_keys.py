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
ENGINE_LOCALE = 'en-US'

FLUENT_ID_RE = re.compile(r'^-?[A-Za-z][A-Za-z0-9_-]*$')
KEY_CHUNK_RE = re.compile(r'^[A-Za-z0-9_-]*$')
LOC_CALL_RE = re.compile(r'(?:\.(GetString|TryGetString)|\bnew\s+LocId)\s*\(')
SPECIAL_CHAR_RE = re.compile(r'["\'/]')
LOCID_DECL_RE = re.compile(r'\bLocId\b\??\s+\w+\s*(?:\{[^{}]*\}\s*)?=(?![=>])')
LOCID_MEMBER_RE = re.compile(r'\bLocId\b\??\s+(\w+)\s*(?:[;{=]|$)', re.MULTILINE)
LOCID_COLLECTION_RE = re.compile(
    r'(?:\b(?:List|IReadOnlyList|IList|HashSet|ISet|IEnumerable|ICollection|IReadOnlyCollection'
    r'|ImmutableArray|ImmutableList)\s*<\s*LocId\??\s*>'
    r'|\bLocId\??\s*\[\]'
    r'|\b(?:Dictionary|IReadOnlyDictionary|IDictionary|FrozenDictionary)\s*<\s*(?!string\b)[\w.?]+\s*,\s*LocId\??\s*>)'
    r'\??\s+\w+\s*(?:\{[^{}]*\}\s*)?=(?![=>])'
)
ASSIGN_RE = re.compile(r'\b([A-Za-z_]\w*)\s*=(?![=>])')
CALL_NAME_RE = re.compile(r'\b([A-Za-z_]\w*)\s*(?:<[^<>(){};=]*>)?\s*\(')
NAMED_ARG_RE = re.compile(r'\s*[A-Za-z_]\w*\s*:(?!:)')
IDENT_RE = re.compile(r'[A-Za-z_]\w*')
PARAM_ATTR_RE = re.compile(r'^\s*\[[^\]]*\]\s*')
PARAM_MODIFIERS = {'this', 'ref', 'out', 'in', 'params', 'scoped', 'readonly'}
NOT_METHODS = {
    'if', 'for', 'foreach', 'while', 'switch', 'using', 'lock', 'catch', 'return', 'nameof', 'typeof',
    'sizeof', 'default', 'when', 'fixed', 'checked', 'unchecked', 'new', 'await', 'throw', 'base', 'this',
    'is', 'as', 'and', 'or', 'not', 'var', 'get', 'set', 'init', 'add', 'remove', 'where',
}
XAML_LOC_RE = re.compile(
    r'\{Loc\s+(?:\'([^\']+)\'|"([^"]+)"|([A-Za-z][A-Za-z0-9_-]*))'
)
XML_COMMENT_RE = re.compile(r'<!--.*?-->', re.DOTALL)

FTL_ENTRY_RE = re.compile(r'^(-?[A-Za-z][A-Za-z0-9_-]*)\s*=(.*)$')
FTL_ATTR_RE = re.compile(r'^\s+\.([A-Za-z][A-Za-z0-9_-]*)\s*=')
FTL_REF_RE = re.compile(r'(-?)([A-Za-z][A-Za-z0-9_-]*)(?:\.([A-Za-z][A-Za-z0-9_-]*))?')
FTL_NAMED_ARG_RE = re.compile(r'[A-Za-z][A-Za-z0-9_-]*\s*:')
FTL_NUMBER_RE = re.compile(r'-?[0-9]+(?:\.[0-9]+)?')
FTL_VARIABLE_RE = re.compile(r'\$[A-Za-z][A-Za-z0-9_-]*')

WILDCARD = '*'

Hit = Tuple[str, int, str]
Span = Tuple[int, int]


def should_check(key: str) -> bool:
    if not key or '{' in key or '\\' in key:
        return False
    # GetString("On") i podobne
    if key.endswith('-') or not FLUENT_ID_RE.fullmatch(key) or '-' not in key:
        return False
    return True


def skip_char_literal(text: str, i: int) -> int:
    n = len(text)
    i += 1
    while i < n and text[i] != '\n':
        if text[i] == '\\':
            i += 2
        elif text[i] == "'":
            return i + 1
        else:
            i += 1
    return min(i, n)


def read_string(text: str, i: int) -> Optional[Tuple[int, str]]:
    n = len(text)
    j = i
    verbatim = interpolated = False
    while j < n and text[j] in '@$' and j - i < 2:
        verbatim |= text[j] == '@'
        interpolated |= text[j] == '$'
        j += 1
    if j >= n or text[j] != '"':
        return None
    j += 1
    body_start = j
    depth = 0
    while j < n:
        c = text[j]
        if depth:
            if c == '"' or (c in '@$' and j + 1 < n and text[j + 1] in '"@$'):
                inner = read_string(text, j)
                if inner is not None:
                    j = inner[0]
                    continue
            elif c == "'":
                j = skip_char_literal(text, j)
                continue
            elif c == '{':
                depth += 1
            elif c == '}':
                depth -= 1
            j += 1
            continue
        if interpolated and c in '{}':
            if j + 1 < n and text[j + 1] == c:
                j += 2
                continue
            if c == '{':
                depth = 1
            j += 1
            continue
        if verbatim:
            if c == '"':
                if j + 1 < n and text[j + 1] == '"':
                    j += 2
                    continue
                return j + 1, text[body_start:j]
            j += 1
            continue
        if c == '\\':
            j += 2
            continue
        if c == '"' or c == '\n':
            return j + 1, text[body_start:j]
        j += 1
    return n, text[body_start:n]


def strip_cs_comments(text: str) -> str:
    out: List[str] = []
    i = 0
    n = len(text)
    while i < n:
        match = SPECIAL_CHAR_RE.search(text, i)
        if match is None:
            out.append(text[i:])
            break
        j = match.start()
        ch = text[j]

        if ch == '"':
            k = j
            while k > i and text[k - 1] in '@$' and j - k < 2:
                k -= 1
            lit = read_string(text, k)
            end = lit[0] if lit is not None else j + 1
            out.append(text[i:end])
            i = end
            continue

        if ch == "'":
            end = skip_char_literal(text, j)
            out.append(text[i:end])
            i = end
            continue

        out.append(text[i:j])
        if text.startswith('//', j):
            end = text.find('\n', j)
            end = n if end == -1 else end
            out.append(' ' * (end - j))
        elif text.startswith('/*', j):
            end = text.find('*/', j + 2)
            end = n if end == -1 else end + 2
            out.append(re.sub(r'[^\n]', ' ', text[j:end]))
        else:
            end = j + 1
            out.append('/')
        i = end
    return ''.join(out)


def strip_xml_comments(text: str) -> str:
    def blank(match: re.Match[str]) -> str:
        return re.sub(r'[^\n]', ' ', match.group(0))

    return XML_COMMENT_RE.sub(blank, text)


def iter_files(root: Path, suffixes: Tuple[str, ...]) -> Iterable[Path]:
    if not root.is_dir():
        return
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [name for name in dirnames if name not in WALK_SKIP_DIR_NAMES]
        for name in filenames:
            if name.endswith(suffixes):
                yield Path(dirpath) / name


def line_of(text: str, index: int) -> int:
    return text.count('\n', 0, index) + 1


def skip_spaces(text: str, i: int) -> int:
    while i < len(text) and text[i].isspace():
        i += 1
    return i


def prev_char(text: str, i: int) -> str:
    i -= 1
    while i >= 0 and text[i].isspace():
        i -= 1
    return text[i] if i >= 0 else ''


def ends_with_word(text: str, i: int, word: str) -> bool:
    i -= 1
    while i >= 0 and text[i].isspace():
        i -= 1
    start = i - len(word) + 1
    if start < 0 or text[start:i + 1] != word:
        return False
    return start == 0 or not (text[start - 1].isalnum() or text[start - 1] == '_')


def match_bracket(text: str, i: int) -> int:
    depth = 0
    n = len(text)
    while i < n:
        ch = text[i]
        if ch in '"@$':
            lit = read_string(text, i)
            if lit is not None:
                i = lit[0]
                continue
        if ch == "'":
            i = skip_char_literal(text, i)
            continue
        if ch in '([{':
            depth += 1
        elif ch in ')]}':
            depth -= 1
            if depth == 0:
                return i + 1
        i += 1
    return n


def top_level(expr: str) -> Iterable[Tuple[int, str]]:
    depth = 0
    i = 0
    n = len(expr)
    while i < n:
        ch = expr[i]
        if ch in '"@$':
            lit = read_string(expr, i)
            if lit is not None:
                if depth == 0:
                    yield i, '"'
                i = lit[0]
                continue
        if ch == "'":
            i = skip_char_literal(expr, i)
            continue
        if ch in '([{':
            if depth == 0:
                yield i, ch
            depth += 1
        elif ch in ')]}':
            depth -= 1
        elif depth == 0:
            if expr.startswith('??', i):
                yield i, '??'
                i += 2
                continue
            if expr.startswith('=>', i):
                yield i, '=>'
                i += 2
                continue
            if ch == '?' and not expr.startswith(('?.', '?['), i):
                yield i, '?'
            elif ch in ':+,':
                yield i, ch
            elif ch == 's' and re.match(r'switch\b', expr[i:]) and (i == 0 or not expr[i - 1].isalnum()):
                yield i, 'switch'
        i += 1


def split_top(expr: str, sep: str) -> List[str]:
    parts: List[str] = []
    last = 0
    for pos, token in top_level(expr):
        if token == sep:
            parts.append(expr[last:pos])
            last = pos + len(sep)
    parts.append(expr[last:])
    return parts


def unwrap_parens(expr: str) -> str:
    expr = expr.strip()
    while expr.startswith('(') and match_bracket(expr, 0) == len(expr):
        expr = expr[1:-1].strip()
    return expr


def interpolation_pattern(literal: str) -> Optional[str]:
    lit = read_string(literal, 0)
    if lit is None or lit[0] != len(literal):
        return None
    body = lit[1]
    if '$' not in literal[:literal.index('"')]:
        return body
    out: List[str] = []
    i = 0
    n = len(body)
    while i < n:
        ch = body[i]
        if ch in '{}' and i + 1 < n and body[i + 1] == ch:
            out.append(ch)
            i += 2
            continue
        if ch == '{':
            i = match_bracket(body, i)
            out.append(WILDCARD)
            continue
        out.append(ch)
        i += 1
    return ''.join(out)


def expression_patterns(expr: str) -> List[str]:
    expr = unwrap_parens(expr)
    if not expr:
        return []
    tokens = list(top_level(expr))
    kinds = [token for _pos, token in tokens]

    if '??' in kinds:
        return [p for part in split_top(expr, '??') for p in expression_patterns(part)]

    if '?' in kinds:
        start = next(pos for pos, token in tokens if token == '?')
        nested = 0
        for pos, token in tokens:
            if pos <= start:
                continue
            if token == '?':
                nested += 1
            elif token == ':':
                if nested == 0:
                    return expression_patterns(expr[start + 1:pos]) + expression_patterns(expr[pos + 1:])
                nested -= 1
        return []

    if 'switch' in kinds:
        brace = next((pos for pos, token in tokens if token == '{'), None)
        if brace is None:
            return []
        body = expr[brace + 1:match_bracket(expr, brace) - 1]
        patterns: List[str] = []
        for arm in split_top(body, ','):
            arrow = next((pos for pos, token in top_level(arm) if token == '=>'), None)
            if arrow is not None:
                patterns += expression_patterns(arm[arrow + 2:])
        return patterns

    if '+' in kinds:
        pieces: List[str] = []
        for part in split_top(expr, '+'):
            part = unwrap_parens(part)
            pattern = interpolation_pattern(part) if part[:1] in '"@$' else None
            pieces.append(pattern if pattern is not None else WILDCARD)
        return [re.sub(r'\*+', WILDCARD, ''.join(pieces))]

    if kinds == ['"']:
        pattern = interpolation_pattern(expr)
        if pattern is not None and WILDCARD in pattern:
            return [re.sub(r'\*+', WILDCARD, pattern)]
    return []


def is_key_pattern(pattern: str) -> bool:
    if WILDCARD not in pattern:
        return False
    chunks = pattern.split(WILDCARD)
    literal = ''.join(chunks)
    if not all(KEY_CHUNK_RE.fullmatch(chunk) for chunk in chunks):
        return False
    return len(literal) >= 3 and '-' in literal and any(c.isalpha() for c in literal)


def is_value_literal(text: str, start: int, end: int) -> bool:
    before = prev_char(text, start)
    if before == '+' or (before == '=' and prev_char(text, text.rindex('=', 0, start)) in ('=', '!')):
        return False
    return not text.startswith(('+', '.', '[', '==', '!=', '=>'), skip_spaces(text, end))


def scan_expression(text: str, start: int) -> Tuple[int, List[Tuple[int, str]]]:
    literals: List[Tuple[int, str]] = []
    transparent: List[bool] = []
    opened_at: List[int] = []
    i = start
    n = len(text)
    while i < n:
        ch = text[i]
        if ch in '"@$':
            lit = read_string(text, i)
            if lit is not None:
                end, value = lit
                if all(transparent) and is_value_literal(text, i, end):
                    literals.append((i, value))
                i = end
                continue
        if ch == "'":
            i = skip_char_literal(text, i)
            continue
        if ch in '([{':
            before = prev_char(text, i)
            if ch == '(':
                transparent.append(not before or not (before.isalnum() or before in '_>])+'))
            elif ch == '{':
                transparent.append(ends_with_word(text, i, 'switch'))
            else:
                transparent.append(False)
            opened_at.append(len(literals))
        elif ch in ')]}':
            if not transparent:
                break
            transparent.pop()
            if text.startswith('+', skip_spaces(text, i + 1)):
                del literals[opened_at.pop():]
            else:
                opened_at.pop()
        elif ch in ',;' and not transparent:
            break
        i += 1
    return i, literals


def collection_literals(text: str, start: int) -> List[Tuple[int, str]]:
    literals: List[Tuple[int, str]] = []
    depth = 0
    i = start
    n = len(text)
    while i < n:
        ch = text[i]
        if ch in '"@$':
            lit = read_string(text, i)
            if lit is not None:
                literals.append((i, lit[1]))
                i = lit[0]
                continue
        if ch == "'":
            i = skip_char_literal(text, i)
            continue
        if ch in '([{':
            depth += 1
        elif ch in ')]}':
            if depth == 0:
                break
            depth -= 1
        elif ch == ';' and depth == 0:
            break
        i += 1
    return literals


def iter_call_args(text: str, open_end: int) -> Iterable[Tuple[int, Optional[int], int]]:
    # (indeks argumentu lub None dla nazwanego, początek, koniec)
    i = open_end
    index = 0
    n = len(text)
    while i < n:
        start = i
        named = NAMED_ARG_RE.match(text, i)
        if named:
            i = named.end()
        end, _literals = scan_expression(text, i)
        if text[start:end].strip():
            yield (None if named else index), i, end
        if end >= n or text[end] != ',':
            return
        index += 1
        i = end + 1


def parse_params(params: str) -> List[Tuple[str, str]]:
    result: List[Tuple[str, str]] = []
    if not params.strip():
        return result
    fragments: List[str] = []
    open_angles = 0
    for raw in split_top(params, ','):
        if open_angles > 0:
            fragments[-1] += ',' + raw
        else:
            fragments.append(raw)
        head = PARAM_ATTR_RE.sub('', raw).split('=', 1)[0]
        open_angles += head.count('<') - head.count('>')
    for raw in fragments:
        param = PARAM_ATTR_RE.sub('', raw)
        param = param.split('=', 1)[0]
        words = param.split()
        while words and words[0] in PARAM_MODIFIERS:
            words.pop(0)
        if len(words) < 2 or not IDENT_RE.fullmatch(words[-1]):
            return []
        result.append((' '.join(words[:-1]), words[-1]))
    return result


class Method:
    __slots__ = ('name', 'params', 'body')

    def __init__(self, name: str, params: List[Tuple[str, str]], body: Span):
        self.name = name
        self.params = params
        self.body = body


def find_methods(text: str) -> List[Method]:
    methods: List[Method] = []
    for match in CALL_NAME_RE.finditer(text):
        name = match.group(1)
        if name in NOT_METHODS:
            continue
        before = prev_char(text, match.start())
        if not before or not (before.isalnum() or before in '_>]?') or ends_with_word(text, match.start(), 'new'):
            continue
        close = match_bracket(text, match.end() - 1)
        params = parse_params(text[match.end():close - 1])
        if not params:
            continue
        after = skip_spaces(text, close)
        if text.startswith('where', after):
            brace = text.find('{', after)
            arrow = text.find('=>', after)
            after = min(p for p in (brace, arrow) if p != -1) if brace != -1 or arrow != -1 else after
        if text.startswith('{', after):
            body = (after, match_bracket(text, after))
        elif text.startswith('=>', after):
            body = (after + 2, scan_expression(text, after + 2)[0])
        else:
            continue
        methods.append(Method(name, params, body))
    return methods


def is_locid_type(type_name: str) -> bool:
    return re.fullmatch(r'LocId\??', type_name.strip()) is not None


def find_wrappers(sources: List[Tuple[str, str]]) -> Dict[str, Set[int]]:
    wrappers: Dict[str, Set[int]] = defaultdict(set)
    # (metoda, indeks jej parametru, wywoływana metoda, indeks argumentu)
    links: List[Tuple[str, int, str, int]] = []
    for _rel, text in sources:
        for method in find_methods(text):
            names = {name: index for index, (_type, name) in enumerate(method.params)}
            for index, (type_name, _name) in enumerate(method.params):
                if is_locid_type(type_name):
                    wrappers[method.name].add(index)
            start, stop = method.body
            for match in LOC_CALL_RE.finditer(text, start, stop):
                end, _literals = scan_expression(text, match.end())
                index = names.get(text[match.end():end].strip())
                if index is not None:
                    wrappers[method.name].add(index)
            for match in CALL_NAME_RE.finditer(text, start, stop):
                callee = match.group(1)
                if callee in NOT_METHODS:
                    continue
                for arg_index, arg_start, arg_end in iter_call_args(text, match.end()):
                    index = names.get(text[arg_start:arg_end].strip())
                    if arg_index is not None and index is not None:
                        links.append((method.name, index, callee, arg_index))

    changed = True
    while changed:
        changed = False
        for method, index, callee, arg_index in links:
            if arg_index in wrappers.get(callee, ()) and index not in wrappers[method]:
                wrappers[method].add(index)
                changed = True
    return wrappers


def iter_key_args(text: str, wrappers: Dict[str, Set[int]]) -> Iterable[Tuple[int, bool]]:
    for match in LOC_CALL_RE.finditer(text):
        yield match.end(), match.group(1) == 'TryGetString'
    for match in CALL_NAME_RE.finditer(text):
        indices = wrappers.get(match.group(1))
        if not indices:
            continue
        for index, start, _end in iter_call_args(text, match.end()):
            if index in indices:
                yield start, False


def extract_cs(
    text: str,
    rel: str,
    locid_members: Set[str],
    wrappers: Dict[str, Set[int]],
) -> List[Hit]:
    hits: Set[Hit] = set()
    variables: Dict[str, bool] = {}

    def consume(start: int, optional: bool = False) -> None:
        end, literals = scan_expression(text, start)
        for pos, key in literals:
            if should_check(key):
                hits.add((rel, line_of(text, pos), key))
        expr = text[start:end]
        if not optional:
            for pattern in expression_patterns(expr):
                if WILDCARD not in pattern:
                    if should_check(pattern):
                        hits.add((rel, line_of(text, start), pattern))
                elif is_key_pattern(pattern):
                    hits.add((rel, line_of(text, start), pattern))
        arg = expr.strip()
        if IDENT_RE.fullmatch(arg):
            variables[arg] = variables.get(arg, True) and optional

    for start, optional in iter_key_args(text, wrappers):
        consume(start, optional)

    for match in LOCID_DECL_RE.finditer(text):
        consume(match.end())

    for match in LOCID_COLLECTION_RE.finditer(text):
        for pos, key in collection_literals(text, match.end()):
            if should_check(key):
                hits.add((rel, line_of(text, pos), key))

    for match in ASSIGN_RE.finditer(text):
        if match.group(1) in locid_members:
            consume(match.end())

    done: Dict[str, bool] = {}
    while any(done.get(name) != optional for name, optional in variables.items()):
        for name, optional in sorted(variables.items()):
            if done.get(name) == optional:
                continue
            done[name] = optional
            for match in re.finditer(rf'(?<![\w.]){re.escape(name)}\s*=(?![=>])', text):
                consume(match.end(), optional)

    return sorted(hits)


def extract_xaml(text: str, rel: str) -> List[Hit]:
    hits: List[Hit] = []
    for match in XAML_LOC_RE.finditer(text):
        key = match.group(1) or match.group(2) or match.group(3)
        if should_check(key):
            hits.append((rel, line_of(text, match.start()), key))
    return hits


def scan_projects(repo_root: Path) -> List[Hit]:
    cs_sources: List[Tuple[str, str]] = []
    hits: List[Hit] = []
    for project in SCAN_PROJECTS:
        for path in iter_files(repo_root / project, ('.cs', '.xaml')):
            rel = repo_rel(path, repo_root)
            try:
                raw = path.read_text(encoding='utf-8-sig')
            except (OSError, UnicodeDecodeError):
                continue
            if path.suffix.lower() == '.xaml':
                hits.extend(extract_xaml(strip_xml_comments(raw), rel))
            else:
                cs_sources.append((rel, strip_cs_comments(raw)))

    members: Set[str] = set()
    for _rel, text in cs_sources:
        members.update(LOCID_MEMBER_RE.findall(text))
    wrappers = find_wrappers(cs_sources)
    for rel, text in cs_sources:
        hits.extend(extract_cs(text, rel, members, wrappers))
    return hits


class FtlEntry:
    __slots__ = ('name', 'line', 'text', 'attributes')

    def __init__(self, name: str, line: int, text: str):
        self.name = name
        self.line = line
        self.text = text
        self.attributes: Set[str] = set()


def parse_ftl_entries(path: Path) -> List[FtlEntry]:
    try:
        text = path.read_text(encoding='utf-8-sig')
    except (OSError, UnicodeDecodeError):
        return []
    entries: List[FtlEntry] = []
    current: Optional[FtlEntry] = None
    body: List[str] = []

    def close() -> None:
        if current is not None:
            current.text = '\n'.join(body)
            entries.append(current)

    for index, raw in enumerate(text.splitlines(), start=1):
        line = raw.rstrip('\r')
        if line[:1] not in ('', ' ', '\t'):
            close()
            current = None
            match = FTL_ENTRY_RE.match(line)
            if match:
                current = FtlEntry(match.group(1), index, '')
                body = [match.group(2)]
            continue
        if current is None:
            continue
        attr = FTL_ATTR_RE.match(line)
        if attr:
            current.attributes.add(attr.group(1))
        body.append(line)
    close()
    return entries


def ftl_references(s: str) -> List[Tuple[int, str, Optional[str]]]:
    refs: List[Tuple[int, str, Optional[str]]] = []
    n = len(s)

    def ws(i: int) -> int:
        while i < n and s[i] in ' \t\r\n':
            i += 1
        return i

    def pattern(i: int, in_variant: bool) -> int:
        while i < n:
            c = s[i]
            if c == '{':
                i = placeable(i + 1)
                continue
            if in_variant:
                if c == '}':
                    return i
                if c == '\n':
                    j = i + 1
                    while j < n and s[j] in ' \t':
                        j += 1
                    if s.startswith(('[', '*[', '}'), j):
                        return j
            i += 1
        return i

    def inline(i: int) -> int:
        i = ws(i)
        if i >= n:
            return i
        c = s[i]
        if c == '"':
            i += 1
            while i < n and s[i] != '"':
                i += 2 if s[i] == '\\' else 1
            return i + 1
        if c == '{':
            return placeable(i + 1)
        if c == '$':
            match = FTL_VARIABLE_RE.match(s, i)
            return match.end() if match else i + 1
        number = FTL_NUMBER_RE.match(s, i)
        if number:
            return number.end()
        match = FTL_REF_RE.match(s, i)
        if not match:
            return i + 1
        after = match.end()
        j = after
        while j < n and s[j] in ' \t':
            j += 1
        is_call = j < n and s[j] == '('
        if match.group(1) or not is_call:
            refs.append((i, match.group(1) + match.group(2), match.group(3)))
        return call_args(j + 1) if is_call else after

    def call_args(i: int) -> int:
        while i < n:
            i = ws(i)
            if i < n and s[i] == ')':
                return i + 1
            named = FTL_NAMED_ARG_RE.match(s, i)
            start = i
            i = inline(named.end() if named else i)
            i = ws(i)
            if i < n and s[i] == ',':
                i += 1
            elif i < n and s[i] == ')':
                return i + 1
            elif i == start:
                return i + 1
            else:
                return i
        return i

    def placeable(i: int) -> int:
        i = ws(inline(i))
        if s.startswith('->', i):
            i += 2
            while True:
                i = ws(i)
                if i >= n:
                    return n
                if s[i] == '}':
                    return i + 1
                if s[i] == '*':
                    i += 1
                if i < n and s[i] == '[':
                    close = s.find(']', i)
                    i = pattern(n if close == -1 else close + 1, True)
                else:
                    i += 1
        if i < n and s[i] == '}':
            return i + 1
        close = s.find('}', i)
        return n if close == -1 else close + 1

    pattern(0, False)
    return refs


def load_locale_entries(roots: List[Path], repo_root: Path) -> List[Tuple[str, FtlEntry]]:
    result: List[Tuple[str, FtlEntry]] = []
    for root in roots:
        for path in iter_files(root, ('.ftl',)):
            rel = repo_rel(path, repo_root)
            result.extend((rel, entry) for entry in parse_ftl_entries(path))
    return result


def check_ftl_references(entries: List[Tuple[str, FtlEntry]], checked_roots: Tuple[str, ...]) -> List[Issue]:
    defined: Dict[str, Set[str]] = {}
    for _rel, entry in entries:
        defined.setdefault(entry.name, set()).update(entry.attributes)
    issues: List[Issue] = []
    for rel, entry in entries:
        if not rel.startswith(checked_roots) or '{' not in entry.text:
            continue
        for offset, name, attribute in ftl_references(entry.text):
            line = entry.line + entry.text.count('\n', 0, offset)
            if name not in defined:
                issues.append(Issue('missing-ftl-reference', rel, f'"{entry.name}" odwołuje się do nieistniejącego "{name}"', line))
            elif attribute and attribute not in defined[name]:
                issues.append(Issue(
                    'missing-ftl-reference',
                    rel,
                    f'"{entry.name}" odwołuje się do nieistniejącego atrybutu "{name}.{attribute}"',
                    line,
                ))
    return issues


def pattern_regex(pattern: str) -> re.Pattern[str]:
    chunks = pattern.split(WILDCARD)
    body = '[^\n]*'.join(re.escape(chunk) for chunk in chunks if chunk)
    prefix = '' if pattern.startswith(WILDCARD) else '^'
    suffix = '' if pattern.endswith(WILDCARD) else '$'
    return re.compile(prefix + body + suffix, re.MULTILINE)


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
        label = f'{key}  (wzorzec: żaden klucz nie pasuje)' if WILDCARD in key else key
        print(f'  {label}')
        for rel, line, _key in usages[:8]:
            print(f'    {rel}:{line}')
        if len(usages) > 8:
            print(f'    ... i jeszcze {len(usages) - 8}')
        shown += 1


def print_references(issues: List[Issue], limit: int) -> None:
    print(f'\nZepsute odwołania w FTL ({len(issues)}):')
    if not issues:
        print('  (brak)')
        return
    for issue in issues[:limit]:
        print(f'  {issue.path}:{issue.line}  {issue.message}')
    if len(issues) > limit:
        print(f'  ... i jeszcze {len(issues) - limit}')


def locale_dirs(repo_root: Path) -> List[Path]:
    root = repo_root / CONTENT_LOCALE_ROOT
    if not root.is_dir():
        return []
    return [child for child in sorted(root.iterdir()) if child.is_dir() and child.name not in IGNORED_LOCALE_DIRS]


def check_code_locale_keys(repo_root: Path, limit: int) -> int:
    engine_root = repo_root / ENGINE_FTL
    known: Set[str] = set()
    reference_issues: List[Issue] = []
    locales = locale_dirs(repo_root)

    print('=== Code locale keys ===')
    print(f'Repo: {repo_root}')
    print(f'Skan: {", ".join(SCAN_PROJECTS)}')
    for locale in locales:
        roots = [locale]
        if locale.name == ENGINE_LOCALE and engine_root.is_dir():
            roots.append(engine_root)
        entries = load_locale_entries(roots, repo_root)
        names = {entry.name for _rel, entry in entries}
        known |= names
        print(f'FTL {locale.name}: {len(names)} kluczy')
        reference_issues += check_ftl_references(entries, (repo_rel(locale, repo_root) + '/',))
    if not engine_root.is_dir():
        print('Uwaga: brak RobustToolbox/Resources/Locale/en-US — klucze silnika nie wejdą do zbioru')

    hits = scan_projects(repo_root)

    blob = '\n'.join(sorted(known))
    pattern_cache: Dict[str, bool] = {}

    def exists(key: str) -> bool:
        if WILDCARD not in key:
            return key in known
        if key not in pattern_cache:
            pattern_cache[key] = pattern_regex(key).search(blob) is not None
        return pattern_cache[key]

    missing: Dict[str, List[Hit]] = defaultdict(list)
    for rel, line, key in hits:
        if not exists(key):
            missing[key].append((rel, line, key))

    print(f'Kluczy w kodzie: {len(hits)} (wzorców: {sum(1 for h in hits if WILDCARD in h[2])})')
    print_missing(missing, limit)
    print_references(reference_issues, limit)

    unique = len(missing)
    usages = sum(len(v) for v in missing.values())
    print('\nPodsumowanie:')
    print(f'  Unikalne brakujące klucze: {unique}')
    print(f'  Użycia bez FTL: {usages}')
    print(f'  Zepsute odwołania w FTL: {len(reference_issues)}')

    if os.environ.get('GITHUB_ACTIONS'):
        annotations: List[Issue] = []
        for key in sorted(missing):
            rel, line, _key = missing[key][0]
            extra = f' (+{len(missing[key]) - 1} użyć)' if len(missing[key]) > 1 else ''
            if WILDCARD in key:
                message = f'Żaden klucz FTL nie pasuje do wzorca "{key}"{extra}'
            else:
                message = f'Klucz "{key}" jest w kodzie, ale nie ma go w FTL{extra}'
            annotations.append(Issue('missing-ftl-key', rel, message, line))
        annotations += reference_issues
        for issue in annotations[:MAX_ANNOTATIONS]:
            gh_error(issue)
        if len(annotations) > MAX_ANNOTATIONS:
            print(f'::warning::Pokazano {MAX_ANNOTATIONS} z {len(annotations)} problemów. Reszta w logu powyżej.')

    if unique or reference_issues:
        print('\nCode locale keys FAILED')
        return 1
    print('\nCode locale keys OK')
    return 0


def main(argv: Optional[List[str]] = None) -> int:
    parser = argparse.ArgumentParser(
        description='Sprawdza, czy klucze FTL używane przez kod gry (Content.*) i odwołania w FTL istnieją.',
    )
    parser.add_argument(
        '--limit',
        type=int,
        default=40,
        help='maks. unikalnych problemów w logu',
    )
    args = parser.parse_args(argv)
    return check_code_locale_keys(find_repo_root(), args.limit)


if __name__ == '__main__':
    raise SystemExit(main())
