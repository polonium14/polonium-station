plant-analyzer-component-no-seed = no plant found

plant-analyzer-component-yes = Yes
plant-analyzer-component-no = No

plant-analyzer-component-health = Health:
plant-analyzer-component-age = Age:
plant-analyzer-component-water = Water:
plant-analyzer-component-nutrition = Nutrition:
plant-analyzer-component-toxins = Toxins:
plant-analyzer-component-pests = Pests:
plant-analyzer-component-weeds = Weeds:

plant-analyzer-component-alive = [color=green]ALIVE[color]
plant-analyzer-component-dead = [color=red]DEAD[color]
plant-analyzer-component-unviable = [color=red]UNVIABLE[color]
plant-analyzer-component-mutating = [color=#00ff5f]MUTATING[color]
plant-analyzer-component-kudzu = [color=red]KUDZU[color]

plant-analyzer-soil = Unabsorbed Reagents: [color=white]{$chemicals}[/color]
plant-analyzer-soil-empty = Unabsorbed Reagents: [color=gray]None[/color]

plant-analyzer-component-environment = [bold]Desired Environment Analysis[/bold] {$nl}
plant-analyzer-component-light = Light Level: [color=white]{$lightLevel} ± {$lightTolerance}[/color]{$nl}

plant-analyzer-component-temperature = Temperature: [color=lightsalmon]{$temp}°k ± {$tempTolerance}°k[/color]{$nl}

plant-analyzer-component-pressure = Pressure: [color=lightblue]{$kpa}kPa ± {$kpaTolerance}kPa[/color]{$nl}
plant-analyzer-component-requiredgas = Required Gases: [color=lightgray]{$gases}[/color]
plant-analyzer-component-nogas = Required Gases: [color=gray]None[/color]


plant-analyzer-produce-title = [bold]Plant Produce Analysis[/bold] {$nl}
plant-analyzer-produce-amount = Produce: {$yield ->
        [0][color=gray]None[/color]$nl
        [one][color=#a4885c][bold]{$yield}[/bold] {$produce}[/color]
        *[other][color=#a4885c][bold]{$yield}[/bold] {$producePlural}[/color]
        }{$nl}

plant-analyzer-produce-size = Potency: {$yield ->
        [0][color=gray]N/A[/color]
        *[other][color=lightgreen][bold]{$potency}[/bold] {"("}{$potencyDesc}{")"}[/color]
        }{$nl}

plant-analyzer-produce-seedless = Seedless: {$seedless ->
        [true]{" "}[color=red]Yes[/color]
        *[false]{" "}[color=green]No[/color]
        }{$nl}

plant-analyzer-produce-gases = Emitted Gases: {$gasCount ->
        [0][color=gray]None[/color]
        *[other][color=lightgray]{$gases}[/color]
        }{$nl}

plant-analyzer-produce-reagents = Reagents: {$yield ->
        [0][color=gray]None[/color]
        *[other]{$chemCount ->
                [0][color=gray]None[/color]
                *[other][color=white]{$chemicals}[/color]
                }
        }

plant-analyzer-produce-plural = {MAKEPLURAL($thing)}

plant-analyzer-potency-tiny = Tiny
plant-analyzer-potency-small = Small
plant-analyzer-potency-medium = Medium
plant-analyzer-potency-large = Large
plant-analyzer-potency-huge = Huge
plant-analyzer-potency-gigantic = Gigantic
plant-analyzer-potency-ludicrous = Ludicrous
plant-analyzer-potency-immeasurable = [italic]Immeasurable[/italic]
plant-analyzer-potency-perfect = [color=yellow][bold]Perfect[/bold][/color]

plant-analyzer-print = Print
plant-analyzer-printout-missing = N/A
plant-analyzer-printout = [color=#9FED58][head=2]Plant Analyzer Report[/head][/color]{$nl
    }──────────────────────────────{$nl
    }[bullet/] Species: {$seedName}{$nl
    }{$indent}[bullet/] Viable: {$viable ->
        [Yes][color=green]Yes[/color]
        [No][color=red]No[/color]
        *[other]{LOC("plant-analyzer-printout-missing")}
    }{$nl
    }{$indent}[bullet/] Kudzu: {$kudzu ->
        [Yes][color=red]Yes[/color]
        [No][color=green]No[/color]
        *[other]{LOC("plant-analyzer-printout-missing")}
    }{$nl
    }{$indent}[bullet/] Endurance: {$endurance}{$nl
    }{$indent}[bullet/] Lifespan: {$lifespan}{$nl
    }{$indent}[bullet/] Produce: {$yield ->
        [-1]{LOC("plant-analyzer-printout-missing")}
        [0][color=gray]None[/color]$nl
        [one][color=#a4885c][bold]{$yield}[/bold] {$produce}[/color]
        *[other][color=#a4885c][bold]{$yield}[/bold] {$producePlural}[/color]
        }{$nl
    }{$indent}[bullet/] Potency: {$yield ->
        [-1]{LOC("plant-analyzer-printout-missing")}
        [0][color=red]0[/color]
        *[other][color=lightgreen][bold]{$potency}[/bold] {"("}{$potencyDesc}{")"}[/color]
        }{$nl
    }{$indent}[bullet/] Seedless: {$seeds ->
        [Yes][color=red]Yes[/color]
        [No][color=green]No[/color]
        *[other]{LOC("plant-analyzer-printout-missing")}
        }{$nl
    }[bullet/] Growth profile:{$nl
    }{$indent}[bullet/] Water: [color=cyan]{$water}[/color]{$nl
    }{$indent}[bullet/] Nutrition: [color=orange]{$nutrients}[/color]{$nl
    }{$indent}[bullet/] Toxins: [color=yellowgreen]{$toxins}[/color]{$nl
    }{$indent}[bullet/] Pests: [color=magenta]{$pests}[/color]{$nl
    }{$indent}[bullet/] Weeds: [color=red]{$weeds}[/color]{$nl
    }[bullet/] Desired Environment:{$nl
    }{$indent}[bullet/] Light Level: [color=gray][bold]{$lightLevel} ± {$lightTolerance}[/bold][/color]{$nl
    }{$indent}[bullet/] Temperature: [color=lightsalmon]{$temp}°k ± {$tempTolerance}°k[/color]{$nl
    }{$indent}[bullet/] Pressure: [color=lightblue]{$kpa}kPa ± {$kpaTolerance}kPa[/color]{$nl
    }{$indent}[bullet/] Required Gases [color=lightgray]{$gasesIn}[/color]{$nl
    }[bullet/] Emitted Gases: {$gasCount ->
        [0][color=gray]None[/color]
        *[other][color=lightgray]{$gasesOut}[/color]
        }{$nl
    }[bullet/] Reagents: {$yield ->
        [0][color=gray]None[/color]
        *[other]{$chemCount ->
                [0][color=gray]None[/color]
                *[other][color=gray]{$chemicals}[/color]
                }
        }
