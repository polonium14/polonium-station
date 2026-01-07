ensnare-component-try-free =
    Trudzisz się ze zdejmowaniem { $ensnare } { GENDER($ensnare) ->
        [male] który cię złapał!
        [female] która cię złapała!
       *[other] które cię złapało!
    }
ensnare-component-try-free-complete = Uwalniasz się od { $ensnare }!
ensnare-component-try-free-fail = Nie udaje ci się uwolnić od { $ensnare }!
ensnare-component-try-free-complete-other = Uwalniasz { $user } od { $ensnare }!
ensnare-component-try-free-fail-other = Nie udaje ci się uwolnić { $user } od { $ensnare }!
ensnare-component-try-free-other =
    Próbujesz uwolnić { $user } { GENDER($user) ->
        [male] złapanego
        [female] złapaną
        [epicene] złapanu
       *[neuter] złapane
    } w { $ensnare }!
