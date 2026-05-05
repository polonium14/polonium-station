ssssagent-id-new =
    { $number ->
        [0] Nie uzyskano żadnych nowych dostępów z { $card }.
        [one] Zyskano jeden nowy dostęp z { $card }.
        [few] Zyskano { $number } nowe dostępy z { $card }.
       *[other] Zyskano { $number } nowych dostępów z { $card }.
    }

# SPDX-FileCopyrightText: 2022 Rane <60792108+Elijahrane@users.noreply.github.com>
# SPDX-FileCopyrightText: 2023 PrPleGoo <PrPleGoo@users.noreply.github.com>
# SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
#
# SPDX-License-Identifier: MIT

agent-id-no-new = Didn't gain any new accesses from { THE($card) }.
agent-id-new-1 = Gained one new access from { THE($card) }.
agent-id-new = Gained { $number } new accesses from { THE($card) }.
agent-id-card-current-name = Imię:
agent-id-card-current-job = Zawód:
agent-id-card-job-icon-label = Ikona zawodu:
agent-id-menu-title = Identyfikator agenta
