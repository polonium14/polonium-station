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
melee-weapon-dealt-no-damage = { CAPITALIZE($weapon) } nie zadaje obrazeń { $target }!
melee-self-weapon-dealt-no-damage = Nie zadajesz obrażeń { THE($target) }!
# MeleeBatteryHitsLeftSystem
examine-battery-hits-left = Jest wystarczająco naładowany, by uderzyć [color={ $color }]{ $count }[/color] razy.
