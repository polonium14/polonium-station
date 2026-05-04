# SPDX-FileCopyrightText: 2023 Nemanja <98561806+EmoGarbage404@users.noreply.github.com>
# SPDX-FileCopyrightText: 2024 Errant <35878406+Errant-4@users.noreply.github.com>
# SPDX-FileCopyrightText: 2024 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
# SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
#
# SPDX-License-Identifier: MIT

set-game-preset-command-description = Sets the game preset for the specified number of upcoming rounds.
set-game-preset-command-help-text = setgamepreset <id> [number of rounds, defaulting to 1]
set-game-preset-optional-argument-not-integer = If argument 2 is provided it must be a number.
set-game-preset-preset-set = Set game preset to "{ $preset }"
set-game-preset-preset-error = Unable to find game preset "{ $preset }"
#set-game-preset-preset-set = Set game preset to "{$preset}"
set-game-preset-command-hint-1 = <id>
set-game-preset-command-hint-2 =  [liczba rund]
set-game-preset-command-hint-3 =  [fałszywy tryb]
set-game-preset-decoy-error = Jeśli podano trzeci argument, musi on być prawidłowym trybem gry. Nie można znaleźć trybu gry „{ $preset }”
set-game-preset-preset-set-finite-with-decoy = Ustawiono tryb gry na „{ $preset }” na następne { $rounds } rund(y), pokazując w lobby „{ $decoy }”.
set-game-preset-preset-set-finite = Set game preset to "{ $preset }" for the next { $rounds } rounds.
