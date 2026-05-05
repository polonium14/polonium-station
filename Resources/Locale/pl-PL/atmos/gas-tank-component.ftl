# SPDX-FileCopyrightText: 2021 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
# SPDX-FileCopyrightText: 2021 Galactic Chimp <63882831+GalacticChimp@users.noreply.github.com>
# SPDX-FileCopyrightText: 2021 Kara D <lunarautomaton6@gmail.com>
# SPDX-FileCopyrightText: 2021 Vera Aguilera Puerto <6766154+Zumorica@users.noreply.github.com>
# SPDX-FileCopyrightText: 2022 Morb <14136326+Morb0@users.noreply.github.com>
# SPDX-FileCopyrightText: 2023 Slava0135 <40753025+Slava0135@users.noreply.github.com>
# SPDX-FileCopyrightText: 2024 AJCM-git <60196617+AJCM-git@users.noreply.github.com>
# SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
#
# SPDX-License-Identifier: MIT


### GasTankComponent stuff.

# Examine text showing pressure in tank.
comp-gas-tank-examine = Ciśnienie: [color=orange]{ PRESSURE($pressure) }[/color].
# Examine text when internals are active.
comp-gas-tank-connected = Jest połączony z zewnętrznym komponentem.
# Examine text when valve is open or closed.
comp-gas-tank-examine-open-valve = Zawór wypuszczający gaz jest [color=red]otwarty[/color].
comp-gas-tank-examine-closed-valve = Zawór wypuszczający gaz jest [color=green]zamknięty[/color].

## ControlVerb

control-verb-open-control-panel-text = Otwórz panel sterowania

## UI

gas-tank-window-internals-toggle-button = Przełącz
gas-tank-window-output-pressure-label = Ciśnienie Wyjściowe
gas-tank-window-tank-pressure-text = Ciśnienie: { $tankPressure } kPA
gas-tank-window-internal-text = Internals: { $status }
gas-tank-window-internal-connected = [color=green]Połączony[/color]
gas-tank-window-internal-disconnected = [color=red]Rozłączony[/color]

## Valve

comp-gas-tank-open-valve = Otwórz Zawór
comp-gas-tank-close-valve = Zamknij Zawór
