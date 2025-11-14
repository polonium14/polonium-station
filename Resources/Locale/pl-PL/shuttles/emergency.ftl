# Commands


## Delay shuttle round end

emergency-shuttle-command-round-desc = Zatrzymuje odliczanie rozpoczynane gdy wahadłowiec wyjdzie z hiperprzestrzeni.
emergency-shuttle-command-round-yes = Runda przedłużona.
emergency-shuttle-command-round-no = Nie można przedłużyć rundy.

## Dock emergency shuttle

emergency-shuttle-command-dock-desc = Powiadamia wahadłowiec aby zadokował do stacji... jeśli może.

## Launch emergency shuttle

emergency-shuttle-command-launch-desc = Przedwcześnie startuje wchadłowiec awaryjny, jeśli można.
# Emergency shuttle
emergency-shuttle-left = Wahadłowiec awaryjny opuścił stację. {$transitTime ->
        [one] -> Oczekiwana
        *[other] -> Oczekiwane
    } {$transitTime} {$transitTime ->
        [one] -> sekundę
        [few] -> sekundy
        *[other] -> sekund
    } do Centralnego Dowództwa.
emergency-shuttle-launch-time = Wahadłowiec awaryjny wystartuje za {$consoleAccumulator} {$consoleAccumulator ->
        [one] -> sekundę
        [few] -> sekundy
        *[other] -> sekund
    }.
emergency-shuttle-docked = Wahadłowiec awaryjny zadokował na {$direction} od {$location} stacji. Wystartuje on za {$time} {$time ->
        [one] -> sekundę
        [few] -> sekundy
        *[other] -> sekund
    }. {$extended}
emergency-shuttle-good-luck = Wahadłowiec awaryjny nie jest w stanie znaleźć stacji. Powodzenia.
emergency-shuttle-nearby = Wahadłowiec awaryjny nie jset w stanie znaleźć wolnego portu. Przemieścił się na {$direction} od {$location} stacji. Wystartuje on za {$time} {$time ->
        [one] -> sekundę
        [few] -> sekundy
        *[other] -> sekund
    }. {$extended}
emergency-shuttle-extended = { " " }Czas do startu został przedłużony ze względu na nieprzychylne okoliczności.
# Emergency shuttle console popup / announcement
emergency-shuttle-console-no-early-launches = Przedwczesny odlot jest niedostępny.
emergency-shuttle-console-auth-left = brakuje {$remaining ->
        [one] -> jednego uprawnienia
        *[other] -> {$remaining} uprawnień
    } do  przedwczesnego odlotu.
emergency-shuttle-console-auth-revoked = Uprawnienie do odlotu wycofane, wymagane {$remaining ->
        [one] -> jedno uprawnienie
        *[other] -> {$remaining} uprawnienia
    }.
emergency-shuttle-console-denied = Odmowa dostępu
# UI
emergency-shuttle-console-window-title = Konsola wahadłowca awaryjnego
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
map-name-terminal = Port odlotów
cmd-delayroundend-desc = Zatrzymuje odliczanie rozpoczynane gdy wahadłowiec wyjdzie z hiperprzestrzeni.
cmd-delayroundend-help = Użycie: delayroundend
cmd-dockemergencyshuttle-desc = Powiadamia wahadłowiec aby zadokował do stacji... jeśli może.
cmd-dockemergencyshuttle-help = Użycie: dockemergencyshuttle
cmd-launchemergencyshuttle-desc = Przedwcześnie startuje wchadłowiec awaryjny, jeśli można.
cmd-launchemergencyshuttle-help = Użycie: launchemergencyshuttle
