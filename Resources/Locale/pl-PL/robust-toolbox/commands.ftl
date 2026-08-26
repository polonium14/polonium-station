### Lokalizacja dla poleceń konsoli silnika

cmd-hint-float = [float]

## ogólne błędy poleceń

cmd-invalid-arg-number-error = Nieprawidłowa liczba argumentów.
cmd-parse-failure-integer = { $arg } nie jest prawidłową liczbą całkowitą.
cmd-parse-failure-float = { $arg } nie jest prawidłową liczbą zmiennoprzecinkową.
cmd-parse-failure-bool = { $arg } nie jest prawidłową wartością boolowską.
cmd-parse-failure-uid = { $arg } nie jest prawidłowym UID-em encji.
cmd-parse-failure-mapid = { $arg } nie jest prawidłowym MapId.
cmd-parse-failure-enum = { $arg } nie jest prawidłowym elementem Enum typu { $enum }.
cmd-parse-failure-grid = { $arg } nie jest prawidłową siatką.
cmd-parse-failure-cultureinfo = "{ $arg }" nie jest prawidłowym CultureInfo.
cmd-parse-failure-entity-exist = UID { $arg } nie odpowiada żadnej istniejącej encji.
cmd-parse-failure-session = Nie istnieje sesja z nazwą użytkownika: { $username }
cmd-error-file-not-found = Nie można znaleźć pliku: { $file }.
cmd-error-dir-not-found = Nie można znaleźć katalogu: { $dir }.
cmd-failure-no-attached-entity = Do tej powłoki nie jest przypisana żadna encja.

## polecenie 'help'

cmd-help-desc = Wyświetla ogólną pomoc lub tekst pomocy dla konkretnego polecenia
cmd-help-help = { $command }
    Użycie: help [nazwa polecenia]
    Gdy nie podano nazwy polecenia, wyświetlany jest ogólny tekst pomocy.
    Jeśli nazwa polecenia została podana, wyświetlana jest pomoc dla tego polecenia.
cmd-help-no-args =
    Aby wyświetlić pomoc dla konkretnego polecenia, wpisz 'help <polecenie>'.
    Aby zobaczyć listę wszystkich dostępnych poleceń, wpisz 'list'.
    Aby wyszukać polecenia, użyj 'list <filtr>'.
cmd-help-unknown = Nieznane polecenie: { $command }
cmd-help-top = { $command } — { $description }
cmd-help-invalid-args = Nieprawidłowa liczba argumentów.
cmd-help-arg-cmdname = [nazwa polecenia]

## polecenie 'cvar'

cmd-cvar-desc = Pobiera lub ustawia zmienną CVar.
cmd-cvar-help = { $command }
    Użycie: cvar <nazwa | ?> [wartość]
    Jeśli podano wartość, zostanie ona sparsowana i ustawiona jako nowa wartość CVara.
    Jeśli nie, wyświetlona zostanie obecna wartość CVara.
    Użyj 'cvar ?' aby uzyskać listę wszystkich zarejestrowanych CVarów.
cmd-cvar-invalid-args = Należy podać dokładnie jeden lub dwa argumenty.
cmd-cvar-not-registered = CVar '{ $cvar }' nie jest zarejestrowany. Użyj 'cvar ?' aby uzyskać listę wszystkich zarejestrowanych CVarów.
cmd-cvar-parse-error = Wprowadzona wartość ma nieprawidłowy format dla typu { $type }
cmd-cvar-compl-list = Lista dostępnych CVarów
cmd-cvar-arg-name = <nazwa | ?>
cmd-cvar-value-hidden = <wartość ukryta>

## polecenie 'cvar_subs'

cmd-cvar_subs-desc = Wyświetla subskrypcje OnValueChanged dla CVara.
cmd-cvar_subs-help = Użycie: cvar_subs <nazwa> { $command }
cmd-cvar_subs-invalid-args = Należy podać dokładnie jeden argument.
cmd-cvar_subs-arg-name = <nazwa>

## polecenie 'list'

cmd-list-desc = Wyświetla listę dostępnych poleceń, z opcjonalnym filtrem
cmd-list-help = { $command }
    Użycie: list [filtr]
    Wyświetla wszystkie dostępne polecenia.
    Jeśli podano argument, będzie on użyty do filtrowania nazw poleceń.
