# SPDX-FileCopyrightText: 2022 Eoin Mcloughlin <helloworld@eoinrul.es>
# SPDX-FileCopyrightText: 2022 Rinkashikachi <15rinkashikachi15@gmail.com>
# SPDX-FileCopyrightText: 2022 eoineoineoin <eoin.mcloughlin+gh@gmail.com>
# SPDX-FileCopyrightText: 2023 Justin <justinly@usc.edu>
# SPDX-FileCopyrightText: 2023 Thom <119594676+ItsMeThom@users.noreply.github.com>
# SPDX-FileCopyrightText: 2023 chromiumboy <50505512+chromiumboy@users.noreply.github.com>
# SPDX-FileCopyrightText: 2023 deltanedas <39013340+deltanedas@users.noreply.github.com>
# SPDX-FileCopyrightText: 2024 Crotalus <Crotalus@users.noreply.github.com>
# SPDX-FileCopyrightText: 2024 Nemanja <98561806+EmoGarbage404@users.noreply.github.com>
# SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
#
# SPDX-License-Identifier: MIT

lathe-menu-title = Menu Tokarki
lathe-menu-queue = Kolejka
lathe-menu-server-list = Lista serwerów
lathe-menu-sync = Synchronizuj
lathe-menu-search-designs = Znajdź przepis
lathe-menu-category-all = Wszystkie
lathe-menu-search-filter = Filtr:
lathe-menu-amount = Ilość:
lathe-menu-reagent-slot-examine = Posiada wejście na zlewkę u boku.
lathe-reagent-dispense-no-container = Liquid pours out of { THE($name) } onto the floor!
lathe-menu-result-reagent-display = { $reagent } ({ $amount }u)
lathe-menu-material-display = { $material } ({ $amount })
lathe-menu-tooltip-display = { $amount } sztuk { $material }
lathe-menu-description-display = [italic]{ $description }[/italic]
lathe-menu-material-amount =
    { $amount ->
        [1] { NATURALFIXED($amount, 2) } { $unit }
       *[other] { NATURALFIXED($amount, 2) } { MAKEPLURAL($unit) }
    }
lathe-menu-material-amount-missing =
    { $amount ->
        [1] { NATURALFIXED($amount, 2) } { $unit } of { $material } ([color=red]{ NATURALFIXED($missingAmount, 2) } { $unit } missing[/color])
       *[other] { NATURALFIXED($amount, 2) } { MAKEPLURAL($unit) } of { $material } ([color=red]{ NATURALFIXED($missingAmount, 2) } { MAKEPLURAL($unit) } missing[/color])
    }
lathe-menu-no-materials-message = Brak materiałów.
lathe-menu-silo-linked-message = Silos połączony
lathe-menu-fabricating-message = Wytwarzanie...
lathe-menu-materials-title = Materiały
lathe-menu-queue-title = Kolejka budowania
