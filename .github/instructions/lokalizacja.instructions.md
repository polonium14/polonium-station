---
applyTo: "Resources/Locale/**"
---

# Pliki FTL w Polonium Station

- `en-US/` to źródło, a `pl-PL/` to tłumaczenie. Nowy klucz musi być w obu językach, w plikach o tej samej ścieżce. Treści Polonium są w folderach `_Polonium`.
- Pliki obsługiwane przez Crowdin tłumaczy się na Crowdinie. Bot przenosi stamtąd zatwierdzone tłumaczenia w PR "New Crowdin updates". Jeśli inny PR zmienia istniejące polskie tłumaczenia w takich plikach, zapytaj, dlaczego nie przez Crowdin. W repozytorium tłumaczy się pliki wyłączone z Crowdina: `_Polonium`, `datasets`, `accent`, `chat`, `speech` i `_Corvax/StationGoal`.
- Pliki w `prototypes/generated/` powstają ze skryptu na podstawie angielskich `name`, `description` i `suffix` z prototypów. Polski tekst nie trafia do prototypu YAML.

Zgłaszaj:

- zmienne (`{ $user }`), selektory i znaczniki (`[color=...]`, `[bold]`), które różnią się od oryginału,
- brakujące formy liczby (`[one]`, `[few]`, `*[other]`) w polskich tekstach z liczbami,
- usunięte `CAPITALIZE()`,
- terminy niezgodne z `glossary.csv` (np. Head of Security to Komendant, a Warden to Nadzorca Ochrony) i różne terminy dla tej samej rzeczy,
- wielkie litery i interpunkcję inne niż w oryginale, pominięte zdania i błędy gramatyczne (rodzaj, liczba, odmiana),
- nazwy handlowe i znaki towarowe,
- zduplikowane klucze, niepotrzebne zmiany nazw istniejących kluczy i klucze bez znaczenia, np. `popup-1`,
- BOM na początku pliku (pliki są w UTF-8 bez BOM),
- atrybut `.suffix` encji przed `.desc`.
