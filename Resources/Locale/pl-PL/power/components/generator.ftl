# SPDX-FileCopyrightText: 2023 Nemanja <98561806+EmoGarbage404@users.noreply.github.com>
# SPDX-FileCopyrightText: 2023 deltanedas <39013340+deltanedas@users.noreply.github.com>
# SPDX-FileCopyrightText: 2023 deltanedas <@deltanedas:kde.org>
# SPDX-FileCopyrightText: 2024 Aiden <aiden@djkraz.com>
# SPDX-FileCopyrightText: 2024 Pieter-Jan Briers <pieterjan.briers+git@gmail.com>
# SPDX-FileCopyrightText: 2024 lzk <124214523+lzk228@users.noreply.github.com>
# SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
#
# SPDX-License-Identifier: MIT

generator-clogged = { CAPITALIZE(THE($generator)) } shuts off abruptly!
portable-generator-verb-start = Uruchom generator
portable-generator-verb-start-msg-unreliable = Uruchom generator, może wymagać paru prób.
portable-generator-verb-start-msg-reliable = Uruchom generator.
portable-generator-verb-start-msg-unanchored = Generator musi być najpierw przykręcony!
portable-generator-verb-stop = Zatrzymaj generator
portable-generator-start-fail = Ciągniesz za linkę, ale on się nie uruchamia.
portable-generator-start-success = Ciągniesz za linkę, i zaczyna on burgotać się do życia.
portable-generator-ui-title = Przenośny generator
portable-generator-ui-status-stopped = Zatrzymany:
portable-generator-ui-status-starting = Uruchamia się:
portable-generator-ui-status-running = Uruchomiony:
portable-generator-ui-start = Uruchom
portable-generator-ui-stop = Zatrzymaj
portable-generator-ui-target-power-label = Moc docelowa (kW):
portable-generator-ui-efficiency-label = Wydajność:
portable-generator-ui-fuel-use-label = Użycie paliwa:
portable-generator-ui-fuel-left-label = Pozostałe paliwo:
portable-generator-ui-clogged = Zanieczyszczenia wykryte w zbiorniku paliwa!
portable-generator-ui-eject = Wyjmij
portable-generator-ui-eta = (~{ $minutes } min.)
portable-generator-ui-unanchored = Odkręcony
portable-generator-ui-current-output = Obecne napięcie: { $voltage }
portable-generator-ui-network-stats = Sieć:
portable-generator-ui-network-stats-value = { POWERWATTS($supply) } / { POWERWATTS($load) }
portable-generator-ui-network-stats-not-connected = Nie podłączone
power-switchable-generator-examine = Napięcie wyjściowe ustawiona na { $voltage }.
power-switchable-generator-switched = Przełączono na { $voltage }!
power-switchable-voltage =
    { $voltage ->
        [HV] [color=orange]WN[/color]
        [MV] [color=yellow]ŚN[/color]
       *[LV] [color=green]NN[/color]
    }
power-switchable-switch-voltage = Przełącz na { $voltage }
fuel-generator-verb-disable-on = Najpierw wyłącz generator!
