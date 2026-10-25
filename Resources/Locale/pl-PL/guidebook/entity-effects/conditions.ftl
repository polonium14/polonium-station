entity-condition-guidebook-total-satiation =
    { $max ->
        [2147483648] cel ma łącznie co najmniej { NATURALFIXED($min, 2) } { $type }
       *[other]
            { $min ->
                [0] cel ma łącznie co najwyżej { NATURALFIXED($max, 2) } { $type }
               *[other] cel ma łącznie od { NATURALFIXED($min, 2) } do { NATURALFIXED($max, 2) } { $type }
            }
    }
entity-condition-guidebook-reagent-threshold =
    { $max ->
        [2147483648] jest co najmniej { NATURALFIXED($min, 2) }u reagentu { $reagent }
       *[other]
            { $min ->
                [0] jest co najwyżej { NATURALFIXED($max, 2) }u reagentu { $reagent }
               *[other] jest od { NATURALFIXED($min, 2) }u do { NATURALFIXED($max, 2) }u reagentu { $reagent }
            }
    }
entity-condition-guidebook-mob-state-condition = mob jest w stanie: { $state }
entity-condition-guidebook-job-condition = zawód celu to { $job }
entity-condition-guidebook-body-temperature =
    temperatura ciała wynosi { $max ->
        [2147483648] co najmniej { NATURALFIXED($min, 2) }k
       *[other]
            { $min ->
                [0] co najwyżej { NATURALFIXED($max, 2) }k
               *[other] od { NATURALFIXED($min, 2) }k do { NATURALFIXED($max, 2) }k
            }
    }
entity-condition-guidebook-organ-type =
    metabolizujący narząd { $shouldhave ->
        [true] jest
       *[false] nie jest
    } narządem typu { $name }
entity-condition-guidebook-has-tag =
    cel { $invert ->
        [true] nie ma
       *[false] ma
    } tag { $tag }
entity-condition-guidebook-breathing =
    metabolizujący { $isBreathing ->
        [true] oddycha normalnie
       *[false] się dusi
    }
entity-condition-guidebook-internals =
    metabolizujący { $usingInternals ->
        [true] korzysta z butli z powietrzem
       *[false] oddycha powietrzem atmosferycznym
    }
entity-condition-guidebook-unknown-reagent = nieznany reagent we krwi
entity-condition-guidebook-blood-reagent-threshold =
    { $max ->
        [2147483648] we krwi jest co najmniej { NATURALFIXED($min, 2) }u reagentu { $reagent }
       *[other]
            { $min ->
                [0] we krwi jest co najwyżej { NATURALFIXED($max, 2) }u reagentu { $reagent }
               *[other] we krwi jest od { NATURALFIXED($min, 2) }u do { NATURALFIXED($max, 2) }u reagentu { $reagent }
            }
    }
