limited-charges-charges-remaining =
    { $charges ->
        [one] Ma [color=fuchsia]{ $charges }[/color] pozostały ładunek.
        [few] Ma [color=fuchsia]{ $charges }[/color] pozostałe ładunki.
       *[other] Ma [color=fuchsia]{ $charges }[/color] pozostałych ładunków.
    }
limited-charges-max-charges = Ma [color=green]maksymalną[/color] liczbę ładunków.
limited-charges-recharging =
    { $seconds ->
        [one] Do następnego ładunku pozostała [color=yellow]{ $seconds }[/color] sekunda.
        [few] Do następnego ładunku pozostały [color=yellow]{ $seconds }[/color] sekundy.
       *[other] Do następnego ładunku pozostało [color=yellow]{ $seconds }[/color] sekund.
    }
limited-charges-no-charges = Brak ładunków!
