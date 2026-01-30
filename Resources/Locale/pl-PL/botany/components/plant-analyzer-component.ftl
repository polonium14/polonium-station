plant-analyzer-component-no-seed = Brak Rośliny

plant-analyzer-component-yes = Tak
plant-analyzer-component-no = Nie

plant-analyzer-component-health = Zdrowie:
plant-analyzer-component-age = Wiek:
plant-analyzer-component-water = Nawodnienie:
plant-analyzer-component-nutrition = Odżywienie:
plant-analyzer-component-toxins = Toksyny:
plant-analyzer-component-pests = Szkodniki:
plant-analyzer-component-weeds = Chwasty:

plant-analyzer-component-alive = [color=green]ŻYWE[color]
plant-analyzer-component-dead = [color=red]MARTWE[color]
plant-analyzer-component-unviable = [color=red]NIEZDATNE[color]
plant-analyzer-component-mutating = [color=#00ff5f]MUTUJĄCE[color]
plant-analyzer-component-kudzu = [color=red]KUDZU[color]

plant-analyzer-soil = Niewchłonięte Reagenty: [color=white]{$chemicals}[/color]
plant-analyzer-soil-empty = Niewchłonięte Reagenty: [color=gray]Brak[/color]

plant-analyzer-component-environment = [bold]Analiza Optymalnych Warunków[/bold] {$nl}
plant-analyzer-component-light = Poziom Naświetlenia: [color=white]{$lightLevel} ± {$lightTolerance}[/color]{$nl}

plant-analyzer-component-temperature = Temperatura: [color=lightsalmon]{$temp}°k ± {$tempTolerance}°k[/color]{$nl}

plant-analyzer-component-pressure = Ciśnienie: [color=lightblue]{$kpa}kPa ± {$kpaTolerance}kPa[/color]{$nl}
plant-analyzer-component-requiredgas = Wymagane Gazy: [color=lightgray]{$gases}[/color]
plant-analyzer-component-nogas = Wymagane Gazy: [color=gray]Brak[/color]


plant-analyzer-produce-title = [bold]Analiza Plonu Rośliny[/bold] {$nl}
plant-analyzer-produce-amount = Plony: {$yield ->
        [0][color=gray]Brak[/color]$nl
        [one][color=#a4885c][bold]{$yield}[/bold] {$produce}[/color]
        *[other][color=#a4885c][bold]{$yield}[/bold] {$producePlural}[/color]
        }{$nl}

plant-analyzer-produce-size = Bogactwo Plonów: {$yield ->
        [0][color=gray]N/A[/color]
        *[other][color=lightgreen][bold]{$potency}[/bold] {"("}{$potencyDesc}{")"}[/color]
        }{$nl}

plant-analyzer-produce-seedless = Brak Nasion: {$seedless ->
        [true]{" "}[color=red]Tak[/color]
        *[false]{" "}[color=green]Nie[/color]
        }{$nl}

plant-analyzer-produce-gases = Wydzielane Gazy: {$gasCount ->
        [0][color=gray]Brak[/color]
        *[other][color=lightgray]{$gases}[/color]
        }{$nl}

plant-analyzer-produce-reagents = Reagenty: {$yield ->
        [0][color=gray]Brak[/color]
        *[other]{$chemCount ->
                [0][color=gray]Brak[/color]
                *[other][color=white]{$chemicals}[/color]
                }
        }

plant-analyzer-produce-plural = {MAKEPLURAL($thing)}

plant-analyzer-potency-tiny = Minimalna
plant-analyzer-potency-small = Mała
plant-analyzer-potency-medium = Średnia
plant-analyzer-potency-large = Duża
plant-analyzer-potency-huge = Wielka
plant-analyzer-potency-gigantic = Gigantyczna
plant-analyzer-potency-ludicrous = Niedorzeczna
plant-analyzer-potency-immeasurable = [italic]Nieobliczalna[/italic]
plant-analyzer-potency-perfect = [color=yellow][bold]Perfekcyjna[/bold][/color]

plant-analyzer-print = Drukuj
plant-analyzer-printout-missing = N/A
plant-analyzer-printout = [color=#9FED58][head=2]Raport Analizatora Roślin[/head][/color]{$nl
    }──────────────────────────────{$nl
    }[bullet/] Gatunek: {$seedName}{$nl
    }{$indent}[bullet/] Zdatne: {$viable ->
        [Yes][color=green]Tak[/color]
        [No][color=red]Nie[/color]
        *[other]{LOC("plant-analyzer-printout-missing")}
    }{$nl
    }{$indent}[bullet/] Kudzu: {$kudzu ->
        [Yes][color=red]Tak[/color]
        [No][color=green]Nie[/color]
        *[other]{LOC("plant-analyzer-printout-missing")}
    }{$nl
    }{$indent}[bullet/] Wytrzymałość: {$endurance}{$nl
    }{$indent}[bullet/] Długość życia: {$lifespan}{$nl
    }{$indent}[bullet/] Owoc: {$yield ->
        [-1]{LOC("plant-analyzer-printout-missing")}
        [0][color=gray]Brak[/color]$nl
        [one][color=#a4885c][bold]{$yield}[/bold] {$produce}[/color]
        *[other][color=#a4885c][bold]{$yield}[/bold] {$producePlural}[/color]
        }{$nl
    }{$indent}[bullet/] Potencja: {$yield ->
        [-1]{LOC("plant-analyzer-printout-missing")}
        [0][color=red]0[/color]
        *[other][color=lightgreen][bold]{$potency}[/bold] {"("}{$potencyDesc}{")"}[/color]
        }{$nl
    }{$indent}[bullet/] Brak Nasion: {$seeds ->
        [Yes][color=red]Tak[/color]
        [No][color=green]Nie[/color]
        *[other]{LOC("plant-analyzer-printout-missing")}
        }{$nl
    }[bullet/] Dane:{$nl
    }{$indent}[bullet/] Woda: [color=cyan]{$water}[/color]{$nl
    }{$indent}[bullet/] Odżywnienie: [color=orange]{$nutrients}[/color]{$nl
    }{$indent}[bullet/] Toksyny: [color=yellowgreen]{$toxins}[/color]{$nl
    }{$indent}[bullet/] Szkodniki: [color=magenta]{$pests}[/color]{$nl
    }{$indent}[bullet/] Chwasty: [color=red]{$weeds}[/color]{$nl
    }[bullet/] Pożądane Warunki:{$nl
    }{$indent}[bullet/] Poziom Oświetlenia: [color=gray][bold]{$lightLevel} ± {$lightTolerance}[/bold][/color]{$nl
    }{$indent}[bullet/] Temperatura: [color=lightsalmon]{$temp}°k ± {$tempTolerance}°k[/color]{$nl
    }{$indent}[bullet/] Ciśnienie: [color=lightblue]{$kpa}kPa ± {$kpaTolerance}kPa[/color]{$nl
    }{$indent}[bullet/] Wymagane Gazy: [color=lightgray]{$gasesIn}[/color]{$nl
    }[bullet/] Wydzielane Gazy: {$gasCount ->
        [0][color=gray]Brak[/color]
        *[other][color=lightgray]{$gasesOut}[/color]
        }{$nl
    }[bullet/] Reagenty: {$yield ->
        [0][color=gray]Brak[/color]
        *[other]{$chemCount ->
                [0][color=gray]Brak[/color]
                *[other][color=gray]{$chemicals}[/color]
                }
        }
