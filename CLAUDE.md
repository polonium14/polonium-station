@AGENTS.md

## Claude Code

- [.claude/settings.json](.claude/settings.json) blokuje komendy, które mogłyby ujawnić sekrety (zmienne środowiskowe, pliki z danymi logowania, `gh auth token` itp.), oraz zmiany w `RobustToolbox/` i `SpaceStation14.slnx`. Jeśli hook zablokuje komendę, nie obchodź go innym poleceniem, tylko wyjaśnij użytkownikowi, czego potrzebujesz.
- Hook wymaga Pythona 3.8 lub nowszego, a na Windowsie także Git Bash (instaluje się z Git for Windows, inną ścieżkę wskazuje `CLAUDE_CODE_GIT_BASH_PATH`). Bez nich hook blokuje wszystkie komendy powłoki i narzędzia Crowdina. Wtedy poproś użytkownika o instalację brakującego narzędzia i ponowne uruchomienie sesji.
- Żeby sprawdzić, czy zmienna środowiskowa jest ustawiona, bez wypisywania jej wartości, uruchom `python .claude/hooks/guard.py --check-env NAZWA_ZMIENNEJ` (na Linuksie i macOS `python3`).
- Serwer MCP Crowdina jest skonfigurowany w [.mcp.json](.mcp.json) bez sekretów. Logowanie przez OAuth i zasady pracy opisuje [Resources/Locale/AGENTS.md](Resources/Locale/AGENTS.md). Każda operacja zapisu w Crowdinie wymaga potwierdzenia użytkownika.
