parse-minutes-fail = Nie udało się odczytać „{ $minutes }” jako minut
parse-session-fail = Nie znaleziono sesji dla „{ $username }”

## Role Timer Commands

# - playtime_addoverall
cmd-playtime_addoverall-desc = Dodaje podaną liczbę minut do ogólnego czasu gry gracza
cmd-playtime_addoverall-help = Użycie: { $command } <nazwa użytkownika> <minuty>
cmd-playtime_addoverall-succeed = Zwiększono ogólny czas gry gracza { $username } do { TOSTRING($time, "dddd\\:hh\\:mm") }
cmd-playtime_addoverall-arg-user = <nazwa użytkownika>
cmd-playtime_addoverall-arg-minutes = <minuty>
cmd-playtime_addoverall-error-args = Oczekiwano dokładnie dwóch argumentów
# - playtime_addrole
cmd-playtime_addrole-desc = Dodaje podaną liczbę minut do czasu gry gracza w danej roli
cmd-playtime_addrole-help = Użycie: { $command } <nazwa użytkownika> <rola> <minuty>
cmd-playtime_addrole-succeed = Zwiększono czas gry gracza { $username } w roli „{ $role }” do { TOSTRING($time, "dddd\\:hh\\:mm") }
cmd-playtime_addrole-arg-user = <nazwa użytkownika>
cmd-playtime_addrole-arg-role = <rola>
cmd-playtime_addrole-arg-minutes = <minuty>
cmd-playtime_addrole-error-args = Oczekiwano dokładnie trzech argumentów
# - playtime_getoverall
cmd-playtime_getoverall-desc = Pobiera ogólny czas gry gracza w minutach
cmd-playtime_getoverall-help = Użycie: { $command } <nazwa użytkownika>
cmd-playtime_getoverall-success = Ogólny czas gry gracza { $username } wynosi { TOSTRING($time, "dddd\\:hh\\:mm") }.
cmd-playtime_getoverall-arg-user = <nazwa użytkownika>
cmd-playtime_getoverall-error-args = Oczekiwano dokładnie jednego argumentu
# - GetRoleTimer
cmd-playtime_getrole-desc = Pobiera wszystkie liczniki ról gracza lub licznik jednej roli
cmd-playtime_getrole-help = Użycie: { $command } <nazwa użytkownika> [rola]
cmd-playtime_getrole-no = Nie znaleziono liczników ról
cmd-playtime_getrole-role = Rola: { $role }, czas gry: { $time }
cmd-playtime_getrole-overall = Ogólny czas gry wynosi { $time }
cmd-playtime_getrole-succeed = Czas gry gracza { $username } wynosi: { TOSTRING($time, "dddd\\:hh\\:mm") }.
cmd-playtime_getrole-arg-user = <nazwa użytkownika>
cmd-playtime_getrole-arg-role = <rola|'Overall'>
cmd-playtime_getrole-error-args = Oczekiwano jednego lub dwóch argumentów
# - playtime_save
cmd-playtime_save-desc = Zapisuje czasy gry gracza w bazie danych
cmd-playtime_save-help = Użycie: { $command } <nazwa użytkownika>
cmd-playtime_save-succeed = Zapisano czas gry gracza { $username }
cmd-playtime_save-arg-user = <nazwa użytkownika>
cmd-playtime_save-error-args = Oczekiwano dokładnie jednego argumentu

## 'playtime_flush' command'

cmd-playtime_flush-desc = Przekazuje aktywne liczniki do wewnętrznego magazynu śledzenia czasu gry.
cmd-playtime_flush-help =
    Użycie: { $command } [nazwa użytkownika]
    To polecenie przekazuje dane tylko do wewnętrznego magazynu, nie zapisuje ich od razu w bazie danych.
    Jeśli podano użytkownika, przekazywane są dane tylko tego użytkownika.
cmd-playtime_flush-error-args = Oczekiwano zera lub jednego argumentu
cmd-playtime_flush-arg-user = [nazwa użytkownika]
