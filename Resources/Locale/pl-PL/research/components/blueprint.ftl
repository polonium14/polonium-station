blueprint-receiver-popup-insert =
    { CAPITALIZE(THE($user)) } { GENDER($user) ->
        [male] włożył
        [female] włożyła
        [epicene] włożyłu
       *[neuter] włożyło
    } { THE($blueprint) } do { THE($receiver) }.
blueprint-receiver-popup-recipe-exists = Ten sam plan już został włożony!
