# SPDX-FileCopyrightText: 2021 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
# SPDX-FileCopyrightText: 2021 Galactic Chimp <63882831+GalacticChimp@users.noreply.github.com>
# SPDX-FileCopyrightText: 2021 mirrorcult <lunarautomaton6@gmail.com>
# SPDX-FileCopyrightText: 2022 actually-reb <61338113+actually-reb@users.noreply.github.com>
# SPDX-FileCopyrightText: 2024 Tadeo <td12233a@gmail.com>
# SPDX-FileCopyrightText: 2024 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
# SPDX-FileCopyrightText: 2025 V <97265903+formlessnameless@users.noreply.github.com>
# SPDX-FileCopyrightText: 2025 corresp0nd <46357632+corresp0nd@users.noreply.github.com>
# SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
#
# SPDX-License-Identifier: MIT


## Entity

crayon-drawing-label =
    Drawing: [color={ $color }]{ $state }[/color] { $infinite ->
       *[false] ({ $charges }/{ $capacity })
        [true] { "" }
    }
crayon-interact-not-enough-left-text = Za mało pozostało.
crayon-interact-used-up-text = { $owner } się zużył.
crayon-interact-invalid-location = Nie możesz tam dosięgnąć!

## UI

crayon-window-title = Kredka
crayon-window-placeholder = Wyszukaj lub wpisz listę nazw oddzieloną przecinkami
crayon-category-1-brushes = Pędzle
crayon-category-2-alphanum = Cyfry i litery
crayon-category-3-symbols = Symbole
crayon-category-4-info = Znaki
crayon-category-5-graffiti = Graffiti
crayon-category-random = Losowe