cmd-list-heading = NAZWA POLECENIA       OPIS{ "\u000A" }-------------------------{ "\u000A" }
cmd-list-arg-filter = [filtr]

## polecenie '>' (remote exec)

cmd-remoteexec-desc = Wykonuje polecenia po stronie serwera
cmd-remoteexec-help =
    Użycie: > <polecenie> [arg] [arg] [arg...]
    Wykonuje polecenie na serwerze.
    Jest to konieczne, jeśli istnieje polecenie o tej samej nazwie po stronie klienta, ponieważ zwykłe uruchomienie spowodowałoby uruchomienie polecenia klienta.

## polecenie 'gc'

cmd-gc-desc = Uruchamia GC (Garbage Collector)
cmd-gc-help = { $command }
    Użycie: gc [generacja]
    Używa GC.Collect() do uruchomienia Garbage Collectora.
    Jeśli podano argument, jest on interpretowany jako numer generacji GC i używane jest GC.Collect(int).
    Użyj polecenia 'gfc' aby wykonać pełny GC z kompaktowaniem LOH.
cmd-gc-failed-parse = Nie udało się sparsować argumentu.
cmd-gc-arg-generation = [generacja]

## polecenie 'gcf'

cmd-gcf-desc = Uruchamia pełny GC, z kompaktowaniem LOH i wszystkiego.
cmd-gcf-help = { $command }
    Użycie: gcf
    Wykonuje pełny GC.Collect(2, GCCollectionMode.Forced, true, true), dodatkowo kompaktując LOH.
    Może to spowodować zawieszenie na setki milisekund – ostrzeżenie.

## polecenie 'gc_mode'

cmd-gc_mode-desc = Zmienia/odczytuje tryb opóźnienia GC
cmd-gc_mode-help = { $command }
    Użycie: gc_mode [typ]
    Jeśli nie podano argumentu, zwraca obecny tryb opóźnienia GC.
    Jeśli podano argument, jest on interpretowany jako GCLatencyMode i ustawiany jako tryb opóźnienia GC.
cmd-gc_mode-current = obecny tryb opóźnienia GC: { $prevMode }
cmd-gc_mode-possible = dostępne tryby:
cmd-gc_mode-option = - { $mode }
cmd-gc_mode-unknown = nieznany tryb opóźnienia GC: { $arg }
cmd-gc_mode-attempt = próba zmiany trybu GC: { $prevMode } -> { $mode }
cmd-gc_mode-result = ustawiony tryb opóźnienia GC: { $mode }
cmd-gc_mode-arg-type = [typ]

## polecenie 'mem'

cmd-mem-desc = Wyświetla informacje o zarządzanej pamięci
cmd-mem-help = Użycie: mem { $command }
cmd-mem-report =
    Rozmiar sterty: { TOSTRING($heapSize, "N0") }
    Całkowita alokacja: { TOSTRING($totalAllocated, "N0") }

## polecenie 'physics'

cmd-physics-overlay = { $overlay } nie jest rozpoznaną nakładką

## polecenie 'lsasm'

cmd-lsasm-desc = Wyświetla listę załadowanych assembly według kontekstu
cmd-lsasm-help = Użycie: lsasm

## polecenie 'exec'

cmd-exec-desc = Wykonuje plik skryptu z zapisywalnego katalogu danych gry
cmd-exec-help = { $command }
    Użycie: exec <plik>
    Każda linia w pliku jest wykonywana jako pojedyncze polecenie, chyba że zaczyna się od #
cmd-exec-arg-filename = <plik>

## polecenie 'dump_net_comps'

cmd-dump_net_comps-desc = Wyświetla tabelę komponentów sieciowych.
cmd-dump_net_comps-help = Użycie: dump_net-comps { $command }
cmd-dump_net_comps-error-writeable = Rejestracja wciąż możliwa do zapisu, identyfikatory sieciowe nie zostały wygenerowane.
cmd-dump_net_comps-header = Rejestracje komponentów sieciowych:

## polecenie 'dump_event_tables'

cmd-dump_event_tables-desc = Wyświetla tabele zdarzeń skierowanych dla encji.
cmd-dump_event_tables-help = Użycie: dump_event_tables <entityUid> { $command }
cmd-dump_event_tables-missing-arg-entity = Brak argumentu encji
cmd-dump_event_tables-error-entity = Nieprawidłowa encja
cmd-dump_event_tables-arg-entity = <entityUid>

