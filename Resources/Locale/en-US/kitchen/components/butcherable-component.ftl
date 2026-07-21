butcherable-different-tool =
    Bedziesz { GENDER($user) ->
        [male] potrzebował
        [female] potrzebowała
        [epicene] potrzebowału
       *[neuter] potrzebowało
    } innego narzędzia aby rozłożyć { $target }.
butcherable-knife-butchered-success = Rozłożono { $target } używając { $knife }.
butcherable-need-knife = Użyj ostrego obiektu aby rozłożyć { $target }.
butcherable-not-in-container = { CAPITALIZE($target) } nie może być w pojemniku.
butcherable-mob-isnt-dead =
    Musi być { GENDER($target) ->
        [male] martwy
        [female] martwa
        [epicene] martwu
       *[neuter] martwe
    }.
butcherable-verb-name = Rozłóż
