# Playback Commands

cmd-replay-play-desc = Wznawia odtwarzanie powtórki.
cmd-replay-play-help = replay_play

cmd-replay-pause-desc = Wstrzymuje odtwarzanie powtórki.
cmd-replay-pause-help = replay_pause

cmd-replay-toggle-desc = Wznawia lub wstrzymuje odtwarzanie powtórki.
cmd-replay-toggle-help = replay_toggle

cmd-replay-stop-desc = Zatrzymuje i rozładowuje powtórkę.
cmd-replay-stop-help = replay_stop

cmd-replay-load-desc = Załadowuje i rozpoczyna powtórkę.
cmd-replay-load-help = replay_load <folder powtórki>
cmd-replay-load-hint = Folder powtórki

cmd-replay-skip-desc = Przeskakuje do przodu lub do tyłu w czasie.
cmd-replay-skip-help = replay_skip <tik lub przedział czasowy>
cmd-replay-skip-hint = Tiki lub przedział czasowy (GG:MM:SS).

cmd-replay-set-time-desc = Skacza do przodu lub do tyłu do zaznaczonego czasu.
cmd-replay-set-time-help = replay_set <tik lub czas>
cmd-replay-set-time-hint = Tik lub przedział czasowy (GG:MM:SS), zaczynając od

cmd-replay-error-time = "{$time}" nie jest liczbą całkowitą lub przedziałem czasowym.
cmd-replay-error-args = Błędna ilość argumentów.
cmd-replay-error-no-replay = Aktualnie nie odtwarzasz powtórkę.
cmd-replay-error-already-loaded = Powtórka już jest załadowana.
cmd-replay-error-run-level = Nie można załadować powtórki podczas trwania połączenia z serwerem.

# Recording commands

cmd-replay-recording-start-desc = Rozpoczyna nagrywanie powtórki, opcjonalnie z określonym limitem czasowym.
cmd-replay-recording-start-help = Użycie: replay_recording_start [name] [overwrite] [time limit]
cmd-replay-recording-start-success = Rozpocznięto nagrywanie powtórki.
cmd-replay-recording-start-already-recording = Już trwa nagrywanie powtórki.
cmd-replay-recording-start-error = Wystąpił błąd podczas próby rozpoczęcia nagrywania..
cmd-replay-recording-start-hint-time = [limit czasowy (w minutach)]
cmd-replay-recording-start-hint-name = [nazwa]
cmd-replay-recording-start-hint-overwrite = [overwrite (bool)]

cmd-replay-recording-stop-desc = Zatrzymuje nagrywanie powtórki.
cmd-replay-recording-stop-help = Użycie: replay_recording_stop
cmd-replay-recording-stop-success = Zatrzymano nagrywanie powtórki.
cmd-replay-recording-stop-not-recording = Aktualnie nie odtwarzasz powtórkę.

cmd-replay-recording-stats-desc = Displays information about the current replay recording. Wyświetla informację o aktualnym nagrywaniu powtórki.
cmd-replay-recording-stats-help = Użycie: replay_recording_stats
cmd-replay-recording-stats-result = Czas trwania: {$time} min, Tiki: {$ticks}, Rozmiar: {$size} MB, szybkość: {$rate} MB/min.


# Time Control UI
replay-time-box-scrubbing-label = Dynamiczne Absorbowanie
replay-time-box-replay-time-label = Czas Nagrywania: {$current} / {$end}  ({$percentage}%)
replay-time-box-server-time-label = Czas Serwera: {$current} / {$end}
replay-time-box-index-label = Indeks: {$current} / {$total}
replay-time-box-tick-label = Tik: {$current} / {$total}
