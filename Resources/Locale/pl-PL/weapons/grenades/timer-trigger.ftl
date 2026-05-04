# SPDX-FileCopyrightText: 2022 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
# SPDX-FileCopyrightText: 2022 Veritius <veritiusgaming@gmail.com>
# SPDX-FileCopyrightText: 2023 Psychpsyo <60073468+Psychpsyo@users.noreply.github.com>
# SPDX-FileCopyrightText: 2024 Kevin Zheng <kevinz5000@gmail.com>
# SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
#
# SPDX-License-Identifier: MIT

verb-trigger-timer-set =
    { $time } { $time ->
        [one] sekunda
        [few] sekundy
       *[many] sekund
    }
verb-trigger-timer-set-current =
    { $time } { $time ->
        [one] sekunda
        [few] sekundy
       *[many] sekund
    } (obecnie)
verb-trigger-timer-cycle = Przełącz czas odliczania
examine-trigger-timer =
    Zegar ustawiony na { $time } { $time ->
        [one] sekundę
        [few] sekundy
       *[many] sekund
    }.
popup-trigger-timer-set =
    Zegar ustawiony na { $time } { $time ->
        [one] sekundę
        [few] sekundy
       *[many] sekund
    }.
verb-start-detonation = Rozpocznij odliczanie
verb-toggle-start-on-stick = Przełącz autozapalnik
popup-start-on-stick-off = Urządzenie nie zostanie aktywowane po umieszczeniu
popup-start-on-stick-on = Urządzenie zostanie aktywowane po umieszczeniu
trigger-activated = You activate { THE($device) }.
