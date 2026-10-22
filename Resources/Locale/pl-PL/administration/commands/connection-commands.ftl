## Strings for the "grant_connect_bypass" command.

cmd-grant_connect_bypass-desc = Tymczasowo pozwala użytkownikowi ominąć zwykłe sprawdzanie połączeń.
cmd-grant_connect_bypass-help =
    Użycie: grant_connect_bypass <użytkownik> [czas w minutach]
    Tymczasowo przyznaje użytkownikowi możliwość ominięcia zwykłych ograniczeń połączeń.
    Obejście dotyczy tylko tego serwera gry i domyślnie wygasa po 1 godzinie.
    Użytkownik będzie mógł dołączyć niezależnie od białej listy, panic bunkra czy limitu graczy.
cmd-grant_connect_bypass-arg-user = <użytkownik>
cmd-grant_connect_bypass-arg-duration = [czas w minutach]
cmd-grant_connect_bypass-invalid-args = Oczekiwano 1 lub 2 argumentów
cmd-grant_connect_bypass-unknown-user = Nie znaleziono użytkownika „{ $user }”
cmd-grant_connect_bypass-invalid-duration = Nieprawidłowy czas „{ $duration }”
cmd-grant_connect_bypass-success = Pomyślnie dodano obejście dla użytkownika „{ $user }”
