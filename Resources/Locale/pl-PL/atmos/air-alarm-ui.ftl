# UI


## Window

air-alarm-ui-access-denied = Brak wystarczających uprawnień!
air-alarm-ui-window-pressure-label = Ciśnienie
air-alarm-ui-window-temperature-label = Temperatura
air-alarm-ui-window-alarm-state-label = Status
air-alarm-ui-window-address-label = Adres
air-alarm-ui-window-device-count-label = Liczba urządzeń
air-alarm-ui-window-resync-devices-label = Ponowna synchronizacja
air-alarm-ui-window-mode-label = Tryb
air-alarm-ui-window-mode-select-locked-label = [bold][color=red] Błąd selektora trybu! [/color][/bold]
air-alarm-ui-window-auto-mode-label = Tryb automatyczny
-air-alarm-state-name =
    { $state ->
        [normal] Normalny
        [warning] Ostrzeżenie
        [danger] Niebezpieczeństwo
        [emagged] Zhakowane
       *[invalid] Nieprawidłowe
    }
air-alarm-ui-window-listing-title = {$address} : {-air-alarm-state-name(state:$state)}
air-alarm-ui-window-pressure = { $pressure } kPa
air-alarm-ui-window-pressure-indicator = Ciśnienie: [color={ $color }]{ $pressure } kPa[/color]
air-alarm-ui-window-temperature = { $tempC } °C ({ $temperature } K)
air-alarm-ui-window-temperature-indicator = Temperatura: [color={ $color }]{ $tempC } °C ({ $temperature } K)[/color]
air-alarm-ui-window-alarm-state = [color={$color}]{-air-alarm-state-name(state:$state)}[/color]
air-alarm-ui-window-alarm-state-indicator = Status: [color={$color}]{-air-alarm-state-name(state:$state)}[/color]

air-alarm-ui-window-tab-vents = Odpowietrzniki
air-alarm-ui-window-tab-scrubbers = Filtry
air-alarm-ui-window-tab-sensors = Czujniki
air-alarm-ui-gases = { $gas }: { $amount } mol ({ $percentage }%)
air-alarm-ui-gases-indicator = { $gas }: [color={ $color }]{ $amount } mol ({ $percentage }%)[/color]
air-alarm-ui-mode-filtering = Filtrowanie
air-alarm-ui-mode-wide-filtering = Filtrowanie (szerokie)
air-alarm-ui-mode-fill = Napełnianie
air-alarm-ui-mode-panic = Tryb paniki
air-alarm-ui-mode-none = Brak

## Widgets


### General

air-alarm-ui-widget-enable = Włączone
air-alarm-ui-widget-copy = Skopiuj ustawienia do podobnych urządzeń
air-alarm-ui-widget-copy-tooltip = Kopiuje ustawienia tego urządzenia do wszystkich urządzeń w tej zakładce alarmu powietrza.
air-alarm-ui-widget-ignore = Ignoruj
air-alarm-ui-atmos-net-device-label = Adres: { $address }

### Vent pumps

air-alarm-ui-vent-pump-label = Kierunek odpowietrzania
air-alarm-ui-vent-pressure-label = Granica ciśnienia
air-alarm-ui-vent-external-bound-label = Granica zewnętrzna
air-alarm-ui-vent-internal-bound-label = Granica wewnętrzna

### Scrubbers

air-alarm-ui-scrubber-pump-direction-label = Kierunek
air-alarm-ui-scrubber-volume-rate-label = Prędkość (L)
air-alarm-ui-scrubber-wide-net-label = WideNet

### Thresholds

air-alarm-ui-sensor-gases = Gazy
air-alarm-ui-sensor-thresholds = Progi
air-alarm-ui-thresholds-pressure-title = Progi ciśnienia (kPa)
air-alarm-ui-thresholds-temperature-title = Progi temperatury (K)
air-alarm-ui-thresholds-gas-title = Progi gazów (%)
air-alarm-ui-thresholds-upper-bound = Niebezpieczeństwo powyżej
air-alarm-ui-thresholds-lower-bound = Niebezpieczeństwo poniżej
air-alarm-ui-thresholds-upper-warning-bound = Ostrzeżenie powyżej
air-alarm-ui-thresholds-lower-warning-bound = Ostrzeżenie poniżej
air-alarm-ui-thresholds-copy = Skopiuj progi do wszystkich urządzeń
air-alarm-ui-thresholds-copy-tooltip = Kopiuje progi czujników tego urządzenia do wszystkich urządzeń w tej zakładce alarmu powietrza.
air-alarm-ui-title = Alarm Powietrzny
air-alarm-ui-pump-direction-siphoning = Odsysanie
air-alarm-ui-pump-direction-scrubbing = Oczyszczanie
air-alarm-ui-pump-direction-releasing = Wypuszczanie
air-alarm-ui-pressure-bound-nobound = Bez ograniczeń
air-alarm-ui-pressure-bound-internalbound = Ograniczenie wewnętrzne
air-alarm-ui-pressure-bound-externalbound = Ograniczenie zewnętrzne
air-alarm-ui-pressure-bound-both = Oba
air-alarm-ui-widget-gas-filters = Filtry gazu
air-alarm-ui-scrubber-select-all-gases-label = Zaznacz wszystkie
air-alarm-ui-scrubber-deselect-all-gases-label = Odznacz wszystkie
