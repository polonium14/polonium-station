# Polonium Station: instrukcje dla GitHub Copilota

Polonium Station to polski fork Space Station 14 (C#, silnik RobustToolbox, architektura ECS), oparty na forky-station i upstreamie Wizard's Den. Pełne zasady są w [AGENTS.md](../AGENTS.md) i [CONTRIBUTING.md](../CONTRIBUTING.md). Opisy PR, changelogi i komentarze w przeglądach piszemy po polsku, a identyfikatory i komentarze w kodzie po angielsku.

## Zasady repozytorium

- Każda zmiana rozwiązuje realny, opisany problem. Jeden PR to jeden temat: nowa funkcja, poprawka błędu i refaktoryzacja idą w osobnych PR-ach.
- Nowy kod, prototypy, teksty i zasoby są w folderach `_Polonium`. Pliki upstreamu i innych forków zmienia się tylko wtedy, gdy nie da się inaczej: minimalnie i z komentarzem zawierającym słowo `Polonium` oraz powód. Kodu z upstreamu nie poprawiamy stylistycznie i nie refaktoryzujemy.
- Submoduł `RobustToolbox` i plik `SpaceStation14.slnx` zmieniają tylko maintainerzy, w osobnym PR.
- Każdy tekst widoczny dla gracza jest w FTL i trafia do gry przez `Loc.GetString`. Nowe klucze są w `en-US` i `pl-PL`.
- Balans oceniamy pod mały online. Zmiana liczb wymaga uzasadnienia w opisie PR.
- Grafika i dźwięki mają licencję i autora w `meta.json` lub `attributions.yml`. Nie przyjmujemy grafik, dźwięków ani muzyki wygenerowanych przez AI. Porty z innych forków podają źródło.

## Przegląd PR

Zgłaszaj:

- zmiany niezwiązane z celem PR: przeformatowanie, zmiany nazw, poprawki stylu w plikach upstreamu, przypadkowe zmiany w `.slnx`, `.csproj`, plikach IDE i białych znakach,
- zmiany w plikach spoza `_Polonium` bez komentarza `Polonium` z powodem,
- teksty zapisane na sztywno w C# lub YAML, nowe klucze bez polskiej wersji, zdania sklejane z kawałków w kodzie,
- dane od klienta, których serwer nie sprawdza, wyjątki rzucane przez błędne dane od klienta i stan widoczny dla klienta bez synchronizacji,
- usunięty lub przemianowany prototyp encji bez wpisu w `Resources/migration.yml`,
- nowy system bez testów integracyjnych logiki serwera,
- nową regułę gry, której nie ma w tabelach trybów,
- nową treść (antagonistę, rolę, odczynnik, maszynę, mechanikę), która pasuje do działu podręcznika (`Resources/ServerInfo/_Polonium/Guidebook/`), a nie jest w nim opisana,
- opis PR bez konkretnego powodu zmiany i usunięcia, których opis nie wymienia,
- changelog (`:cl:`), który nie jest po polsku, nie ma kategorii `add`, `remove`, `tweak` lub `fix`, opisuje szczegóły techniczne albo dotyczy zmian niewidocznych dla gracza.

Nie zgłaszaj:

- stylu, który wymusza `.editorconfig`, ani drobiazgów w kodzie, którego PR nie zmienia,
- brakujących nagłówków SPDX, bo dodaje je automatyzacja REUSE,
- sugestii, których nie umiesz uzasadnić konkretnym błędem albo zasadą z tego repozytorium.
