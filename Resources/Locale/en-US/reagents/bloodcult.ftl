reagent-name-edge-essentia = edge essentia
reagent-desc-edge-essentia = A dark, cursed substance that corrupts the blood of wounded victims, turning their bleeding wounds into sources of unholy blood.


reagent-effect-condition-guidebook-is-blood-cultist = { $invert ->
    [true] the target is not a blood cultist
    *[false] the target is a blood cultist
    }


reagent-effect-guidebook-juggernaut-blood-corruption =
    { $chance ->
        [1] Corrupts
        *[other] corrupt
    } blood into unholy blood upon contact with a juggernaut
