# SPDX-FileCopyrightText: 2021 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
# SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
#
# SPDX-License-Identifier: MIT

# Examine text
comp-ghost-examine-time-minutes =
    Zmarł(-a) [color=yellow]{ $minutes ->
        [one] minutę
        [few] minuty
       *[other] minut
    } temu.[/color]
comp-ghost-examine-time-seconds =
    Zmarł(-a) [color=yellow]{ $seconds ->
        [one] sekundę
        [few] sekundy
       *[other] sekund
    } temu.[/color]
