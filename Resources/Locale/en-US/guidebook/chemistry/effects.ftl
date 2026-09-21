reagent-effect-guidebook-create-entity-reaction-effect =
    { $chance ->
        [1] Tworzy
       *[other] tworzy
    } { $amount ->
        [1] { INDEFINITE($entname) }
       *[other] { $amount } { MAKEPLURAL($entname) }
    }
