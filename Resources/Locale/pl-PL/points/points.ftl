point-scoreboard-winner = The winner was [color=lime]{$player}![/color]
point-scoreboard-header = [bold]Tablica wyników[/bold]
point-scoreboard-list =
    { $place }. [bold][color=cyan]{ $name }[/color][/bold] uzyskał [color=yellow]{ $points ->
        [one] { $points } punkt
       *[other] { $points } punktów
    }.[/color]