## polecenie 'monitor'

cmd-monitor-desc = Przełącza monitor debugowy w menu F3.
cmd-monitor-help = { $command }
    Użycie: monitor <nazwa>
    Dostępne monitory: { $monitors }
    Możesz także użyć specjalnych wartości "-all" i "+all", aby ukryć lub pokazać wszystkie monitory.
cmd-monitor-arg-monitor = <monitor>
cmd-monitor-invalid-name = Nieprawidłowa nazwa monitora
cmd-monitor-arg-count = Brak argumentu monitora
cmd-monitor-minus-all-hint = Ukrywa wszystkie monitory
cmd-monitor-plus-all-hint = Pokazuje wszystkie monitory

## polecenie 'setambientlight'

cmd-set-ambient-light-desc = Umożliwia ustawienie światła otoczenia dla wybranej mapy, w przestrzeni SRGB.
cmd-set-ambient-light-help = setambientlight [mapid] [r g b a] { $command }
cmd-set-ambient-light-parse = Nie można sparsować argumentów jako wartości bajtowych dla koloru.

## Polecenia mapowania

cmd-savemap-desc = Zapisuje mapę na dysk. Nie zapisze mapy po inicjalizacji, chyba że wymuszone.
cmd-savemap-help = savemap <MapID> <Ścieżka> [force] { $command }
cmd-savemap-not-exist = Docelowa mapa nie istnieje.
cmd-savemap-init-warning = Próbowano zapisać mapę po inicjalizacji bez wymuszenia zapisu.
cmd-savemap-attempt = Próba zapisania mapy { $mapId } do { $path }.
cmd-savemap-success = Mapa została pomyślnie zapisana.
cmd-savemap-error = Nie udało się zapisać mapy! Szczegóły w logu serwera.
cmd-hint-savemap-id = <MapID>
cmd-hint-savemap-path = <Ścieżka>
cmd-hint-savemap-force = [bool]
cmd-loadmap-desc = Ładuje mapę z dysku do gry.
cmd-loadmap-help = loadmap <MapID> <Ścieżka> [x] [y] [rotacja] [consistentUids] { $command }
cmd-loadmap-nullspace = Nie można załadować do mapy 0.
cmd-loadmap-exists = Mapa { $mapId } już istnieje.
cmd-loadmap-success = Mapa { $mapId } została załadowana z { $path }.
cmd-loadmap-error = Wystąpił błąd podczas ładowania mapy z { $path }.
cmd-hint-loadmap-x-position = [pozycja-x]
cmd-hint-loadmap-y-position = [pozycja-y]
cmd-hint-loadmap-rotation = [rotacja]
cmd-hint-loadmap-uids = [float]
cmd-hint-savebp-id = <Grid EntityID>

## polecenie 'flushcookies'

# Uwaga: polecenie flushcookies pochodzi z Robust.Client.WebView, nie z głównego kodu silnika.

cmd-flushcookies-desc = Zapisuje pamięć cookie CEF na dysk
cmd-flushcookies-help = { $command }
    Zapewnia, że pliki cookie zostaną poprawnie zapisane na dysk w przypadku nieczystego zamknięcia.
    Zwróć uwagę, że operacja ta jest asynchroniczna.
cmd-ldrsc-desc = Buforuje zasób w pamięci podręcznej.
cmd-ldrsc-help = Użycie: ldrsc <ścieżka> <typ> { $command }
cmd-rldrsc-desc = Przeładowuje zasób.
cmd-rldrsc-help = Użycie: rldrsc <ścieżka> <typ> { $command }
cmd-gridtc-desc = Pobiera liczbę kafelków w siatce.
cmd-gridtc-help = Użycie: gridtc <gridId> { $command }
# Polecenia po stronie klienta
cmd-guidump-desc = Zrzuca drzewo GUI do pliku /guidump.txt w danych użytkownika.
cmd-guidump-help = Użycie: guidump { $command }
cmd-uitest-desc = Otwiera przykładowe okno testowe UI
cmd-uitest-help = Użycie: uitest { $command }

## polecenie 'uitest2'

