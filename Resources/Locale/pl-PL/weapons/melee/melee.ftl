melee-inject-failed-hardsuit =
    { GENDER($weapon) ->
       *[male] Twój
        [female] Twoja
        [other] Twoje
    } { $weapon } nie może wstrzykiwać przez kombinezony ochronne!
melee-balloon-pop =
    { CAPITALIZE($balloon) } { GENDER($balloon) ->
       *[male] pęknął
        [female] pękneła
        [other] pękło
    }!
# BatteryComponent
melee-battery-examine =
    Ma wystarczająco napięcia dla [color={ $color }]{ $count }[/color] { $count ->
        [one] uderzenia
       *[other] uderzeń
    }.
