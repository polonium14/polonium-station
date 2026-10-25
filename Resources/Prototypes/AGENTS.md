# Prototypy YAML: instrukcje dla agentów AI

Uzupełnienie [głównego AGENTS.md](../../AGENTS.md) o zasady pracy z prototypami.

## Gdzie i jak

- Prototypy Polonium są w `Resources/Prototypes/_Polonium/`. Prototypu z upstreamu nie zmieniaj, jeśli możesz go rozszerzyć własnym (przez `parent`) albo dodać osobny wariant. Gdy zmiana w pliku upstreamu jest konieczna, oznacz ją komentarzem ze słowem `Polonium` i powodem, np. `# Polonium: nasze dźwięki alarmów`.
- Kolejność pól w pierwszym bloku: `type`, `abstract`, `parent`, `id`, `categories`, `name`, `suffix`, `description`, `components`. ID w PascalCase, nazwy pól w camelCase. Listę `categories` i `parent` z kilkoma rodzicami zapisuj w jednej linii (`[ A, B ]`), pozostałe listy zwykle, bez dodatkowego wcięcia elementów. Nie używaj ID w stylu `prefiks.Nazwa`.
- `name`, `description` i `suffix` encji piszemy po angielsku, bo z nich powstają pliki `en-US`. Polskie teksty trafiają do `Resources/Locale/pl-PL/prototypes/generated/` (generuje je `Tools/translationTools/translation.bat` albo `translation.sh`), nigdy do YAML. Pozostałe teksty dla gracza w prototypach (popupy, etykiety UI) podawaj jako klucze FTL.
- Tekstur nie podawaj w prototypach abstrakcyjnych.

## Częste błędy z przeglądów

- Każde dodane pole musi być czytane przez kod. Pole, którego nie używa żaden system, nic nie robi, a wygląda jak działająca mechanika. Sprawdź w C#, zanim je dodasz.
- Nie twórz nowego tagu, jeśli encję da się rozpoznać po istniejącym komponencie (`whitelist: components: [ Xeno ]`). Każdy użyty tag musi być zdefiniowany jako prototyp `Tag`, inaczej testy się nie powiodą.
- Logika należy do C#. Nie buduj w YAML "programów" z warunków i efektów. Gdy mechanika wymaga logiki, dodaj system.
- Prototyp nie może zmieniać komponentów przy spawnie (test zgłasza wtedy `Prototype X modifies component on spawn`). Najczęściej chodzi o kontenery albo fizykę różniące się od zadeklarowanych.
- Zawartość automatycznie napełnianych pojemników musi się w nich zmieścić. Błąd `failed to insert` w logach serwera trzeba naprawić.
- Usuwając prototyp encji albo zmieniając jego ID, dodaj wpis w `Resources/migration.yml`, żeby mapy dalej się ładowały.
- Wyposażenie konkretnej roli dodawaj tam, gdzie pasuje (np. do automatu z ubraniami tej roli), a nie do szafki, która ma już ustalony zestaw.
- Ceny w cargo i nagrody za zlecenia muszą się zgadzać, bo inaczej da się zarabiać na arbitrażu (pilnuje tego `CargoTest`). Zestaw hurtowy kosztuje więcej niż suma pojedynczych zamówień, żeby logistyka miała co robić.
- Zmiany wartości (obrażenia, pancerz, prędkość, ceny, cooldowny) uzasadniaj w opisie PR liczbami i porównaniem z podobnymi przedmiotami.

## Sprawdzenie

Po zmianach uruchom `dotnet run --project Content.YAMLLinter/Content.YAMLLinter.csproj`. Przy nowych encjach i zmianach komponentów uruchom też testy integracyjne (komendy w [głównym AGENTS.md](../../AGENTS.md)).
