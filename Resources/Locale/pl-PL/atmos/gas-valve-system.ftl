# SPDX-FileCopyrightText: 2021 E F R <602406+Efruit@users.noreply.github.com>
# SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
#
# SPDX-License-Identifier: MIT

# Examine Text
gas-valve-system-examined =
    Zawór jest [color={ $statusColor }]{ $open ->
        [true] otwarty
       *[false] zamknięty
    }[/color].
