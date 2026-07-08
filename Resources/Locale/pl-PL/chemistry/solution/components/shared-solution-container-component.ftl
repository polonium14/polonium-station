shared-solution-container-component-on-examine-main-text = Zawiera {INDEFINITE($desc)} [color={$color}]{$colorName} {$desc}[/color] { $chemCount ->
    [1] substancję chemiczną.
   *[other] mieszaninę chemikaliów.
    }

examinable-solution-has-recognizable-chemicals = Rozpoznajesz w roztworze {$recognizedString}.
examinable-solution-recognized = [color={$color}]{$chemical}[/color]

examinable-solution-on-examine-volume = Zawarty roztwór { $fillLevel ->
    [exact] zawiera [color=white]{$current}/{$max}u[/color].
   *[other] jest [bold]{ -solution-vague-fill-level(fillLevel: $fillLevel) }[/bold].
}

examinable-solution-on-examine-volume-no-max = Zawarty roztwór { $fillLevel ->
    [exact] zawiera [color=white]{$current}u[/color].
   *[other] jest [bold]{ -solution-vague-fill-level(fillLevel: $fillLevel) }[/bold].
}

examinable-solution-on-examine-volume-puddle = Kałuża { $fillLevel ->
    [exact] ma [color=white]{$current}u[/color].
    [full] jest ogromna i przelewa się!
    [mostlyfull] jest ogromna i przelewa się!
    [halffull] jest głęboka i płynie.
    [halfempty] jest bardzo głęboka.
   *[mostlyempty] zbiera się w kałużę.
    [empty] tworzy kilka małych kałuż.
}

-solution-vague-fill-level =
    { $fillLevel ->
        [full] [color=white]pełny[/color]
        [mostlyfull] [color=#DFDFDF]prawie pełny[/color]
        [halffull] [color=#C8C8C8]w połowie pełny[/color]
        [halfempty] [color=#C8C8C8]w połowie pusty[/color]
        [mostlyempty] [color=#A4A4A4]prawie pusty[/color]
       *[empty] [color=gray]pusty[/color]
    }
