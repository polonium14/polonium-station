# ban
cmd-ban-desc = Banuje kogoś
cmd-ban-help = Użycie: ban <nazwa albo id gracza> <powód> [długość w minutachm, zostaw 0 jeśli ban jest permanentny]
cmd-ban-player = Nie znaleziono takiego gracza o podanej nazwie.
cmd-ban-invalid-minutes = {$minutes} nie jest prawidłową liczbą minut!
cmd-ban-invalid-severity = {$severity} nie jest prawidłowym poziomem surowości!
cmd-ban-invalid-arguments = Nieprawidłowa liczba argumentów
cmd-ban-hint = <nazwa/ID użytkownika>
cmd-ban-hint-reason = <powód>
cmd-ban-hint-duration = [czas trwania]
cmd-ban-hint-severity = [poziom surowości]

cmd-ban-hint-duration-1 = Permanentny
cmd-ban-hint-duration-2 = 1 dzień
cmd-ban-hint-duration-3 = 3 dni
cmd-ban-hint-duration-4 = 1 tydzień
cmd-ban-hint-duration-5 = 2 tygodnie
cmd-ban-hint-duration-6 = 1 miesiąc

# ban panel
cmd-banpanel-desc = Otwiera panel banów
cmd-banpanel-help = Użycie: banpanel [nazwa lub GUID użytkownika]
cmd-banpanel-server = Nie można używać z poziomu konsoli serwera
cmd-banpanel-player-err = Nie znaleziono podanego gracza

# listbans
cmd-banlist-desc = Wyświetla aktywne bany użytkownika.
cmd-banlist-help = Użycie: banlist <nazwa lub ID użytkownika>
cmd-banlist-empty = Nie znaleziono aktywnych banów dla {$user}
cmd-banlist-hint = <nazwa/ID użytkownika>

cmd-ban_exemption_update-desc = Ustawia wyjątek od określonego rodzaju bana dla gracza.
cmd-ban_exemption_update-help = Użycie: ban_exemption_update <gracz> <flaga> [<flaga> [...]]
    Podaj wiele flag, aby nadać graczowi kilka wyjątków od banów.
    Aby usunąć wszystkie wyjątki, uruchom tę komendę i podaj "None" jako jedyną flagę.


cmd-ban_exemption_update-nargs = Oczekiwano co najmniej 2 argumentów
cmd-ban_exemption_update-locate = Nie udało się znaleźć gracza '{$player}'.
cmd-ban_exemption_update-invalid-flag = Nieprawidłowa flaga '{$flag}'.
cmd-ban_exemption_update-success = Zaktualizowano wyjątki od banów dla '{$player}' ({$uid}).
cmd-ban_exemption_update-arg-player = <gracz>
cmd-ban_exemption_update-arg-flag = <flaga>

cmd-ban_exemption_get-desc = Wyświetla wyjątki od banów dla określonego gracza.
cmd-ban_exemption_get-help = Użycie: ban_exemption_get <gracz>

cmd-ban_exemption_get-nargs = Oczekiwano dokładnie 1 argumentu
cmd-ban_exemption_get-none = Użytkownik nie jest wyłączony z żadnych banów.
cmd-ban_exemption_get-show = Użytkownik jest wyłączony z następujących banów: {$flags}.
cmd-ban_exemption_get-arg-player = <gracz>

ban-panel-title = Panel banów
ban-panel-player = Gracz
ban-panel-ip = IP
ban-panel-hwid = HWID
ban-panel-reason = Powód
ban-panel-last-conn = Użyć IP i HWID z ostatniego połączenia?
ban-panel-submit = Zbanuj
ban-panel-confirm = Na pewno?
ban-panel-tabs-basic = Podstawowe informacje
ban-panel-tabs-reason = Powód
ban-panel-tabs-players = Lista graczy
ban-panel-tabs-role = Informacje o banie roli
ban-panel-no-data = Musisz podać użytkownika, IP lub HWID, aby zbanować
ban-panel-invalid-ip = Adres IP nie mógł zostać przetworzony. Spróbuj ponownie
ban-panel-select = Wybierz typ
ban-panel-server = Ban serwerowy
ban-panel-role = Ban roli
ban-panel-minutes = Minuty
ban-panel-hours = Godziny
ban-panel-days = Dni
ban-panel-weeks = Tygodnie
ban-panel-months = Miesiące
ban-panel-years = Lata
ban-panel-permanent = Stały
ban-panel-ip-hwid-tooltip = Pozostaw puste i zaznacz poniżej, aby użyć danych z ostatniego połączenia
ban-panel-severity = Surowość:
ban-panel-erase = Usuń wiadomości na czacie i gracza z rundy

# Ban string
server-ban-string = {$admin} utworzył ban serwerowy o surowości {$severity}, który wygasa {$expires} dla [{$name}, {$ip}, {$hwid}], z powodem: {$reason}
server-ban-string-no-pii = {$admin} utworzył ban serwerowy o surowości {$severity}, który wygasa {$expires} dla {$name} z powodem: {$reason}
server-ban-string-never = nigdy


# Kick on ban
ban-kick-reason = Zostałeś zbanowany
