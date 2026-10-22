# Baza danych: instrukcje dla agentów AI

Uzupełnienie [głównego AGENTS.md](../AGENTS.md) o zasady pracy z bazą danych (EF Core, Postgres i SQLite).

- Unikaj zmian w bazie, jeśli funkcję da się zrobić bez nich. Każda migracja to dodatkowa praca przy aktualizacjach z upstreamu.
- Nie zmieniaj tabel upstreamu. Dane Polonium trzymaj w osobnych tabelach, powiązanych z tabelami upstreamu relacją jeden do jednego. EF Core źle znosi zmiany w tabelach, które równolegle zmienia upstream.
- Nigdy nie edytuj istniejących migracji. Dodaj nową, osobno dla Postgres (`Migrations/Postgres`) i SQLite (`Migrations/Sqlite`), i sprawdź działanie na obu bazach. Nazwa migracji opisuje zmianę (np. `TutorialCompletion`).
- Każdy model i każde pole ma komentarz XML wyjaśniający, co przechowuje.
- Nie blokuj wątku (`.Result`, `.Wait()`), grupuj zapisy, czasy normalizuj (`NormalizeDatabaseTime`), a dane graczy przypisuj do `NetUserId`, nie do sesji ani nazwy postaci.
- Uważaj na wyrażenia LINQ, które Postgres i SQLite tłumaczą inaczej. W razie wątpliwości przelicz dane po stronie C#.
- Obsługa bazy należy do managerów (`IServerDbManager` i podobnych), nie do systemów encji.
