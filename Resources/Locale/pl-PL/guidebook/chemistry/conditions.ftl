reagent-effect-condition-guidebook-total-damage =
    { $max ->
        [2147483648] ma przynajmniej { NATURALFIXED($min, 2) } całkowitych obrażeń
       *[other]
            { $min ->
                [0] ma co najwyżej { NATURALFIXED($max, 2) } całkowitych obrażeń
               *[other] ma pomiędzy { NATURALFIXED($min, 2) } a { NATURALFIXED($max, 2) } całkowitych obrażeń
            }
    }
reagent-effect-condition-guidebook-total-hunger =
    { $max ->
        [2147483648] cel ma przynajmniej { NATURALFIXED($min, 2) } całkowitego głodu
       *[other]
            { $min ->
                [0] cel ma co najwyżej { NATURALFIXED($max, 2) } całkowitego głodu
               *[other] cel ma pomiędzy { NATURALFIXED($min, 2) } a { NATURALFIXED($max, 2) } całkowitego głodu
            }
    }
reagent-effect-condition-guidebook-reagent-threshold =
    { $max ->
        [2147483648] jest przynajmniej { NATURALFIXED($min, 2) }u { $reagent }
       *[other]
            { $min ->
                [0] jest co najwyżej { NATURALFIXED($max, 2) }u { $reagent }
               *[other] jest pomiędzy { NATURALFIXED($min, 2) }u a { NATURALFIXED($max, 2) }u { $reagent }
            }
    }
reagent-effect-condition-guidebook-mob-state-condition = stwór jest w stanie { $state }
reagent-effect-condition-guidebook-job-condition = praca celu to { $job }
reagent-effect-condition-guidebook-solution-temperature =
    temperatura roztworu wynosi { $max ->
        [2147483648] przynajmniej { NATURALFIXED($min, 2) }k
       *[other]
            { $min ->
                [0] co najwyżej { NATURALFIXED($max, 2) }k
               *[other] pomiędzy { NATURALFIXED($min, 2) }k a { NATURALFIXED($max, 2) }k
            }
    }
reagent-effect-condition-guidebook-body-temperature =
    temperatura ciała wynosi { $max ->
        [2147483648] przynajmniej { NATURALFIXED($min, 2) }k
       *[other]
            { $min ->
                [0] co najwyżej { NATURALFIXED($max, 2) }k
               *[other] pomiędzy { NATURALFIXED($min, 2) }k a { NATURALFIXED($max, 2) }k
            }
    }
reagent-effect-condition-guidebook-organ-type =
    organ metabolizujący { $shouldhave ->
        [true] jest
       *[false] nie jest
    } organem { $name }
reagent-effect-condition-guidebook-has-tag =
    cel { $invert ->
        [true] nie ma
       *[false] ma
    } tag { $tag }
reagent-effect-condition-guidebook-this-reagent = ten reagent
reagent-effect-condition-guidebook-blood-reagent-threshold =
    { $max ->
        [2147483648] w krwiobiegu jest co najmniej { NATURALFIXED($min, 2) }u { $reagent }
       *[other]
            { $min ->
                [0] w krwiobiegu jest co najwyżej { NATURALFIXED($max, 2) }u { $reagent }
               *[other] w krwiobiegu jest między { NATURALFIXED($min, 2) }u a { NATURALFIXED($max, 2) }u { $reagent }
            }
    }
reagent-effect-condition-guidebook-breathing =
    metabolizujący { $isBreathing ->
        [true] oddycha normalnie
       *[false] dusi się
    }
reagent-effect-condition-guidebook-ling = cel jest Odmieńcem
reagent-effect-condition-guidebook-damage-threshold =
    { $max ->
        [2147483648] cel ma przynajmniej { NATURALFIXED($min, 2) } obrażeń typu { $damage }
       *[other]
            { $min ->
                [0] cel ma co najwyżej { NATURALFIXED($max, 2) } obrażeń typu { $damage }
               *[other] cel ma pomiędzy { NATURALFIXED($min, 2) } a { NATURALFIXED($max, 2) } obrażeń typu { $damage }
            }
    }
reagent-effect-condition-guidebook-internals =
    metabolizujący { $usingInternals ->
        [true] używa butli z tlenem
       *[false] oddycha powietrzem atmosferycznym
    }
