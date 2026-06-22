# Instrukcja obsługi narzędzi tłumaczeniowych

## Przegląd

Skrypty w tym folderze służą do synchronizacji i zarządzania tłumaczeniami w projekcie Space Station 14, szczególnie po pobieraniu zmian z innych repozytoriów.

## Wymagania

Zainstaluj wymagane zależności Pythona (zaleca się korzystanie ze [środowiska wirtualnego (.venv)](https://www.geeksforgeeks.org/python/creating-python-virtual-environment-windows-linux/)):

```bash
pip install -r requirements.txt
```

Python w wersji 3.9 lub nowszej jest wymagany do uruchomienia skryptów.

## Główne skrypty

### Windows
```powershell
translation.bat
```

### Linux/Mac
```bash
./translation.sh
```

Te skrypty sekwencyjnie uruchomią skrypty `yamlextractor.py`, `keyfinder.py`, `clean_duplicates.py` oraz `clean_empty.py`. Automatyzują cały proces synchronizacji tłumaczeń.

## Poszczególne narzędzia

### 1. `yamlextractor.py`
Wyodrębnia klucze (nazwy, opisy, sufiksy, itp.) z plików YAML (prototypów) i generuje pliki Fluent (.ftl) w katalogach lokalizacji.

**Co robi:**
- Skanuje katalog prototypów projektu.
- Dla każdego pliku YAML wyciąga elementy (m.in. nazwy, opisy, atrybuty) i serializuje je do wiadomości Fluent.
- Generuje/aktualizuje odpowiadający plik `.ftl` po stronie en-US.
- Tworzy plik `.ftl` dla pl-PL tylko, jeśli jeszcze nie istnieje (kopiuje zawartość z en-US jako punkt startowy).

**Wejście:**
- Pliki YAML z prototypami (automatycznie wykrywane na podstawie konfiguracji `project.py`).

**Wyjście:**
- Pliki `.ftl` w:
  - `Resources/Locale/en-US/prototypes/generated`
  - `Resources/Locale/pl-PL/prototypes/generated`
- Struktura podkatalogów odpowiada względnym ścieżkom plików prototypów (konwertowana do małych liter).
- Nazwa pliku `.ftl` odpowiada nazwie pliku `.yml`.

**Tryby (`--mode`):**
- `both` (domyślnie) — aktualizuje en-US z YAML; tworzy brakujący pl-PL jako kopię en-US.
- `en-only` — tylko en-US z YAML; pl-PL nie jest zmieniany.
- `pl-only` — tylko pl-PL z YAML; en-US nie jest zmieniany.

**Uwagi:**
- Korzysta z lokalizacji katalogów projektu ustalanych przez klasę `Project`.
- Uruchamiany automatycznie przez `translation.bat`/`translation.sh` (tryb `both`).
- Aktualizuje pliki punktowo; niezmiennione bloki pozostają bez zmian.
- Przy nietypowych myślnikach uruchom dodatkowo `dash_normalizer.py` po generacji.

### 2. `keyfinder.py`
Synchronizuje klucze i pliki między en-US a pl-PL w plikach Fluent (.ftl).

**Co robi:**
- Buduje pary plików en-US/pl-PL na podstawie identycznej ścieżki względnej względem katalogów lokalizacji.
- Jeśli brakuje pliku pl-PL, tworzy go jako kopię pliku en-US.
- Dla każdej wiadomości i atrybutu obecnych w en-US, a nieobecnych w pl-PL, dodaje brakujące elementy do pliku pl-PL, zachowując kolejność z en-US.
- Nie usuwa nadmiarowych kluczy w pl-PL; loguje ostrzeżenia dla kluczy w pl-PL bez odpowiedników w en-US.
- Ostrzega, gdy plik pl-PL nie ma angielskiego odpowiednika (z wyjątkiem ścieżek zawierających `robust-toolbox`).

**Wejście:**
- Automatycznie skanowane katalogi lokalizacji określane przez `project.py`:
  - en-US: `Project.en_locale_dir_path`
  - pl-PL: `Project.pl_locale_dir_path`

**Wyjście:**
- Nowe pliki pl-PL utworzone z kopii en-US.
- Zmodyfikowane pliki pl-PL z dodanymi brakującymi kluczami i atrybutami.

**Zasady nadpisywania:**
- Nie nadpisuje istniejących wartości, tylko dodaje brakujące klucze i atrybuty.

**Tryby (`--mode`):**
- `both` (domyślnie) — dwustronna synchronizacja: pl-PL z en-US i en-US z pl-PL; brakujące pliki tworzone w obu kierunkach.
- `pl-from-en` — tylko uzupełnia pl-PL z en-US; en-US nie jest zmieniany; loguje ostrzeżenia o kluczach/plikach bez odpowiednika w en-US.
- `en-from-pl` — tylko uzupełnia en-US z pl-PL; pl-PL nie jest zmieniany.

**Uwagi:**
- Korzysta z konfiguracji ścieżek z klasy `Project`. Flaga `--add-missing-en` jest przestarzała (równoważna `--mode both`).
- Uruchamiany automatycznie przez `translation.bat`/`translation.sh`.
- Ignorowane foldery są konfigurowalne w stałej `IGNORED_FOLDERS`.

### 3. `clean_duplicates.py`
Usuwa zduplikowane wpisy Fluent (`ent-*`, `trait-*`, itd.) w plikach `.ftl` — w tym duplikaty w jednym pliku i między plikami.

**Co robi:**
- Przechodzi rekursywnie przez katalog lokalizacji (`pl-PL`, `en-US` lub oba — `--locale`).
- Zachowuje pierwsze wystąpienie każdego identyfikatora wiadomości w danej lokalizacji, kolejne kopie usuwa (według AST, nie `str.replace`).
- Usuwa zduplikowane atrybuty w obrębie jednej wiadomości (np. podwójne `.desc`).
- Zapis tylko w UTF-8; plik zapisywany tylko przy realnej zmianie.
- Tworzy log z informacjami o usuniętych duplikatach.

```bash
python clean_duplicates.py
python clean_duplicates.py --locale en-US
python clean_duplicates.py --locale both
```

**Wejście:**
- Pliki `.ftl` w katalogu docelowym lokalizacji (iteracja przez wszystkie podfoldery).

**Wyjście:**
- Zmodyfikowane pliki `.ftl` bez powtarzających się bloków `ent-*`.
- Plik logu w katalogu uruchomienia.

**Uwagi:**
- Wykrywanie duplikatów opiera się na dosłownym dopasowaniu tekstu całego bloku, tzn. zmiany w formatowaniu mogą uniemożliwić wykrycie duplikatu.
- Nie obsługuje innych typów wiadomości poza wzorcem `ent-*`.
- Usuwanie wykorzystuje `str.replace`, przy identycznych fragmentach kontekstowych może usunąć więcej niż zamierzony blok (rzadkie, ale możliwe).
- Uruchamiany automatycznie przez `translation.bat`/`translation.sh`.

### 4. `clean_empty.py`
Czyści strukturę katalogów lokalizacji usuwając puste pliki i puste foldery.

**Co robi:**
1. Odnajduje katalog główny projektu po pliku `SpaceStation14.sln`.
2. Ustawia katalog bazowy: `Resources/Locale`.
3. Rekurencyjnie przechodzi przez wszystkie podfoldery.
4. Usuwa pliki o rozmiarze 0 bajtów oraz pliki zawierające wyłącznie białe znaki (spacje, tabulatory, puste linie).
5. Po przetworzeniu plików próbuje usunąć katalog, jeśli jest pusty.

**Wejście:**
- Struktura katalogów lokalizacji (en-US, pl-PL, inne locale jeśli istnieją) pod `Resources/Locale`.

**Wyjście:**
- Usunięte fizycznie puste pliki i katalogi.
- Log operacji w katalogu uruchomienia + bieżące wypisy w konsoli.

**Uwagi**:
- Nie analizuje zawartości plików .ftl.
- Usuwa pliki puste lub z samymi białymi znakami (nie analizuje treści `.ftl` z kluczami).
- Uruchamiany automatycznie przez `translation.bat`/`translation.sh`.
- Aby ograniczyć czyszczenie do jednej lokalizacji (np. tylko pl-PL), zmień `root_dir = os.path.join(main_folder, "Resources\\Locale")` na `root_dir = os.path.join(main_folder, "Resources\\Locale\\pl-PL")`.

### 5. `compare_generated_locales.py`
Porównuje katalogi `Resources/Locale/en-US/prototypes/generated` i `Resources/Locale/pl-PL/prototypes/generated` — strukturę plików lub obecność kluczy Fluent (bez porównywania wartości tłumaczeń).

**Co robi:**
- W trybie `structure` — wykrywa pliki `.ftl` i podkatalogi obecne tylko w jednej lokalizacji.
- W trybie `keys` — dla par plików o tej samej ścieżce względnej porównuje identyfikatory wiadomości (`ent-*`, `-term`) oraz nazwy atrybutów (np. `.desc`, `.suffix`, `.gender`).
- Z flagą `--fix` (tylko z `--mode keys`) — dopisuje brakujące klucze i atrybuty z drugiej lokalizacji do pliku partnerskiego (per plik, bez pomijania „globalnych” duplikatów w innych ścieżkach).

**Wejście:**
- Pliki `.ftl` w obu katalogach `prototypes/generated` (ścieżki z `project.py`).

**Wyjście:**
- Raport w konsoli (podsumowanie + listy różnic, limitowane parametrem `--limit`).
- Przy `--fix` — zmodyfikowane pliki `.ftl` w obu lokalizacjach (tylko dopisane brakujące bloki; istniejące wartości nie są nadpisywane).

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

# Uzupełnij brakujące klucze w obu lokalizacjach, potem pokaż raport
python compare_generated_locales.py --mode keys --fix
python compare_generated_locales.py --mode keys --fix --limit 1000
```

**Interpretacja raportu (`--mode keys`):**
- `tylko en-US` — klucz jest w angielskim pliku, brakuje go w polskim odpowiedniku (ta sama ścieżka).
- `tylko pl-PL` — klucz jest w polskim pliku, brakuje go w angielskim odpowiedniku.
- `Wspólne pliki z różnicą kluczy` — lista plików wymagających synchronizacji.
- Klucz może istnieć w pl-PL pod inną ścieżką (np. `deltav/...`) i jednocześnie brakować w `pl-PL/_funkystation/...` — `keyfinder.py` może wtedy nie dopisać go (globalne pomijanie duplikatów); `--fix` w tym skrypcie działa **per plik** i usuwa taką rozbieżność w obrębie `generated`.

**Uwagi po `--fix`:**
- Nowe wpisy w pl-PL są kopiowane z en-US (angielski tekst) — warto je potem przetłumaczyć ręcznie.
- `--fix` **nie** dopisuje klucza, jeśli ten sam identyfikator wiadomości już istnieje gdzie indziej w tej samej lokalizacji (unika duplikatów między `_polonium/...` a `entities/...`).
- Po `--fix` uruchom `python clean_duplicates.py --locale both`, jeśli linter nadal zgłasza duplikaty Fluent.
- Skrypt nie jest częścią `translation.bat`/`translation.sh`; uruchamiaj go ręcznie po `yamlextractor.py` lub gdy podejrzewasz rozjazd `generated`.
- Pomoc: `python compare_generated_locales.py --help`

### 6. `dash_normalizer.py`
Normalizuje myślniki w plikach Fluent (.ftl) – zamienia zwykłe łączniki `-` otoczone spacjami na półpauzę `—`.

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

### Pozostałe skrypty są modułami pomocniczymi.

## Typowy workflow

1. **Po pobraniu zmian z upstream:**
   ```bash
   # Windows
   translation.bat

   # Linux/Mac
   ./translation.sh
   ```

2. **Sprawdzenie `prototypes/generated` (opcjonalnie):**
   ```bash
   python compare_generated_locales.py --mode keys
   python compare_generated_locales.py --mode keys --fix
   ```

3. **Ręczne czyszczenie (jeśli potrzebne):**
   ```bash
   python clean_duplicates.py
   python clean_empty.py
   python dash_normalizer.py
   ```

## Pliki legacy

- **`translationTool__old.py`** - Stara wersja narzędzia (deprecated). Tworzy jeden pojedynczy plik z lokalizacją wszystkich prototypów.
- **`sync_locales.py`** - Odpowiednik funkcjonalny `keyfinder.py` z mniejszą logiką.

> [!NOTE]
> Wszystkie skrypty w tym folderze są licencjonowane na warunkach GNU Affero General Public License v3.0 (AGPL-3.0).
> Oryginalne komponenty użyte w projekcie były pierwotnie dostarczane na licencji MIT (patrz plik [LICENSE](https://github.com/space-syndicate/space-station-14/blob/master/LICENSE.TXT)).
