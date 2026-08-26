# Instrukcja obsługi narzędzi tłumaczeniowych

> [!NOTE]
> Treść tego dokumentu została częściowo wygenerowana przez SI. Jeśli zauważysz błędy, prosimy o ich zgłoszenie.

## Przegląd

Skrypty w tym folderze służą do synchronizacji i zarządzania tłumaczeniami w projekcie Space Station 14, szczególnie po pobieraniu zmian z innych repozytoriów.

**Zasada przenoszenia kluczy:** jeśli klucz już istnieje w locale z tłumaczeniem, ale siedzi w złym pliku, skrypty **przenoszą** ten blok (zachowują treść, wycinają ze starego miejsca, wstawiają na kanoniczną ścieżkę). Nie kopiują angielskiego stuba na istniejące tłumaczenie i nie pomijają klucza tylko dlatego, że „już jest gdzie indziej”. Pusty plik po wycięciu kluczy jest usuwany.

Niezmienione bloki, komentarze, puste linie i końce linii (LF/CRLF) zostają bez zmian — skrypty nie serializują całych `.ftl` od zera.

## Wymagania

Zainstaluj wymagane zależności Pythona (zaleca się korzystanie ze [środowiska wirtualnego (.venv)](https://www.geeksforgeeks.org/python/creating-python-virtual-environment-windows-linux/)):

```bash
pip install -r requirements.txt
```

Python w wersji 3.9 lub nowszej jest wymagany do uruchomienia skryptów.

Uruchamiaj skrypty z katalogu `Tools/translationTools` (klasa `Project` liczy ścieżki względem `cwd`). Wyjątek: `check_locales.py` i `clean_empty.py` same znajdują katalog repo po `.sln` / `.slnx`.

## Główne skrypty

### Windows
```powershell
translation.bat
```

### Linux/Mac
```bash
./translation.sh
```

Te skrypty sekwencyjnie uruchomią `yamlextractor.py`, `merge_generated_structure.py`, `keyfinder.py`, `clean_duplicates.py` oraz `clean_empty.py`. Automatyzują cały proces synchronizacji tłumaczeń.

## Poszczególne narzędzia

### 1. `yamlextractor.py`
Wyodrębnia klucze (nazwy, opisy, sufiksy, itp.) z plików YAML (prototypów) i generuje pliki Fluent (`.ftl`) w katalogach lokalizacji.

**Co robi:**
- Skanuje katalog prototypów projektu.
- Dla każdego pliku YAML wyciąga elementy (m.in. nazwy, opisy, atrybuty) i serializuje je do wiadomości Fluent.
- Aktualizuje `en-US/prototypes/generated` — tylko klucze, które naprawdę się zmieniły (reszta pliku bez ruszenia).
- Tworzy brakujący `pl-PL` jako kopię en-US, potem **przenosi** istniejące polskie tłumaczenia z innych plików na kanoniczną ścieżkę (nadpisuje stub EN).
- Indeksuje całą locale (nie tylko `generated`), żeby znaleźć klucze w starych ścieżkach (np. `_Polonium/...`).
- Na Windows `_RMC14` i `_rmc14` to ten sam plik — skrypt tego nie myli i nie wycina kluczy „z siebie”.

**Wejście:**
- Pliki YAML z prototypami (automatycznie wykrywane na podstawie konfiguracji `project.py`).
- **YAML musi mieć angielskie** `name` / `description` / `suffix` — to źródło dla en-US. Polski tekst należy do `pl-PL/*.ftl`, nie do prototypu.

**Wyjście:**
- Pliki `.ftl` w:
  - `Resources/Locale/en-US/prototypes/generated`
  - `Resources/Locale/pl-PL/prototypes/generated`
- Struktura podkatalogów odpowiada względnym ścieżkom plików prototypów (konwertowana do małych liter).
- Nazwa pliku `.ftl` odpowiada nazwie pliku `.yml`.

**Uwagi:**
- Korzysta z lokalizacji katalogów projektu ustalanych przez klasę `Project`.
- Uruchamiany automatycznie przez `translation.bat`/`translation.sh`.
- Zachowuje istniejące atrybuty typu `.gender` przy regeneracji z YAML.
- Przy nietypowych myślnikach uruchom dodatkowo `dash_normalizer.py` po generacji.

### 1b. `merge_generated_structure.py`
Po `yamlextractor` aktualizuje strukturę Fluent w `pl-PL/prototypes/generated` według `en-US` (referencje do rodziców, atrybuty z YAML), zachowując polskie literały (nazwy, opisy, sufiksy, `.gender`).

Najpierw ściąga tłumaczenia z innych plików pl-PL na kanoniczną ścieżkę, potem łata tylko te wpisy, których struktura naprawdę się różni. Nie przepisuje całego pliku — komentarze i puste linie zostają.

**Po co:** samo `yamlextractor` nie nadpisuje istniejących plików pl-PL — bez tego kroku zostają stare odwołania typu `{ ent-OldParent }`, które psują lokalizację i YAMLLinter.

### 2. `keyfinder.py`
Synchronizuje klucze i pliki między en-US a pl-PL w plikach Fluent (`.ftl`).

**Co robi:**
- Buduje pary plików en-US/pl-PL na podstawie identycznej ścieżki względnej względem katalogów lokalizacji.
- Jeśli brakuje pliku po jednej stronie, tworzy analog (kopia z drugiej locale), potem **przenosi** istniejące tłumaczenia na tę ścieżkę zamiast zostawiać angielski stub.
- Dla kluczy obecnych w pliku źródłowym, a nieobecnych w docelowym: najpierw szuka ich w całej locale i przenosi; dopiero gdy klucza nigdzie nie ma, kopiuje z drugiej locale.
- Nawet gdy kanoniczny plik ma już stub EN, leftover z tłumaczeniem wygrywa (`overwrite`).
- Nie formatuje nowo utworzonych plików (FluentFormatter psułby whitespace przeniesionych bloków).
- Ostrzega, gdy plik pl-PL nie ma angielskiego odpowiednika (z wyjątkiem ścieżek zawierających `robust-toolbox`), w trybie `pl-from-en`.

**Wejście:**
- Automatycznie skanowane katalogi lokalizacji określane przez `project.py`:
  - en-US: `Project.en_locale_dir_path`
  - pl-PL: `Project.pl_locale_dir_path`

**Wyjście:**
- Nowe pliki analogiczne (kopia + przeniesione tłumaczenia).
- Zmodyfikowane pliki z dodanymi albo przeniesionymi kluczami i atrybutami.

**Zasady nadpisywania:**
- Istniejących przetłumaczonych wartości nie nadpisuje kopią z drugiej locale.
- Stub skopiowany z EN **jest** nadpisywany, jeśli ten sam klucz ma już tłumaczenie w innym pliku.

**Tryby (`--mode`):**
- `both` (domyślnie) — dwustronna synchronizacja: pl-PL z en-US i en-US z pl-PL; brakujące pliki tworzone w obu kierunkach.
- `pl-from-en` — tylko uzupełnia pl-PL z en-US; en-US nie jest zmieniany; loguje ostrzeżenia o kluczach/plikach bez odpowiednika w en-US.
- `en-from-pl` — tylko uzupełnia en-US z pl-PL; pl-PL nie jest zmieniany.

**Uwagi:**
- Korzysta z konfiguracji ścieżek z klasy `Project`. Flaga `--add-missing-en` jest przestarzała (równoważna `--mode both`).
- Uruchamiany automatycznie przez `translation.bat`/`translation.sh`.
- Ignorowane foldery są konfigurowalne w stałej `IGNORED_FOLDERS`.
- Katalog `datasets` nie jest indeksowany przy przenoszeniu (za duży — listy imion).

### 3. `clean_duplicates.py`
Usuwa zduplikowane wpisy Fluent w plikach `.ftl` — w jednym pliku i między plikami.

**Co robi:**
- Przechodzi rekursywnie przez katalog lokalizacji (`pl-PL`, `en-US` lub oba — `--locale`).
- Zachowuje pierwsze wystąpienie każdego identyfikatora wiadomości w danej lokalizacji, kolejne kopie wycina ze spanów AST.
- Usuwa zduplikowane atrybuty w obrębie jednej wiadomości (np. podwójne `.desc`).
- Zachowuje oryginalne końce linii pliku.
- Tworzy log z informacjami o usuniętych duplikatach.

```bash
python clean_duplicates.py
python clean_duplicates.py --locale en-US
python clean_duplicates.py --locale both
```

**Wejście:**
- Pliki `.ftl` w katalogu docelowym lokalizacji (iteracja przez wszystkie podfoldery).

**Wyjście:**
- Zmodyfikowane pliki `.ftl` bez powtarzających się wiadomości.
- Plik logu w katalogu uruchomienia.

**Uwagi:**
- Uruchamiany automatycznie przez `translation.bat`/`translation.sh`.
- Po przeniesieniu kluczy na kanoniczne ścieżki ten krok zwykle nie ma już nic do roboty.

### 4. `clean_empty.py`
Czyści strukturę katalogów lokalizacji usuwając puste pliki i puste foldery.

**Co robi:**
1. Odnajduje katalog główny projektu po pliku `SpaceStation14.sln` / `SpaceStation14.slnx`.
2. Ustawia katalog bazowy: `Resources/Locale`.
3. Rekurencyjnie przechodzi przez wszystkie podfoldery.
4. Usuwa pliki o rozmiarze 0 bajtów oraz pliki zawierające wyłącznie białe znaki (spacje, tabulatory, puste linie).
5. Po przetworzeniu plików próbuje usunąć katalog, jeśli jest pusty.

**Wejście:**
- Struktura katalogów lokalizacji (en-US, pl-PL, inne locale jeśli istnieją) pod `Resources/Locale`.

**Wyjście:**
- Usunięte fizycznie puste pliki i katalogi.
- Log operacji w katalogu uruchomienia + bieżące wypisy w konsoli.

**Uwagi:**
- Nie analizuje zawartości plików `.ftl` poza testem „czy jest jakakolwiek treść”.
- Uruchamiany automatycznie przez `translation.bat`/`translation.sh`.
- Aby ograniczyć czyszczenie do jednej lokalizacji (np. tylko pl-PL), zmień `root_dir` w skrypcie na `Resources/Locale/pl-PL`.

### 5. `compare_generated_locales.py`
Porównuje katalogi `Resources/Locale/en-US/prototypes/generated` i `Resources/Locale/pl-PL/prototypes/generated` — strukturę plików lub obecność kluczy Fluent (bez porównywania wartości tłumaczeń).

**Co robi:**
- W trybie `structure` — wykrywa pliki `.ftl` i podkatalogi obecne tylko w jednej lokalizacji.
- W trybie `keys` — dla par plików o tej samej ścieżce względnej porównuje identyfikatory wiadomości (`ent-*`, `-term`) oraz nazwy atrybutów (np. `.desc`, `.suffix`, `.gender`).
- Z flagą `--fix` (tylko z `--mode keys`) — najpierw **przenosi** istniejące tłumaczenia z innych plików na kanoniczną ścieżkę, potem dopisuje to, czego nigdzie nie ma.

**Wejście:**
- Pliki `.ftl` w obu katalogach `prototypes/generated` (ścieżki z `project.py`).

**Wyjście:**
- Raport w konsoli (podsumowanie + listy różnic, limitowane parametrem `--limit`).
- Przy `--fix` — przeniesione i/lub dopisane klucze; reszta pliku bez formatowania.

**Tryby (`--mode`):**
| Tryb | Opis |
|------|------|
| `structure` (domyślny) | Różnice w ścieżkach plików i katalogów |
| `keys` | Różnice w zestawach kluczy w parach plików o identycznej ścieżce |

**Przykłady użycia:**

```bash
cd Tools/translationTools

# Struktura: które pliki/katalogi są tylko po jednej stronie
python compare_generated_locales.py
python compare_generated_locales.py --mode structure --limit 100

# Klucze: które identyfikatory brakują w en-US lub pl-PL (ta sama ścieżka pliku)
python compare_generated_locales.py --mode keys
python compare_generated_locales.py --mode keys --limit 1000

# Wypisz też pliki bez różnic kluczy (diagnostyka)
python compare_generated_locales.py --mode keys --show-equal

# Przenieś / uzupełnij brakujące klucze, potem pokaż raport
python compare_generated_locales.py --mode keys --fix
python compare_generated_locales.py --mode keys --fix --limit 1000
```

**Interpretacja raportu (`--mode keys`):**
- `tylko en-US` — klucz jest w angielskim pliku, brakuje go w polskim odpowiedniku (ta sama ścieżka).
- `tylko pl-PL` — klucz jest w polskim pliku, brakuje go w angielskim odpowiedniku.
- `Wspólne pliki z różnicą kluczy` — lista plików wymagających synchronizacji.

**Uwagi po `--fix`:**
- Jeśli klucz już ma tłumaczenie w innym pliku, `--fix` je **przenosi** (nie kopiuje EN i nie zostawia duplikatu).
- Nowe wpisy, których nigdzie nie było, są kopiowane z drugiej locale — warto je potem przetłumaczyć.
- Skrypt nie jest częścią `translation.bat`/`translation.sh`; uruchamiaj go ręcznie po `yamlextractor.py` albo gdy podejrzewasz rozjazd `generated`.
- Pomoc: `python compare_generated_locales.py --help`

### 6. `check_locales.py`
Audyt CI: czy locale są zsynchronizowane. Nie zmienia plików.

**Co robi:**
- Oczekuje tylko `en-US` i `pl-PL` w parze — `nl-NL` (i inne w `IGNORED_LOCALE_DIRS` w skrypcie) zostaje w repo, ale CI go nie skanuje i nie traktuje jako błąd.
- Łapie puste pliki i puste klucze (bez wartości i atrybutów).
- Łapie duplikaty w jednym pliku i między plikami w tej samej locale.
- Łapie pliki i klucze bez pary en-US ↔ pl-PL, w tym klucz w złym pliku.
- Stosuje te same `ignore` co `crowdin.yml` (obecnie `**/datasets`, `**/accent`, `_Polonium`) po obu stronach en-US i pl-PL.

```bash
python check_locales.py
python check_locales.py --limit 20
```

Na GitHubie to workflow **Locale Check** (`.github/workflows/check-locales.yml`): PR i push na `master` / `staging` / `stable`, gdy ruszane są `Resources/Locale/**` albo `crowdin.yml`. Błędy lecą jako adnotacje na plikach.

`check_locales.py` nie wymaga `pip install` (tylko stdlib).

### 7. `dash_normalizer.py`
Normalizuje myślniki w plikach Fluent (`.ftl`) — zamienia zwykłe łączniki `-` otoczone spacjami na półpauzę `—`.

**Co robi:**
- Przechodzi cały katalog `pl-PL` (wg ścieżek z `project.py`) i przetwarza wszystkie `.ftl`.
- Dla linii w formacie `klucz = wartość` modyfikuje tylko część po `=`.
- Zamienia wyłącznie łącznik `-` występujący pomiędzy białymi znakami na `—`.
- Pomija puste linie, komentarze i elementy list (`#`, `-` na początku linii).
- Nie zmienia myślników na początku lub końcu wartości.

**Wejście:**
- Pliki `.ftl` w `Resources/Locale/pl-PL`.

**Wyjście:**
- Zaktualizowane pliki `.ftl` (nadpisywane w miejscu).
- Informacje w konsoli dla zmienionych plików.

### Moduły pomocnicze

- **`ftl_relocator.py`** — indeks kluczy w locale i przenoszenie bloków na kanoniczną ścieżkę. Używany przez `yamlextractor`, `keyfinder`, `merge_generated_structure` i `compare_generated_locales`. Nie uruchamiaj osobno.
- **`file.py`**, **`project.py`**, **`fluentast.py`**, **`fluentformatter.py`** — I/O, ścieżki, AST. `FluentFile.save_data` zachowuje końce linii istniejącego pliku.

## Typowy workflow

1. **Po pobraniu zmian z upstream:**
   ```bash
   # Windows
   translation.bat

   # Linux/Mac
   ./translation.sh
   ```

2. **Sprawdzenie synchronizacji:**
   ```bash
   python check_locales.py
   python compare_generated_locales.py --mode keys
   ```

3. **Ręczne czyszczenie (jeśli potrzebne):**
   ```bash
   python compare_generated_locales.py --mode keys --fix
   python clean_duplicates.py
   python clean_empty.py
   python dash_normalizer.py
   ```

## Pliki legacy

- **`translationTool__old.py`** — stara wersja narzędzia (deprecated). Tworzy jeden pojedynczy plik z lokalizacją wszystkich prototypów.
- **`sync_locales.py`** — odpowiednik funkcjonalny `keyfinder.py` z mniejszą logiką.

> [!NOTE]
> Wszystkie skrypty w tym folderze są licencjonowane na warunkach GNU Affero General Public License v3.0 (AGPL-3.0).
> Oryginalne komponenty użyte w projekcie były pierwotnie dostarczane na licencji MIT (patrz plik [LICENSE](https://github.com/space-syndicate/space-station-14/blob/master/LICENSE.TXT)).
