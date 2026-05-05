# SPDX-FileCopyrightText: 2021 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
# SPDX-FileCopyrightText: 2021 Galactic Chimp <63882831+GalacticChimp@users.noreply.github.com>
# SPDX-FileCopyrightText: 2021 Morbo <exstrominer@gmail.com>
# SPDX-FileCopyrightText: 2021 Paul Ritter <ritter.paul1@googlemail.com>
# SPDX-FileCopyrightText: 2021 ShadowCommander <10494922+ShadowCommander@users.noreply.github.com>
# SPDX-FileCopyrightText: 2021 Vera Aguilera Puerto <6766154+Zumorica@users.noreply.github.com>
# SPDX-FileCopyrightText: 2022 Interrobang01 <113810873+Interrobang01@users.noreply.github.com>
# SPDX-FileCopyrightText: 2022 ZeroDayDaemon <60460608+ZeroDayDaemon@users.noreply.github.com>
# SPDX-FileCopyrightText: 2023 AJCM-git <60196617+AJCM-git@users.noreply.github.com>
# SPDX-FileCopyrightText: 2023 OctoRocket <88291550+OctoRocket@users.noreply.github.com>
# SPDX-FileCopyrightText: 2023 deltanedas <39013340+deltanedas@users.noreply.github.com>
# SPDX-FileCopyrightText: 2023 deltanedas <@deltanedas:kde.org>
# SPDX-FileCopyrightText: 2023 keronshb <54602815+keronshb@users.noreply.github.com>
# SPDX-FileCopyrightText: 2024 Aiden <aiden@djkraz.com>
# SPDX-FileCopyrightText: 2024 Arkanic <50847107+Arkanic@users.noreply.github.com>
# SPDX-FileCopyrightText: 2024 Errant <35878406+Errant-4@users.noreply.github.com>
# SPDX-FileCopyrightText: 2024 Mr. 27 <45323883+Dutch-VanDerLinde@users.noreply.github.com>
# SPDX-FileCopyrightText: 2024 Tadeo <td12233a@gmail.com>
# SPDX-FileCopyrightText: 2024 slarticodefast <161409025+slarticodefast@users.noreply.github.com>
# SPDX-FileCopyrightText: 2024 username <113782077+whateverusername0@users.noreply.github.com>
# SPDX-FileCopyrightText: 2025 Tay <td12233a@gmail.com>
# SPDX-FileCopyrightText: 2025 pa.pecherskij <pa.pecherskij@interfax.ru>
# SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
#
# SPDX-License-Identifier: MIT


## Traitor
## GOOB EDITED

traitor-round-end-codewords = Hasłami były: [color=White]{ $codewords }[/color]
traitor-round-end-agent-name = zdrajca
objective-issuer-syndicate = [color=crimson]Syndykat[/color]
objective-issuer-unknown = Nieznany

# Shown at the end of a round of Traitor

traitor-title = Zdrajca
traitor-description = Wśród nas są zdrajcy...
traitor-not-enough-ready-players = Nie ma wystarczającej liczby graczy gotowych do gry! Było { $readyPlayersCount } graczy gotowych z { $minimumPlayers } potrzebnych. Nie można rozpocząć trybu "Zdrajca".
traitor-no-one-ready = Nikt nie jest gotowy! Nie można rozpocząć trybu "Zdrajca".

## TraitorDeathMatch

traitor-death-match-title = Walka na śmierć zdrajców
traitor-death-match-description = Wszyscy są zdrajcami. Wszyscy chcą się nawzajem zabić.
traitor-death-match-station-is-too-unsafe-announcement = Stacja jest zbyt niebezpieczna, aby kontynuować. Masz jedną minutę.
traitor-death-match-end-round-description-first-line = PDA odzyskane po rundzie...
traitor-death-match-end-round-description-entry = PDA { $originalName }, z { $tcBalance } TK
# TraitorRole
traitor-role-greeting =
    Jesteś agentem wysłanym przez { $corporation } w imieniu [color = darkred]Syndykatu.[/color]
    Twoje cele i hasła są wymienione w menu postaci.
    Wykorzystaj swój uplink, aby kupić narzędzia potrzebne do wykonania tej misji.
    Śmierć Nanotrasen!

## TraitorRole

# TraitorRole
traitor-role-codewords =
    Hasła to: [color = lightgray]
    { $codewords }.[/color]
    Hasła mogą być używane w zwykłych rozmowach, aby dyskretnie zidentyfikować się przed innymi agentami syndykatu.
    Słuchaj ich i trzymaj w tajemnicy.
traitor-role-uplink-code =
    Ustaw dzwonek w swoim PDA na [color = lightgray]{ $code }[/color], aby zablokować lub odblokować swój uplink.
    Pamiętaj, aby go zablokować po użyciu, inaczej ktokolwiek łatwo go otworzy!
traitor-role-moreinfo = Znajdź więcej informacji o swojej roli w menu postaci.
traitor-role-nouplink = Nie masz uplinku Syndykatu. Wykorzystaj to.
traitor-role-allegiances = Twoje przynależności:
traitor-role-uplink-implant =
    Twój implant uplinku został aktywowany, dostęp do niego uzyskasz z paska skrótów.
    Uplink jest bezpieczny, dopóki ktoś nie usunie go z twojego ciała.
traitor-role-notes = Notatki od twojego pracodawcy:
# don't need all the flavour text for character menu
traitor-role-codewords-short =
    Hasła to:
    { $codewords }.
traitor-role-uplink-implant-short = Twój uplink został wszczepiony. Uzyskaj do niego dostęp z paska skrótów.
traitor-role-uplink-code-short = Twój kod uplinku to { $code }. Ustaw go jako dzwonek w swoim PDA, aby uzyskać dostęp do uplinku.
