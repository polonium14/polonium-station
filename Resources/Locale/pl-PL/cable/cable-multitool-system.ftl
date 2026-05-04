# SPDX-FileCopyrightText: 2021 20kdc <asdd2808@gmail.com>
# SPDX-FileCopyrightText: 2022 Kara <lunarautomaton6@gmail.com>
# SPDX-FileCopyrightText: 2022 mirrorcult <lunarautomaton6@gmail.com>
# SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
#
# SPDX-License-Identifier: MIT

cable-multitool-system-internal-error-no-power-node = Twój multitool wyświetla: "BŁĄD WEWNĘTRZNY: TO NIE JEST KABEL ZASILAJĄCY".
cable-multitool-system-internal-error-missing-component = Twój multitool wyświetla: "BŁĄD WEWNĘTRZNY: NIEPRAWIDŁOWY KABEL".
cable-multitool-system-verb-name = Zasilanie
cable-multitool-system-verb-tooltip = Użyj multitoola, aby sprawdzić statystyki zasilania.
cable-multitool-system-statistics =
    Twój multitool pokazuje listę statystyk:
    Aktualne zasilanie: { POWERWATTS($supplyc) }
    Z baterii: { POWERWATTS($supplyb) }
    Teoretyczne zasilanie: { POWERWATTS($supplym) }
    Idealne zużycie: { POWERWATTS($consumption) }
    Magazyn wejściowy: { POWERJOULES($storagec) } / { POWERJOULES($storagem) } ({ TOSTRING($storager, "P1") })
    Magazyn wyjściowy: { POWERJOULES($storageoc) } / { POWERJOULES($storageom) } ({ TOSTRING($storageor, "P1") })
