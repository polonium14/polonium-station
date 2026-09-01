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
melee-weapon-dealt-no-damage = { CAPITALIZE(THE($weapon)) } is not damaging { THE($target) }!
melee-self-weapon-dealt-no-damage = You are not damaging { THE($target) }!
# MeleeBatteryHitsLeftSystem
examine-battery-hits-left = It has enough charge for [color={ $color }]{ $count }[/color] hits.
