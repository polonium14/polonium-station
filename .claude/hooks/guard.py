#!/usr/bin/env python3
"""PreToolUse hook for Claude Code (see .claude/settings.json).

- Blocks Bash/PowerShell commands that could leak secrets: dumping environment
  variables, referencing variables with tokens, reading credential files and
  commands that print stored tokens.
- Blocks commands that change the RobustToolbox submodule or SpaceStation14.slnx.
- Asks the user before every Crowdin MCP call that is not a read.

Manual use: `guard.py --check-env NAME` tells whether a variable is set
without printing its value.
"""
from __future__ import annotations

import json
import os
import re
import sys

SECRET_NAME = r"[A-Za-z0-9_]*(?:TOKEN|SECRET|PASSWORD|PASSWD|API_?KEY|PRIVATE_?KEY|CREDENTIALS?)[A-Za-z0-9_]*"

SECRET_RULES = [
    (r"\bprintenv\b", "wypisanie zmiennych środowiskowych"),
    (r"(?:^|[;&|(]|\s)env(?:\s+(?:-0|--null))?\s*(?:$|[;&|)>])", "wypisanie zmiennych środowiskowych"),
    (r"(?:^|[;&|(]|/c\s)\s*(?:set|export(?:\s+-p)?)\s*(?:$|[;&|)>])", "wypisanie zmiennych powłoki"),
    (r"\b(?:declare|typeset)\s+-[a-zA-Z]*[px]", "wypisanie zmiennych powłoki"),
    (r"/proc/[^\s/]+/environ", "odczyt środowiska procesu"),
    (r"(?i)\b(?:Get-ChildItem|gci|ls|dir|Get-Item|gi|Get-Content|gc|cat|type)\s+(?:-(?:Path|LiteralPath)\s+)?['\"]?env:",
     "wypisanie zmiennych środowiskowych"),
    (r"(?i)\bGetEnvironmentVariables?\b|\bos\.environ\b|\bos\.getenv\b|\bprocess\.env\b|\bSystem\.getenv\b",
     "odczyt zmiennych środowiskowych z kodu"),
    (r"(?i)\$\{?[#!]?(?:env:)?" + SECRET_NAME + r"\b", "odwołanie do zmiennej z sekretem"),
    (r"(?i)%" + SECRET_NAME + r"%", "odwołanie do zmiennej z sekretem"),
    (r"(?i)\bgh\s+auth\s+token\b|--show-token\b", "wypisanie tokenu GitHuba"),
    (r"(?i)\bgit\s+credential\b", "odczyt danych logowania gita"),
    (r"(?i)\bsecurity\s+find-(?:generic|internet)-password\b|\bsecret-tool\s+lookup\b|\bGet-Secret\b|\bGet-StoredCredential\b",
     "odczyt sekretu z pęku kluczy"),
    (r"(?i)\.git-credentials\b|\.netrc\b|\.claude\.json\b|\.credentials\.json\b|\bgh[/\\]hosts\.yml\b|[/\\]\.ssh[/\\]"
     r"|(?:^|[\s'\"=/\\])\.env(?:\.[\w-]+)?(?=$|[\s'\";|&)])",
     "odczyt pliku z danymi logowania"),
]

ENGINE_RULES = [
    (r"\bgit\b[^;&|\n]*\bsubmodule\b[^;&|\n]*\bupdate\b[^;&|\n]*--remote\b", "aktualizacja submodułu RobustToolbox"),
    (r"\bgit\b[^;&|\n]*\badd\b[^;&|\n]*\bRobustToolbox\b", "dodanie zmian submodułu RobustToolbox"),
    (r"\bgit\s+-C\s+['\"]?(?:\./)?RobustToolbox['\"]?\s+(?:checkout|switch|pull|reset|merge|rebase|commit)\b",
     "zmiana wersji RobustToolbox"),
    (r"\bcd\s+['\"]?(?:\./)?RobustToolbox['\"]?\s*(?:&&|;)\s*git\s+(?:checkout|switch|pull|reset|merge|rebase|commit)\b",
     "zmiana wersji RobustToolbox"),
    (r"(?i)\bdotnet\s+sln\b", "zmiana pliku SpaceStation14.slnx"),
]

COMPILED_SECRET_RULES = [(re.compile(p, re.MULTILINE), why) for p, why in SECRET_RULES]
COMPILED_ENGINE_RULES = [(re.compile(p, re.MULTILINE), why) for p, why in ENGINE_RULES]

CROWDIN_PREFIX = "mcp__crowdin__"
CROWDIN_READ_VERBS = {"list", "get", "search", "find", "read", "check", "concordance", "view", "show"}


def block(message: str) -> None:
    sys.stderr.write(message + "\n")
    sys.exit(2)


def ask(reason: str) -> None:
    output = {
        "hookSpecificOutput": {
            "hookEventName": "PreToolUse",
            "permissionDecision": "ask",
            "permissionDecisionReason": reason,
        }
    }
    sys.stdout.write(json.dumps(output))
    sys.exit(0)


def check_command(command: str) -> None:
    for rule, why in COMPILED_SECRET_RULES:
        if rule.search(command):
            block(
                f"Zablokowano: {why}. Ta komenda mogłaby ujawnić token lub hasło w wyniku, "
                "a wynik trafia do historii rozmowy. Nie obchodź tej blokady innym poleceniem. "
                "Żeby sprawdzić, czy zmienna jest ustawiona, użyj "
                "`python .claude/hooks/guard.py --check-env NAZWA` (na Linuksie i macOS `python3`). "
                "Jeśli komenda tylko przeszukuje lub edytuje pliki i nie dotyka sekretów, "
                "użyj narzędzi Grep, Read lub Edit zamiast powłoki."
            )
    for rule, why in COMPILED_ENGINE_RULES:
        if rule.search(command):
            block(
                f"Zablokowano: {why}. Submodułu RobustToolbox i pliku SpaceStation14.slnx nie zmieniamy "
                "w zwykłych PR-ach. Aktualizację silnika maintainerzy robią w osobnym PR."
            )


def check_crowdin(tool_name: str) -> None:
    operation = tool_name[len(CROWDIN_PREFIX):].lower()
    verb = operation.split("_", 1)[0]
    if verb in CROWDIN_READ_VERBS:
        return
    if "approv" in operation:
        ask(
            "Zatwierdzanie tłumaczeń w Crowdinie: tylko na wyraźną prośbę użytkownika, "
            "który jest korektorem projektu, i tylko dla wskazanych tekstów."
        )
    if "delete" in operation or "remove" in operation:
        ask("Usuwanie danych w Crowdinie: potwierdź, że to jest zamierzone.")
    ask(f"Zapis w Crowdinie ({operation}): potwierdź tę operację.")


def check_env(name: str) -> None:
    state = "ustawiona" if os.environ.get(name) else "nieustawiona"
    print(f"{name}: {state}")


def main() -> None:
    for stream in (sys.stdout, sys.stderr):
        stream.reconfigure(encoding="utf-8", errors="replace")

    if len(sys.argv) == 3 and sys.argv[1] == "--check-env":
        check_env(sys.argv[2])
        return

    try:
        event = json.loads(sys.stdin.buffer.read().decode("utf-8"))
    except (ValueError, UnicodeDecodeError):
        return

    tool_name = event.get("tool_name") or ""
    tool_input = event.get("tool_input") or {}

    if tool_name in ("Bash", "PowerShell"):
        check_command(str(tool_input.get("command") or ""))
    elif tool_name.startswith(CROWDIN_PREFIX):
        check_crowdin(tool_name)


if __name__ == "__main__":
    main()
