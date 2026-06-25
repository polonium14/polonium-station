# Popups

network-configurator-device-saved = Urządzenie sieciowe { $device } o adresie { $address } zostało pomyślnie zapisane!
network-configurator-device-failed = Nie udało się zapisać urządzenia sieciowego { $device }! Nie przypisano adresu!
network-configurator-too-many-devices = Na tym urządzeniu zapisano zbyt wiele urządzeń!
network-configurator-update-ok = Zaktualizowano pamięć urządzenia.
network-configurator-device-already-saved = urządzenie sieciowe: { $device } jest już zapisane.
network-configurator-device-access-denied = Odmowa dostępu!
network-configurator-link-mode-started = Rozpoczęto łączenie urządzenia: { $device }
network-configurator-link-mode-stopped = Zatrzymano łączenie.
network-configurator-mode-link = Łącz
network-configurator-mode-list = Listuj
network-configurator-switched-mode = Zmieniono tryb na: { $mode }
# Verbs
network-configurator-save-device = Zapisz urządzenie
network-configurator-configure = Konfiguruj
network-configurator-switch-mode = Zmień tryb
network-configurator-link-defaults = Podłącz domyślne
network-configurator-start-link = Zacznij łączenie
network-configurator-link = Łączenie
# ui
network-configurator-title-saved-devices = Zapisane urządzenia
network-configurator-title-device-configuration = Konfiguracja urządzenia
network-configurator-ui-clear-button = Wyczyść
network-configurator-ui-count-label =
    { $count } { $count ->
        [one] urządzenie
        [few] urządzenia
       *[many] urządzeń
    }
# tooltips
network-configurator-tooltip-set = Ustawia listę urządzeń docelowych
network-configurator-tooltip-add = Dodaje do listy urządzeń docelowych
network-configurator-tooltip-edit = Edytuj listę urządzeń docelowych
network-configurator-tooltip-clear = Wyczyść listę urządzeń
network-configurator-tooltip-copy = Skopiuj listę urządzeń docelowych do narzędzia
network-configurator-tooltip-show = Wyświetl holograficzną wizualizację listy urządzeń docelowych
# examine
network-configurator-examine-mode-link = [color=red]Łączenie[/color]
network-configurator-examine-mode-list = [color=green]Listowanie[/color]
network-configurator-examine-current-mode = Aktualny tryb: { $mode }
network-configurator-examine-switch-modes = Naciśnij { $key }, aby przełączyć tryb
# command
cmd-clearnetworklinkoverlays-desc = Usuń wszystkie nakładki połączeń sieciowych.
cmd-clearnetworklinkoverlays-help = Sposób użycia: clearnetworklinkoverlays
# item status
network-configurator-item-status-label =
    Tryb: { $mode }
    Przełącz: { $keybinding }
