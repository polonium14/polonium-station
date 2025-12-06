lube-success =
    { CAPITALIZE(THE($target)) } { GENDER($target) ->
        [male] został pokryty
        [female] została pokryta
        [epicene] zostału pokrytu
       *[neuter] zostało pokryte
    } smarem!
lubed-name-prefix = nasmarowany { $baseName }
lube-failure = Nie można pokryć { THE($target) } smarem!
lube-slip = { CAPITALIZE(THE($target)) } wyślizga ci się z rąk!
lube-verb-text = Nałóż smar
lube-verb-message = Nasmaruj obiekt
