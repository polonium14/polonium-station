contraband-examine-text-Minor =
    { $type ->
        *[item] [color={ $color }]Ten przedmiot jest uznawany za drobną kontrabandę.[/color]
        [reagent] [color={ $color }]Ten odczynnik jest uznawany za drobną kontrabandę.[/color]
    }

contraband-examine-text-Restricted =
    { $type ->
        *[item] [color={ $color }]Ten przedmiot jest ograniczony do użycia przez określone działy.[/color]
        [reagent] [color={ $color }]Ten odczynnik jest ograniczony do użycia przez określone działy.[/color]
    }

contraband-examine-text-Restricted-department =
    { $type ->
        *[item] [color={ $color }]Ten przedmiot jest dostępny tylko dla działów: { $departments }, i może być uznany za kontrabandę.[/color]
        [reagent] [color={ $color }]Ten odczynnik jest dostępny tylko dla działów: { $departments }, i może być uznany za kontrabandę.[/color]
    }

contraband-examine-text-Major =
    { $type ->
        *[item] [color={ $color }]Ten przedmiot jest uznawany za poważną kontrabandę.[/color]
        [reagent] [color={ $color }]Ten odczynnik jest uznawany za poważną kontrabandę.[/color]
    }

contraband-examine-text-GrandTheft =
    { $type ->
        *[item] [color={ $color }]Ten przedmiot jest wyjątkowo cenny dla agentów Syndykatu![/color]
        [reagent] [color={ $color }]Ten odczynnik jest wyjątkowo cenny dla agentów Syndykatu![/color]
    }

contraband-examine-text-Highly-Illegal =
    { $type ->
        *[item] [color={ $color }]Ten przedmiot to wysoce nielegalna kontrabanda![/color]
        [reagent] [color={ $color }]Ten odczynnik to wysoce nielegalna kontrabanda![/color]
    }

contraband-examine-text-Syndicate =
    { $type ->
        *[item] [color={ $color }]Ten przedmiot to wysoce nielegalna kontrabanda Syndykatu![/color]
        [reagent] [color={ $color }]Ten odczynnik to wysoce nielegalna kontrabanda Syndykatu![/color]
    }

contraband-examine-text-Magical =
    { $type ->
        *[item] [color={ $color }]Ten przedmiot to wysoce nielegalna magiczna kontrabanda![/color]
        [reagent] [color={ $color }]Ten odczynnik to wysoce nielegalna magiczna kontrabanda![/color]
    }

contraband-examine-text-avoid-carrying-around = [color=red][italic]Lepiej nie noś tego na widoku bez dobrego powodu.[/italic][/color]
contraband-examine-text-in-the-clear = [color=green][italic]Możesz bezpiecznie nosić to na widoku.[/italic][/color]
contraband-examinable-verb-text = Legalność
contraband-examinable-verb-message = Sprawdź legalność tego przedmiotu.
contraband-department-plural = { $department }
contraband-job-plural = { $job }
