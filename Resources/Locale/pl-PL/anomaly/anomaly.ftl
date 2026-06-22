anomaly-component-contact-damage = Anomalia przypala Twoją skórę!
anomaly-vessel-component-anomaly-assigned = Anomalia przypisana do pojemnika.
anomaly-vessel-component-not-assigned = Ten pojemnik nie jest przypisany do żadnej anomalii. Spróbuj użyć na nim skanera.
anomaly-vessel-component-assigned = Ten pojemnik jest obecnie przypisany do anomalii.
anomaly-particles-delta = Cząstki delta
anomaly-particles-epsilon = Cząstki epsilon
anomaly-particles-zeta = Cząstki zeta
anomaly-particles-omega = Cząstki omega
anomaly-particles-sigma = Cząstki sigma
anomaly-scanner-component-scan-complete = Skanowanie zakończone!
anomaly-scanner-ui-title = skaner anomalii
anomaly-scanner-no-anomaly = Brak zeskanowanej anomalii.
anomaly-scanner-severity-percentage = Aktualna siła: [color=gray]{ $percent }[/color]
anomaly-scanner-severity-percentage-unknown = Aktualna siła: [color=red]BŁĄD[/color]
anomaly-scanner-stability-low = Aktualny stan anomalii: [color=gold]Rozkład[/color]
anomaly-scanner-stability-medium = Aktualny stan anomalii: [color=forestgreen]Stabilna[/color]
anomaly-scanner-stability-high = Aktualny stan anomalii: [color=crimson]Rośnie[/color]
anomaly-scanner-stability-unknown = Aktualny stan anomalii: [color=red]BŁĄD[/color]
anomaly-scanner-point-output = Wynik punktowy: [color=gray]{ $point }[/color]
anomaly-scanner-point-output-unknown = Wynik punktowy: [color=red]BŁĄD[/color]
anomaly-scanner-particle-readout = Analiza reakcji cząstek:
anomaly-scanner-particle-danger = - [color=crimson]Typ zagrożenia:[/color] { $type }
anomaly-scanner-particle-unstable = - [color=plum]Typ niestabilny:[/color] { $type }
anomaly-scanner-particle-containment = - [color=goldenrod]Typ powstrzymujący:[/color] { $type }
anomaly-scanner-particle-transformation = - [color=#6b75fa]Typ transformacji:[/color] { $type }
anomaly-scanner-particle-danger-unknown = - [color=crimson]Typ zagrożenia:[/color] [color=red]BŁĄD[/color]
anomaly-scanner-particle-unstable-unknown = - [color=plum]Typ niestabilny:[/color] [color=red]BŁĄD[/color]
anomaly-scanner-particle-containment-unknown = - [color=goldenrod]Typ powstrzymujący:[/color] [color=red]BŁĄD[/color]
anomaly-scanner-particle-transformation-unknown = - [color=#6b75fa]Typ transformacji:[/color] [color=red]BŁĄD[/color]
anomaly-scanner-pulse-timer = Czas do następnego impulsu: [color=gray]{ $time }[/color]
anomaly-gorilla-core-slot-name = Rdzeń anomalii
anomaly-gorilla-charge-none = Nie ma w nim [bold]rdzenia anomalii[/bold].
anomaly-gorilla-charge-limit =
    Pozostało mu [color={ $count ->
        [3] zielony
        [2] żółty
        [1] pomarańczowy
        [0] czerwony
       *[other] fioletowy
    }]{ $count } { $count ->
        [one] ładunek
        [few] ładunki
       *[other] ładunków
    }[/color].
anomaly-gorilla-charge-infinite = Ma [color=gold]nieskończone ładunki[/color]. [italic]Na razie...[/italic]
anomaly-sync-connected = Anomalia została pomyślnie podłączona
anomaly-sync-disconnected = Połączenie z anomalią zostało utracone!
anomaly-sync-no-anomaly = Brak anomalii w zasięgu.
anomaly-sync-examine-connected = Jest [color=darkgreen]podłączona[/color] do anomalii.
anomaly-sync-examine-not-connected = Nie jest [color=darkred]podłączona[/color] do anomalii.
anomaly-sync-connect-verb-text = Podłącz anomalię
anomaly-sync-connect-verb-message = Podłącz pobliską anomalię do { $machine }.
anomaly-generator-ui-title = Generator anomalii
anomaly-sync-disconnect-verb-text = Detach anomaly
anomaly-sync-disconnect-verb-message = Detach the connected anomaly from { $machine }.
anomaly-generator-fuel-display = Paliwo:
anomaly-generator-cooldown = Czas odnowienia: [color=gray]{ $time }[/color]
anomaly-generator-no-cooldown = Czas odnowienia: [color=gray]Gotowe[/color]
anomaly-generator-yes-fire = Status: [color=forestgreen]Gotowy[/color]
anomaly-generator-no-fire = Status: [color=crimson]Nie gotowy[/color]
anomaly-generator-generate = Wygeneruj anomalię
anomaly-generator-charges =
    { $charges ->
        [one] { $charges } ładunek
        [few] { $charges } ładunki
       *[other] { $charges } ładunków
    }
anomaly-generator-announcement = Anomalia została wygenerowana!
anomaly-command-pulse = Wysyła impuls do wybranej anomalii
anomaly-command-supercritical = Sprawia, że wybrana anomalia staje się nadkrytyczna
# Flavor text on the footer
anomaly-generator-flavor-left = Anomalia może pojawić się w operatorze.
anomaly-generator-flavor-right = v1.1
anomaly-behavior-unknown = [color=red]BŁĄD. Nie można odczytać.[/color]
anomaly-behavior-title = analiza odchyleń zachowania:
anomaly-behavior-point = [color=gold]Anomalia generuje { $mod }% punktów[/color]
anomaly-behavior-safe = [color=forestgreen]Anomalia jest bardzo stabilna. Bardzo rzadkie pulsacje.[/color]
anomaly-behavior-slow = [color=forestgreen]Częstotliwość pulsacji jest znacznie mniejsza.[/color]
anomaly-behavior-light = [color=forestgreen]Moc pulsacji jest znacznie zmniejszona.[/color]
anomaly-behavior-balanced = Brak wykrytych odchyleń zachowania.
anomaly-behavior-delayed-force = Częstotliwość pulsacji jest znacznie zmniejszona, ale ich siła zwiększona.
anomaly-behavior-rapid = Częstotliwość pulsacji jest znacznie większa, ale ich siła osłabiona.
anomaly-behavior-reflect = Wykryto powłokę ochronną.
anomaly-behavior-nonsensivity = Wykryto słabą reakcję na cząstki.
anomaly-behavior-sensivity = Wykryto wzmocnioną reakcję na cząstki.
anomaly-behavior-invisibility = Wykryto zniekształcenia fali świetlnej.
anomaly-behavior-secret = Wykryto zakłócenia. Niektóre dane są nieczytelne.
anomaly-behavior-inconstancy = [color=crimson]Wykryto nietrwałość. Typy cząstek mogą się zmieniać z czasem.[/color]
anomaly-behavior-fast = [color=crimson]Częstotliwość pulsacji jest znacznie zwiększona.[/color]
anomaly-behavior-strenght = [color=crimson]Moc pulsacji jest znacznie zwiększona.[/color]
anomaly-behavior-moving = [color=crimson]Wykryto niestabilność współrzędnych.[/color]
