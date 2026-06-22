shared-solution-container-component-on-examine-empty-container = Nie zawiera chemikaliów.
shared-solution-container-component-on-examine-main-text = Zawiera [color={ $color }]{ $desc }[/color] { $wordedAmount }
shared-solution-container-component-on-examine-worded-amount-one-reagent = substancję chemiczną.
shared-solution-container-component-on-examine-worded-amount-multiple-reagents = mieszaninę chemikaliów.
examinable-solution-has-recognizable-chemicals = Rozpoznajesz w roztworze { $recognizedString }.
examinable-solution-recognized-first = [color={ $color }]{ $chemical }[/color]
examinable-solution-recognized-next = , [color={ $color }]{ $chemical }[/color]
examinable-solution-recognized-last = i [color={ $color }]{ $chemical }[/color]

examinable-solution-recognized = [color={$color}]{$chemical}[/color]

examinable-solution-on-examine-volume-puddle = The puddle is { $fillLevel ->
    [exact] [color=white]{$current}u[/color].
    [full] huge and overflowing!
    [mostlyfull] huge and overflowing!
    [halffull] deep and flowing.
    [halfempty] very deep.
   *[mostlyempty] pooling together.
    [empty] forming multiple small pools.
}

-solution-vague-fill-level =
    { $fillLevel ->
        [full] [color=white]Full[/color]
        [mostlyfull] [color=#DFDFDF]Mostly Full[/color]
        [halffull] [color=#C8C8C8]Half Full[/color]
        [halfempty] [color=#C8C8C8]Half Empty[/color]
        [mostlyempty] [color=#A4A4A4]Mostly Empty[/color]
       *[empty] [color=gray]Empty[/color]
    }
