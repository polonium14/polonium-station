### Localization for role ban command

cmd-roleban-desc = Banuje gracza z grania tą rolą
cmd-roleban-help = Użycie: roleban <nazwa lub ID gracza> <rola> <powód> [długość w minutach, pozostaw puste lub 0 dla bana permamentnego]

## Completion result hints

cmd-roleban-hint-1 = <nazwa lub ID gracza>
cmd-roleban-hint-2 = <rola>
cmd-roleban-hint-3 = <powód>
cmd-roleban-hint-4 = [długość w minutach, pozostaw puste lub 0 dla bana permamentnego]
cmd-roleban-hint-5 = [severity]
cmd-roleban-hint-duration-1 = Permamentny
cmd-roleban-hint-duration-2 = 1 dzień
cmd-roleban-hint-duration-3 = 3 dni
cmd-roleban-hint-duration-4 = 1 tydzień
cmd-roleban-hint-duration-5 = 2 tygodnie
cmd-roleban-hint-duration-6 = 1 miesiąc
cmd-roleban-hint-round = Numer rundy sytuacji

### Localization for role unban command

cmd-roleunban-desc = Zdejmuje ban na rolę gracza
cmd-roleunban-help = Użycie: roleunban <id bana na rolę>
cmd-roleunban-unable-to-parse-id =
    Nie można odczytać { $id } jako numeru id bana.
    { $help }

## Completion result hints

cmd-roleunban-hint-1 = <id bana na rolę>

### Localization for roleban list command

cmd-rolebanlist-desc = Wyświetl bany ról gracza
cmd-rolebanlist-help = Użycie: <nazwa lub ID gracza> [zawrzyj odbanowane]

## Completion result hints

cmd-rolebanlist-hint-1 = <nazwa lub ID gracza>
cmd-rolebanlist-hint-2 = [zawrzyj odbanowane]
cmd-roleban-minutes-parse = { $time } nie jest poprawną liczbą minut.\n{ $help }
cmd-roleban-severity-parse = { $severity } nie jest poprawnym poziomem\n{ $help }.
cmd-roleban-arg-count = Niepoprawna liczba argumentów.
cmd-roleban-job-parse = Rola { $job } nie istnieje.
cmd-roleban-name-parse = Nie znaleziono gracza z taką nazwą.
cmd-roleban-round-parse = "{ $round }" nie jest numerem rundy.
cmd-roleban-existing = { $target } już ma bana na { $role }.
cmd-roleban-success = Zbanowano { $target } z roli { $role } z powodem { $reason } { $length }.
cmd-roleban-inf = permamentnie
cmd-roleban-until = do { $expires }
# Department bans
cmd-departmentban-desc = Banuje gracza z ról z danego departamentu
cmd-departmentban-help = Użycie: departmentban <nazwa lub ID gracza> <department> <powód> [długość w minutach, pozostaw puste lub 0 dla bana permamentnego]
