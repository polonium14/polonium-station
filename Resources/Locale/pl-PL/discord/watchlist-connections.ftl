# SPDX-FileCopyrightText: 2025 Tay <td12233a@gmail.com>
# SPDX-FileCopyrightText: 2025 slarticodefast <161409025+slarticodefast@users.noreply.github.com>
# SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
#
# SPDX-License-Identifier: MIT

discord-watchlist-connection-header =
    { $players ->
        [one] { $players } gracz z listy obserwowania
        [few] { $players } gracza z listy obserwowania
       *[other] { $players } graczy z listy obserwowania
    } podłączyły się do { $serverName }
discord-watchlist-connection-entry =
    - { $playerName } with message "{ $message }"{ $expiry ->
        [0] { "" }
       *[other] { " " }(wygasa  <t:{ $expiry }:R>)
    }{ $otherWatchlists ->
        [0] { "" }
        [one] { " " }i { $otherWatchlists } inną listą obserwowania
       *[other] { " " }i { $otherWatchlists } innymi listami obserwowania
    }
