wiki-effect-jittering =
    { $chance ->
        [1] Causes
       *[other] cause
    } jittering for at least { NATURALFIXED($time, 3) } { MANY("second", $time) } { $refresh ->
        [true] without accumulation
       *[false] with accumulation
    }
wiki-effect-drunk =
    { $chance ->
        [1] Causes
       *[other] cause
    } drunkenness for at least { NATURALFIXED($time, 3) } { MANY("second", $time) } with accumulation
wiki-effect-modify-bleed-amount =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Increases
               *[-1] Reduces
            }
       *[other]
            { $deltasign ->
                [1] increase
               *[-1] reduce
            }
    } bleeding by { NATURALFIXED($amount, 3) }
wiki-effect-modify-blood-level =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Increases
               *[-1] Decreases
            }
       *[other]
            { $deltasign ->
                [1] increase
               *[-1] decrease
            }
    } blood level by { NATURALFIXED($amount, 3) }u
wiki-effect-flammable-reaction =
    { $chance ->
        [1] Increases
       *[other] increase
    } flammability by { NATURALFIXED($amount, 3) }
