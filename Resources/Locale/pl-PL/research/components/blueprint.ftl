blueprint-receiver-popup-insert =
    { CAPITALIZE($user) } { GENDER($user) ->
        [male] włożył
        [female] włożyła
        [epicene] włożyłu
       *[neuter] włożyło
    } { $blueprint } do { $receiver }.
blueprint-receiver-popup-recipe-exists = Ten sam plan już został włożony!
