---
applyTo: "**/*.cs"
---

# C# w Polonium Station

Obowiązują [konwencje Wizdenu](https://docs.spacestation14.com/en/general-development/codebase-info/conventions.html) i `.editorconfig`. Zgłaszaj:

- logikę w komponentach: komponenty przechowują dane (`[DataField]`, `[Access]`), logika jest w systemach, a gettery i settery nie mają efektów ubocznych,
- `IoCManager.Resolve` tam, gdzie można użyć `[Dependency]`,
- klasy, które nie są `sealed`, `abstract`, `static` ani oznaczone `[Virtual]`,
- ogólne systemy (interakcje, obrażenia, identyfikacja), które odwołują się do konkretnych funkcji lub encji zamiast wywołać event,
- eventy bez `[ByRefEvent]` lub sufiksu `Event` i handlery, które obsługują event, ale nie ustawiają `args.Handled = true`,
- zmianę stanu sieciowego bez `Dirty` i losowość w przewidywanym kodzie `Content.Shared`, która da inny wynik na kliencie i na serwerze,
- dane od klienta bez sprawdzenia na serwerze (zasięg, stan gracza, uprawnienia, rozmiar) i wyjątki rzucane przez błędne dane,
- czas w `DateTime` albo w liczbach zamiast `TimeSpan` z `IGameTiming.CurTime` oraz pola czasu w komponentach bez `[AutoPausedField]`,
- alokacje, LINQ i przeglądanie wszystkich encji w `Update` lub na gorących ścieżkach, odpytywanie co tick tam, gdzie wystarczy event,
- porzucone `Task`, `.Result`, `.Wait()` oraz bazę danych, sieć lub Discord w `EntitySystem` zamiast w managerze,
- magiczne wartości: ID prototypów jako zwykłe stringi zamiast `ProtoId<T>` lub `EntProtoId`, liczby balansu w kodzie zamiast w `[DataField]` albo w CVarze,
- teksty dla gracza poza `Loc.GetString` albo składane z kawałków zdań,
- istotne działania graczy bez logu administracyjnego,
- bezpowrotne usuwanie ciał graczy i usuwanie encji bez wyrzucenia zawartości ich kontenerów,
- nieużywany kod, skopiowane metody i publiczne API bez dokumentacji XML,
- zmiany w plikach spoza `_Polonium` bez komentarza ze słowem `Polonium` i powodem,
- CVary Polonium poza `Content.Shared/CCVar/_Polonium/`.

Nie proponuj refaktoryzacji kodu z upstreamu ani zmian stylu, których PR nie dotyczy.
