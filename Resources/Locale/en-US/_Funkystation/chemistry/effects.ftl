# SPDX-FileCopyrightText: 2025 Eris <erisfiregamer1@gmail.com>
# SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
#
# SPDX-License-Identifier: AGPL-3.0-or-later AND MIT

reagent-effect-guidebook-cure-changeling =
    { $chance ->
        [1] Removes any
       *[other] remove any
    } parasitic infection
reagent-effect-guidebook-reduce-seizure-build =
    { $chance ->
        [1] Reduces
       *[other] reduce
    } neurological seizure buildup by { NATURALFIXED($amount, 2) } points
reagent-effect-guidebook-mutation =
    { $chance ->
        [1] Powoduje losową mutację genetyczną.
       *[other] { $chance } szans na spowodowanie losowej mutacji genetycznej.
    }
reagent-effect-guidebook-mutation-removal =
    { $chance ->
        [1] Usuwa losową mutację genetyczną.
       *[other] { $chance } szans na usunięcie losowej mutacji genetycznej.
    }
