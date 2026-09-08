# Shown when greeted with the Suspicion role
suspicion-role-greeting = Jesteś { $roleName }!
# Shown when greeted with the Suspicion role
suspicion-objective = Cel: { $objectiveText }
# Shown when greeted with the Suspicion role
suspicion-partners-in-crime =
    { $partnersCount ->
        [zero] Jesteś zdany na siebie. Powodzenia!
        [one] Twoim wspólnikiem w zbrodni jest{ $partnerNames }.
       *[other] Twoi wspólnicy w zbrodni to{ $partnerNames }.
    }
