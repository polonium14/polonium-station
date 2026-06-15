guidebook-reagent-effect-description =
    { $chance ->
        [1] { $effect }
       *[other] Ma { NATURALPERCENT($chance, 2) } szansę na { $effect }
    }{ $conditionCount ->
        [0] .
       *[other] { " " }, gdy { $conditions }.
    }
guidebook-reagent-name = [bold][color={ $color }]{ CAPITALIZE($name) }[/color][/bold]
guidebook-reagent-recipes-header = Przepis
guidebook-reagent-recipes-reagent-display = [bold]{ $reagent }[/bold] \[{ $ratio }\]
guidebook-reagent-sources-header = Składniki
guidebook-reagent-sources-ent-wrapper = [bold]{ $name }[/bold] \[1\]
guidebook-reagent-sources-gas-wrapper = [bold]{ $name } (gaz)[/bold] \[1\]
guidebook-reagent-effects-header = Efekty
guidebook-reagent-effects-metabolism-group-rate = [bold]{ $group }[/bold] [color=gray]({ $rate } jednostek na sekundę)[/color]
guidebook-reagent-plant-metabolisms-header = Metabolizm roślinny
guidebook-reagent-plant-metabolisms-rate = [bold]Metabolizm roślinny[/bold] [color=gray](1 jednostka co 3 sekundy)[/color]
guidebook-reagent-physical-description = [italic]Substancja wygląda { $description }.[/italic]
guidebook-reagent-recipes-mix-info =
    { $minTemp ->
        [0]
            { $hasMax ->
                [true] { CAPITALIZE($verb) } poniżej { NATURALFIXED($maxTemp, 2) }K
               *[false] { CAPITALIZE($verb) }
            }
       *[other]
            { CAPITALIZE($verb) } { $hasMax ->
                [true] pomiędzy { NATURALFIXED($minTemp, 2) }K i { NATURALFIXED($maxTemp, 2) }K
               *[false] powyżej { NATURALFIXED($minTemp, 2) }K
            }
    }
