# SPDX-FileCopyrightText: 2021 Galactic Chimp <63882831+GalacticChimp@users.noreply.github.com>
# SPDX-FileCopyrightText: 2021 moonheart08 <moonheart08@users.noreply.github.com>
# SPDX-FileCopyrightText: 2022 20kdc <asdd2808@gmail.com>
# SPDX-FileCopyrightText: 2022 Kara <lunarautomaton6@gmail.com>
# SPDX-FileCopyrightText: 2022 Moony <moonheart08@users.noreply.github.com>
# SPDX-FileCopyrightText: 2022 Morber <14136326+Morb0@users.noreply.github.com>
# SPDX-FileCopyrightText: 2022 wrexbe <81056464+wrexbe@users.noreply.github.com>
# SPDX-FileCopyrightText: 2023 Chief-Engineer <119664036+Chief-Engineer@users.noreply.github.com>
# SPDX-FileCopyrightText: 2023 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
# SPDX-FileCopyrightText: 2023 Moony <moony@hellomouse.net>
# SPDX-FileCopyrightText: 2023 crazybrain23 <44417085+crazybrain23@users.noreply.github.com>
# SPDX-FileCopyrightText: 2024 Brandon Hu <103440971+Brandon-Huu@users.noreply.github.com>
# SPDX-FileCopyrightText: 2024 Simon <63975668+Simyon264@users.noreply.github.com>
# SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
#
# SPDX-License-Identifier: MIT


### for technical and/or system messages


## General

shell-command-success = Komenda wykonana pomyślnie
shell-invalid-command = Nieprawidłowa komenda.
shell-invalid-command-specific = Nieprawidłowa komenda { $commandName }.
shell-cannot-run-command-from-server = Nie możesz uruchomić tej komendy z serwera.
shell-only-players-can-run-this-command = Tylko gracze mogą uruchomić tę komendę.
shell-must-be-attached-to-entity = Musisz być przypisany do encji, aby uruchomić tę komendę.

## Arguments

shell-need-exactly-one-argument = Wymagany dokładnie jeden argument.
shell-wrong-arguments-number-need-specific = Wymagane { $properAmount } argumenty, podano { $currentAmount }.
shell-argument-must-be-number = Argument musi być liczbą.
shell-argument-must-be-boolean = Argument musi być wartością logiczną (true/false).
shell-wrong-arguments-number = Nieprawidłowa liczba argumentów.
shell-need-between-arguments = Wymagane od { $lower } do { $upper } argumentów!
shell-need-minimum-arguments = Wymagane co najmniej { $minimum } argumentów!
shell-need-minimum-one-argument = Wymagany co najmniej jeden argument!
shell-argument-uid = EntityUid

## Guards

shell-entity-is-not-mob = Docelowa encja nie jest mobem!
shell-invalid-entity-id = Nieprawidłowy identyfikator encji.
shell-invalid-grid-id = Nieprawidłowy identyfikator siatki.
shell-invalid-map-id = Nieprawidłowy identyfikator mapy.
shell-invalid-entity-uid = { $uid } nie jest prawidłowym identyfikatorem encji (uid).
shell-invalid-bool = Nieprawidłowa wartość logiczna.
shell-entity-uid-must-be-number = EntityUid musi być liczbą.
shell-could-not-find-entity = Nie znaleziono encji { $entity }.
shell-could-not-find-entity-with-uid = Nie znaleziono encji o uid { $uid }.
shell-entity-with-uid-lacks-component = Entity with uid { $uid } doesn't have { INDEFINITE($componentName) } { $componentName } component
shell-invalid-color-hex = Nieprawidłowy kolor w formacie hex!
shell-target-player-does-not-exist = Docelowy gracz nie istnieje!
shell-target-entity-does-not-have-message = Target entity does not have { INDEFINITE($missing) } { $missing }!
shell-timespan-minutes-must-be-correct = { $span } nie jest prawidłowym przedziałem czasu w minutach.
shell-argument-must-be-prototype = Argument { $index } musi być typu { LOC($prototypeName) }!
shell-argument-number-must-be-between = Argument { $index } musi być liczbą z zakresu od { $lower } do { $upper }!
shell-argument-station-id-invalid = Argument { $index } musi być prawidłowym identyfikatorem stacji!
shell-argument-map-id-invalid = Argument { $index } musi być prawidłowym identyfikatorem mapy!
shell-argument-number-invalid = Argument { $index } musi być prawidłową liczbą!
# Hints
shell-argument-username-hint = <nazwa użytkownika>
shell-argument-username-optional-hint = [nazwa użytkownika]
