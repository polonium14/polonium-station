# SPDX-FileCopyrightText: 2021 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
# SPDX-FileCopyrightText: 2021 Galactic Chimp <63882831+GalacticChimp@users.noreply.github.com>
# SPDX-FileCopyrightText: 2022 Duddino <47313600+Duddino@users.noreply.github.com>
# SPDX-FileCopyrightText: 2022 Jessica M <jessica@jessicamaybe.com>
# SPDX-FileCopyrightText: 2022 Kara <lunarautomaton6@gmail.com>
# SPDX-FileCopyrightText: 2022 Mervill <mervills.email@gmail.com>
# SPDX-FileCopyrightText: 2023 Vordenburg <114301317+Vordenburg@users.noreply.github.com>
# SPDX-FileCopyrightText: 2023 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
# SPDX-FileCopyrightText: 2024 lzk <124214523+lzk228@users.noreply.github.com>
# SPDX-FileCopyrightText: 2025 Mish <bluscout78@yahoo.com>
# SPDX-FileCopyrightText: 2025 deathride58 <deathride58@users.noreply.github.com>
# SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
#
# SPDX-License-Identifier: MIT

lobby-state-paused = Wstrzymano
lobby-state-soon = Runda wkrótce się rozpocznie
lobby-state-right-now-question = Natychmiast?
lobby-state-right-now-confirmation = Natychmiast
lobby-state-round-start-countdown-text = Runda rozpocznie się za: { $timeLeft }
lobby-state-ready-button-join-state = Dołącz
lobby-state-ready-button-ready-up-state = Zgłoś gotowość
lobby-state-player-status-not-ready = Brak gotowości
lobby-state-player-status-ready = Gotowy(a)
lobby-state-player-status-observer = Obserwator
lobby-state-player-status-round-not-started = Runda nie została jeszcze rozpoczęta
lobby-state-player-status-round-time =
    Czas rundy: { $hours } { $hours ->
        [1] godzina
       *[other] godzin
    } i { $minutes } { $minutes ->
        [1] minuta
       *[other] minut
    }
lobby-state-song-text = Obecnie grane: [color=white]{ $songTitle }[/color] autorstwa [color=white]{ $songArtist }[/color]
lobby-state-song-no-song-text = Nie odtwarzana jest obecnie żadna piosenka.
lobby-state-song-unknown-title = [color=dimgray]Nieznany tytuł[/color]
lobby-state-song-unknown-artist = [color=dimgray]Nieznany wykonawca[/color]
lobby-state-playtime-comment-normal =
    Spędziłeś(-aś) { $hours } { $hours ->
        [1] godzinę
        [few] godziny
       *[other] godzin
    } dziś w grze. Pamiętaj o przerwach!
lobby-state-playtime-comment-concerning = Dziś grałeś(-aś) { $hours } godzin. Zrób sobie przerwę.
lobby-state-playtime-comment-grasstouchless = Grałeś(-aś) { $hours } godzin. Rozważ wylogowanie się, aby zadbać o swoje potrzeby.
lobby-state-playtime-comment-selfdestructive = { $hours } godzin. Serio?
