# SPDX-FileCopyrightText: 2022 LittleBuilderJane <63973502+LittleBuilderJane@users.noreply.github.com>
# SPDX-FileCopyrightText: 2022 Myctai <108953437+Myctai@users.noreply.github.com>
# SPDX-FileCopyrightText: 2022 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
# SPDX-FileCopyrightText: 2022 metalgearsloth <metalgearsloth@gmail.com>
# SPDX-FileCopyrightText: 2024 Aiden <aiden@djkraz.com>
# SPDX-FileCopyrightText: 2024 Aidenkrz <aiden@djkraz.com>
# SPDX-FileCopyrightText: 2024 IProduceWidgets <107586145+IProduceWidgets@users.noreply.github.com>
# SPDX-FileCopyrightText: 2024 MilenVolf <63782763+MilenVolf@users.noreply.github.com>
# SPDX-FileCopyrightText: 2024 Nemanja <98561806+EmoGarbage404@users.noreply.github.com>
# SPDX-FileCopyrightText: 2024 Pieter-Jan Briers <pieterjan.briers+git@gmail.com>
# SPDX-FileCopyrightText: 2024 Piras314 <p1r4s@proton.me>
# SPDX-FileCopyrightText: 2024 Tadeo <td12233a@gmail.com>
# SPDX-FileCopyrightText: 2024 strO0pwafel <153459934+strO0pwafel@users.noreply.github.com>
# SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
#
# SPDX-License-Identifier: MIT


# Commands


## Delay shuttle round end

emergency-shuttle-command-round-desc = Zatrzymuje odliczanie rozpoczynane gdy wahadłowiec wyjdzie z hiperprzestrzeni.
emergency-shuttle-command-round-yes = Runda przedłużona.
emergency-shuttle-command-round-no = Nie można przedłużyć rundy.

## Dock emergency shuttle

emergency-shuttle-command-dock-desc = Powiadamia wahadłowiec aby zadokował do stacji... jeśli może.

## Launch emergency shuttle

emergency-shuttle-command-launch-desc = Przedwcześnie startuje wchadłowiec ratunkowy, jeśli można.
# Emergency shuttle
emergency-shuttle-left =
    Wahadłowiec ratunkowy opuścił stację. { $transitTime ->
        [one] Oczekiwana
       *[other] Oczekiwane
    } { $transitTime } { $transitTime ->
        [one] sekunda
        [few] sekundy
       *[other] sekund
    } do Centralnego Dowództwa.
emergency-shuttle-launch-time =
    Wahadłowiec ratunkowy wystartuje za { $consoleAccumulator } { $consoleAccumulator ->
        [one] sekundę
        [few] sekundy
       *[other] sekund
    }.
emergency-shuttle-docked =
    Wahadłowiec ratunkowy zadokował na { $direction } od { $location } stacji. Wystartuje on za { $time } { $time ->
        [one] sekundę
        [few] sekundy
       *[other] sekund
    }. { $extended }
emergency-shuttle-good-luck = Wahadłowiec ratunkowy nie jest w stanie znaleźć stacji. Powodzenia.
emergency-shuttle-nearby =
    Wahadłowiec ratunkowy nie jest w stanie znaleźć wolnego portu. Przemieścił się na { $direction } od { $location } stacji. Wystartuje on za { $time } { $time ->
        [one] sekundę
        [few] sekundy
       *[other] sekund
    }. { $extended }
emergency-shuttle-extended = { " " }Czas do startu został przedłużony ze względu na nieprzychylne okoliczności.
# Emergency shuttle console popup / announcement
emergency-shuttle-console-no-early-launches = Przedwczesny odlot jest niedostępny.
emergency-shuttle-console-auth-left =
    brakuje { $remaining ->
        [one] jednego uprawnienia
       *[other] { $remaining } uprawnień
    } do  przedwczesnego odlotu.
emergency-shuttle-console-auth-revoked =
    Uprawnienie do odlotu wycofane, wymagane { $remaining ->
        [one] jedno uprawnienie
       *[other] { $remaining } uprawnienia
    }.
emergency-shuttle-console-denied = Odmowa dostępu
# UI
emergency-shuttle-console-window-title = Konsola wahadłowca ratunkowego
emergency-shuttle-ui-engines = SILNIKI:
emergency-shuttle-ui-idle = Bezczynny
emergency-shuttle-ui-repeal-all = Odwołaj wszystkie
emergency-shuttle-ui-early-authorize = Uprawnienie przedwczesnego odlotu
emergency-shuttle-ui-authorize = UPRAWNIJ
emergency-shuttle-ui-repeal = WYCOFAJ
emergency-shuttle-ui-authorizations = Uprawnienia
emergency-shuttle-ui-remaining = Brakuje: { $remaining }
# Map Misc.
map-name-centcomm = Centralne Dowództwo
cmd-delayroundend-desc = Zatrzymuje odliczanie rozpoczynane gdy wahadłowiec wyjdzie z hiperprzestrzeni.
cmd-delayroundend-help = Użycie: delayroundend
cmd-dockemergencyshuttle-desc = Powiadamia wahadłowiec aby zadokował do stacji... jeśli może.
cmd-dockemergencyshuttle-help = Użycie: dockemergencyshuttle
cmd-launchemergencyshuttle-desc = Przedwcześnie startuje wchadłowiec ratunkowy, jeśli można.
cmd-launchemergencyshuttle-help = Użycie: launchemergencyshuttle
map-name-terminal = Port odlotów
