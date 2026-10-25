# Loading Screen

replay-loading = Wczytywanie ({ $cur }/{ $total })
replay-loading-reading = Odczytywanie plików
replay-loading-processing = Przetwarzanie plików
replay-loading-spawning = Tworzenie encji
replay-loading-initializing = Inicjalizowanie encji
replay-loading-starting = Uruchamianie encji
replay-loading-failed =
    Nie udało się wczytać powtórki. Błąd:
    { $reason }
replay-loading-retry = Try load with more exception tolerance — MAY CAUSE BUGS!
replay-loading-cancel = Anuluj
# Main Menu
replay-menu-subtext = Klient powtórek
replay-menu-load = Wczytaj wybraną powtórkę
replay-menu-select = Wybierz powtórkę
replay-menu-open = Otwórz folder z powtórkami
replay-menu-none = Nie znaleziono powtórek.
# Main Menu Info Box
replay-info-title = Informacje o powtórce
replay-info-none-selected = Nie wybrano powtórki
replay-info-invalid = [color=red]Wybrano nieprawidłową powtórkę[/color]
replay-info-info =
    { "[" }color=gray]Wybrano:[/color]  { $name } ({ $file })
    { "[" }color=gray]Czas:[/color]   { $time }
    { "[" }color=gray]ID rundy:[/color]   { $roundId }
    { "[" }color=gray]Czas trwania:[/color]   { $duration }
    { "[" }color=gray]ForkId:[/color]   { $forkId }
    { "[" }color=gray]Wersja:[/color]   { $version }
    { "[" }color=gray]Silnik:[/color]   { $engVersion }
    { "[" }color=gray]Type Hash:[/color]   { $hash }
    { "[" }color=gray]Comp Hash:[/color]   { $compHash }
# Replay selection window
replay-menu-select-title = Wybierz powtórkę
# Replay related verbs
replay-verb-spectate = Obserwuj
# command
cmd-replay-spectate-help = replay_spectate [opcjonalna encja]
cmd-replay-spectate-desc = Podłącza lokalnego gracza do podanej encji (uid) lub go od niej odłącza.
cmd-replay-spectate-hint = Optional EntityUid
