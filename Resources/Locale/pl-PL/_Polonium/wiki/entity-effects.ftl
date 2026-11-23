wiki-effect-jittering =
    { $chance ->
        [1] Wywołuje
       *[other] wywołać
    } drgawki na co najmniej { NATURALFIXED($time, 3) } { $time ->
        [one] sekundę
        [few] sekundy
        [many] sekund
       *[other] sekundy
    } { $refresh ->
        [true] bez kumulacji
       *[false] z kumulacją
    }
wiki-effect-drunk =
    { $chance ->
        [1] Wywołuje
       *[other] wywołać
    } upojenie alkoholowe na co najmniej { NATURALFIXED($time, 3) } { $time ->
        [one] sekundę
        [few] sekundy
        [many] sekund
       *[other] sekundy
    } z kumulacją
wiki-effect-modify-bleed-amount =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Zwiększa
               *[-1] Zmniejsza
            }
       *[other]
            { $deltasign ->
                [1] zwiększyć
               *[-1] zmniejszyć
            }
    } krwawienie o { NATURALFIXED($amount, 3) }
wiki-effect-modify-blood-level =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Zwiększa
               *[-1] Zmniejsza
            }
       *[other]
            { $deltasign ->
                [1] zwiększyć
               *[-1] zmniejszyć
            }
    } poziom krwi o { NATURALFIXED($amount, 3) }u
wiki-effect-flammable-reaction =
    { $chance ->
        [1] Zwiększa
       *[other] zwiększyć
    } łatwopalność o { NATURALFIXED($amount, 3) }
