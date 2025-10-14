shared-solution-container-component-on-examine-empty-container = Contains no chemicals.
shared-solution-container-component-on-examine-main-text = It contains { INDEFINITE($desc) } [color={ $color }]{ $desc }[/color] { $wordedAmount }
shared-solution-container-component-on-examine-worded-amount-one-reagent = chemical.
shared-solution-container-component-on-examine-worded-amount-multiple-reagents = mixture of chemicals.
examinable-solution-has-recognizable-chemicals = You can recognize { $recognizedString } in the solution.
examinable-solution-recognized-first = [color={ $color }]{ $chemical }[/color]
examinable-solution-recognized-next = , [color={ $color }]{ $chemical }[/color]
examinable-solution-recognized-last = and [color={ $color }]{ $chemical }[/color]
examinable-solution-recognized = [color={ $color }]{ $chemical }[/color]
examinable-solution-on-examine-volume-puddle =
    The puddle is { $fillLevel ->
        [exact] [color=white]{ $current }u[/color].
        [full] huge and overflowing!
        [mostlyfull] huge and overflowing!
        [halffull] deep and flowing.
        [halfempty] very deep.
       *[mostlyempty] pooling together.
        [empty] forming multiple small pools.
    }
