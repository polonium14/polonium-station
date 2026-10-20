# Battery Status
battery-status-charge = Ładunek: [color=#5E7C16]{ $percent }[/color] %
battery-status-switchable-state =
    { $state ->
        [on] [color=green]Wł.[/color]
        [off] [color=red]Wył.[/color]
       *[other] Nieznany
    }
battery-status-state = Stan: { $state }
# Charge Status
charge-status-count = Ładunki: [color=fuchsia]{ $current }/{ $max }[/color]
charge-status-recharge = Ładowanie: [color=yellow]{ $seconds } s[/color]
# Tank Pressure Status
tank-pressure-status = Ciśn.: [color=orange]{ $pressure } kPa[/color]
tank-status-switchable-state =
    { $state ->
        [open] [color=red]Otwarty[/color]
        [closed] [color=green]Zamknięty[/color]
       *[other] Nieznany
    }
tank-status-state = Stan: { $state }
# Magazine Status
magazine-status-rounds = Naboje: [color=yellow]{ $current }/{ $max }[/color]
# Anomaly Status
anomaly-status-infinite = [color=gold]Nieskończone ładunki[/color]
anomaly-status-charges = [color=orange]Ładunki: { $charges }[/color]
# Timer Trigger Status
timer-trigger-status-delay = Ustawione opóźnienie: [color=white]{ $delay } s[/color]
