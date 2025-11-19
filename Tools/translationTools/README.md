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

**Uwagi:**
- Skrypt jest bezparametrowy. Korzysta z lokalizacji katalogów projektu ustalanych przez klasę `Project`.
- Uruchamiany automatycznie przez `translation.bat`/`translation.sh`.
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
- pl-PL: nie nadpisuje istniejących wartości, tylko dodaje brakujące atrybuty.
- en-US: nie jest modyfikowany.

**Uwagi:**
- Skrypt jest bezparametrowy - korzysta z konfiguracji ścieżek z klasy `Project`.
- Uruchamiany automatycznie przez `translation.bat`/`translation.sh`.
- Ignorowane foldery są konfigurowalne w stałej `IGNORED_FOLDERS`.

### 3. `clean_duplicates.py`
Usuwa zduplikowane wpisy (wiadomości typu `ent-*` wraz z `.desc` i `.suffix`) w plikach Fluent (.ftl) dla wskazanej lokalizacji.

**Co robi:**
- Przechodzi rekursywnie przez katalog lokalizacji (domyślnie `Resources/Locale/pl-PL`).
- Parsuje bloki zaczynające się od `ent-` aż do pustej linii lub zakończenia sekcji.
- Zachowuje pierwsze wystąpienie każdej encji, kolejne duplikaty usuwa.
- Czyści nadmiarowe puste linie (redukcja wielokrotnych przejść linii do maks. dwóch).
- Tworzy log z informacjami o usuniętych duplikatach.

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
4. Usuwa każdy plik o rozmiarze 0 bajtów.
5. Po przetworzeniu plików próbuje usunąć katalog, jeśli jest pusty.

**Wejście:**
- Struktura katalogów lokalizacji (en-US, pl-PL, inne locale jeśli istnieją) pod `Resources/Locale`.

**Wyjście:**
- Usunięte fizycznie puste pliki i katalogi.
- Log operacji w katalogu uruchomienia + bieżące wypisy w konsoli.

**Uwagi**:
- Nie analizuje zawartości plików .ftl.
- Usuwa wyłącznie pliki o dokładnym rozmiarze 0 bajtów.
- Uruchamiany automatycznie przez `translation.bat`/`translation.sh`.
- Aby ograniczyć czyszczenie do jednej lokalizacji (np. tylko pl-PL), zmień `root_dir = os.path.join(main_folder, "Resources\\Locale")` na `root_dir = os.path.join(main_folder, "Resources\\Locale\\pl-PL")`.

### 5. `dash_normalizer.py`
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

2. **Ręczne czyszczenie (jeśli potrzebne):**
   ```bash
   python clean_duplicates.py
   python clean_empty.py
   python dash_normalizer.py
   ```

## Pliki legacy

- **`translationTool__old.py`** - Stara wersja narzędzia (deprecated). Tworzy jeden pojedynczy plik z lokalizacją wszystkich prototypów.
- **`sync_locales.py`** - Odpowiednik funkcjonalny `keyfinder.py` z mniejszą logiką.
