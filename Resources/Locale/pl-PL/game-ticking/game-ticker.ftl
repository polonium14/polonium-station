game-ticker-restart-round = Restartowanie rundy...
game-ticker-start-round = Runda właśnie się zaczyna...
game-ticker-start-round-cannot-start-game-mode-fallback = Nie udało się uruchomić trybu { $failedGameMode }! Użyto trybu domyślnego { $fallbackMode }...
game-ticker-start-round-cannot-start-game-mode-restart = Nie udało się uruchomić trybu { $failedGameMode }! Restartowanie rundy...
game-ticker-start-round-invalid-map = Wybrana mapa { $map } jest nieodpowiednia dla trybu { $mode }. Tryb gry może nie działać prawidłowo...
game-ticker-unknown-role = Nieznany
game-ticker-delay-start = Start rundy został opóźniony o { $seconds } sekund.
game-ticker-pause-start = Start rundy został wstrzymany.
game-ticker-pause-start-resumed = Odliczanie do startu rundy zostało wznowione.
game-ticker-player-join-game-message = Witamy w Space Station 14! Jeśli grasz po raz pierwszy, zapoznaj się z zasadami gry i nie bój się prosić o pomoc na czacie LOOC (lokalny OOC) lub OOC (zazwyczaj dostępny tylko między rundami).
game-ticker-get-info-text =
    Cześć i witaj w [color=white]Space Station 14![/color]
    Aktualna runda: [color=white]#{ $roundId }[/color]
    Liczba graczy: [color=white]{ $playerCount }[/color]
    Aktualna mapa: [color=white]{ $mapName }[/color]
    Aktualny tryb gry: [color=white]{ $gmTitle }[/color]
    >[color=yellow]{ $desc }[/color]
game-ticker-get-info-preround-text =
    Cześć i witaj w [color=white]Space Station 14![/color]
    Aktualna runda: [color=white]#{ $roundId }[/color]
    Liczba graczy: [color=white]{ $playerCount }[/color] ([color=white]{ $readyCount }[/color] { $readyCount ->
        [one] jest
       *[other] jest
    } gotowych)
    Aktualna mapa: [color=white]{ $mapName }[/color]
    Aktualny tryb gry: [color=white]{ $gmTitle }[/color]
    >[color=yellow]{ $desc }[/color]
game-ticker-no-map-selected = [color=yellow]Mapa nie została jeszcze wybrana![/color]
game-ticker-player-no-jobs-available-when-joining = Podczas próby dołączenia do gry nie było dostępnych żadnych stanowisk.
# Displayed in chat to admins when a player joins
player-join-message = Gracz { $name } dołączył.
game-ticker-player-no-character-for-job-available-when-joining = Podczas próby dołączenia do gry nie było dostępnych żadnych postaci dla wybranego stanowiska { $job }.
player-first-join-message = Gracz { $name } dołączył po raz pierwszy.
# Displayed in chat to admins when a player leaves
player-leave-message = Gracz { $name } opuścił grę.
latejoin-arrival-announcement = { $character } ({ $job }) przybył(a) na stację!
latejoin-arrival-announcement-special = { $job } { $character } na pokładzie!
latejoin-arrival-announcement-ai = SI stacji "{ $character }" została załadowana i uruchomiona.
latejoin-arrival-sender = Stacja
latejoin-arrivals-direction = Wahadłowiec, który przetransportuje cię na stację, wkrótce przybędzie.
latejoin-arrivals-direction-time = Wahadłowiec, który przetransportuje cię na stację, przybędzie za { $time }.
latejoin-arrivals-dumped-from-shuttle = Tajemnicza siła uniemożliwia ci opuszczenie wahadłowca.
latejoin-arrivals-teleport-to-spawn = Tajemnicza siła teleportuje cię z wahadłowca. Miłej zmiany!
preset-not-enough-ready-players = Nie można uruchomić { $presetName }. Wymaganych jest { $minimumPlayers } graczy, ale mamy tylko { $readyPlayersCount }.
preset-no-one-ready = Nie można uruchomić { $presetName }. Nikt nie jest gotowy.
game-run-level-PreRoundLobby = Lobby przed rundą
game-run-level-InRound = W trakcie rundy
game-run-level-PostRound = Po rundzie
