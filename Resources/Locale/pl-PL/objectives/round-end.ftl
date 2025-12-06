objectives-round-end-result =
    { $count ->
        [one] Był jeden { $agent }
       *[other] Było { $count } { $agent }.
    }
objectives-round-end-result-in-custody = { $custody } z { $count } { $agent } zostało schwytanych.
objectives-player-user-named = [color=White]{ $name }[/color] ([color=gray]{ $user }[/color])
objectives-player-named = [color=White]{ $name }[/color]
objectives-no-objectives =
    { $custody }{ $title } { GENDER($title) ->
        [male] był
        [female] była
        [epicene] byłu
       *[neuter] było
    } { $agent }.
objectives-with-objectives =
    { $custody }{ $title } { GENDER($title) ->
        [male] był
        [female] była
        [epicene] byłu
       *[neuter] było
    } { $agent } z następującymi zadaniami:
objectives-objective-success = { $objective } | [color=green]Sukces![/color] ({ TOSTRING($progress, "P0") })
objectives-objective-partial-success = { $objective } | [color=yellow]W większości sukces![/color] ({ TOSTRING($progress, "P0") })
objectives-objective-partial-failure = { $objective } | [color=orange]Po części porażka![/color] ({ TOSTRING($progress, "P0") })
objectives-objective-fail = { $objective } | [color=red]Porażka![/color] ({ TOSTRING($progress, "P0") })
objectives-in-custody = [bold][color=red]| SCHWYTANY | [/color][/bold]
