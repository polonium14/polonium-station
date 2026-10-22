#!/usr/bin/env bash
# Runs guard.py with the first available Python 3 (on Windows `python3` is often a Store stub).
dir="${CLAUDE_PROJECT_DIR:+$CLAUDE_PROJECT_DIR/.claude/hooks}"
dir="${dir:-$(dirname "$0")}"

for p in python3 python py; do
    if command -v "$p" >/dev/null 2>&1 && "$p" -c 'import sys; sys.exit(sys.version_info < (3, 8))' >/dev/null 2>&1 </dev/null; then
        exec "$p" "$dir/guard.py" "$@"
    fi
done

# Without Python the command cannot be checked, so it is always blocked.
echo "Nie znaleziono Pythona 3.8 lub nowszego (python3, python ani py), więc hook bezpieczeństwa nie może sprawdzić tej operacji i ją blokuje. Zainstaluj Pythona 3 i uruchom sesję ponownie." >&2
exit 2
