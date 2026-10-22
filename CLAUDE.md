@AGENTS.md

## Claude Code

- [.claude/settings.json](.claude/settings.json) blokuje komendy, które mogłyby ujawnić sekrety (zmienne środowiskowe, pliki z danymi logowania, `gh auth token` itp.), oraz zmiany w `RobustToolbox/` i `SpaceStation14.slnx`. Jeśli hook zablokuje komendę, nie obchodź go innym poleceniem, tylko wyjaśnij użytkownikowi, czego potrzebujesz.
- Żeby sprawdzić, czy zmienna środowiskowa jest ustawiona, bez wypisywania jej wartości, uruchom `python .claude/hooks/guard.py --check-env NAZWA_ZMIENNEJ` (na Linuksie i macOS `python3`).
- Serwer MCP Crowdina jest skonfigurowany w [.mcp.json](.mcp.json) bez sekretów. Logowanie przez OAuth i zasady pracy opisuje [Resources/Locale/AGENTS.md](Resources/Locale/AGENTS.md). Każda operacja zapisu w Crowdinie wymaga potwierdzenia użytkownika.