cmd-uitest2-desc = Otwiera okno testowe UI kontrolki systemowej
cmd-uitest2-help = Użycie: uitest2 <zakładka> { $command }
cmd-uitest2-arg-tab = <zakładka>
cmd-uitest2-error-args = Oczekiwano co najwyżej jednego argumentu
cmd-uitest2-error-tab = Nieprawidłowa zakładka: '{ $value }'
cmd-uitest2-title = UITest2
cmd-setclipboard-desc = Ustawia zawartość systemowego schowka
cmd-setclipboard-help = Użycie: setclipboard <tekst> { $command }
cmd-getclipboard-desc = Pobiera zawartość systemowego schowka
cmd-getclipboard-help = Użycie: getclipboard { $command }
cmd-togglelight-desc = Przełącza renderowanie światła.
cmd-togglelight-help = Użycie: togglelight { $command }
cmd-togglefov-desc = Przełącza pole widzenia (FOV) klienta.
cmd-togglefov-help = Użycie: togglefov { $command }
cmd-togglehardfov-desc = Przełącza „twardy” FOV klienta. (do debugowania space-station-14#2353)
cmd-togglehardfov-help = Użycie: togglehardfov { $command }
cmd-toggleshadows-desc = Przełącza renderowanie cieni.
cmd-toggleshadows-help = Użycie: toggleshadows { $command }
cmd-togglelightbuf-desc = Przełącza renderowanie oświetlenia. Obejmuje cienie, ale nie FOV.
cmd-togglelightbuf-help = Użycie: togglelightbuf { $command }
cmd-chunkinfo-desc = Pobiera informacje o fragmencie mapy pod kursorem myszy.
cmd-chunkinfo-help = Użycie: chunkinfo { $command }
cmd-rldshader-desc = Przeładowuje wszystkie shadery.
cmd-rldshader-help = Użycie: rldshader { $command }
cmd-cldbglyr-desc = Przełącza warstwy debugowania FOV i światła.
cmd-cldbglyr-help = { $command }
    Użycie: cldbglyr <warstwa>: Przełącza <warstwa>
    cldbglyr: Wyłącza wszystkie warstwy
cmd-key-info-desc = Pobiera informacje o klawiszu.
cmd-key-info-help = Użycie: keyinfo <Klawisz> { $command }

## polecenie 'bind'

cmd-bind-desc = Przypisuje kombinację klawiszy do polecenia wejściowego.
cmd-bind-help = { $command }
    Użycie: bind { cmd-bind-arg-key } { cmd-bind-arg-mode } { cmd-bind-arg-command }
    Zauważ, że NIE zapisuje to automatycznie powiązań.
    Użyj polecenia 'svbind', aby zapisać konfigurację powiązań.
cmd-bind-arg-key = <NazwaKlawisza>
cmd-bind-arg-mode = <TrybPowiązania>
cmd-bind-arg-command = <PolecenieWejściowe>
cmd-net-draw-interp-desc = Przełącza debugowanie rysowania interpolacji sieciowej.
cmd-net-draw-interp-help = Użycie: net_draw_interp { $command }
cmd-net-watch-ent-desc = Wypisuje w konsoli wszystkie aktualizacje sieciowe dla EntityId.
cmd-net-watch-ent-help = Użycie: net_watchent <0|EntityUid> { $command }
cmd-net-refresh-desc = Żąda pełnego stanu serwera.
cmd-net-refresh-help = Użycie: net_refresh { $command }
cmd-net-entity-report-desc = Przełącza panel raportu encji sieciowych.
cmd-net-entity-report-help = Użycie: net_entityreport { $command }
cmd-fill-desc = Wypełnia konsolę dla celów debugowania.
cmd-fill-help = Wypełnia konsolę losowymi danymi do debugowania. { $command }
cmd-cls-desc = Czyści konsolę.
cmd-cls-help = Czyści konsolę debugowania ze wszystkich wiadomości. { $command }
cmd-sendgarbage-desc = Wysyła śmieci do serwera.
cmd-sendgarbage-help = Serwer odpowie 'no u' { $command }
cmd-loadgrid-desc = Ładuje grid z pliku do istniejącej mapy.
cmd-loadgrid-help = loadgrid <MapID> <Ścieżka> [x y] [obrót] [storeUids] { $command }
cmd-loc-desc = Wypisuje w konsoli absolutną pozycję encji gracza.
cmd-loc-help = loc { $command }
cmd-tpgrid-desc = Teleportuje grid do nowej lokalizacji.
cmd-tpgrid-help = tpgrid <gridId> <X> <Y> [<MapId>] { $command }
cmd-rmgrid-desc = Usuwa grid z mapy. Nie można usunąć domyślnego gridu.
cmd-rmgrid-help = rmgrid <gridId> { $command }
cmd-mapinit-desc = Uruchamia inicjalizację mapy.
cmd-mapinit-help = mapinit <mapID> { $command }
cmd-lsmap-desc = Wyświetla listę map.
cmd-lsmap-help = lsmap { $command }
cmd-lsgrid-desc = Wyświetla listę gridów.
cmd-lsgrid-help = lsgrid { $command }
cmd-addmap-desc = Dodaje nową pustą mapę do rundy. Jeśli mapID już istnieje, ta komenda nic nie robi.
cmd-addmap-help = addmap <mapID> [pre-init] { $command }
cmd-rmmap-desc = Usuwa mapę ze świata. Nie można usunąć nullspace.
cmd-rmmap-help = rmmap <mapId> { $command }
cmd-savegrid-desc = Zapisuje grid na dysk.
cmd-savegrid-help = savegrid <gridID> <Ścieżka> { $command }
cmd-testbed-desc = Ładuje środowisko testowe fizyki na wybranej mapie.
cmd-testbed-help = testbed <mapid> <test> { $command }

## 'flushcookies' command

# Uwaga: komenda flushcookies pochodzi z Robust.Client.WebView, nie jest częścią głównego silnika.

## 'addcomp' command

cmd-addcomp-desc = Dodaje komponent do encji.
cmd-addcomp-help = addcomp <uid> <nazwaKomponentu> { $command }
cmd-addcompc-desc = Dodaje komponent do encji po stronie klienta.
cmd-addcompc-help = addcompc <uid> <nazwaKomponentu> { $command }

## 'rmcomp' command

cmd-rmcomp-desc = Usuwa komponent z encji.
cmd-rmcomp-help = rmcomp <uid> <nazwaKomponentu> { $command }
cmd-rmcompc-desc = Usuwa komponent z encji po stronie klienta.
cmd-rmcompc-help = rmcomp <uid> <nazwaKomponentu> { $command }

## 'addview' command

cmd-addview-desc = Pozwala subskrybować widok encji do celów debugowania.
cmd-addview-help = addview <entityUid> { $command }
cmd-addviewc-desc = Pozwala subskrybować widok encji do celów debugowania.
cmd-addviewc-help = addview <entityUid> { $command }

## 'removeview' command

cmd-removeview-desc = Pozwala odsubskrybować widok encji do celów debugowania.
cmd-removeview-help = removeview <entityUid> { $command }

## 'loglevel' command

cmd-loglevel-desc = Zmienia poziom logowania dla wybranego sawmilla.
cmd-loglevel-help = { $command }
    Użycie: loglevel <sawmill> <poziom>
    sawmill: Etykieta poprzedzająca wiadomości logów. Dla niej ustawiany jest poziom.
    poziom: Poziom logowania. Musi odpowiadać wartościom z wyliczenia LogLevel.
cmd-testlog-desc = Zapisuje testowy log do sawmilla.
cmd-testlog-help = { $command }
    Użycie: testlog <sawmill> <poziom> <wiadomość>
    sawmill: Etykieta poprzedzająca logowaną wiadomość.
    poziom: Poziom logowania. Musi odpowiadać wartościom z wyliczenia LogLevel.
    wiadomość: Wiadomość do zalogowania. Użyj cudzysłowów, jeśli zawiera spacje.

## 'vv' command

cmd-vv-desc = Otwiera View Variables.
cmd-vv-help = Użycie: vv <ID encji|nazwa interfejsu IoC|nazwa interfejsu SIoC> { $command }

## 'showvelocities' command

cmd-showvelocities-desc = Wyświetla prędkości kątowe i liniowe gracza.
cmd-showvelocities-help = Użycie: showvelocities { $command }

## 'setinputcontext' command

cmd-setinputcontext-desc = Ustawia aktywny kontekst wejściowy.
cmd-setinputcontext-help = Użycie: setinputcontext <kontekst> { $command }

## 'forall' command

cmd-forall-desc = Wykonuje komendę na wszystkich encjach z danym komponentem.
cmd-forall-help = Użycie: forall <zapytanie bql> do <komenda...> { $command }

## 'delete' command

cmd-delete-desc = Usuwa encję o podanym ID.
cmd-delete-help = delete <UID encji> { $command }
# System commands
cmd-showtime-desc = Wyświetla czas serwera.
cmd-showtime-help = showtime { $command }
cmd-restart-desc = Łagodnie restartuje serwer (nie tylko rundę).
cmd-restart-help = restart { $command }
cmd-shutdown-desc = Łagodnie wyłącza serwer.
cmd-shutdown-help = shutdown { $command }
cmd-saveconfig-desc = Zapisuje konfigurację serwera do pliku konfiguracyjnego.
cmd-saveconfig-help = saveconfig { $command }
cmd-netaudit-desc = Wyświetla informacje o bezpieczeństwie NetMsg.
cmd-netaudit-help = netaudit { $command }
# Player commands
cmd-tp-desc = Teleportuje gracza do dowolnej lokalizacji w rundzie.
cmd-tp-help = tp <x> <y> [<mapID>] { $command }
cmd-tpto-desc = Teleportuje obecnego gracza lub podanych graczy/encje do lokalizacji pierwszego gracza/encji.
cmd-tpto-help = tpto <nazwaUżytkownika|uid> [nazwaUżytkownika|NetEntity]... { $command }
cmd-tpto-destination-hint = cel (NetEntity lub nazwaUżytkownika)
cmd-tpto-victim-hint = encja do teleportacji (NetEntity lub nazwaUżytkownika)
cmd-tpto-parse-error = Nie można odnaleźć encji ani gracza: { $str }
cmd-listplayers-desc = Wyświetla listę wszystkich obecnie połączonych graczy.
cmd-listplayers-help = listplayers { $command }
cmd-kick-desc = Wyrzuca połączonego gracza z serwera, rozłączając go.
cmd-kick-help = kick <IndeksGracza> [<Powód>] { $command }
# Spin command
cmd-spin-desc = Powoduje, że encja zaczyna się obracać. Domyślnie encja nadrzędna gracza.
cmd-spin-help = spin prędkość [tarcie] [entityUid] { $command }
# Localization command
cmd-rldloc-desc = Przeładowuje lokalizację (klient i serwer).
cmd-rldloc-help = Użycie: rldloc { $command }
# Debug entity controls
cmd-spawn-desc = Spawnuje encję określonego typu.
cmd-spawn-help = spawn <prototyp> LUB spawn <prototyp> <ID encji względnej> LUB spawn <prototyp> <x> <y> { $command }
cmd-cspawn-desc = Spawnuje encję po stronie klienta u stóp gracza.
cmd-cspawn-help = cspawn <typ encji> { $command }
cmd-scale-desc = Zwiększa lub zmniejsza rozmiar encji w prosty sposób.
cmd-scale-help = scale <entityUid> <float>
cmd-dumpentities-desc = Zrzuca listę encji.
cmd-dumpentities-help = Zrzuca listę encji z UID-ami i prototypami. { $command }
cmd-getcomponentregistration-desc = Pobiera informacje o rejestracji komponentu.
cmd-getcomponentregistration-help = Użycie: getcomponentregistration <nazwaKomponentu> { $command }
cmd-showrays-desc = Przełącza debugowanie promieni fizyki. Należy podać liczbę całkowitą dla <raylifetime>.
cmd-showrays-help = Użycie: showrays <raylifetime> { $command }
cmd-disconnect-desc = Natychmiast rozłącza z serwerem i wraca do głównego menu.
cmd-disconnect-help = Użycie: disconnect { $command }
cmd-entfo-desc = Wyświetla szczegółową diagnostykę encji.
cmd-entfo-help = { $command }
    Użycie: entfo <entityuid>
    UID encji można poprzedzić literą 'c', aby przekonwertować go na UID encji klienckiej.
cmd-fuck-desc = Rzuca wyjątek
cmd-fuck-help = Użycie: fuck { $command }
cmd-showpos-desc = Pokazuje pozycję wszystkich encji na ekranie.
cmd-showpos-help = Użycie: showpos { $command }
cmd-showrot-desc = Pokazuje rotację wszystkich encji na ekranie.
cmd-showrot-help = Użycie: showrot { $command }
cmd-showvel-desc = Pokazuje lokalną prędkość wszystkich encji na ekranie.
cmd-showvel-help = Użycie: showvel { $command }
cmd-showangvel-desc = Pokazuje prędkość kątową wszystkich encji na ekranie.
cmd-showangvel-help = Użycie: showangvel { $command }
cmd-sggcell-desc = Wypisuje encje w komórce snap grid.
cmd-sggcell-help = Użycie: sggcell <gridID> <vector2i>\nParametr vector2i ma formę x<int>,y<int>. { $command }
cmd-overrideplayername-desc = Zmienia nazwę używaną przy próbie połączenia z serwerem.
cmd-overrideplayername-help = Użycie: overrideplayername <nazwa> { $command }
cmd-showanchored-desc = Pokazuje unieruchomione encje na danym kafelku.
cmd-showanchored-help = Użycie: showanchored { $command }
cmd-dmetamem-desc = Zrzuca członków typu w formacie odpowiednim do pliku konfiguracyjnego sandboxa.
cmd-dmetamem-help = Użycie: dmetamem <typ> { $command }
cmd-launchauth-desc = Ładuje tokeny uwierzytelniające z danych launchera, aby ułatwić testowanie serwerów produkcyjnych.
cmd-launchauth-help = Użycie: launchauth <nazwaKonta> { $command }
cmd-lightbb-desc = Przełącza widoczność ramek ograniczających światła.
cmd-lightbb-help = Użycie: lightbb { $command }
cmd-monitorinfo-desc = Wyświetla informacje o monitorze
cmd-monitorinfo-help = Użycie: monitorinfo <id> { $command }
cmd-setmonitor-desc = Ustawia monitor
cmd-setmonitor-help = Użycie: setmonitor <id> { $command }
cmd-physics-desc = Pokazuje debugowy overlay fizyki. Podany argument określa nakładkę.
cmd-physics-help = Użycie: physics <aabbs / com / contactnormals / contactpoints / distance / joints / shapeinfo / shapes> { $command }
cmd-hardquit-desc = Natychmiast zabija klienta gry.
cmd-hardquit-help = Natychmiast zabija klienta gry, nie zostawiając śladów. Nie informuje serwera o rozłączeniu. { $command }
cmd-quit-desc = Zamyka klienta gry w sposób kontrolowany.
cmd-quit-help = Poprawnie zamyka klienta gry, powiadamiając podłączony serwer itd. { $command }
cmd-csi-desc = Otwiera interaktywną konsolę C#.
cmd-csi-help = Użycie: csi { $command }
cmd-scsi-desc = Otwiera interaktywną konsolę C# po stronie serwera.
cmd-scsi-help = Użycie: scsi { $command }
cmd-watch-desc = Otwiera okno podglądu zmiennych.
cmd-watch-help = Użycie: watch { $command }
cmd-showspritebb-desc = Przełącza widoczność granic sprite’ów
cmd-showspritebb-help = Użycie: showspritebb { $command }
cmd-togglelookup-desc = Pokazuje/ukrywa granice entitylookup poprzez nakładkę.
cmd-togglelookup-help = Użycie: togglelookup { $command }
cmd-net_entityreport-desc = Przełącza panel raportu encji sieciowych.
cmd-net_entityreport-help = Użycie: net_entityreport { $command }
cmd-net_refresh-desc = Żąda pełnego stanu serwera.
cmd-net_refresh-help = Użycie: net_refresh { $command }
cmd-net_graph-desc = Przełącza panel statystyk sieciowych.
cmd-net_graph-help = Użycie: net_graph { $command }
cmd-net_watchent-desc = Zrzuca wszystkie aktualizacje sieciowe dla EntityId do konsoli.
cmd-net_watchent-help = Użycie: net_watchent <0|EntityUid> { $command }
cmd-net_draw_interp-desc = Przełącza debugowe rysowanie interpolacji sieciowej.
cmd-net_draw_interp-help = Użycie: net_draw_interp <0|EntityUid> { $command }
cmd-vram-desc = Wyświetla statystyki użycia pamięci wideo przez grę.
cmd-vram-help = Użycie: vram { $command }
cmd-showislands-desc = Pokazuje aktualne ciała fizyczne w każdej wyspie fizyki.
cmd-showislands-help = Użycie: showislands { $command }
cmd-showgridnodes-desc = Pokazuje węzły podziału siatki.
cmd-showgridnodes-help = Użycie: showgridnodes { $command }
cmd-profsnap-desc = Tworzy snapshot profilowania.
cmd-profsnap-help = Użycie: profsnap { $command }
cmd-devwindow-desc = Okno developerskie
cmd-devwindow-help = Użycie: devwindow { $command }
cmd-scene-desc = Natychmiast zmienia scenę/stan UI.
cmd-scene-help = Użycie: scene <nazwaKlasy> { $command }
cmd-szr_stats-desc = Raportuje statystyki serializatora.
cmd-szr_stats-help = Użycie: szr_stats { $command }
cmd-hwid-desc = Zwraca aktualne HWID (identyfikator sprzętu).
cmd-hwid-help = Użycie: hwid { $command }
cmd-vvread-desc = Pobiera wartość ścieżki przy użyciu VV (View Variables).
cmd-vvread-help = Użycie: vvread <ścieżka> { $command }
cmd-vvwrite-desc = Modyfikuje wartość ścieżki przy użyciu VV (View Variables).
cmd-vvwrite-help = Użycie: vvwrite <ścieżka> { $command }
cmd-vvinvoke-desc = Wywołuje/uruchamia ścieżkę z argumentami przy użyciu VV.
cmd-vvinvoke-help = Użycie: vvinvoke <ścieżka> [argumenty...] { $command }
cmd-dump_dependency_injectors-desc = Zrzuca cache injectorów zależności IoCManagera.
cmd-dump_dependency_injectors-help = Użycie: dump_dependency_injectors { $command }
cmd-dump_dependency_injectors-total-count = Całkowita liczba: { $total }
cmd-dump_netserializer_type_map-desc = Zrzuca mapę typów NetSerializera i hash serializatora.
cmd-dump_netserializer_type_map-help = Użycie: dump_netserializer_type_map { $command }
cmd-hub_advertise_now-desc = Natychmiast reklamuje się do głównego serwera hub.
cmd-hub_advertise_now-help = Użycie: hub_advertise_now { $command }
cmd-echo-desc = Wypisuje podane argumenty z powrotem do konsoli
cmd-echo-help = Użycie: echo "<wiadomość>" { $command }

## 'vfs_ls' command

cmd-vfs_ls-desc = Wypisuje zawartość katalogu w VFS.
cmd-vfs_ls-help = { $command }
    Użycie: vfs_list <ścieżka>
    Przykład:
    vfs_list /Assemblies
cmd-vfs_ls-err-args = Wymagany dokładnie 1 argument.
cmd-vfs_ls-hint-path = <ścieżka>
cmd-reloadtiletextures-desc = Przeładowuje atlas tekstur kafelków, aby umożliwić hot reload sprite’ów kafelków.
cmd-reloadtiletextures-help = Użycie: reloadtiletextures { $command }
cmd-audio_length-desc = Pokazuje długość pliku audio.
cmd-audio_length-help = Użycie: audio_length { cmd-audio_length-arg-file-name } { $command }
cmd-audio_length-arg-file-name = <nazwa pliku>

## PVS

cmd-pvs-override-info-desc = Wyświetla informacje o nadpisaniach PVS związanych z encją.
cmd-pvs-override-info-empty = Encja { $nuid } nie ma nadpisania PVS.
cmd-pvs-override-info-global = Encja { $nuid } ma globalne nadpisanie.
cmd-pvs-override-info-clients = Encja { $nuid } ma nadpisanie sesji dla { $clients }.
cmd-localization_set_culture-desc = Ustawia DefaultCulture dla klienta LocalizationManager.
cmd-localization_set_culture-help = Użycie: localization_set_culture <nazwaKultury> { $command }
cmd-localization_set_culture-culture-name = <nazwaKultury>
cmd-localization_set_culture-changed = Lokalizacja zmieniona na { $code } ({ $nativeName } / { $englishName })

cmd-pausemap-desc = Pauses a map, pausing all simulation processing on it.

cmd-pausemap-help = Usage: pausemap <map ID>

cmd-unpausemap-desc = Unpauses a map, resuming all simulation processing on it.

cmd-unpausemap-help = Usage: unpausemap <map ID>

cmd-querymappaused-desc = Check whether a map is paused or not.

cmd-querymappaused-help = Usage: querymappaused <map ID>

cmd-addmap-hint-2 = runMapInit [true / false]
